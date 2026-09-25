using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>미니게임 기록 한 구간에 대한 보상 (§8 카드 수급).</summary>
    [System.Serializable]
    public class RewardTier
    {
        [Tooltip("이 기록 이상이면 아래 보상을 준다")]
        public int minRecord = 1;
        [Min(0)] public int cards = 1;
        [Range(0, 100), Tooltip("각 장이 희귀로 나올 확률(%)")]
        public int rareChance;
    }

    /// <summary>출처(미니게임) 하나의 보상 규칙.</summary>
    [System.Serializable]
    public class SourceRule
    {
        public AcquireSource source;
        [Tooltip("알림 문구에 쓰는 단위 — \"점수\" / \"라운드\"")]
        public string recordLabel = "점수";
        [Tooltip("minRecord 오름차순. 기록이 넘긴 구간 중 가장 높은 것이 적용된다")]
        public List<RewardTier> tiers = new();

        public RewardTier TierFor(int record)
        {
            RewardTier best = null;
            foreach (var t in tiers)
                if (record >= t.minRecord && (best == null || t.minRecord > best.minRecord))
                    best = t;
            return best;
        }
    }

    /// <summary>
    /// 전 카드 목록 + 미니게임 보상 규칙을 담은 단일 에셋.
    ///
    /// <b>Resources 폴더에 있어서 어느 씬에서든 배선 없이 불러올 수 있다.</b>
    /// 미니게임 씬은 이 에셋도, PlayerData도 몰라도 되고 <see cref="TatoReward.Grant"/> 한 줄만 부르면 된다.
    /// 카드 목록은 `TatoGames ▸ Generate MVP Cards`가 갱신하고, 보상 규칙은 인스펙터에서 튜닝한다.
    /// </summary>
    public class CardLibrary : ScriptableObject
    {
        public const string ResourcePath = "CardLibrary";

        [Tooltip("전 카드 (생성기가 자동 갱신 — 직접 편집하지 말 것)")]
        public List<CardData> cards = new();

        [Tooltip("미니게임별 보상 규칙 — 밸런싱 대상. 인스펙터에서 자유롭게 조정")]
        public List<SourceRule> rules = new();

        static CardLibrary cached;

        /// <summary>Resources에서 한 번만 읽어 캐시한다. 없으면 null(호출부가 경고).</summary>
        public static CardLibrary Load()
        {
            if (cached == null) cached = Resources.Load<CardLibrary>(ResourcePath);
            return cached;
        }

        public CardData Find(string id) =>
            string.IsNullOrEmpty(id) ? null : cards.FirstOrDefault(c => c != null && c.id == id);

        public SourceRule RuleFor(AcquireSource source) =>
            rules.FirstOrDefault(r => r.source == source);

        /// <summary>해당 출처·희귀도의 카드 중 무작위 1장. 없으면 null.</summary>
        public CardData PickRandom(AcquireSource source, Rarity rarity)
        {
            var pool = cards.Where(c => c != null && c.source == source && c.rarity == rarity).ToList();
            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>기본 보상 규칙 — 에셋을 처음 만들 때만 넣는다(이후엔 인스펙터 값 보존).</summary>
        public void FillDefaultRules()
        {
            if (rules.Count > 0) return;
            // ⚠️ 전부 추정값이다. 늘어나라·모아모아는 "개수" 단위, 밭의 생존자는 라운드(1~7).
            //    실제로 몇 점이 나오는지 플레이해보고 인스펙터에서 조정할 것.
            rules.Add(new SourceRule
            {
                source = AcquireSource.Neulteona, recordLabel = "점수",
                tiers = new List<RewardTier>
                {
                    new() { minRecord = 5,  cards = 1, rareChance = 0 },
                    new() { minRecord = 15, cards = 2, rareChance = 20 },
                    new() { minRecord = 30, cards = 3, rareChance = 35 },
                }
            });
            rules.Add(new SourceRule
            {
                source = AcquireSource.MoaMoa, recordLabel = "점수",
                tiers = new List<RewardTier>
                {
                    new() { minRecord = 10, cards = 1, rareChance = 0 },
                    new() { minRecord = 30, cards = 2, rareChance = 20 },
                    new() { minRecord = 60, cards = 3, rareChance = 35 },
                }
            });
            rules.Add(new SourceRule
            {
                source = AcquireSource.FieldSurvivor, recordLabel = "라운드",
                tiers = new List<RewardTier>
                {
                    new() { minRecord = 2, cards = 1, rareChance = 0 },
                    new() { minRecord = 4, cards = 2, rareChance = 25 },
                    new() { minRecord = 6, cards = 3, rareChance = 40 },
                }
            });
        }
    }
}
