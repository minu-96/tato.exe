using System.Collections.Generic;
using System.Linq;
using TatoGames.CardGame;
using UnityEngine;

namespace TatoGames.Launcher
{
    /// <summary>런처에 담기는 프로그램 1개 (§4 저장공간).</summary>
    public class GameEntry
    {
        public string id;                 // 저장 키
        public string displayName;
        public string sceneName;          // 실행할 씬
        public string exeLabel;           // 로딩 화면 문구
        public int price;                 // 구매가(토인). 0 = 기본 제공
        public int sizeMb;                // 차지하는 저장공간
        public AcquireSource cardSource;  // 이 게임이 주는 카드의 출처
        public bool core;                 // tato.exe 본편 — 항상 보유·설치, 손댈 수 없음
        public bool bundled;              // 기본 제공 — 처음부터 보유, 판매 불가
        public string tileSprite;         // 타일 아트 (Resorces 상대경로)
        /// <summary>
        /// 실행할 때 로딩 화면에 띄우는 팁. 보상 조건은 정확한 점수 대신 대략적으로만 알려준다
        /// (구간 수치는 CardLibrary 보상 규칙 — 숫자를 외워 계산하는 게임이 되지 않도록).
        /// </summary>
        public string tip;

        /// <summary>되팔 때 돌려받는 토인 — 구매가의 30%.</summary>
        public int SellPrice => Mathf.FloorToInt(price * StorageData.SellRate);
        public bool CanSell => !core && !bundled && price > 0;
    }

    /// <summary>
    /// 저장공간 시스템 (§4) — 런처가 OS 셸이라는 컨셉의 뼈대.
    ///
    /// <b>보유(구매)와 설치는 별개다.</b>
    ///   · 삭제 = 설치만 내림. 보유는 유지되고 카드도 남는다 → 저장공간 확보용
    ///   · 판매 = 소유 자체를 처분. 구매가의 30%를 돌려받고 <b>그 게임의 카드가 사라진다</b>(§13.2)
    ///
    /// 조작 위치도 나뉜다: 설치·삭제는 미니게임 탭, 구매·판매는 상점.
    ///
    /// ⚠️ 가격·용량은 전부 밸런싱 대상 초안이다.
    /// </summary>
    public static class StorageData
    {
        /// <summary>전체 저장공간. 지금은 전부 설치해도 들어간다(480/500).</summary>
        public const int TotalMb = 500;

        /// <summary>판매가 비율 — 구매가의 30%.</summary>
        public const float SellRate = 0.3f;

        public static readonly GameEntry[] Catalog =
        {
            new GameEntry
            {
                id = "tato", displayName = "tato.exe", sceneName = "Battle",
                exeLabel = "tato.exe", price = 0, sizeMb = 180, core = true, bundled = true,
                cardSource = AcquireSource.Combat, tileSprite = "Shop/Tile/TATOEXE",
            },
            new GameEntry
            {
                id = "snake", displayName = "늘어나라 pooo-tato", sceneName = "SnakeTitle",
                exeLabel = "늘어나라_pooo-tato.exe", price = 0, sizeMb = 85, bundled = true,
                cardSource = AcquireSource.Neulteona, tileSprite = "Minigame/Tile/poootato",
                tip = "사과를 많이 먹을수록 공격 카드를 더 많이 받고, 희귀 카드도 잘 나와요",
            },
            new GameEntry
            {
                id = "moamoa", displayName = "모아모아 10tato", sceneName = "MoaMoaTitle",
                exeLabel = "모아모아_10tato.exe", price = 0, sizeMb = 100, bundled = true,
                cardSource = AcquireSource.MoaMoa, tileSprite = "Minigame/Tile/moamoa",
                tip = "시간 안에 감자를 많이 모을수록 방어 카드를 더 많이 받고, 희귀 카드도 잘 나와요",
            },
            new GameEntry
            {
                id = "field", displayName = "밭의 생존자", sceneName = "GameStart0",
                exeLabel = "밭의_생존자.exe", price = 30, sizeMb = 115,
                cardSource = AcquireSource.FieldSurvivor, tileSprite = "Minigame/Tile/thepotato",
                tip = "오래 살아남을수록 중독·뿌리 카드를 더 많이 받아요. 끝까지 버티면 가장 좋아요",
            },
        };

        const string OwnedKey = "tato_owned";
        const string InstalledKey = "tato_installed";

        public static event System.Action Changed;
        static void Raise() => Changed?.Invoke();

        public static GameEntry Find(string id) => Catalog.FirstOrDefault(g => g.id == id);
        public static GameEntry FindByScene(string scene) => Catalog.FirstOrDefault(g => g.sceneName == scene);

        /// <summary>미니게임만 (본편 제외).</summary>
        public static IEnumerable<GameEntry> MiniGames => Catalog.Where(g => !g.core);

        // ── 저장 ──
        static HashSet<string> Load(string key, System.Func<GameEntry, bool> initial)
        {
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw))
            {
                var init = new HashSet<string>(Catalog.Where(initial).Select(g => g.id));
                Save(key, init);
                return init;
            }
            return new HashSet<string>(raw.Split(',').Where(x => !string.IsNullOrEmpty(x)));
        }

        static void Save(string key, HashSet<string> set)
        {
            PlayerPrefs.SetString(key, string.Join(",", set));
            PlayerPrefs.Save();
        }

        // 기본 제공 게임은 처음부터 보유·설치돼 있다
        static HashSet<string> Owned() => Load(OwnedKey, g => g.bundled);
        static HashSet<string> InstalledSet() => Load(InstalledKey, g => g.bundled);

        public static bool IsOwned(string id)
        {
            var g = Find(id);
            if (g == null) return false;
            return g.core || Owned().Contains(id);
        }

        public static bool IsInstalled(string id)
        {
            var g = Find(id);
            if (g == null) return false;
            if (g.core) return true;                      // 본편은 항상 설치
            return IsOwned(id) && InstalledSet().Contains(id);
        }

        public static int UsedMb => Catalog.Where(g => IsInstalled(g.id)).Sum(g => g.sizeMb);
        public static int FreeMb => Mathf.Max(0, TotalMb - UsedMb);

        // ── 상점: 구매 · 판매 ──

        /// <summary>구매 — 토인을 내고 보유한다. 공간이 남으면 바로 설치까지 해준다.</summary>
        public static bool TryPurchase(string id, out string reason)
        {
            reason = null;
            var g = Find(id);
            if (g == null) { reason = "없는 프로그램이에요"; return false; }
            if (IsOwned(id)) { reason = "이미 보유 중이에요"; return false; }
            if (PlayerData.Toin < g.price) { reason = $"토인이 부족해요 ({g.price} 필요)"; return false; }

            var owned = Owned(); owned.Add(id); Save(OwnedKey, owned);
            if (g.price > 0) PlayerData.AddToin(-g.price);

            if (g.sizeMb <= FreeMb)                       // 공간이 되면 설치까지
            {
                var inst = InstalledSet(); inst.Add(id); Save(InstalledKey, inst);
            }
            Raise();
            return true;
        }

        /// <summary>
        /// 판매 — 구매가의 30%를 돌려받고 소유를 처분한다.
        /// <b>그 게임에서 얻은 카드는 사라진다</b>(§13.2 손실 채널).
        /// 반환값 = 사라진 카드 장수.
        /// </summary>
        public static bool TrySell(string id, out int refund, out int cardsLost, out string reason)
        {
            refund = 0; cardsLost = 0; reason = null;
            var g = Find(id);
            if (g == null) { reason = "없는 프로그램이에요"; return false; }
            if (!g.CanSell) { reason = "기본 제공 프로그램은 팔 수 없어요"; return false; }
            if (!IsOwned(id)) { reason = "보유하고 있지 않아요"; return false; }
            // 판매하면 그 게임 카드가 사라진다 — 진행 중인 런의 덱에 있으면 런 도중에 덱이 바뀌므로 막는다
            // (덱 잠금과 같은 이유. 빠진 자리를 창고 카드가 자동으로 채우는 문제도 있었다)
            if (PlayerData.RunInProgress && PlayerData.DeckCountBySource(g.cardSource) > 0)
            { reason = "진행 중인 런의 덱에 이 게임 카드가 있어요 — 런이 끝난 뒤에 팔 수 있어요"; return false; }

            var owned = Owned(); owned.Remove(id); Save(OwnedKey, owned);
            var inst = InstalledSet(); inst.Remove(id); Save(InstalledKey, inst);

            refund = g.SellPrice;
            if (refund > 0) PlayerData.AddToin(refund);
            cardsLost = PlayerData.DestroyBySource(g.cardSource);
            Raise();
            return true;
        }

        // ── 미니게임 탭: 설치 · 삭제 ──

        /// <summary>설치 — 보유한 것만, 공간이 있어야 한다.</summary>
        public static bool TryInstall(string id, out string reason)
        {
            reason = null;
            var g = Find(id);
            if (g == null) { reason = "없는 프로그램이에요"; return false; }
            if (!IsOwned(id)) { reason = "먼저 상점에서 구매해야 해요"; return false; }
            if (IsInstalled(id)) { reason = "이미 설치돼 있어요"; return false; }
            if (g.sizeMb > FreeMb)
            { reason = $"저장공간이 부족해요 ({g.sizeMb}MB 필요 · {FreeMb}MB 남음)"; return false; }

            var inst = InstalledSet(); inst.Add(id); Save(InstalledKey, inst);
            Raise();
            return true;
        }

        /// <summary>삭제 — 설치만 내린다. 보유와 카드는 그대로라 언제든 다시 깔 수 있다.</summary>
        public static bool TryUninstall(string id, out string reason)
        {
            reason = null;
            var g = Find(id);
            if (g == null) { reason = "없는 프로그램이에요"; return false; }
            if (g.core) { reason = "본편은 삭제할 수 없어요"; return false; }
            if (!IsInstalled(id)) { reason = "설치돼 있지 않아요"; return false; }

            var inst = InstalledSet(); inst.Remove(id); Save(InstalledKey, inst);
            Raise();
            return true;
        }

        /// <summary>판매하면 사라질 카드 수 (경고 문구용).</summary>
        /// <summary>테스트용 — 보유·설치 상태를 처음(기본 제공만)으로 되돌린다.</summary>
        public static void ResetOwnership()
        {
            PlayerPrefs.DeleteKey(OwnedKey);
            PlayerPrefs.DeleteKey(InstalledKey);
            PlayerPrefs.Save();
            Raise();
        }

        public static int CardsAtRisk(string id)
        {
            var g = Find(id);
            return g == null || g.core ? 0 : PlayerData.CountBySource(g.cardSource);
        }
    }
}
