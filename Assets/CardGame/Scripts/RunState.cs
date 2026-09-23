using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TatoGames.CardGame
{
    public enum NodeType { Battle, Rest, Boss, Forge }

    public class RunNode
    {
        public NodeType type;
        public string enemyId;          // Battle / Boss 일 때
        /// <summary>다음 열에서 이 노드와 선으로 이어진 노드들의 인덱스.</summary>
        public List<int> next = new();
    }

    /// <summary>
    /// 런 진행 상태. 맵은 열(column) 단위 노드 그래프이고, 각 노드는 다음 열의 노드와
    /// 연결선(next)으로 이어진다. 현재 노드에서 선으로 이어진 노드만 들어갈 수 있다(§11.3 분기 최대 2).
    /// 스테이지 3장(감자밭·뿌리층·깊은토양)을 보스로 넘기며 진행한다(§11.1 축소판).
    /// </summary>
    public static class RunState
    {
        public const int RestHeal = 18;    // §11.2
        public const int StageCount = 3;

        public static List<List<RunNode>> Columns = new();
        public static int Col;      // 현재 열
        public static int Row;      // 현재 열에서의 노드 인덱스
        public static int Stage;    // 0-based
        public static int PlayerHp;
        public static bool Active;

        public static RunNode Current =>
            (Active && Col >= 0 && Col < Columns.Count && Row < Columns[Col].Count)
                ? Columns[Col][Row] : null;

        public static bool IsBossNode => Current != null && Current.type == NodeType.Boss;
        public static bool HasNextColumn => Active && Col + 1 < Columns.Count;
        public static bool HasNextStage => Stage < StageCount - 1;

        /// <summary>현재 노드에서 갈 수 있는 다음 열 노드 인덱스들.</summary>
        public static List<int> Reachable =>
            (Current != null && HasNextColumn) ? Current.next : new List<int>();

        // 장별 스케일 (§11.6 제안: 체력 / 공격력)
        public static float HpScale => Stage switch { 1 => 1.3f, 2 => 1.6f, _ => 1f };
        public static float AtkScale => Stage switch { 1 => 1.2f, 2 => 1.4f, _ => 1f };

        public static string StageName => Stage switch
        {
            0 => "1단계 · 감자밭",
            1 => "2단계 · 뿌리층",
            2 => "3단계 · 깊은 토양",
            _ => $"{Stage + 1}단계",
        };

        public static void StartRun(int startHp)
        {
            Stage = 0;
            Columns = BuildMap();
            Col = 0; Row = 0;
            PlayerHp = startHp;
            Active = true;
        }

        /// <summary>보스를 깬 뒤 다음 스테이지로 — 맵을 새로 만들고 처음 열로.</summary>
        public static void NextStage()
        {
            Stage++;
            Columns = BuildMap();
            Col = 0; Row = 0;
        }

        /// <summary>다음 열의 row번째 노드로 이동(연결된 노드만 허용).</summary>
        public static bool MoveTo(int row)
        {
            if (!HasNextColumn) return false;
            if (!Reachable.Contains(row)) return false;
            Col++;
            Row = Mathf.Clamp(row, 0, Columns[Col].Count - 1);
            return true;
        }

        public static void End()
        {
            Active = false;
            Columns = new List<List<RunNode>>();
            Col = 0; Row = 0; Stage = 0;
        }

        // ── 맵 생성 (§11.3 · 적 설계 §4.3) ──
        const string Clod = "enemy_clod";
        const string Mole = "enemy_shell_mole";
        const string Spore = "enemy_spore_tato";
        const string Field = "enemy_field_mole";
        const string Grub = "enemy_potato_grub";
        const string BossId = "enemy_scarecrow";

        /// 열별 노드 개수 — 1·2열 고정(교육 순서), 7·9는 휴식, 10은 보스
        static readonly int[] Widths = { 1, 1, 2, 2, 2, 2, 1, 2, 1, 1 };

        static List<List<RunNode>> BuildMap()
        {
            string[] basics = { Clod, Mole, Spore };
            string[] mids = { Field, Grub, Mole, Spore };

            var cols = new List<List<RunNode>>();
            for (int c = 0; c < Widths.Length; c++)
            {
                var col = new List<RunNode>();
                for (int r = 0; r < Widths[c]; r++)
                    col.Add(MakeNode(c, basics, mids));
                cols.Add(col);
            }

            ConnectColumns(cols);
            return cols;
        }

        static RunNode MakeNode(int col, string[] basics, string[] mids) => col switch
        {
            0 => Battle(Clod),                       // 고정 — 기본 주고받기
            1 => Battle(Mole),                       // 고정 — 공격 방어 소개
            6 => new RunNode { type = NodeType.Forge },   // §11.3 7번 = 대장간
            8 => new RunNode { type = NodeType.Rest },
            9 => new RunNode { type = NodeType.Boss, enemyId = BossId },
            2 or 3 => Battle(Pick(basics)),
            _ => Battle(Pick(mids)),
        };

        /// <summary>
        /// 열 사이를 잇는다. 모든 노드가 들어오는 선과 나가는 선을 최소 1개씩 갖도록 보장하고,
        /// 2×2 구간에서는 가끔 선을 교차시켜 경로가 갈라졌다 합쳐지게 한다.
        /// </summary>
        static void ConnectColumns(List<List<RunNode>> cols)
        {
            for (int c = 0; c < cols.Count - 1; c++)
            {
                var cur = cols[c];
                var nxt = cols[c + 1];

                if (cur.Count == 1)
                {
                    for (int r = 0; r < nxt.Count; r++) cur[0].next.Add(r);   // 하나 → 전부
                }
                else if (nxt.Count == 1)
                {
                    foreach (var n in cur) n.next.Add(0);                      // 전부 → 하나
                }
                else
                {
                    cur[0].next.Add(0);
                    cur[1].next.Add(1);
                    // 가끔 교차로 길을 섞는다
                    if (Random.value < 0.35f) cur[0].next.Add(1);
                    else if (Random.value < 0.35f) cur[1].next.Add(0);
                }
            }
        }

        static RunNode Battle(string id) => new() { type = NodeType.Battle, enemyId = id };
        static string Pick(string[] pool) => pool[Random.Range(0, pool.Length)];

        public static string Label(RunNode n) => n == null ? "?" : n.type switch
        {
            NodeType.Rest => "휴식",
            NodeType.Forge => "대장간",
            NodeType.Boss => "보스",
            _ => "전투",
        };
    }
}
