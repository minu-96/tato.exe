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
            EffectType.DrawPerRemainingEnergy => $"남은 에너지×{e.value} 드로우",
            EffectType.RetainBlock => "블록 유지",
            EffectType.ReflectHalfDamage => "피해 절반 반사",
            EffectType.DisableBlockThisTurn => "이번 턴 블록 불가",
            EffectType.AmplifyPoisonPerTurn => $"중독 피해 +{e.value}",
            EffectType.ApplyStatusForTurns => $"{e.duration}턴 {Kor(e.status)} +{e.value}",
            _ => e.type.ToString(),
        };

        /// <summary>카드 설명 — description이 있으면 그걸, 없으면 효과를 조합.</summary>
        public static string Describe(CardData c)
        {
            if (c == null) return "";
            if (!string.IsNullOrEmpty(c.description)) return c.description;
            if (c.effects == null || c.effects.Count == 0) return "";
            var lines = new string[c.effects.Count];
            for (int i = 0; i < c.effects.Count; i++) lines[i] = Effect(c.effects[i]);
            return string.Join("\n", lines);
        }
    }
}
