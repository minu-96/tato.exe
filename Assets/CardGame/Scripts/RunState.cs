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
        // §11.2 — 최대체력 36의 약 80%. 회복이 이만큼 커야 스테이지가 서로 "분리"된다.
        // 회복이 작으면 누적 소모가 사망을 2스테이지로 앞당겨서, 목표 사망 분포(3:27:70)가
        // 아무리 배율을 조절해도 37:61 / 47:51 쪽으로 무너진다(실측).
        public const int RestHeal = 29;
        public const int StageCount = 3;

        // ── 런 진행 상태 ──
        // 전부 PlayerPrefs에 저장된다. 앱을 껐다 켜도 하던 런을 이어서 한다.
        // (예전에는 메모리 전용이라 재시작하면 런이 증발했고, 그게 손실 회피 구멍이었다)
        static List<List<RunNode>> columns = new();
        static int col, row, stage, playerHp;
        static bool active;
        static bool nodeCleared;   // 현재 노드를 끝냈고 다음 노드를 고를 차례
        static bool stageIntro;    // 새 스테이지 맵을 막 받았고 첫 노드를 고를 차례
        static bool loaded;

        public static List<List<RunNode>> Columns { get { EnsureLoaded(); return columns; } set { columns = value; } }
        public static int Col { get { EnsureLoaded(); return col; } set { col = value; Persist(); } }
        public static int Row { get { EnsureLoaded(); return row; } set { row = value; Persist(); } }
        public static int Stage { get { EnsureLoaded(); return stage; } set { stage = value; Persist(); } }
        public static int PlayerHp { get { EnsureLoaded(); return playerHp; } set { playerHp = value; Persist(); } }
        public static bool Active { get { EnsureLoaded(); return active; } set { active = value; Persist(); } }

        /// <summary>
        /// 현재 노드를 이미 끝냈는가(전투 승리·휴식·대장간 나가기). 다시 들어오면 그 노드를 반복하지 않고 맵부터 연다.
        /// 예전엔 저장이 "현재 노드"만 가리켜서, 이긴 뒤 나갔다 오면 같은 전투를 또 해 보상을 무한히 받았다.
        /// </summary>
        public static bool NodeCleared { get { EnsureLoaded(); return nodeCleared; } set { nodeCleared = value; Persist(); } }

        /// <summary>새 스테이지에 막 들어와 첫 노드를 고를 차례인가 (보스 격파 직후).</summary>
        public static bool StageIntro { get { EnsureLoaded(); return stageIntro; } set { stageIntro = value; Persist(); } }

        public static RunNode Current =>
            (Active && Col >= 0 && Col < Columns.Count && Row < Columns[Col].Count)
                ? Columns[Col][Row] : null;

        public static bool IsBossNode => Current != null && Current.type == NodeType.Boss;
        public static bool HasNextColumn => Active && Col + 1 < Columns.Count;
        public static bool HasNextStage => Stage < StageCount - 1;

        /// <summary>현재 노드에서 갈 수 있는 다음 열 노드 인덱스들.</summary>
        public static List<int> Reachable =>
            (Current != null && HasNextColumn) ? Current.next : new List<int>();

        // 장별 스케일 (§11.6). 1스테이지는 튜토리얼이라 배율 없음(×1.0) — 기준값.
        //
        // 설계 기준은 "능력 게이팅": 1스테이지는 강화 없이, 2스테이지는 대장간 강화 절반,
        // 3스테이지는 강화를 전부 해야 뚫린다. 표준 플레이(강화 50% · 싹 20%)에서 완주 31%.
        //
        // 실측(시드 3묶음 × 4000런, 전투 출처 20종 포함 39장 로스터) — 각 스테이지 통과율:
        //   강화 0%  · 싹 20% →  S1 91.9% / S2 32.2% / 완주  9.3%   (2스테이지에서 막힘)
        //   강화 50% · 싹 20% →  S1 99.4% / S2 60.0% / 완주 30.8%   ← 기준점
        //   강화 100%· 싹 20% →  S1 99.8% / S2 70.9% / 완주 43.5%
        //
        // 체력 배율을 공격력 배율보다 빠르게 올린다(공격 = 1 + (체력−1)×0.6).
        // 적이 단단해져 전투는 길어지되 한 방에 죽지는 않아서, 막는 플레이로 만회할 여지가 남는다.
        // 난이도는 배율보다 최대체력(36)·휴식(29)에 훨씬 민감하다 — 배율만 만지면 곡선이 안 잡힌다.
        //
        // ⚠️ 위 실측은 "모든 스테이지에 적 6종이 섞여 나오던" 시절 값이다. 스테이지별 적 구성으로
        //    바뀐 뒤(2026-09-26) 재측정하지 않았다 — 특히 2·3스테이지는 적이 1종씩이라 곡선이 달라진다.
        public static float HpScale => Stage switch { 1 => 1.17f, 2 => 1.42f, _ => 1f };
        public static float AtkScale => Stage switch { 1 => 1.10f, 2 => 1.25f, _ => 1f };

        public static string StageName => Stage switch
        {
            0 => "1단계 · 감자밭",
            1 => "2단계 · 뿌리층",
            2 => "3단계 · 깊은 토양",
            _ => $"{Stage + 1}단계",
        };

        const string SaveKey = "tato_run";

        /// <summary>
        /// 런 상태 직렬화. 구분자는 서로 겹치지 않게 고른다
        /// (# 최상위 · ; 열 · , 노드 · : 노드 필드 · - 연결선).
        /// </summary>
        static void Persist()
        {
            if (!active)
            {
                PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
                return;
            }

            var map = new System.Text.StringBuilder();
            for (int c = 0; c < columns.Count; c++)
            {
                if (c > 0) map.Append(';');
                for (int r = 0; r < columns[c].Count; r++)
                {
                    if (r > 0) map.Append(',');
                    var n = columns[c][r];
                    map.Append((int)n.type).Append(':').Append(n.enemyId ?? "").Append(':')
                       .Append(string.Join("-", n.next));
                }
            }
            PlayerPrefs.SetString(SaveKey,
                $"{stage}#{col}#{row}#{playerHp}#{map}#{(nodeCleared ? 1 : 0)}#{(stageIntro ? 1 : 0)}");
            PlayerPrefs.Save();
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;   // 실패해도 다시 시도하지 않는다(무한 재귀 방지)

            string raw = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            var f = raw.Split('#');
            if (f.Length < 5) return;
            if (!int.TryParse(f[0], out stage) || !int.TryParse(f[1], out col) ||
                !int.TryParse(f[2], out row) || !int.TryParse(f[3], out playerHp))
            { stage = col = row = playerHp = 0; return; }

            columns = new List<List<RunNode>>();
            foreach (var colStr in f[4].Split(';'))
            {
                if (string.IsNullOrEmpty(colStr)) continue;
                var list = new List<RunNode>();
                foreach (var nodeStr in colStr.Split(','))
                {
                    var nf = nodeStr.Split(':');
                    if (nf.Length < 3) continue;
                    var node = new RunNode
                    {
                        type = (NodeType)(int.TryParse(nf[0], out int t) ? t : 0),
                        enemyId = nf[1],
                    };
                    foreach (var nx in nf[2].Split('-'))
                        if (int.TryParse(nx, out int v)) node.next.Add(v);
                    list.Add(node);
                }
                columns.Add(list);
            }
            active = columns.Count > 0;
            // 6·7번째 칸은 나중에 추가됨 — 없으면(구버전 저장) 둘 다 false
            nodeCleared = f.Length > 5 && f[5] == "1";
            stageIntro = f.Length > 6 && f[6] == "1";
        }

        /// <summary>테스트·초기화용 — 저장된 런을 버린다.</summary>
        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            columns = new List<List<RunNode>>();
            col = row = stage = playerHp = 0;
            active = false;
            nodeCleared = stageIntro = false;
            loaded = true;
            BattleSave.ClearAll();
        }

        /// <param name="enemies">맵에 배치할 적 후보 전체. 스테이지마다 <see cref="EnemyData.stages"/>로 거른다.</param>
        public static void StartRun(int startHp, IList<EnemyData> enemies)
        {
            EnsureLoaded();
            stage = 0;
            columns = BuildMap(stage, enemies);
            col = 0; row = 0;
            playerHp = startHp;
            active = true;
            nodeCleared = stageIntro = false;
            BattleSave.ClearAll();   // 지난 런의 전투·보상 저장이 남아 있으면 버린다
            RecordStage();
            Persist();
        }

        /// <summary>지금까지 도달한 최고 스테이지(1-based). 앱을 껐다 켜도 남는다 — HUD 표시용.</summary>
        public const string BestStageKey = "tato_best_stage";

        public static int BestStageReached
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(BestStageKey, 1), 1, StageCount);
            private set => PlayerPrefs.SetInt(BestStageKey, value);
        }

        static void RecordStage()
        {
            if (Stage + 1 > BestStageReached) { BestStageReached = Stage + 1; PlayerPrefs.Save(); }
        }

        /// <summary>보스를 깬 뒤 다음 스테이지로 — 그 스테이지 적으로 맵을 새로 만들고 처음 열로.</summary>
        public static void NextStage(IList<EnemyData> enemies)
        {
            EnsureLoaded();
            stage++;
            RecordStage();
            columns = BuildMap(stage, enemies);
            col = 0; row = 0;
            nodeCleared = false;
            stageIntro = true;
            Persist();
        }

        /// <summary>다음 열의 row번째 노드로 이동(연결된 노드만 허용).</summary>
        public static bool MoveTo(int row)
        {
            if (!HasNextColumn) return false;
            if (!Reachable.Contains(row)) return false;
            col++;
            RunState.row = Mathf.Clamp(row, 0, columns[col].Count - 1);
            nodeCleared = false;
            Persist();
            return true;
        }

        public static void End()
        {
            EnsureLoaded();
            active = false;
            columns = new List<List<RunNode>>();
            col = 0; row = 0; stage = 0;
            nodeCleared = stageIntro = false;
            BattleSave.ClearAll();
            Persist();   // active=false 라 저장을 지운다
        }

        // ── 맵 생성 (§11.3 · 적 설계 §4.3) ──
        //
        // 스테이지마다 나오는 적이 다르다. 어떤 적이 어느 스테이지에 나오는지는 적 데이터
        // (EnemyData.stages)에 있다 — 새 몬스터는 에셋을 만들고 스테이지만 체크하면 맵에 섞인다.
        // 현재 구성: 1스테이지 흙덩이·밭두더지·감자벌레 / 2스테이지 껍질 두더지 / 3스테이지 포자 감자.
        // 보스는 스테이지마다 따로 두는 게 목표인데, 2·3스테이지 보스가 아직 없어서 허수아비가 셋 다 맡는다.

        /// 1스테이지 첫 두 열은 교육 순서로 고정 — 기본 주고받기(흙덩이) → 공격 방어(밭두더지).
        /// 그 스테이지에 없는 적이면 무시하고 무작위로 채운다.
        static readonly string[] Stage1Intro = { "enemy_clod", "enemy_field_mole" };

        /// 열별 노드 개수 — 1·2열 한 칸, 7 대장간, 9 휴식, 10 보스
        static readonly int[] Widths = { 1, 1, 2, 2, 2, 2, 1, 2, 1, 1 };

        static List<List<RunNode>> BuildMap(int stageIndex, IList<EnemyData> enemies)
        {
            var normals = EnemyIdsFor(stageIndex, enemies, boss: false);
            var bosses = EnemyIdsFor(stageIndex, enemies, boss: true);

            var cols = new List<List<RunNode>>();
            for (int c = 0; c < Widths.Length; c++)
            {
                var col = new List<RunNode>();
                for (int r = 0; r < Widths[c]; r++)
                    col.Add(MakeNode(c, stageIndex, normals, bosses));
                cols.Add(col);
            }

            ConnectColumns(cols);
            return cols;
        }

        /// <summary>그 스테이지에 나오는 적(또는 보스) id. 배정된 적이 없으면 맵이 비지 않게 전체에서 고른다.</summary>
        static List<string> EnemyIdsFor(int stageIndex, IList<EnemyData> enemies, bool boss)
        {
            bool Kind(EnemyData e) => e != null && !string.IsNullOrEmpty(e.id) && (e.tier == EnemyTier.Boss) == boss;
            if (enemies == null) return new List<string>();

            var ids = enemies.Where(e => Kind(e) && e.AppearsIn(stageIndex)).Select(e => e.id).ToList();
            if (ids.Count == 0)
            {
                Debug.LogWarning($"[TatoGames] {stageIndex + 1}스테이지에 배정된 {(boss ? "보스" : "적")}가 없음 — " +
                                 "전체에서 고름 (EnemyData.stages 확인)");
                ids = enemies.Where(Kind).Select(e => e.id).ToList();
            }
            return ids;
        }

        static RunNode MakeNode(int col, int stageIndex, List<string> normals, List<string> bosses) => col switch
        {
            0 or 1 when stageIndex == 0 && normals.Contains(Stage1Intro[col]) => Battle(Stage1Intro[col]),
            6 => new RunNode { type = NodeType.Forge },   // §11.3 7번 = 대장간
            8 => new RunNode { type = NodeType.Rest },
            9 => new RunNode { type = NodeType.Boss, enemyId = Pick(bosses) },
            _ => Battle(Pick(normals)),
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
        static string Pick(List<string> pool) => pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];

        public static string Label(RunNode n) => n == null ? "?" : n.type switch
        {
            NodeType.Rest => "휴식",
            NodeType.Forge => "대장간",
            NodeType.Boss => "보스",
            _ => "전투",
        };
    }
}
