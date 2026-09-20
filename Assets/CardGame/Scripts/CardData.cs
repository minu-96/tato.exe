using System.Collections.Generic;
using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 카드 원본 (기획서 §8.2 ①). 밸런싱 수치는 이 데이터에만 두고
    /// 코드에 하드코딩하지 않는다(§14.4). 인스펙터에서 직접 편집 가능.
    /// </summary>
    [CreateAssetMenu(fileName = "Card", menuName = "TatoGames/Card", order = 0)]
    public class CardData : ScriptableObject
    {
        [Header("식별")]
        public string id;                 // 예: atk_potato_punch
        public string displayName;        // 예: 감자 펀치
        [TextArea] public string description;

        [Header("분류")]
        public CardType type;
        public Rarity rarity = Rarity.Common;
        public int cost = 1;
        public AcquireSource source = AcquireSource.Starter;
        public CardKeyword keyword = CardKeyword.None;

        [Header("아트 (팀 리소스 도착 후 인스펙터에서 연결)")]
        public Sprite artwork;   // 카드 중앙 일러스트 (프레임·텍스트는 카드 UI가 데이터로 합성)

        [Header("효과 (§8.1 — 생 / 싹 상태별)")]
        public List<CardEffect> effects = new();        // 생(Fresh) 상태
        public List<CardEffect> sproutEffects = new();  // 싹(Sprouted) 상태 — 그 런 내내 유지

        /// <summary>획득처가 시작덱이면 귀속(bound): 소멸·썩음·판매 면제 (§8.2).</summary>
        public bool IsBound => source == AcquireSource.Starter;

        /// <summary>
        /// 현재 상태에 맞는 효과 목록.
        /// 싹은 sproutEffects(없으면 effects로 폴백), 썩음은 사용 불가라 빈 목록.
        /// </summary>
        public List<CardEffect> EffectsFor(CardState state)
        {
            switch (state)
            {
                case CardState.Sprouted:
                    return (sproutEffects != null && sproutEffects.Count > 0) ? sproutEffects : effects;
                case CardState.Rotten:
                    return new List<CardEffect>();
                default:
                    return effects;
            }
        }
    }
}
