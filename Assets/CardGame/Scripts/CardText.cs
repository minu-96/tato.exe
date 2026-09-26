namespace TatoGames.CardGame
{
    /// <summary>카드·상태이상 표기 공용 포맷터 (UI 여러 곳에서 같은 문구를 쓰도록).</summary>
    public static class CardText
    {
        public static string Kor(StatusType s) => s switch
        {
            StatusType.Vulnerable => "취약",
            StatusType.Weak => "약화",
            StatusType.Poison => "중독",
            StatusType.Strength => "힘",
            StatusType.Dexterity => "민첩",
            StatusType.Regen => "재생",
            _ => s.ToString(),
        };

        public static string Effect(CardEffect e) => e.type switch
        {
            EffectType.Damage => e.hits > 1 ? $"{e.value} 피해 ×{e.hits}" : $"{e.value} 피해",
            EffectType.Block => $"{e.value} 블록",
            EffectType.EffectDefense => $"효과방어 {e.value}",
            EffectType.ApplyStatus => $"{Kor(e.status)} {e.value}",
            EffectType.DrawPerRemainingEnergy => e.value == 1 ? "남은 에너지만큼 카드 뽑기"
                                                              : $"남은 에너지×{e.value}장 카드 뽑기",
            EffectType.TemporaryBlock => $"{e.value} 블록 (이번 턴만)",
            EffectType.ReflectHalfDamage => "받은 피해 절반 반사",
            EffectType.DisableBlockThisTurn => "이번 턴 블록 불가",
            EffectType.AmplifyPoisonPerTurn => $"중독 피해 +{e.value}",
            EffectType.ApplyStatusForTurns => $"{e.duration}턴 {Kor(e.status)} +{e.value}",
            EffectType.Heal => $"체력 {e.value} 회복",
            EffectType.Draw => $"카드 {e.value}장 뽑기",
            EffectType.GainEnergy => $"에너지 +{e.value}",
            EffectType.DamageFromBlock => $"방어의 {e.value}%만큼 추가 피해",
            EffectType.DamageConsumingBlock => e.value >= 100
                ? "방어를 모두 소모해 그만큼 피해"
                : $"방어의 {e.value}%를 소모해 그만큼 피해",
            _ => e.type.ToString(),
        };

        /// <summary>키워드 표기 (§10.3).</summary>
        public static string Keyword(CardKeyword k) => k switch
        {
            CardKeyword.Exhaust => "소멸",
            CardKeyword.Retain => "보존",
            CardKeyword.Innate => "무상",
            _ => "",
        };

        /// <summary>
        /// 카드 설명 — <b>항상 실제 효과(effects)에서 조합한다.</b> 키워드는 뒤에 붙인다.
        /// 손으로 쓴 description은 쓰지 않는다: 수치를 고치면 글이 낡고, 실제 동작과 다른 설명이
        /// 나간 적이 있다(연쇄 수확 "턴 종료 시" ↔ 실제는 즉시). 카드목록.md도 같은 조합을 쓴다.
        /// description 필드는 기획 메모용으로만 남는다.
        /// </summary>
        public static string Describe(CardData c)
        {
            if (c == null) return "";
            string kw = Keyword(c.keyword);
            string tail = string.IsNullOrEmpty(kw) ? "" : "\n〈" + kw + "〉";
            if (c.effects == null || c.effects.Count == 0) return tail.TrimStart();
            var lines = new string[c.effects.Count];
            for (int i = 0; i < c.effects.Count; i++) lines[i] = Effect(c.effects[i]);
            return string.Join("\n", lines) + tail;
        }
    }
}
