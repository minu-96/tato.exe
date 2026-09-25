using System.Collections.Generic;
using System.IO;
using System.Linq;
using TatoGames.CardGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MVP 전투 씬 + 스테이지1 적 6종 SO를 조립한다. 메뉴: TatoGames ▸ Card Game ▸ Build MVP Battle.
/// 카드는 CardLibraryGenerator(§8.3)에 맡기고, 여기서는 적 SO 생성 + 보상 풀 할당 +
/// 씬 구성만 한다. 적 수치는 몬스터 AI 이미지 시트(감자밭 1단계) 기준. 여러 번 실행해도 안전.
/// </summary>
public static class CardGameBuilder
{
    const string CardsDir = "Assets/CardGame/Data/Cards";
    const string EnemiesDir = "Assets/CardGame/Data/Enemies";
    const string SceneDir = "Assets/CardGame/Scenes";
    const string ScenePath = SceneDir + "/Battle.unity";
    const string FontPath = "Assets/MoaMoa/Font/WinKor.ttf";

    static readonly string[] StarterIds =
    {
        "atk_potato_punch", "def_peel_shield", "atk_dirt_flick", "def_dirt_smear",
    };

    [MenuItem("TatoGames/Card Game/Build MVP Battle")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 1) 카드 19종 보장
        if (!AssetDatabase.IsValidFolder(CardsDir) ||
            AssetDatabase.LoadAssetAtPath<CardData>($"{CardsDir}/{StarterIds[0]}.asset") == null)
            CardLibraryGenerator.Generate();

        // 2) 시작덱(각 ×2) + 보상 풀(시작덱 제외)
        var deck = new List<CardData>();
        foreach (var id in StarterIds)
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>($"{CardsDir}/{id}.asset");
            if (card == null) { Debug.LogError($"[TatoGames] 시작덱 카드 없음: {id}"); return; }
            deck.Add(card); deck.Add(card);
        }
        // 전투 보상은 '전투 출처' 카드만 준다 (§8 획득처).
        // 미니게임 카드는 그 미니게임을 플레이해야 나온다 — 그래야 런처의 미니게임이 존재 이유를 갖는다.
        var rewardPool = LoadAllCards().Where(c => c.source == AcquireSource.Combat).ToArray();

        // 3) 스테이지1 적 6종 (몬스터 AI 이미지 시트 기준)
        var clod = MakeEnemy("enemy_clod", "흙덩이", EnemyTier.Normal, 15, 0, 0, new[]
        { Atk("공격", 5), Atk("공격", 5), Atk("내리치기", 8) });

        var shellMole = MakeEnemy("enemy_shell_mole", "껍질 두더지", EnemyTier.Normal, 15, 6, 0, new[]
        { Blk("웅크리기", 5), Atk("공격", 5), Atk("공격", 5) });

        var spore = MakeEnemy("enemy_spore_tato", "포자 감자", EnemyTier.Normal, 15, 0, 2, new[]
        { Deb("포자", StatusType.Poison, 3), Atk("공격", 5), Deb("삭힌 바람", StatusType.Weak, 2) });

        var fieldMole = MakeEnemy("enemy_field_mole", "밭두더지", EnemyTier.Normal, 18, 5, 0, new[]
        { Blk("파고들기", 5), Atk("공격", 6), Atk("공격", 4) });

        // 감자벌레: 1페이즈(껍질집) → 공격방어 0 깨지면 2페이즈(본체) 변신
        var grub = MakeEnemy("enemy_potato_grub", "감자벌레집", EnemyTier.Normal, 12, 6, 0, new[]
        { Blk("웅크리기", 4), Atk("공격", 4), Atk("공격", 4) });
        grub.transformOnBlockBreak = true;
        grub.phase2Name = "감자벌레";
        grub.phase2Hp = 10;
        grub.phase2Pattern = new List<EnemyAction>
        { Atk("갉아먹기", 5), Blk("파고들기", 3), Atk("갉아먹기", 5) };
        EditorUtility.SetDirty(grub);

        var scarecrow = MakeEnemy("enemy_scarecrow", "밭의 수호자 허수아비", EnemyTier.Boss, 30, 0, 1, new[]
        { Blk("경계", 5), Atk("내려치기", 6), Deb("씨앗 폭풍", StatusType.Weak, 2), Atk("공격", 8) });

        AssetDatabase.SaveAssets();

        // 4) 씬 조립 (맵이 enemyPool에서 id로 적을 찾는다)
        var enemies = new[] { clod, shellMole, spore, fieldMole, grub, scarecrow };
        BuildScene(deck.ToArray(), clod, rewardPool, enemies);
        RegisterScene();

        AssetDatabase.Refresh();
        Debug.Log($"[TatoGames] MVP 전투 씬 + 적 6종 생성 완료 → {ScenePath} (보상 후보 {rewardPool.Length}장)");
    }

    static List<CardData> LoadAllCards() =>
        AssetDatabase.FindAssets("t:CardData", new[] { CardsDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null).ToList();

    // ── 적 SO (load-or-create) ──
    static EnemyData MakeEnemy(string id, string name, EnemyTier tier, int hp, int block, int ward, EnemyAction[] pattern)
    {
        Directory.CreateDirectory(EnemiesDir);
        string path = $"{EnemiesDir}/{id}.asset";
        var e = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
        if (e == null) { e = ScriptableObject.CreateInstance<EnemyData>(); AssetDatabase.CreateAsset(e, path); }
        e.id = id; e.enemyName = name; e.tier = tier;
        e.hp = hp; e.blockStart = block; e.wardStart = ward; e.wardRefresh = false;
        e.pattern = pattern.ToList(); e.patternLoop = true;
        e.transformOnBlockBreak = false; e.phase2Name = ""; e.phase2Hp = 0; e.phase2Pattern = new List<EnemyAction>();
        EditorUtility.SetDirty(e);
        return e;
    }

    /// <summary>전투 아트 테마 에셋 — 없으면 빈 슬롯으로 만들고, 있으면 그대로 둔다(아트 보존).</summary>
    static BattleTheme LoadOrCreateTheme()
    {
        const string path = "Assets/CardGame/Data/BattleTheme.asset";
        var t = AssetDatabase.LoadAssetAtPath<BattleTheme>(path);
        if (t == null)
        {
            Directory.CreateDirectory("Assets/CardGame/Data");
            t = ScriptableObject.CreateInstance<BattleTheme>();
            AssetDatabase.CreateAsset(t, path);
            EditorUtility.SetDirty(t);
            Debug.Log("[TatoGames] BattleTheme 생성 → " + path + " (여기에 아트를 드롭하세요)");
        }
        return t;
    }

    static EnemyAction Atk(string label, int amount) =>
        new EnemyAction { label = label, kind = EnemyActionKind.Attack, amount = amount, hits = 1 };
    static EnemyAction Blk(string label, int amount) =>
        new EnemyAction { label = label, kind = EnemyActionKind.Block, amount = amount };
    static EnemyAction Deb(string label, StatusType status, int amount) =>
        new EnemyAction { label = label, kind = EnemyActionKind.Debuff, amount = amount, status = status };

    // ── 씬 조립 ──
    static void BuildScene(CardData[] deck, EnemyData enemy, CardData[] rewardPool, EnemyData[] enemyPool)
    {
        Directory.CreateDirectory(SceneDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
        cam.tag = "MainCamera";

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGO = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        // 배경은 BattleController가 BattleTheme.background로 만든다(아트 교체 지점)
        var battle = canvasGO.AddComponent<BattleController>();
        battle.theme = LoadOrCreateTheme();
        battle.starterDeck = deck;
        battle.enemyData = enemy;
        battle.enemyPool = enemyPool;
        battle.rewardPool = rewardPool;
        battle.uiFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void RegisterScene()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
