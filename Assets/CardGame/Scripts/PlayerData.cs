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
        public bool bound;      // 시작덱 귀속: 판매 불가 (덱에서 빼는 건 자유)
        public UpgradeKind upgrade = UpgradeKind.None;

        public bool IsUpgraded => upgrade != UpgradeKind.None;
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
        public const int MinDeck = 8;
        public const int MaxDeck = 30;

        /// <summary>토인·보유 카드·덱이 바뀔 때마다 발생 (HUD 등 표시 갱신용).</summary>
        public static event System.Action Changed;
        static void RaiseChanged() => Changed?.Invoke();

        const string ToinKey = "tato_toin";
        const string InstancesKey = "tato_instances";
        const string DeckKey = "tato_deck";
        const string LegacyCollectionKey = "tato_collection";   // 구버전 마이그레이션용

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
                list.Add(new CardInstance { instanceId = iid, cardId = f[1], bound = f[2] == "1", upgrade = up });
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
                $"{c.instanceId}|{c.cardId}|{(c.bound ? 1 : 0)}|{(int)c.upgrade}");
            PlayerPrefs.SetString(InstancesKey, string.Join(";", recs));
        }

        /// <summary>카드 1장 획득 → 새 인스턴스. 덱에 여유가 있으면 자동으로 덱에 넣는다.</summary>
        public static void AddCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            var list = Instances();
            int next = list.Count == 0 ? 1 : list.Max(c => c.instanceId) + 1;
            var inst = new CardInstance { instanceId = next, cardId = cardId, bound = false };
            list.Add(inst);
            SaveInstances(list);

            var deck = DeckIds();
            if (deck.Count < MaxDeck) { deck.Add(inst.instanceId); SaveDeck(deck); }
            PlayerPrefs.Save();
            RaiseChanged();
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

        /// <summary>덱에 넣기/빼기. 최소 8 · 최대 30을 어기면 false와 사유를 돌려준다.</summary>
        public static bool TrySetInDeck(int instanceId, bool inDeck, out string reason)
        {
            var deck = DeckIds();
            bool has = deck.Contains(instanceId);
            reason = null;

            if (inDeck)
            {
                if (has) return true;
                if (deck.Count >= MaxDeck) { reason = $"덱은 최대 {MaxDeck}장까지예요"; return false; }
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
            return Instances().Where(c => deck.Contains(c.instanceId)).ToList();
        }

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
                deck.Add(c.instanceId);
            }
            SaveDeck(deck);
            PlayerPrefs.Save();
        }

        /// <summary>테스트용 초기화.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(ToinKey);
            PlayerPrefs.DeleteKey(InstancesKey);
            PlayerPrefs.DeleteKey(DeckKey);
            PlayerPrefs.DeleteKey(LegacyCollectionKey);
            PlayerPrefs.Save();
            RaiseChanged();
        }
    }
}
