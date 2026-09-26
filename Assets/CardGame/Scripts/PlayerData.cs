using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>대장간 업그레이드 방향 (§10.10 전직형 — 둘 중 택1, 카드당 1회).</summary>
    public enum UpgradeKind { None, Plus, Minus }

    /// <summary>보유 카드 1장(인스턴스). §8.2 2층 스키마의 ② — 같은 카드라도 개별로 다룬다.</summary>
    public class CardInstance
    {
        public int instanceId;
        public string cardId;
        public bool bound;      // 시작덱 귀속: 판매·썩음 면제 (덱에서 빼는 건 자유)
        public UpgradeKind upgrade = UpgradeKind.None;

        /// <summary>지나온 런 수 (§7 시간축 = 런 수). <b>새 런을 시작할 때마다</b> 보유 카드 전부 +1.</summary>
        public int age;

        /// <summary>이 나이가 되면 썩는다. 획득 시 희귀도별 범위에서 한 번 굴려 고정.</summary>
        public int rotAt;

        public bool IsUpgraded => upgrade != UpgradeKind.None;

        CardState StateAt(int a)
        {
            if (bound || rotAt <= 0) return CardState.Fresh;
            if (a >= rotAt) return CardState.Rotten;
            if (a >= rotAt - 1) return CardState.Sprouted;
            return CardState.Fresh;
        }

        /// <summary>
        /// 지금 이 나이에서의 상태 (생 / 싹 / 썩음, §8.1). 전투 덱을 만들 때 쓰는 "실제" 상태.
        /// 썩기 직전 딱 1런이 싹 = 경고 + 라스트 찬스 강화.
        /// 귀속(시작덱)은 늙지 않는다(§8.2) — 대신 싹 버프도 받지 못한다.
        /// </summary>
        public CardState State => StateAt(age);

        public bool IsRotten => State == CardState.Rotten;

        /// <summary>
        /// <b>다음 런에서</b> 가질 상태 — 감자창고 표시용.
        /// 나이는 런이 시작될 때 먹으므로, 런처에서 보이는 `State`는 이미 끝난 런의 것이라
        /// 한 칸 뒤처진다. 플레이어가 덱을 짤 때 알고 싶은 건 "다음 런에 뭐가 되나"다.
        /// </summary>
        public CardState NextRunState => StateAt(age + 1);

        /// <summary>앞으로 실제로 플레이할 수 있는 런 수. 0이면 다음 런에 썩어 못 쓴다.</summary>
        public int RunsLeftToPlay => bound ? int.MaxValue : Mathf.Max(0, rotAt - age - 1);

        /// <summary>다 쓴 카드 — 다음 런에 썩으므로 두 번 다시 낼 수 없다. 판매 대상.</summary>
        public bool IsSpent => !bound && NextRunState == CardState.Rotten;
    }

    /// <summary>
    /// 런처 ↔ 게임 공용 저장. 토인 + 보유 카드(인스턴스) + 런 덱 구성이 씬·게임을 넘어 유지된다.
    /// MVP는 PlayerPrefs 기반. 진짜 세이브로 갈 때 이 클래스만 갈아끼우면 된다.
    ///
    /// 덱 규칙: 최소 8장 · 최대 30장. 시작덱도 뺄 수 있고(다른 카드로 8장을 채우면 됨),
    /// 시작덱의 유일한 특권은 '귀속 = 팔 수 없음'뿐이다.
    /// </summary>
    public static class PlayerData
    {
        /// <summary>
        /// 희귀도별 수명 (런 단위, 양끝 포함). 획득 시 이 범위에서 한 번 굴려 인스턴스에 고정한다.
        /// 마지막 1런은 싹(라스트 찬스)이므로, 실제로 '생'으로 쓰는 건 rotAt−1런.
        /// </summary>
        public static readonly Dictionary<Rarity, Vector2Int> RotLife = new()
        {
            { Rarity.Common,        new Vector2Int(3, 5) },
            { Rarity.Rare,          new Vector2Int(5, 8) },
            { Rarity.Transcendent,  new Vector2Int(7, 10) },
            { Rarity.Legendary,     new Vector2Int(10, 13) },
        };

        /// <summary>썩은 카드 판매가 — 희귀도와 무관하게 동일(§13.2 손실 채널 정리용).</summary>
        public const int RottenSellPrice = 3;

        /// <summary>멀쩡한 카드 판매가 — 희귀도가 높을수록 비싸다. [밸런싱 대상]</summary>
        public static int SellPriceOf(Rarity r) => r switch
        {
            Rarity.Rare => 10,
            Rarity.Transcendent => 22,
            Rarity.Legendary => 45,
            _ => 5,
        };

        /// <summary>이 카드를 지금 팔면 얼마인가. 썩었으면 고정가.</summary>
        public static int SellValue(CardInstance c)
        {
            if (c == null || c.bound) return 0;
            if (c.IsSpent) return RottenSellPrice;
            var d = CardLibrary.Load()?.Find(c.cardId);
            return d != null ? SellPriceOf(d.rarity) : RottenSellPrice;
        }

        /// <summary>구버전 저장(수명 정보 없음)에서 올라온 카드의 기본 수명.</summary>
        const int LegacyRotAt = 4;

        public static int RollRotAt(Rarity rarity)
        {
            var r = RotLife.TryGetValue(rarity, out var v) ? v : RotLife[Rarity.Common];
            return Random.Range(r.x, r.y + 1);   // 양끝 포함
        }

        public const int MinDeck = 8;
        public const int MaxDeck = 30;

        /// <summary>토인·보유 카드·덱이 바뀔 때마다 발생 (HUD 등 표시 갱신용).</summary>
        public static event System.Action Changed;
        static void RaiseChanged() => Changed?.Invoke();

        const string ToinKey = "tato_toin";
        const string InstancesKey = "tato_instances";
        const string DeckKey = "tato_deck";
        const string LegacyCollectionKey = "tato_collection";   // 구버전 마이그레이션용
        const string NewCardsKey = "tato_new_cards";              // 아직 안 본 새 카드 (감자창고 빨간 점)

        /// 시작덱 4종 ×2 = 8장 (귀속)
        public static readonly string[] StarterIds =
        {
            "atk_potato_punch", "def_peel_shield", "atk_dirt_flick", "def_dirt_smear",
        };
        const int StarterCopies = 2;

        // ── 토인 ──
        public static int Toin => PlayerPrefs.GetInt(ToinKey, 0);

        public static void AddToin(int amount)
        {
            PlayerPrefs.SetInt(ToinKey, Mathf.Max(0, Toin + amount));
            PlayerPrefs.Save();
            RaiseChanged();
        }

        // ── 보유 카드(인스턴스) ──
        public static List<CardInstance> Instances()
        {
            string raw = PlayerPrefs.GetString(InstancesKey, "");
            if (string.IsNullOrEmpty(raw)) return Initialize();

            var list = new List<CardInstance>();
            foreach (var rec in raw.Split(';'))
            {
                if (string.IsNullOrEmpty(rec)) continue;
                var f = rec.Split('|');
                if (f.Length < 3) continue;
                if (!int.TryParse(f[0], out int iid)) continue;
                var up = UpgradeKind.None;
                if (f.Length >= 4) up = f[3] switch { "1" => UpgradeKind.Plus, "2" => UpgradeKind.Minus, _ => UpgradeKind.None };
                bool bound = f[2] == "1";
                // 나이·수명은 5·6번째 칸. 없으면 구버전 저장 → 기본값으로 올린다.
                int age = f.Length >= 5 && int.TryParse(f[4], out int a) ? a : 0;
                int rotAt = f.Length >= 6 && int.TryParse(f[5], out int r) ? r : (bound ? 0 : LegacyRotAt);
                list.Add(new CardInstance
                {
                    instanceId = iid, cardId = f[1], bound = bound, upgrade = up,
                    age = age, rotAt = rotAt,
                });
            }
            return list;
        }

        /// <summary>최초 1회: 시작덱 8장 생성(+구버전 획득분 이관). 전부 덱에 넣는다.</summary>
        static List<CardInstance> Initialize()
        {
            var list = new List<CardInstance>();
            int next = 1;
            foreach (var id in StarterIds)
                for (int i = 0; i < StarterCopies; i++)
                    list.Add(new CardInstance { instanceId = next++, cardId = id, bound = true });

            // 구버전(tato_collection = 획득 id CSV)이 있으면 인스턴스로 이관
            string legacy = PlayerPrefs.GetString(LegacyCollectionKey, "");
            if (!string.IsNullOrEmpty(legacy))
            {
                foreach (var id in legacy.Split(',').Where(x => !string.IsNullOrEmpty(x)))
                    list.Add(new CardInstance { instanceId = next++, cardId = id, bound = false });
                PlayerPrefs.DeleteKey(LegacyCollectionKey);
            }

            SaveInstances(list);
            // 기본 덱 = 최대치까지 전부
            SaveDeck(list.Take(MaxDeck).Select(c => c.instanceId).ToList());
            PlayerPrefs.Save();
            return list;
        }

        static void SaveInstances(List<CardInstance> list)
        {
            var recs = list.Select(c =>
                $"{c.instanceId}|{c.cardId}|{(c.bound ? 1 : 0)}|{(int)c.upgrade}|{c.age}|{c.rotAt}");
            PlayerPrefs.SetString(InstancesKey, string.Join(";", recs));
        }

        /// <summary>
        /// 카드 1장 획득 → 새 인스턴스.
        /// toDeck이면 덱에 여유가 있을 때 덱에 넣고, 덱이 가득(최대치) 찼으면 감자창고에 둔다.
        /// toDeck=false면 항상 감자창고로 간다 — 미니게임 보상이 이쪽이다. 런 도중에 미니게임을 하면
        /// 그 카드가 진행 중인 덱에 섞여 들어가, 패배 시 의도치 않게 같이 소멸했기 때문이다.
        /// 반환값 = 덱에 들어갔는가.
        /// </summary>
        public static bool AddCard(string cardId, Rarity rarity = Rarity.Common, bool toDeck = true)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            var list = Instances();
            int next = list.Count == 0 ? 1 : list.Max(c => c.instanceId) + 1;
            var inst = new CardInstance
            {
                instanceId = next, cardId = cardId, bound = false,
                age = 0, rotAt = RollRotAt(rarity),   // 수명은 획득 시 한 번만 굴린다
            };
            list.Add(inst);
            SaveInstances(list);
            MarkNew(inst.instanceId);   // 감자창고에서 빨간 점 — 마우스를 올리면 사라진다

            bool added = false;
            if (toDeck)
            {
                var deck = DeckIds();
                if (deck.Count < MaxDeck) { deck.Add(inst.instanceId); SaveDeck(deck); added = true; }
            }
            PlayerPrefs.Save();
            RaiseChanged();
            return added;
        }

        // ── 나이 / 썩음 (§7 · §8.1) ──

        /// <summary>
        /// 새 런을 시작할 때 호출. 보유 카드 <b>전부</b>가 한 살 먹는다(덱에 넣었든 아니든 — 쟁여두기 방지).
        /// 귀속 카드는 면제. 이번에 썩은 카드는 덱에서 자동으로 빠지고 창고에는 남는다.
        /// 반환값 = 이번에 새로 썩은 장수.
        /// </summary>
        public static int AgeAll()
        {
            var list = Instances();
            int newlyRotten = 0;

            foreach (var c in list)
            {
                if (c.bound) continue;
                bool wasRotten = c.IsRotten;
                c.age++;
                if (!wasRotten && c.IsRotten) newlyRotten++;
            }
            SaveInstances(list);

            // 썩은 카드는 덱에서 제외 (창고에는 남아 판매 대상이 된다)
            var rotten = new HashSet<int>(list.Where(c => c.IsRotten).Select(c => c.instanceId));
            if (rotten.Count > 0)
                SaveDeck(DeckIds().Where(id => !rotten.Contains(id)).ToList());

            PlayerPrefs.Save();
            TopUpDeck();
            RaiseChanged();
            return newlyRotten;
        }

        /// <summary>
        /// 카드 판매 (§13.2). 썩은 카드는 고정 3토인, 멀쩡한 카드는 희귀도별 가격.
        /// 귀속(시작덱)은 팔 수 없고(§8.2), <b>런 중에는 덱에 든 카드를 팔 수 없다</b>
        /// — 진행 중인 덱을 도중에 헐어 쓰는 것을 막는다.
        /// </summary>
        public static bool TrySellCard(int instanceId, out int refund, out string reason)
        {
            refund = 0; reason = null;
            var list = Instances();
            var inst = list.FirstOrDefault(c => c.instanceId == instanceId);
            if (inst == null) { reason = "카드를 찾을 수 없어요"; return false; }
            if (inst.bound) { reason = "귀속 카드는 팔 수 없어요"; return false; }

            bool inDeck = DeckIds().Contains(instanceId);
            if (RunInProgress && inDeck)
            { reason = "런에 쓰고 있는 카드는 팔 수 없어요"; return false; }
            if (!inst.IsSpent && inDeck && DeckSize() <= MinDeck)
            { reason = $"덱은 최소 {MinDeck}장이 필요해요"; return false; }

            refund = SellValue(inst);
            list.Remove(inst);
            SaveInstances(list);
            SaveDeck(DeckIds().Where(id => id != instanceId).ToList());
            PlayerPrefs.Save();
            TopUpDeck();
            AddToin(refund);            // 저장 + 변경 알림
            return true;
        }

        /// <summary>썩은 카드 판매 — 예전 호출부 호환용.</summary>
        public static bool TrySellRotten(int instanceId, out string reason) =>
            TrySellCard(instanceId, out _, out reason);

        /// <summary>창고에 쌓인 썩은 카드를 한 번에 정리. 반환 = 받은 토인.</summary>
        public static int SellAllRotten()
        {
            var list = Instances();
            var rotten = list.Where(c => c.IsSpent).ToList();
            if (rotten.Count == 0) return 0;

            var ids = new HashSet<int>(rotten.Select(c => c.instanceId));
            SaveInstances(list.Where(c => !ids.Contains(c.instanceId)).ToList());
            SaveDeck(DeckIds().Where(id => !ids.Contains(id)).ToList());
            PlayerPrefs.Save();

            int gained = rotten.Count * RottenSellPrice;
            AddToin(gained);
            return gained;
        }

        // ── 런 덱 ──
        public static List<int> DeckIds()
        {
            string raw = PlayerPrefs.GetString(DeckKey, "");
            if (string.IsNullOrEmpty(raw)) return new List<int>();
            return raw.Split(',').Where(x => !string.IsNullOrEmpty(x))
                      .Select(x => int.TryParse(x, out int v) ? v : -1)
                      .Where(v => v >= 0).ToList();
        }

        static void SaveDeck(List<int> ids) =>
            PlayerPrefs.SetString(DeckKey, string.Join(",", ids));

        public static bool IsInDeck(int instanceId) => DeckIds().Contains(instanceId);

        public static int DeckSize() => DeckIds().Count;

        /// <summary>
        /// 런이 진행 중인가 — 중단하고 런처에 나와 있어도 true.
        /// 이 동안에는 덱을 바꿀 수 없다: 그러지 않으면 전투 중에 나가서 덱을 갈아끼우거나
        /// (§10.7 "복사가 아니라 이동" 위반), 죽기 직전 이탈로 손실을 회피할 수 있다.
        /// </summary>
        public static bool RunInProgress => RunState.Active;

        public const string DeckLockedReason = "런이 진행 중이라 덱을 바꿀 수 없어요 (끝내야 편성 가능)";

        /// <summary>덱에 넣기/빼기. 최소 8 · 최대 30을 어기면 false와 사유를 돌려준다.</summary>
        public static bool TrySetInDeck(int instanceId, bool inDeck, out string reason)
        {
            var deck = DeckIds();
            bool has = deck.Contains(instanceId);
            reason = null;

            // 런 도중에는 편성 잠금 (대장간의 강화·제거는 런의 일부라 별도 경로로 허용)
            if (RunInProgress) { reason = DeckLockedReason; return false; }

            if (inDeck)
            {
                if (has) return true;
                if (deck.Count >= MaxDeck) { reason = $"덱은 최대 {MaxDeck}장까지예요"; return false; }
                var target = Instances().FirstOrDefault(c => c.instanceId == instanceId);
                if (target != null && target.IsSpent)
                { reason = "다음 런에 썩어서 쓸 수 없어요"; return false; }
                deck.Add(instanceId);
            }
            else
            {
                if (!has) return true;
                if (deck.Count <= MinDeck) { reason = $"덱은 최소 {MinDeck}장이 필요해요"; return false; }
                deck.Remove(instanceId);
            }

            SaveDeck(deck);
            PlayerPrefs.Save();
            RaiseChanged();
            return true;
        }

        /// <summary>런 덱에 든 인스턴스(보유 목록 순서대로).</summary>
        public static List<CardInstance> DeckInstances()
        {
            var deck = new HashSet<int>(DeckIds());
            // 썩은 카드는 AgeAll에서 이미 덱에서 빠지지만, 저장이 어긋나도 전투에 안 섞이도록 한 번 더 거른다
            return Instances().Where(c => deck.Contains(c.instanceId) && !c.IsRotten).ToList();
        }

        /// <summary>
        /// 그 출처에서 얻은 카드를 전부 없앤다 (§13.2 손실 채널 — 미니게임 삭제).
        /// 귀속(시작덱)은 면제. 반환값 = 사라진 장수.
        /// </summary>
        public static int DestroyBySource(AcquireSource source)
        {
            var lib = CardLibrary.Load();
            if (lib == null) return 0;

            var all = Instances();
            bool Match(CardInstance c)
            {
                if (c.bound) return false;
                var d = lib.Find(c.cardId);
                return d != null && d.source == source;
            }

            var doomed = all.Where(Match).Select(c => c.instanceId).ToHashSet();
            if (doomed.Count == 0) return 0;

            SaveInstances(all.Where(c => !doomed.Contains(c.instanceId)).ToList());
            SaveDeck(DeckIds().Where(id => !doomed.Contains(id)).ToList());
            PlayerPrefs.Save();
            TopUpDeck();
            RaiseChanged();
            return doomed.Count;
        }

        /// <summary>그 출처 카드를 몇 장 갖고 있나 (삭제 경고용).</summary>
        public static int CountBySource(AcquireSource source)
        {
            var lib = CardLibrary.Load();
            if (lib == null) return 0;
            return Instances().Count(c =>
            {
                if (c.bound) return false;
                var d = lib.Find(c.cardId);
                return d != null && d.source == source;
            });
        }

        /// <summary>런 덱에 든 그 출처 카드 수 — 런 도중 게임 판매를 막는 판단용.</summary>
        public static int DeckCountBySource(AcquireSource source)
        {
            var lib = CardLibrary.Load();
            if (lib == null) return 0;
            return DeckInstances().Count(c =>
            {
                if (c.bound) return false;
                var d = lib.Find(c.cardId);
                return d != null && d.source == source;
            });
        }

        /// <summary>창고에 쌓인 썩은 카드 수 (판매 안내용).</summary>
        public static int RottenCount() => Instances().Count(c => c.IsSpent);

        // ── 대장간 (§10.10 업그레이드 · §10.8 자율 제거) ──
        public const int UpgradeCost = 30;   // [임시]
        public const int RemoveCost = 25;    // [임시]

        /// <summary>카드 1장 강화. 카드당 1회, +(수치↑) 또는 −(코스트↓) 택1.</summary>
        public static bool TryUpgrade(int instanceId, UpgradeKind kind, out string reason)
        {
            reason = null;
            if (kind == UpgradeKind.None) { reason = "강화 방향을 고르세요"; return false; }
            if (Toin < UpgradeCost) { reason = $"토인이 부족해요 ({UpgradeCost} 필요)"; return false; }

            var list = Instances();
            var inst = list.FirstOrDefault(c => c.instanceId == instanceId);
            if (inst == null) { reason = "카드를 찾을 수 없어요"; return false; }
            if (inst.IsUpgraded) { reason = "이미 강화한 카드예요 (카드당 1회)"; return false; }

            inst.upgrade = kind;
            SaveInstances(list);
            PlayerPrefs.Save();
            AddToin(-UpgradeCost);   // 저장 + 변경 알림
            Launcher.Achievements.Bump(Launcher.Achievements.CardsUpgraded);
            return true;
        }

        /// <summary>카드 1장 영구 제거(덱 얇게 만들기). 덱 최소 장수는 지킨다.</summary>
        public static bool TryRemoveCard(int instanceId, out string reason)
        {
            reason = null;
            if (Toin < RemoveCost) { reason = $"토인이 부족해요 ({RemoveCost} 필요)"; return false; }

            var deck = DeckIds();
            if (deck.Contains(instanceId) && deck.Count <= MinDeck)
            { reason = $"덱은 최소 {MinDeck}장이 필요해요"; return false; }

            var list = Instances();
            var inst = list.FirstOrDefault(c => c.instanceId == instanceId);
            if (inst == null) { reason = "카드를 찾을 수 없어요"; return false; }

            list.Remove(inst);
            SaveInstances(list);
            SaveDeck(deck.Where(id => id != instanceId).ToList());
            PlayerPrefs.Save();
            AddToin(-RemoveCost);
            return true;
        }

        /// <summary>
        /// 런 실패 — 인게임 덱 소멸 (§10.7 "복사가 아니라 이동" · §13.2 손실 채널).
        /// 귀속(시작덱) 카드는 면제(§8.2). 소멸 후 덱이 최소치 미만이면 남은 보유분으로 채운다.
        /// 반환값 = 소멸한 장수.
        /// </summary>
        public static int DestroyRunDeck()
        {
            var deckSet = new HashSet<int>(DeckIds());
            var all = Instances();

            bool Lost(CardInstance c) => deckSet.Contains(c.instanceId) && !c.bound;
            int destroyed = all.Count(Lost);

            if (destroyed > 0)
            {
                var kept = all.Where(c => !Lost(c)).ToList();
                SaveInstances(kept);

                var keptIds = new HashSet<int>(kept.Select(c => c.instanceId));
                SaveDeck(DeckIds().Where(keptIds.Contains).ToList());
                PlayerPrefs.Save();
            }

            TopUpDeck();
            RaiseChanged();
            return destroyed;
        }

        /// <summary>덱이 최소치 미만이면 남은 보유 카드로 채운다(소멸 직후 정리).</summary>
        static void TopUpDeck()
        {
            var deck = DeckIds();
            if (deck.Count >= MinDeck) return;
            var inDeck = new HashSet<int>(deck);
            foreach (var c in Instances())
            {
                if (deck.Count >= MinDeck) break;
                if (inDeck.Contains(c.instanceId)) continue;
                if (c.IsRotten) continue;             // 썩은 카드로는 채우지 않는다
                deck.Add(c.instanceId);
            }
            SaveDeck(deck);
            PlayerPrefs.Save();
        }

        /// <summary>테스트용 초기화.</summary>
        // ── 새 카드 표시 (감자창고 빨간 점) ──
        // 얻은 카드는 "아직 안 봄"으로 기록하고, 감자창고에서 그 카드에 마우스를 올리면 지운다.
        // 보유 카드가 사라지면(판매·소멸) 목록에 남아도 아무 데도 안 그려지므로 따로 정리하지 않는다.

        /// <summary>새 카드 표시가 바뀔 때 (사이드바 알림 등). 카드 목록 전체를 다시 그리지 않도록 Changed와 분리.</summary>
        public static event System.Action NewCardsChanged;

        public static HashSet<int> NewCardIds()
        {
            string raw = PlayerPrefs.GetString(NewCardsKey, "");
            var set = new HashSet<int>();
            foreach (var x in raw.Split(','))
                if (int.TryParse(x, out int v)) set.Add(v);
            return set;
        }

        public static bool IsNew(int instanceId) => NewCardIds().Contains(instanceId);

        /// <summary>보유 중인 카드 가운데 아직 안 본 게 있나.</summary>
        public static bool HasUnseenCards()
        {
            var fresh = NewCardIds();
            return fresh.Count > 0 && Instances().Any(c => fresh.Contains(c.instanceId));
        }

        static void MarkNew(int instanceId)
        {
            var set = NewCardIds();
            if (set.Add(instanceId)) SaveNew(set);
        }

        /// <summary>그 카드를 봤다 — 빨간 점을 지운다. 카드 목록을 다시 그리지 않는다(마우스 아래 칸이 깜빡이므로).</summary>
        public static void MarkSeen(int instanceId)
        {
            var set = NewCardIds();
            if (set.Remove(instanceId)) SaveNew(set);
        }

        static void SaveNew(HashSet<int> set)
        {
            PlayerPrefs.SetString(NewCardsKey, string.Join(",", set));
            PlayerPrefs.Save();
            NewCardsChanged?.Invoke();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(ToinKey);
            PlayerPrefs.DeleteKey(InstancesKey);
            PlayerPrefs.DeleteKey(DeckKey);
            PlayerPrefs.DeleteKey(NewCardsKey);
            PlayerPrefs.DeleteKey(LegacyCollectionKey);
            PlayerPrefs.Save();
            RaiseChanged();
        }
    }
}
