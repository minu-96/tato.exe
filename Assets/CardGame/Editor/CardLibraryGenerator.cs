using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TatoGames.CardGame;

/// <summary>
/// 기획서 §8.3 MVP 카드 로스터 39종(시작덱 4 + 미니게임 15 + 전투 20)을 ScriptableObject 에셋으로 한 번에 생성.
/// 메뉴: TatoGames ▸ Generate MVP Cards (39).
/// 이미 있는 카드는 GUID를 보존한 채 내용만 갱신(CopySerialized)한다.
/// 모든 수치는 밸런싱 대상 초안 — 생성 후 인스펙터에서 자유롭게 조정.
/// </summary>
public static class CardLibraryGenerator
{
    const string RootDir = "Assets/CardGame/Data";
    const string CardsDir = "Assets/CardGame/Data/Cards";
    // Resources 폴더 — 미니게임 씬이 배선 없이 카드/보상 규칙을 읽을 수 있게 한다
    const string ResDir = "Assets/CardGame/Resources";
    const string LibPath = ResDir + "/CardLibrary.asset";

    [MenuItem("TatoGames/Generate MVP Cards (39)")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(RootDir))
            AssetDatabase.CreateFolder("Assets/CardGame", "Data");
        if (!AssetDatabase.IsValidFolder(CardsDir))
            AssetDatabase.CreateFolder(RootDir, "Cards");

        var roster = BuildRoster();
        foreach (var card in roster)
            Upsert(card);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int linked = RefreshLibrary();
        Debug.Log($"[TatoGames] MVP 카드 {roster.Count}종 생성/갱신 완료 → {CardsDir}\n" +
                  $"            CardLibrary 갱신: 카드 {linked}장 (미니게임 보상 규칙 포함) → {LibPath}");
    }

    /// <summary>
    /// 전 카드를 CardLibrary(Resources)에 물려둔다. 이게 있어야 미니게임·런처가
    /// 씬 배선 없이 카드를 찾을 수 있다. 보상 규칙은 이미 있으면 건드리지 않는다(튜닝 보존).
    /// </summary>
    static int RefreshLibrary()
    {
        if (!AssetDatabase.IsValidFolder(ResDir))
            AssetDatabase.CreateFolder("Assets/CardGame", "Resources");

        var lib = AssetDatabase.LoadAssetAtPath<CardLibrary>(LibPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<CardLibrary>();
            AssetDatabase.CreateAsset(lib, LibPath);
        }

        lib.cards = AssetDatabase.FindAssets("t:CardData", new[] { CardsDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .OrderBy(c => c.id)
            .ToList();

        lib.FillDefaultRules();          // 비어 있을 때만 채운다
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib.cards.Count;
    }

    static void Upsert(CardData src)
    {
        string path = $"{CardsDir}/{src.id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CardData>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(src, path);
        }
        else
        {
            // 디자이너가 인스펙터에서 붙인 아트는 재생성 시 보존 (덮어쓰지 않음)
            src.artwork = existing.artwork;
            EditorUtility.CopySerialized(src, existing); // GUID·참조 보존
        }
    }

    // ── 효과 생성 단축 헬퍼 ──────────────────────────────────────────────
    static CardEffect D(int v, int hits = 1) => new(EffectType.Damage, v, TargetType.Enemy, hits: hits);
    static CardEffect B(int v) => new(EffectType.Block, v, TargetType.Self);
    static CardEffect ED(int v) => new(EffectType.EffectDefense, v, TargetType.Self);
    static CardEffect St(StatusType s, int v, TargetType t = TargetType.Enemy) => new(EffectType.ApplyStatus, v, t, s);
    static CardEffect DrawE(int mult) => new(EffectType.DrawPerRemainingEnergy, mult, TargetType.Self);
    static CardEffect TempB(int v) => new(EffectType.TemporaryBlock, v, TargetType.Self);
    static CardEffect Reflect() => new(EffectType.ReflectHalfDamage, 0, TargetType.Self);
    static CardEffect NoBlock() => new(EffectType.DisableBlockThisTurn, 0, TargetType.Self);
    static CardEffect AmpPoison(int v) => new(EffectType.AmplifyPoisonPerTurn, v, TargetType.Enemy, StatusType.Poison);
    static CardEffect PoisonTurns(int v, int dur) => new(EffectType.ApplyStatusForTurns, v, TargetType.Enemy, StatusType.Poison, duration: dur);

    static CardEffect Heal(int v) => new(EffectType.Heal, v, TargetType.Self);
    static CardEffect Draw(int v) => new(EffectType.Draw, v, TargetType.Self);
    static CardEffect Energy(int v) => new(EffectType.GainEnergy, v, TargetType.Self);
    static CardEffect BlkDmg(int pct) => new(EffectType.DamageFromBlock, pct, TargetType.Enemy);
    static CardEffect BlkBurst(int pct) => new(EffectType.DamageConsumingBlock, pct, TargetType.Enemy);

    static List<CardEffect> L(params CardEffect[] e) => new(e);

    static CardData Make(string id, string name, CardType type, Rarity rarity, int cost,
                         AcquireSource source, string desc,
                         List<CardEffect> effects, List<CardEffect> sprout,
                         CardKeyword keyword = CardKeyword.None)
    {
        var c = ScriptableObject.CreateInstance<CardData>();
        c.id = id;
        c.displayName = name;
        c.description = desc;
        c.type = type;
        c.rarity = rarity;
        c.cost = cost;
        c.source = source;
        c.keyword = keyword;
        c.effects = effects;
        c.sproutEffects = sprout;
        return c;
    }

    // ── MVP 로스터 39종 (§8.3) ───────────────────────────────────────────
    static List<CardData> BuildRoster()
    {
        const CardType ATK = CardType.Attack, DEF = CardType.Defense, SKL = CardType.Skill, ROOT = CardType.Root;
        const Rarity COM = Rarity.Common, RARE = Rarity.Rare,
                     TRANS = Rarity.Transcendent, LEGEND = Rarity.Legendary;
        const CardKeyword EX = CardKeyword.Exhaust;
        const AcquireSource ST = AcquireSource.Starter, NEU = AcquireSource.Neulteona,
                            MOA = AcquireSource.MoaMoa, FIELD = AcquireSource.FieldSurvivor,
                            CBT = AcquireSource.Combat;

        return new List<CardData>
        {
            // 시작덱 4종 (귀속·일반)
            Make("atk_potato_punch", "감자 펀치", ATK, COM, 1, ST,
                 "5 피해", L(D(5)), L(D(7))),
            Make("def_peel_shield", "껍질 방패", DEF, COM, 1, ST,
                 "5 블록", L(B(5)), L(B(7))),
            Make("atk_dirt_flick", "흙털기", ATK, COM, 1, ST,
                 "3 피해 + 취약 2", L(D(3), St(StatusType.Vulnerable, 2)), L(D(3), St(StatusType.Vulnerable, 3))),
            Make("def_dirt_smear", "흙묻히기", DEF, COM, 1, ST,
                 "3 블록 + 효과방어 2", L(B(3), ED(2)), L(B(3), ED(3))),

            // 늘어나라 Pooo-tato — 공격
            Make("atk_combo", "감자 연타", ATK, COM, 1, NEU,
                 "3 피해 ×2", L(D(3, hits: 2)), L(D(4, hits: 2))),
            Make("atk_charge", "돌진", ATK, COM, 1, NEU,
                 "8 피해, 이번 턴 블록 사용 불가", L(D(8), NoBlock()), L(D(10), NoBlock())),
            Make("skl_momentum", "기세", SKL, COM, 1, NEU,
                 "힘 +2", L(St(StatusType.Strength, 2, TargetType.Self)), L(St(StatusType.Strength, 3, TargetType.Self))),
            Make("atk_potato_cannon", "감자 포탄", ATK, RARE, 2, NEU,
                 "12 피해", L(D(12)), L(D(15))),
            Make("atk_chain_harvest", "연쇄 수확", ATK, RARE, 1, NEU,
                 "4 피해 + 남은 에너지만큼 즉시 카드 뽑기", L(D(4), DrawE(1)), L(D(6), DrawE(1))),

            // 모아모아 10tato — 방어(블록+효과방어)
            Make("def_pack", "다지기", DEF, COM, 1, MOA,
                 "6 블록", L(B(6)), L(B(8))),
            Make("def_dirt_wall", "흙벽", DEF, COM, 1, MOA,
                 "5 블록 + 민첩 +1", L(B(5), St(StatusType.Dexterity, 1, TargetType.Self)), L(B(5), St(StatusType.Dexterity, 2, TargetType.Self))),
            Make("def_gas_mask", "방독막", DEF, COM, 1, MOA,
                 "4 블록 + 효과방어 1", L(B(4), ED(1)), L(B(4), ED(2))),
            // 블록은 기본이 누적이라, 한 방에 크게 주는 철벽만 예외로 이번 턴 한정
            Make("def_iron_wall", "철벽", DEF, RARE, 2, MOA,
                 "12 블록 (누적되지 않고 다음 턴에 사라짐)", L(TempB(12)), L(TempB(15))),
            Make("def_counter", "되받아치기", DEF, RARE, 1, MOA,
                 "6 블록 + 다음 적 턴에 받은 피해 절반 반사", L(B(6), Reflect()), L(B(8), Reflect())),

            // 밭의 생존자 — 중독(디버프)+뿌리
            Make("skl_spore", "독포자", SKL, COM, 1, FIELD,
                 "중독 3", L(St(StatusType.Poison, 3)), L(St(StatusType.Poison, 4))),
            Make("skl_taunt", "약 올리기", SKL, COM, 1, FIELD,
                 "약화 2 + 취약 2", L(St(StatusType.Weak, 2), St(StatusType.Vulnerable, 2)), L(St(StatusType.Weak, 3), St(StatusType.Vulnerable, 3))),
            Make("root_rooting", "뿌리내림", ROOT, COM, 1, FIELD,
                 "매 턴 적이 중독으로 얻는 피해 +1 (지속)", L(AmpPoison(1)), L(AmpPoison(2))),
            Make("skl_plague", "역병", SKL, RARE, 2, FIELD,
                 "중독 6 + 취약 3", L(St(StatusType.Poison, 6), St(StatusType.Vulnerable, 3)), L(St(StatusType.Poison, 8), St(StatusType.Vulnerable, 3))),
            // [수정] 중독은 발동 후 매 턴 −1 이라, 매 턴 +1 은 감소와 정확히 상쇄되어
            // 스택이 전혀 안 쌓였다(총 3피해). +2로 올려야 비로소 누적된다.
            // 실측 총 피해: (1,3)=3 → (2,3)=15 / 싹 (2,4)=24
            //   비교) 역병(중독6·2코스트 희귀)=21, 독포자(중독3·1코스트 일반)=6
            Make("root_mold_garden", "곰팡이 정원", ROOT, RARE, 1, FIELD,
                 "3턴 동안 적에게 중독 +2", L(PoisonTurns(2, 3)), L(PoisonTurns(2, 4))),

            // ── 메인 게임(전투 보상) 10종 ──
            // 세 미니게임이 공격·방어·중독 축을 하나씩 가져갔으므로, 여기는 네 번째 축을
            // 만들지 않고 "축을 잇는" 역할만 맡는다: 흐름(드로우·에너지) · 공수 하이브리드 ·
            // 이 게임 고유 규칙인 '누적 방어'를 자원으로 쓰는 카드.
            //
            // ※ 회복 카드는 의도적으로 넣지 않았다. 난이도 전체가 "24전투 × 고정 체력 예산"이라는
            //   누적 소모 위에 서 있어서, 반복 가능한 회복이 들어오면 예산 개념이 무너진다.
            //   실측: 회복·재생 5종만 추가해도 완주율 28% → 99%. 수치 조정으로는 못 막는다.
            Make("atk_dirt_throw", "흙 던지기", ATK, COM, 1, CBT,
                 "2 피해 + 방어의 50%만큼 추가 피해", L(D(2), BlkDmg(50)), L(D(4), BlkDmg(50))),
            Make("skl_tend", "손질", SKL, COM, 1, CBT,
                 "카드 2장 뽑기", L(Draw(2)), L(Draw(3))),
            // [수정] 소멸이 없으면 무한 콤보가 된다 — 코스트 1에 에너지 +1이라 실질 무료인데,
            // 버림 더미로 갔다가 덱이 한 바퀴 돌면 다시 뽑혀 한 턴에 무한 반복된다.
            Make("skl_boost", "북돋우기", SKL, COM, 1, CBT,
                 "에너지 +1 + 카드 1장 뽑기", L(Energy(1), Draw(1)), L(Energy(1), Draw(2)), EX),
            Make("atk_tamp", "되박기", ATK, COM, 1, CBT,
                 "3 피해 + 3 블록", L(D(3), B(3)), L(D(5), B(5))),
            Make("root_settle", "자리잡기", ROOT, COM, 1, CBT,
                 "민첩 +1 + 카드 1장 뽑기",
                 L(St(StatusType.Dexterity, 1, TargetType.Self), Draw(1)),
                 L(St(StatusType.Dexterity, 2, TargetType.Self), Draw(1))),
            Make("skl_sift", "흙 고르기", SKL, COM, 1, CBT,
                 "효과방어 2 + 카드 1장 뽑기", L(ED(2), Draw(1)), L(ED(3), Draw(1))),

            Make("atk_landslide", "흙사태", ATK, RARE, 2, CBT,
                 "4 피해 + 방어를 모두 소모해 그만큼 피해", L(D(4), BlkBurst(100)), L(D(6), BlkBurst(100))),
            Make("skl_chain_sow", "연쇄 파종", SKL, RARE, 1, CBT,
                 "카드 3장 뽑기", L(Draw(3)), L(Draw(4))),
            Make("skl_resolve", "감자의 결의", SKL, RARE, 2, CBT,
                 "힘 +2 + 민첩 +2",
                 L(St(StatusType.Strength, 2, TargetType.Self), St(StatusType.Dexterity, 2, TargetType.Self)),
                 L(St(StatusType.Strength, 3, TargetType.Self), St(StatusType.Dexterity, 3, TargetType.Self))),
            Make("atk_dig", "굴착", ATK, RARE, 1, CBT,
                 "4 피해 + 카드 1장 뽑기", L(D(4), Draw(1)), L(D(6), Draw(1))),

            // ── 메인 게임 확장 10종 — 소멸(§10.3) 키워드와 상위 희귀도를 쓴다 ──
            // 소멸 = 이번 전투에서 다시 안 나온다. 그 대가로 평소보다 센 1회성 효과를 준다.
            // 초월·전설은 수명이 길어(7~10 / 10~13런) 오래 데리고 다니는 축이다.

            // 일반 3
            Make("atk_stomp", "발 구르기", ATK, COM, 1, CBT,
                 "4 피해 + 효과방어 1", L(D(4), ED(1)), L(D(6), ED(1))),
            Make("def_furrow", "고랑 파기", DEF, COM, 1, CBT,
                 "4 블록 + 카드 1장 뽑기", L(B(4), Draw(1)), L(B(6), Draw(1))),
            Make("skl_breather", "한 숨 돌리기", SKL, COM, 1, CBT,
                 "에너지 +2", L(Energy(2)), L(Energy(3)), EX),

            // 희귀 3
            Make("root_deep_tuber", "뿌리 깊은 감자", ROOT, RARE, 1, CBT,
                 "민첩 +3", L(St(StatusType.Dexterity, 3, TargetType.Self)),
                 L(St(StatusType.Dexterity, 5, TargetType.Self)), EX),
            Make("atk_collapse", "흙더미 붕괴", ATK, RARE, 2, CBT,
                 "10 피해 + 방어를 모두 소모해 그만큼 피해",
                 L(D(10), BlkBurst(100)), L(D(14), BlkBurst(100)), EX),
            Make("skl_seed_sow", "씨감자 뿌리기", SKL, RARE, 1, CBT,
                 "카드 4장 뽑기 + 에너지 +1", L(Draw(4), Energy(1)), L(Draw(5), Energy(1)), EX),

            // 초월 3
            Make("root_pulse", "대지의 맥박", ROOT, TRANS, 2, CBT,
                 "힘 +3 + 민첩 +3",
                 L(St(StatusType.Strength, 3, TargetType.Self), St(StatusType.Dexterity, 3, TargetType.Self)),
                 L(St(StatusType.Strength, 4, TargetType.Self), St(StatusType.Dexterity, 4, TargetType.Self)), EX),
            Make("atk_split", "분열", ATK, TRANS, 2, CBT,
                 "6 피해 ×3", L(D(6, hits: 3)), L(D(8, hits: 3)), EX),
            Make("root_taproot", "깊은 뿌리내림", ROOT, TRANS, 1, CBT,
                 "매 턴 적이 중독으로 얻는 피해 +3 (지속)", L(AmpPoison(3)), L(AmpPoison(4)), EX),

            // 전설 1
            Make("skl_potato_king", "감자의 왕", SKL, LEGEND, 2, CBT,
                 "힘 +4 + 민첩 +4 + 카드 2장 뽑기",
                 L(St(StatusType.Strength, 4, TargetType.Self),
                   St(StatusType.Dexterity, 4, TargetType.Self), Draw(2)),
                 L(St(StatusType.Strength, 5, TargetType.Self),
                   St(StatusType.Dexterity, 5, TargetType.Self), Draw(3)), EX),
        };
    }
}
