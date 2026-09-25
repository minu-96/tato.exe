using System.Collections.Generic;
using System.Linq;
using TatoGames.CardGame;
using UnityEngine;

namespace TatoGames.Launcher
{
    /// <summary>런처에 설치할 수 있는 프로그램 1개 (§4 저장공간).</summary>
    public class GameEntry
    {
        public string id;                 // 저장 키
        public string displayName;
        public string sceneName;          // 실행할 씬
        public string exeLabel;           // 로딩 화면 문구
        public int price;                 // 토인 (0 = 무료)
        public int sizeMb;                // 차지하는 저장공간
        public AcquireSource cardSource;  // 이 게임이 주는 카드의 출처
        public bool core;                 // tato.exe 본편 — 항상 설치, 삭제 불가
        public string tileSprite;         // 상점/미니게임 탭 타일 아트 (Resorces 상대경로)
    }

    /// <summary>
    /// 저장공간 시스템 (§4) — 런처가 OS 셸이라는 컨셉의 뼈대.
    ///
    /// <b>용량이 모자라서 미니게임을 전부 깔 수는 없다.</b> 무엇을 설치할지가 곧
    /// 어떤 카드 축(공격/방어/중독)을 쓸지를 고르는 선택이 된다.
    /// 삭제하면 용량은 돌아오지만 그 게임에서 얻은 카드가 전부 사라진다(§13.2 손실 채널).
    ///
    /// ⚠️ 아래 가격·용량은 전부 밸런싱 대상 초안이다.
    /// </summary>
    public static class StorageData
    {
        /// <summary>전체 저장공간. tato.exe(160) + 미니게임 3종(288) = 448 이라 셋을 다 못 넣는다.</summary>
        public const int TotalMb = 400;

        public static readonly GameEntry[] Catalog =
        {
            new GameEntry
            {
                id = "tato", displayName = "tato.exe", sceneName = "Battle",
                exeLabel = "tato.exe", price = 0, sizeMb = 160, core = true,
                cardSource = AcquireSource.Combat, tileSprite = "Shop/Tile/TATOEXE",
            },
            new GameEntry
            {
                id = "snake", displayName = "늘어나라 pooo-tato", sceneName = "SnakeTitle",
                exeLabel = "늘어나라_pooo-tato.exe", price = 0, sizeMb = 80,
                cardSource = AcquireSource.Neulteona, tileSprite = "Minigame/Tile/poootato",
            },
            new GameEntry
            {
                id = "moamoa", displayName = "모아모아 10tato", sceneName = "MoaMoaTitle",
                exeLabel = "모아모아_10tato.exe", price = 60, sizeMb = 96,
                cardSource = AcquireSource.MoaMoa, tileSprite = "Minigame/Tile/moamoa",
            },
            new GameEntry
            {
                id = "field", displayName = "밭의 생존자", sceneName = "GameStart0",
                exeLabel = "밭의_생존자.exe", price = 90, sizeMb = 112,
                cardSource = AcquireSource.FieldSurvivor, tileSprite = "Minigame/Tile/thepotato",
            },
        };

        /// <summary>처음부터 깔려 있는 것 — 본편과 무료 미니게임 하나.</summary>
        static readonly string[] PreInstalled = { "tato", "snake" };

        const string Key = "tato_installed";

        public static event System.Action Changed;
        static void Raise() => Changed?.Invoke();

        public static GameEntry Find(string id) => Catalog.FirstOrDefault(g => g.id == id);

        public static GameEntry FindByScene(string sceneName) =>
            Catalog.FirstOrDefault(g => g.sceneName == sceneName);

        // ── 설치 목록 ──
        static HashSet<string> Installed()
        {
            string raw = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(raw))
            {
                var init = new HashSet<string>(PreInstalled);
                Save(init);
                return init;
            }
            return new HashSet<string>(raw.Split(',').Where(x => !string.IsNullOrEmpty(x)));
        }

        static void Save(HashSet<string> set)
        {
            PlayerPrefs.SetString(Key, string.Join(",", set));
            PlayerPrefs.Save();
        }

        public static bool IsInstalled(string id)
        {
            var g = Find(id);
            if (g != null && g.core) return true;   // 본편은 항상 설치
            return Installed().Contains(id);
        }

        public static int UsedMb => Catalog.Where(g => IsInstalled(g.id)).Sum(g => g.sizeMb);
        public static int FreeMb => Mathf.Max(0, TotalMb - UsedMb);

        /// <summary>구매 = 설치. 토인과 용량을 모두 만족해야 한다.</summary>
        public static bool TryInstall(string id, out string reason)
        {
            reason = null;
            var g = Find(id);
            if (g == null) { reason = "없는 프로그램이에요"; return false; }
            if (IsInstalled(id)) { reason = "이미 설치돼 있어요"; return false; }
            if (g.sizeMb > FreeMb)
            { reason = $"저장공간이 부족해요 ({g.sizeMb}MB 필요 · {FreeMb}MB 남음)"; return false; }
            if (PlayerData.Toin < g.price)
            { reason = $"토인이 부족해요 ({g.price} 필요)"; return false; }

            var set = Installed();
            set.Add(id);
            Save(set);
            if (g.price > 0) PlayerData.AddToin(-g.price);
            Raise();
            return true;
        }

        /// <summary>
        /// 삭제 — 용량은 돌아오지만 <b>그 게임에서 얻은 카드가 전부 사라진다</b>(§13.2).
        /// 반환값 = 사라진 카드 장수.
        /// </summary>
        public static int Uninstall(string id)
        {
            var g = Find(id);
            if (g == null || g.core || !IsInstalled(id)) return 0;

            var set = Installed();
            set.Remove(id);
            Save(set);

            int lost = PlayerData.DestroyBySource(g.cardSource);
            Raise();
            return lost;
        }

        /// <summary>삭제하면 사라질 카드 수 (경고 문구용).</summary>
        public static int CardsAtRisk(string id)
        {
            var g = Find(id);
            return g == null || g.core ? 0 : PlayerData.CountBySource(g.cardSource);
        }
    }
}
