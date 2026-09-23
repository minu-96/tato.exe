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

        public static CardData Build(CardData source, UpgradeKind kind)
        {
            if (source == null) return null;
            if (kind == UpgradeKind.None) return source;

            var c = Object.Instantiate(source);   // 런타임 복제 — 에셋 보존
            c.name = source.name + "_up";
            c.description = "";                   // 효과에서 다시 조합되도록 비움

            if (kind == UpgradeKind.Plus)
            {
                c.displayName = source.displayName + "+";
                if (c.effects != null)
                    foreach (var e in c.effects) Boost(e);
            }
            else
            {
                c.displayName = source.displayName + "−";
                c.cost = Mathf.Max(0, source.cost - 1);
            }
            return c;
        }

        static void Boost(CardEffect e)
        {
            switch (e.type)
            {
                case EffectType.Damage:
                case EffectType.Block:
                case EffectType.EffectDefense:
                    e.value += PlusValue; break;
                case EffectType.ApplyStatus:
                case EffectType.AmplifyPoisonPerTurn:
                case EffectType.ApplyStatusForTurns:
                case EffectType.DrawPerRemainingEnergy:
                    e.value += PlusStatus; break;
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
