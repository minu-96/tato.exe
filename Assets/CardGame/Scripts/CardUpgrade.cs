using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 대장간 강화를 런타임 카드에 적용 (§10.10 전직형 — 둘 중 택1).
    ///   + : 수치 강화 (피해·블록·효과방어 +3, 상태이상 스택 +1)
    ///   − : 코스트 1 감소 (최소 0)
    /// 원본 SO는 건드리지 않고 복제본에만 적용한다.
    /// </summary>
    public static class CardUpgrade
    {
        public const int PlusValue = 3;
        public const int PlusStatus = 1;

        public static CardData Build(CardData source, UpgradeKind kind) =>
            Build(source, kind, CardState.Fresh);

        /// <summary>
        /// 전투에 올릴 런타임 카드 한 장. 감자 상태(생/싹)와 대장간 강화가 <b>겹쳐서</b> 적용된다 —
        /// 싹난 카드를 강화해두면 그 런이 최고점이고, 다음 런엔 썩어서 사라진다(§8.1 라스트 찬스).
        /// 원본 SO는 건드리지 않고 복제본에만 적용한다.
        /// </summary>
        public static CardData Build(CardData source, UpgradeKind kind, CardState state)
        {
            if (source == null) return null;
            if (kind == UpgradeKind.None && state == CardState.Fresh) return source;

            var c = Object.Instantiate(source);   // 런타임 복제 — 에셋 보존
            c.name = source.name + "_rt";
            c.description = "";                   // 효과에서 다시 조합되도록 비움
            c.displayName = source.displayName;

            // ① 상태별 효과로 교체 (싹이면 sproutEffects)
            if (state != CardState.Fresh)
            {
                c.effects = Clone(source.EffectsFor(state));
                if (state == CardState.Sprouted) c.displayName += " 싹";
            }

            // ② 그 위에 대장간 강화를 얹는다
            if (kind == UpgradeKind.Plus)
            {
                c.displayName += "+";
                if (c.effects != null)
                    foreach (var e in c.effects) Boost(e);
            }
            else if (kind == UpgradeKind.Minus)
            {
                c.displayName += "−";
                c.cost = Mathf.Max(0, source.cost - 1);
            }
            return c;
        }

        static System.Collections.Generic.List<CardEffect> Clone(
            System.Collections.Generic.List<CardEffect> src)
        {
            var o = new System.Collections.Generic.List<CardEffect>();
            if (src == null) return o;
            foreach (var e in src)
                o.Add(new CardEffect(e.type, e.value, e.target, e.status, e.hits, e.duration));
            return o;
        }

        static void Boost(CardEffect e)
        {
            switch (e.type)
            {
                case EffectType.Damage:
                case EffectType.Block:
                case EffectType.TemporaryBlock:
                case EffectType.EffectDefense:
                case EffectType.Heal:
                    e.value += PlusValue; break;
                case EffectType.DamageFromBlock:
                    e.value += 20; break;          // 비율(%)이라 폭을 따로 둔다
                case EffectType.ApplyStatus:
                case EffectType.AmplifyPoisonPerTurn:
                case EffectType.ApplyStatusForTurns:
                case EffectType.DrawPerRemainingEnergy:
                case EffectType.Draw:
                case EffectType.GainEnergy:
                    e.value += PlusStatus; break;
                // DamageConsumingBlock 은 이미 100%라 비율을 올려도 의미가 없다.
                // 대신 같은 카드에 붙은 Damage 효과가 강화된다(흙사태 = 피해 + 방어 소모).
            }
        }

        public static string Suffix(UpgradeKind k) => k switch
        {
            UpgradeKind.Plus => "+",
            UpgradeKind.Minus => "−",
            _ => "",
        };
    }
}
