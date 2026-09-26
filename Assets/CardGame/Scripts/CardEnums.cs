namespace TatoGames.CardGame
{
    /// <summary>카드 타입 4종 (기획서 §10.1)</summary>
    public enum CardType { Attack, Defense, Skill, Root } // 공격 / 방어 / 스킬 / 뿌리

    /// <summary>희귀도 (기획서 §4). 초월·전설은 전투 출처 카드에만 있고 보스 보상으로만 나온다.</summary>
    public enum Rarity { Common, Rare, Transcendent, Legendary } // 일반 / 희귀 / 초월 / 전설

    /// <summary>카드 획득처 (§8.2). Starter(시작덱)는 귀속(bound).</summary>
    public enum AcquireSource { Starter, Neulteona, MoaMoa, FieldSurvivor, Combat, Boss }
    // 시작덱 / 늘어나라 / 모아모아 / 밭의생존자 / 전투 / 보스

    /// <summary>키워드 (§10.3). MVP는 Exhaust(소멸)만.</summary>
    public enum CardKeyword { None, Exhaust, Retain, Innate } // 없음 / 소멸 / 보존 / 무상

    /// <summary>효과 대상.</summary>
    public enum TargetType { Enemy, Self, AllEnemies } // 적 / 자기 / 적 전체

    /// <summary>상태이상 6종 (§11).</summary>
    public enum StatusType { None, Vulnerable, Weak, Poison, Strength, Dexterity, Regen }
    // 없음 / 취약 / 약화 / 중독 / 힘 / 민첩 / 재생

    /// <summary>감자 상태 3단계 (§7·§8.1). 인스턴스가 런 수에 따라 전이.</summary>
    public enum CardState { Fresh, Sprouted, Rotten } // 생 / 싹 / 썩음

    /// <summary>
    /// 효과 종류 — 카드 39종을 데이터로 표현하기 위한 집합.
    /// (실제 실행 로직은 BattleController.ResolveEffect)
    /// </summary>
    public enum EffectType
    {
        Damage,                 // 피해. hits로 연타 (감자 연타 3피해 ×2)
        Block,                  // 공격 방어(블록)
        EffectDefense,          // 효과 방어 — 디버프 N건 무효
        ApplyStatus,            // 상태이상 부여 (status = 종류, value = 스택)
        DrawPerRemainingEnergy, // 턴 종료 시 남은 에너지 × value 장 드로우 (연쇄 수확)
        TemporaryBlock,         // 이번 턴 한정 블록 — 누적되지 않고 다음 턴 시작에 소멸 (철벽)
        ReflectHalfDamage,      // 다음 적 턴에 받은 피해(막아낸 몫 포함) 절반 반사 (되받아치기)
        DisableBlockThisTurn,   // 이번 턴 블록 사용 불가 (돌진의 부작용)
        AmplifyPoisonPerTurn,   // 매 턴 적이 중독으로 얻는 피해 +value (뿌리내림, 지속)
        ApplyStatusForTurns,    // duration턴 동안 매 턴 status +value (곰팡이 정원)

        // ── 메인 게임(전투 출처) 카드용 — 축을 잇는 범용 효과 ──
        // ※ 뒤에만 추가할 것. 중간에 끼우면 기존 SO 에셋의 직렬화(int)가 어긋난다.
        Heal,                   // 체력 즉시 회복 value
        Draw,                   // 카드 value장 뽑기
        GainEnergy,             // 이번 턴 에너지 +value
        DamageFromBlock,        // 누적 방어의 value%만큼 피해 (방어는 유지)
        DamageConsumingBlock    // 누적 방어의 value%를 소모하고, 소모한 만큼 피해
    }
}
