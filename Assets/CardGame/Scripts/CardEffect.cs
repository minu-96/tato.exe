using System;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 카드 효과 한 줄. 카드 하나는 효과를 여러 개 가질 수 있다.
    /// (예: 흙털기 = [피해 3] + [취약 부여 2])
    /// </summary>
    [Serializable]
    public class CardEffect
    {
        public EffectType type = EffectType.Damage;
        public int value;                            // 피해 / 블록 / 스택 / 배수
        public int hits = 1;                         // 연타 횟수
        public TargetType target = TargetType.Enemy;
        public StatusType status = StatusType.None;  // ApplyStatus / Amplify 계열에서 사용
        public int duration;                         // 지속 턴 (ApplyStatusForTurns)

        public CardEffect() { }

        public CardEffect(EffectType type, int value = 0,
                          TargetType target = TargetType.Enemy,
                          StatusType status = StatusType.None,
                          int hits = 1, int duration = 0)
        {
            this.type = type;
            this.value = value;
            this.target = target;
            this.status = status;
            this.hits = hits;
            this.duration = duration;
        }
    }
}
