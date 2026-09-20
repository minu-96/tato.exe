using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TatoGames.CardGame;

/// <summary>
/// 기획서 §8.3 MVP 카드 로스터 19종을 ScriptableObject 에셋으로 한 번에 생성.
/// 메뉴: TatoGames ▸ Generate MVP Cards (19).
/// 이미 있는 카드는 GUID를 보존한 채 내용만 갱신(CopySerialized)한다.
/// 모든 수치는 밸런싱 대상 초안 — 생성 후 인스펙터에서 자유롭게 조정.
/// </summary>
public static class CardLibraryGenerator
{
    const string RootDir = "Assets/CardGame/Data";
    const string CardsDir = "Assets/CardGame/Data/Cards";

    [MenuItem("TatoGames/Generate MVP Cards (19)")]
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
        Debug.Log($"[TatoGames] MVP 카드 {roster.Count}종 생성/갱신 완료 → {CardsDir}");
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
    static CardEffect Retain() => new(EffectType.RetainBlock, 0, TargetType.Self);
    static CardEffect Reflect() => new(EffectType.ReflectHalfDamage, 0, TargetType.Self);
    static CardEffect NoBlock() => new(EffectType.DisableBlockThisTurn, 0, TargetType.Self);
    static CardEffect AmpPoison(int v) => new(EffectType.AmplifyPoisonPerTurn, v, TargetType.Enemy, StatusType.Poison);
    static CardEffect PoisonTurns(int v, int dur) => new(EffectType.ApplyStatusForTurns, v, TargetType.Enemy, StatusType.Poison, duration: dur);

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

    // ── MVP 로스터 19종 (§8.3) ───────────────────────────────────────────
    static List<CardData> BuildRoster()
    {
        const CardType ATK = CardType.Attack, DEF = CardType.Defense, SKL = CardType.Skill, ROOT = CardType.Root;
        const Rarity COM = Rarity.Common, RARE = Rarity.Rare;
        const AcquireSource ST = AcquireSource.Starter, NEU = AcquireSource.Neulteona,
                            MOA = AcquireSource.MoaMoa, FIELD = AcquireSource.FieldSurvivor;

        return new List<CardData>
        {
            // 시작덱 4종 (귀속·일반)
            Make("atk_potato_punch", "감자 펀치", ATK, COM, 1, ST,
                 "5 피해", L(D(5)), L(D(8))),
            Make("def_peel_shield", "껍질 방패", DEF, COM, 1, ST,
                 "5 블록", L(B(5)), L(B(8))),
            Make("atk_dirt_flick", "흙털기", ATK, COM, 1, ST,
                 "3 피해 + 취약 2", L(D(3), St(StatusType.Vulnerable, 2)), L(D(3), St(StatusType.Vulnerable, 3))),
            Make("def_dirt_smear", "흙묻히기", DEF, COM, 1, ST,
                 "3 블록 + 효과방어 2", L(B(3), ED(2)), L(B(3), ED(3))),

            // 늘어나라 Pooo-tato — 공격
            Make("atk_combo", "감자 연타", ATK, COM, 1, NEU,
                 "3 피해 ×2", L(D(3, hits: 2)), L(D(3, hits: 3))),
            Make("atk_charge", "돌진", ATK, COM, 1, NEU,
                 "8 피해, 이번 턴 블록 사용 불가", L(D(8), NoBlock()), L(D(10), NoBlock())),
            Make("skl_momentum", "기세", SKL, COM, 1, NEU,
                 "힘 +2", L(St(StatusType.Strength, 2, TargetType.Self)), L(St(StatusType.Strength, 3, TargetType.Self))),
            Make("atk_potato_cannon", "감자 포탄", ATK, RARE, 2, NEU,
                 "12 피해", L(D(12)), L(D(16))),
            Make("atk_chain_harvest", "연쇄 수확", ATK, RARE, 1, NEU,
                 "4 피해 + 턴 종료 시 남은 에너지 ×1장 뽑기", L(D(4), DrawE(1)), L(D(4), DrawE(2))),

            // 모아모아 10tato — 방어(블록+효과방어)
            Make("def_pack", "다지기", DEF, COM, 1, MOA,
                 "6 블록", L(B(6)), L(B(9))),
            Make("def_dirt_wall", "흙벽", DEF, COM, 1, MOA,
                 "5 블록 + 민첩 +1", L(B(5), St(StatusType.Dexterity, 1, TargetType.Self)), L(B(5), St(StatusType.Dexterity, 2, TargetType.Self))),
            Make("def_gas_mask", "방독막", DEF, COM, 1, MOA,
                 "4 블록 + 효과방어 1", L(B(4), ED(1)), L(B(4), ED(2))),
            Make("def_iron_wall", "철벽", DEF, RARE, 2, MOA,
                 "12 블록, 다음 턴까지 유지", L(B(12), Retain()), L(B(16), Retain())),
            Make("def_counter", "되받아치기", DEF, RARE, 1, MOA,
                 "6 블록, 이번 턴 받은 피해 절반 반사", L(B(6), Reflect()), L(B(9), Reflect())),

            // 밭의 생존자 — 중독(디버프)+뿌리
            Make("skl_spore", "독포자", SKL, COM, 1, FIELD,
                 "중독 3", L(St(StatusType.Poison, 3)), L(St(StatusType.Poison, 5))),
            Make("skl_taunt", "약 올리기", SKL, COM, 1, FIELD,
                 "약화 2 + 취약 2", L(St(StatusType.Weak, 2), St(StatusType.Vulnerable, 2)), L(St(StatusType.Weak, 3), St(StatusType.Vulnerable, 3))),
            Make("root_rooting", "뿌리내림", ROOT, COM, 1, FIELD,
                 "매 턴 적이 중독으로 얻는 피해 +1 (지속)", L(AmpPoison(1)), L(AmpPoison(2))),
            Make("skl_plague", "역병", SKL, RARE, 2, FIELD,
                 "중독 6 + 취약 3", L(St(StatusType.Poison, 6), St(StatusType.Vulnerable, 3)), L(St(StatusType.Poison, 9), St(StatusType.Vulnerable, 3))),
            Make("root_mold_garden", "곰팡이 정원", ROOT, RARE, 1, FIELD,
                 "3턴 동안 적에게 중독 +1", L(PoisonTurns(1, 3)), L(PoisonTurns(2, 3))),
        };
    }
}
