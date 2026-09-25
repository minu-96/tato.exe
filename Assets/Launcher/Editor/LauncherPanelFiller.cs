using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TatoGames.Launcher;
using TatoGames.CardGame;

/// <summary>
/// 비어 있던 탭 패널(감자창고·상점·업적)을 현재 에셋 기준으로 채운다.
/// 메뉴: TatoGames ▸ Fill Tab Panels (Storage·Shop·Achieve).
///
/// LauncherUIBuilder.Build()와 달리 새 씬을 만들지 않고 기존 Launcher.unity를 열어
/// 대상 패널의 (Title 제외) 자식만 지우고 다시 채우므로, 사용자가 손으로 배치한
/// 홈 패널은 건드리지 않는다. 여러 번 실행해도 결과가 같다(idempotent).
///
/// 구조 원칙(해상도 정책 미결 상태 대응 — 좌표 하드코딩 대신 유연 레이아웃):
///  - 각 패널은 Content(1080×820 기준)를 꽉 채우는 Stretch. Title은 상단 160px.
///  - 콘텐츠는 ScrollRect + Viewport(RectMask2D) + Content(LayoutGroup + ContentSizeFitter).
///  - 감자창고/업적 = 세로 스크롤(GridLayoutGroup), 상점 = 가로 스크롤(요청 사항).
///  - 카드/타일은 GridLayoutGroup가 배치 → 항목 수가 바뀌어도 자동 정렬.
///  - 픽셀 정밀이 아니라 1차 셸. 해상도 확정 후 셀 크기·간격만 미세조정.
/// </summary>
public static class LauncherPanelFiller
{
    const string Base = "Assets/Resorces/Launcher/Main/";
    const string ScenePath = "Assets/Launcher/Scenes/Launcher.unity";

    [MenuItem("TatoGames/Fill Tab Panels (Minigame·Storage·Shop·Achieve·Setting)")]
    public static void Fill()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var content = FindContent();
        if (content == null)
        {
            Debug.LogError("[TatoGames] Content 객체를 찾지 못함. 먼저 Build Launcher Shell로 씬을 만드세요.");
            return;
        }

        BuildMinigame(content.Find("Panel_Minigame"));
        BuildSettings(content.Find("Panel_Setting"));
        BuildPatchNote(content.Find("Panel_Patchnote"));
        BuildStorage(content.Find("Panel_Storage"));
        BuildShop(content.Find("Panel_Shop"));
        BuildAchieve(content.Find("Panel_Achieve"));

        EnsureTransition();
        WireLaunchButtons(content);
        WireHudStats(content);
        RegisterLauncherScene();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[TatoGames] 완료 → 미니게임·탭 배치 + 전환 + 실행 버튼 + 토인/컬렉션 연동");
    }

    // ══════════════════════════════════════════════ 감자창고(컬렉션) ══════════
    // 상단 고정: 등급 필터 5 + 검색 + 정렬 / 하단: 세로 스크롤 카드 그리드
    // 미니게임 타일 배치 — 원본 씬 좌표 그대로 (타일 234×294, 버튼 171×61)
    const float TileY = 91f, BtnY = -76.5f;
    static readonly (string obj, string tile, float x, string gameId)[] MinigameTiles =
    {
        ("Game_thepotato", "Minigame/Tile/thepotato", -377f,   "field"),
        ("Game_poootato",  "Minigame/Tile/poootato",  -136.5f, "snake"),
        ("Game_moamoa",    "Minigame/Tile/moamoa",     103f,   "moamoa"),
    };

    /// <summary>
    /// 미니게임 탭 — 타일은 <see cref="MinigameTabView"/>가 런타임에 그린다.
    /// 보유한 게임만 보여야 하고 설치/삭제로 모습이 바뀌므로 고정 배치가 불가능하다.
    /// 여기서는 아트와 배치 값만 넘겨준다.
    /// </summary>
    static void BuildMinigame(Transform panel)
    {
        if (panel == null) { Debug.LogWarning("[TatoGames] Panel_Minigame 없음"); return; }
        EnsureTitle(panel, S("Minigame/name"));
        ClearContent(panel);

        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
        var view = panel.gameObject.GetComponent<MinigameTabView>()
                   ?? panel.gameObject.AddComponent<MinigameTabView>();

        view.labelFont = font;
        view.purchaseTile = S("Minigame/Tile/Purchase");
        view.startIdle = S("Minigame/Tile/Button/Start/Idle");
        view.startHover = S("Minigame/Tile/Button/Start/Hover");
        view.startPressed = S("Minigame/Tile/Button/Start/Pressed");
        view.purchaseIdle = S("Minigame/Tile/Button/Purchase/Idle");
        view.purchaseHover = S("Minigame/Tile/Button/Purchase/Hover");
        view.purchasePressed = S("Minigame/Tile/Button/Purchase/Pressed");

        // 타일 전용 컨테이너 — 패널 전체를 덮는 빈 Rect.
        // 타일은 절대 좌표로 놓이므로 레이아웃 그룹 없이 스트레치만 시킨다.
        var tileRoot = NewRect("Tiles", panel);
        SetStretch(tileRoot, 0, 0, 0, 0);
        view.tileRoot = tileRoot;

        view.tileArts.Clear();
        foreach (var g in StorageData.MiniGames)
            view.tileArts.Add(new MinigameTabView.TileArt { gameId = g.id, sprite = S(g.tileSprite) });

        // 저장공간 현황 — 설치·삭제가 여기서 일어나므로 이 탭에만 둔다
        view.storageLabel = Label(panel, "Label_Storage", "", font, -300f, -170f);
        view.storageLabel.fontSize = 21;

        var barBg = Img(panel, "StorageBar", null, -110f, -206f);
        barBg.rectTransform.sizeDelta = new Vector2(640, 16);
        barBg.color = new Color(1f, 1f, 1f, 0.12f);
        barBg.raycastTarget = false;
        view.storageBarBg = barBg;

        var barFill = Img(barBg.transform, "Fill", null, 0f, 0f);
        barFill.rectTransform.anchorMin = Vector2.zero;
        barFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        barFill.rectTransform.offsetMin = barFill.rectTransform.offsetMax = Vector2.zero;
        barFill.color = new Color(0.35f, 0.62f, 0.85f);
        barFill.raycastTarget = false;
        view.storageBarFill = barFill;

        view.noticeLabel = Label(panel, "Label_Notice", "", font, -300f, -246f);
        view.noticeLabel.fontSize = 19;
        EditorUtility.SetDirty(view);
    }

    /// <summary>패널에 Title이 없으면 만든다(ClearContent가 보존하는 이름).</summary>
    static void EnsureTitle(Transform panel, Sprite sp)
    {
        if (panel.Find("Title") != null || sp == null) return;
        var img = Img(panel, "Title", sp, 0f, 0f);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        img.rectTransform.pivot = new Vector2(0.5f, 1f);
        img.rectTransform.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 설정 탭 — 런처/메인 게임 해상도 드롭다운 + 메인 게임 전체화면 토글.
    /// 미니게임 해상도는 여기 없다(각 타이틀 화면의 ◀▶ 위젯에서만 바꾼다).
    /// 설정 탭 전용 아트가 없어서 감자창고 필터의 알약 버튼 스프라이트를 재사용한다.
    /// </summary>
    static void BuildSettings(Transform panel)
    {
        if (panel == null) { Debug.LogWarning("[TatoGames] Panel_Setting 없음"); return; }
        EnsureTitle(panel, S("Settings/name"));
        ClearContent(panel);

        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
        var idle = S("TATOstorage/Filter/All_Idle");
        var press = S("TATOstorage/Filter/All_Pressed");

        var view = panel.gameObject.GetComponent<DisplaySettingsView>()
                   ?? panel.gameObject.AddComponent<DisplaySettingsView>();

        Label(panel, "Label_Launcher", "런처 해상도", font, -280f, 130f);
        view.launcherDropdown = MakeDropdown(panel, "Dropdown_Launcher", font, idle, 60f, 130f);

        Label(panel, "Label_MainGame", "메인 게임 해상도", font, -280f, 50f);
        view.mainGameDropdown = MakeDropdown(panel, "Dropdown_MainGame", font, idle, 60f, 50f);

        Label(panel, "Label_Fullscreen", "메인 게임 화면", font, -280f, -30f);
        var toggle = Btn(panel, "Btn_Fullscreen", idle, null, press, 60f, -30f, 200, 48);
        view.fullscreenToggle = toggle;
        view.fullscreenToggleLabel = CenterText(toggle.transform, "전체화면", font, 20);

        Label(panel, "Label_Hint",
              "미니게임 해상도는 각 게임 타이틀 화면에서 조절합니다", font, -280f, -110f).fontSize = 18;

        EditorUtility.SetDirty(view);
    }

    /// <summary>레이아웃 그룹 안에 들어가는 검색 입력창. 배경은 기존 검색창 아트.</summary>
    static InputField MakeSearchField(Transform parent, string name, Font font, Sprite bg, float w, float h)
    {
        var go = DefaultControls.CreateInputField(new DefaultControls.Resources { standard = bg });
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);

        var field = go.GetComponent<InputField>();
        foreach (var t in go.GetComponentsInChildren<Text>(true))
        {
            t.font = font; t.fontSize = 18;
            t.color = new Color(0.12f, 0.12f, 0.14f);
        }
        if (field.placeholder is Text ph)
        {
            ph.text = "카드 이름 검색";
            ph.color = new Color(0.45f, 0.45f, 0.5f);
            ph.fontStyle = FontStyle.Normal;
        }
        // 돋보기 아이콘 자리를 비워두도록 좌측 여백을 준다
        var area = go.transform.Find("Text") as RectTransform;
        if (area != null) { area.offsetMin = new Vector2(34, 2); area.offsetMax = new Vector2(-8, -2); }
        var phRt = go.transform.Find("Placeholder") as RectTransform;
        if (phRt != null) { phRt.offsetMin = new Vector2(34, 2); phRt.offsetMax = new Vector2(-8, -2); }
        return field;
    }

    /// <summary>레이아웃 그룹 안에 들어가는 드롭다운. 닫힌 모습·열린 목록 스프라이트를 각각 쓴다.</summary>
    static Dropdown MakeDropdownInLayout(Transform parent, string name, Font font,
                                         Sprite closed, Sprite list, float w, float h)
    {
        var go = DefaultControls.CreateDropdown(new DefaultControls.Resources { standard = closed });
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);

        var dd = go.GetComponent<Dropdown>();
        foreach (var t in go.GetComponentsInChildren<Text>(true))
        {
            t.font = font; t.fontSize = 17;
            t.color = new Color(0.12f, 0.12f, 0.14f);
        }
        // 열린 목록 배경
        if (dd.template != null)
        {
            var img = dd.template.GetComponent<Image>();
            if (img != null && list != null) img.sprite = list;
            dd.template.sizeDelta = new Vector2(dd.template.sizeDelta.x, 200);
        }
        return dd;
    }

    /// <summary>Unity 기본 드롭다운을 코드로 생성(템플릿까지 자동). 스프라이트만 우리 것으로.</summary>
    static Dropdown MakeDropdown(Transform parent, string name, Font font, Sprite bg, float x, float y)
    {
        var res = new DefaultControls.Resources { standard = bg };
        var go = DefaultControls.CreateDropdown(res);
        go.name = name;
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220, 48);
        rt.anchoredPosition = new Vector2(x, y);

        foreach (var t in go.GetComponentsInChildren<Text>(true))
        {
            t.font = font;
            t.fontSize = 20;
            t.color = new Color(0.1f, 0.1f, 0.12f);
        }
        return go.GetComponent<Dropdown>();
    }

    static Text CenterText(Transform parent, string caption, Font font, int size)
    {
        var rt = NewRect("Text", parent);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = rt.gameObject.AddComponent<Text>();
        t.text = caption; t.font = font; t.fontSize = size; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    static Text Label(Transform parent, string name, string text, Font font, float x, float y)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360, 44);
        rt.anchoredPosition = new Vector2(x, y);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text; t.font = font; t.fontSize = 22; t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    /// <summary>패치노트 탭 — 세로 스크롤에 버전별 변경 사항.</summary>
    static void BuildPatchNote(Transform panel)
    {
        if (panel == null) { Debug.LogWarning("[TatoGames] Panel_Patchnote 없음"); return; }
        EnsureTitle(panel, S("patchnote/name"));
        ClearContent(panel);

        var content = MakeScroll(panel, "Scroll_Patch", horizontal: false, l: 40, b: 28, r: 40, t: 176);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4; vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var view = panel.gameObject.GetComponent<PatchNoteView>() ?? panel.gameObject.AddComponent<PatchNoteView>();
        view.content = content;
        view.labelFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
        EditorUtility.SetDirty(view);
    }

    /// <summary>타일 하단의 이름·가격·용량 표시.</summary>
    static Text InfoText(Transform tile, Font font)
    {
        var rt = NewRect("Info", tile);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(220, 52);
        rt.anchoredPosition = new Vector2(0f, 8f);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = 17; t.color = Color.white;
        t.alignment = TextAnchor.LowerCenter; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    static void BuildStorage(Transform panel)
    {
        if (panel == null) { Debug.LogWarning("[TatoGames] Panel_Storage 없음"); return; }
        ClearContent(panel);

        // ── 상단 고정 바 (스크롤 안 됨) ──
        // 좌측: 등급 필터 (일반/희귀/초월/전설 — 에셋명은 Common/Rare/Epic/Legendary)
        var filterBar = NewRect("FilterBar", panel);
        filterBar.anchorMin = filterBar.anchorMax = new Vector2(0, 1);
        filterBar.pivot = new Vector2(0, 1);
        filterBar.anchoredPosition = new Vector2(24, -176);
        filterBar.sizeDelta = new Vector2(560, 36);
        var fhlg = filterBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        fhlg.spacing = 8; fhlg.childAlignment = TextAnchor.MiddleLeft;
        fhlg.childControlWidth = fhlg.childControlHeight = false;
        fhlg.childForceExpandWidth = fhlg.childForceExpandHeight = false;
        // 필터 정의: 에셋명 → 등급 (Epic = 초월/Transcendent)
        var filterDefs = new (string name, bool all, Rarity rarity)[]
        {
            ("All", true, Rarity.Common),
            ("Common", false, Rarity.Common),
            ("Rare", false, Rarity.Rare),
            ("Epic", false, Rarity.Transcendent),
            ("Legendary", false, Rarity.Legendary),
        };
        var cvFilters = new System.Collections.Generic.List<CollectionView.RarityFilter>();
        foreach (var d in filterDefs)
        {
            var btn = BtnInLayout(filterBar, "Filter_" + d.name,
                                  S("TATOstorage/Filter/" + d.name + "_Idle"), null,
                                  S("TATOstorage/Filter/" + d.name + "_Pressed"));
            cvFilters.Add(new CollectionView.RarityFilter { button = btn, all = d.all, rarity = d.rarity });
        }

        // 우측: 검색 + 정렬
        var toolBar = NewRect("SearchSort", panel);
        toolBar.anchorMin = toolBar.anchorMax = new Vector2(1, 1);
        toolBar.pivot = new Vector2(1, 1);
        toolBar.anchoredPosition = new Vector2(-24, -176);
        toolBar.sizeDelta = new Vector2(330, 36);
        var thlg = toolBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        thlg.spacing = 12; thlg.childAlignment = TextAnchor.MiddleRight;
        thlg.childControlWidth = thlg.childControlHeight = false;
        thlg.childForceExpandWidth = thlg.childForceExpandHeight = false;
        // 검색창·정렬 드롭다운은 실제로 동작하는 위젯이다 (예전엔 그림만 있었다).
        // 아트는 기존 스프라이트를 그대로 쓴다: Search/Idle 200×36, Sort/Options 102×36
        var storageFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
        var search = MakeSearchField(toolBar, "SearchBox", storageFont,
                                     S("TATOstorage/Search/Idle"), 200, 36);
        var sortDd = MakeDropdownInLayout(toolBar, "SortDropdown", storageFont,
                                          S("TATOstorage/Sort/Options"),
                                          S("TATOstorage/Sort/Dropdown"), 150, 36);

        // 런 덱 장수 표시 (필터 줄 아래, 우측)
        var deckLbl = NewRect("DeckCount", panel);
        deckLbl.anchorMin = deckLbl.anchorMax = deckLbl.pivot = new Vector2(1, 1);
        deckLbl.anchoredPosition = new Vector2(-24, -220);
        deckLbl.sizeDelta = new Vector2(440, 30);
        var deckTxt = deckLbl.gameObject.AddComponent<Text>();
        deckTxt.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
        deckTxt.fontSize = 20; deckTxt.color = new Color(1f, 0.85f, 0.5f);
        deckTxt.alignment = TextAnchor.MiddleRight;
        deckTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        deckTxt.raycastTarget = false;
        deckTxt.text = "런 덱";

        // ── 세로 스크롤: "보유 카드"만 표시 ──
        // 지금 보유 = 시작덱 4종(귀속). 미니게임/보상 카드는 획득 시 컬렉션에 추가된다(§10.7).
        // 전용 카드 아트가 없어 card.png를 placeholder로 쓰고 이름 라벨로 구분한다.
        var grid = MakeScroll(panel, "Scroll_Cards", horizontal: false, l: 24, b: 24, r: 24, t: 264);
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(166, 300);   // 카드(250) + 이름 라벨(50)
        g.spacing = new Vector2(40, 24);
        g.padding = new RectOffset(8, 8, 8, 8);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 5;
        var fit = grid.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var labelFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
        // 에디터 프리뷰: 시작덱 (플레이 시 CollectionView가 실제 보유분으로 교체)
        foreach (var card in StarterDeck)
            OwnedCardCell(grid.transform, card.id, card.name, labelFont);
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);

        // 런타임: 보유 카드(시작덱 + 게임에서 획득)로 다시 채움 — 게임 보상이 이어진다
        var cv = grid.gameObject.AddComponent<CollectionView>();
        cv.content = grid;
        cv.allCards = LoadAllCardData();
        cv.labelFont = labelFont;
        cv.searchField = search;
        cv.sortDropdown = sortDd;
        cv.cardArt = S("Achievement/card");
        cv.filters = cvFilters;   // 등급 필터 작동 연결
        cv.deckCountLabel = deckTxt;
    }

    static CardData[] LoadAllCardData() =>
        AssetDatabase.FindAssets("t:CardData", new[] { "Assets/CardGame/Data/Cards" })
            .Select(g => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null).ToArray();

    // ══════════════════════════════════════════════════════ 상점 ══════════════
    // 가로 스크롤: 대표타일(TATO.EXE) + 게임타일 2행 그리드 / 우하단 알림(고정)
    /// <summary>
    /// 상점 — 원래 구조 그대로: 대표 타일(tato.exe) + 미니게임 2행 그리드.
    /// 대표 타일은 판매 불가·위치 고정이라 여기서 만들고, 미니게임 타일만
    /// <see cref="ShopView"/>가 런타임에 그린다(보유 여부로 순서·표시가 달라지므로).
    /// </summary>
    static void BuildShop(Transform panel)
    {
        if (panel == null) { Debug.LogWarning("[TatoGames] Panel_Shop 없음"); return; }
        ClearContent(panel);

        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");

        var content = MakeScroll(panel, "Scroll_Shop", horizontal: true, l: 24, b: 28, r: 24, t: 176);
        var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(4, 4, 0, 0);
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        var cfit = content.gameObject.AddComponent<ContentSizeFitter>();
        cfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── 대표 타일 — 본편 tato.exe. 항상 맨 앞, 판매 불가 ──
        var featured = ImgInLayout(content, "Featured_TATOEXE", S("Shop/Tile/TATOEXE"));
        var ownedBtn = Btn(featured.transform, "Btn_Owned", S("Shop/Tile/Button/OWNED_Idle"), null, null, 0f, -188f);
        ownedBtn.interactable = false;
        var core = StorageData.Find("tato");
        var coreInfo = InfoText(featured.transform, font);
        coreInfo.text = core != null ? $"{core.displayName}\n{core.sizeMb}MB · 판매 불가" : "";
        coreInfo.color = new Color(0.85f, 0.88f, 0.95f);

        // ── 미니게임 2행 그리드 (내용은 ShopView가 채운다) ──
        var gridGO = NewRect("Tiles", content);
        var gg = gridGO.gameObject.AddComponent<GridLayoutGroup>();
        gg.cellSize = new Vector2(234, 294);
        gg.spacing = new Vector2(16, 12);
        gg.startAxis = GridLayoutGroup.Axis.Vertical;   // 위→아래 먼저 채우고 다음 열
        gg.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        gg.constraintCount = 2;
        var gfit = gridGO.gameObject.AddComponent<ContentSizeFitter>();
        gfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        gfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // 타일은 런타임에 채워지므로 지금은 비어 있다. 크기를 0으로 두면 가로 레이아웃이
        // 대표 타일 위에 겹쳐 놓으므로, 들어갈 개수만큼 미리 자리를 잡아둔다.
        int cols = Mathf.CeilToInt(StorageData.MiniGames.Count() / 2f);
        gridGO.sizeDelta = new Vector2(cols * 234 + (cols - 1) * 16, 2 * 294 + 12);

        var shop = panel.gameObject.GetComponent<ShopView>() ?? panel.gameObject.AddComponent<ShopView>();
        shop.tileRoot = gridGO;
        shop.labelFont = font;
        shop.buyIdle = S("Shop/Tile/Button/Idle");
        shop.buyHover = S("Shop/Tile/Button/Hover");
        shop.buyPressed = S("Shop/Tile/Button/Pressed");
        shop.tileArts.Clear();
        foreach (var g in StorageData.MiniGames)
            shop.tileArts.Add(new ShopView.TileArt { gameId = g.id, sprite = S(g.tileSprite) });
        shop.noticeLabel = Label(panel, "Label_Notice", "", font, -300f, -320f);
        shop.noticeLabel.fontSize = 19;
        EditorUtility.SetDirty(shop);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // 할인 알림 — 스크롤과 무관하게 우하단 고정
        Img(panel, "Notification", S("Shop/Notification"), 300f, -300f);
    }

    static void BuildAchieve(Transform panel)
    {
        if (panel == null) { Debug.LogWarning("[TatoGames] Panel_Achieve 없음"); return; }
        ClearContent(panel);

        var grid = MakeScroll(panel, "Scroll_Achieve", horizontal: false, l: 24, b: 24, r: 24, t: 176);
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(166, 250);
        g.spacing = new Vector2(40, 24);
        g.padding = new RectOffset(8, 8, 8, 8);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 5;
        var fit = grid.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var achFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
        var av = panel.gameObject.GetComponent<AchievementView>() ?? panel.gameObject.AddComponent<AchievementView>();
        av.content = grid;
        av.cardSprite = S("Achievement/card");
        av.sleeveAcquired = S("Achievement/Card_Sleeve/Acquired");
        av.sleeveLocked = S("Achievement/Card_Sleeve/Unacquired");
        av.labelFont = achFont;
        av.progressLabel = Label(panel, "Label_Progress", "", achFont, -300f, -150f);
        EditorUtility.SetDirty(av);

        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
    }

    // ══════════════════════════════════════════════ 보유 카드(시작덱) ═════════
    // 게임 시작 시 보유 = 시작덱 4종(§10.5 / v0.6 §8.3, 귀속). 데이터/아트가 붙기 전
    // 임시 목록. 실제로는 세이브의 컬렉션 인스턴스에서 읽어와야 한다.
    static readonly (string id, string name)[] StarterDeck =
    {
        ("atk_potato_punch", "감자 펀치"),
        ("def_peel_shield",  "껍질 방패"),
        ("atk_dust_off",     "흙털기"),
        ("def_dirt_on",      "흙묻히기"),
    };

    static void OwnedCardCell(Transform parent, string id, string cardName, Font font)
    {
        var cell = NewRect("Card_" + id, parent);   // 그리드가 크기(166×300) 지정
        // 카드 아트(placeholder) — 상단
        var art = NewRect("Art", cell);
        art.anchorMin = new Vector2(0.5f, 1); art.anchorMax = new Vector2(0.5f, 1); art.pivot = new Vector2(0.5f, 1);
        art.sizeDelta = new Vector2(166, 250); art.anchoredPosition = Vector2.zero;
        var img = art.gameObject.AddComponent<Image>();
        img.sprite = S("Achievement/card"); img.raycastTarget = false;
        // 이름 라벨 — 하단
        var lbl = NewRect("Name", cell);
        lbl.anchorMin = new Vector2(0, 0); lbl.anchorMax = new Vector2(1, 0); lbl.pivot = new Vector2(0.5f, 0);
        lbl.sizeDelta = new Vector2(0, 46); lbl.anchoredPosition = Vector2.zero;
        var txt = lbl.gameObject.AddComponent<Text>();
        txt.text = cardName; txt.font = font; txt.fontSize = 22;
        txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    // ══════════════════════════════════════════════ 전환 시스템 배선 ══════════
    // 미니게임별 대상 씬 + 가정 해상도(각 씬 Canvas 기준 해상도에서 추출 — 확정 필요).
    static void WireLaunchButtons(Transform content)
    {
        // 홈: 본편 카드게임 = MVP 전투 씬(Battle). Build MVP Battle로 생성됨
        SetLaunch(content, "Panel_Home/Btn_GameStart", "Battle", 1280, 720, "tato.exe");
        // 미니게임 3종
        SetLaunch(content, "Panel_Minigame/Game_poootato/Btn_GameStart",
                  "SnakeTitle", 1280, 720, "늘어나라_pooo-tato.exe");
        SetLaunch(content, "Panel_Minigame/Game_moamoa/Btn_GameStart",
                  "MoaMoaTitle", 1920, 1080, "모아모아_10tato.exe");
        SetLaunch(content, "Panel_Minigame/Game_thepotato/Btn_GameStart",
                  "GameStart0", 1920, 1080, "밭의_생존자.exe");
    }

    static void SetLaunch(Transform content, string path, string scene, int w, int h, string exe)
    {
        var t = content.Find(path);
        if (t == null) { Debug.LogWarning($"[TatoGames] 버튼 없음: {path}"); return; }
        if (t.GetComponent<Button>() == null) { Debug.LogWarning($"[TatoGames] Button 아님: {path}"); return; }
        var gl = t.GetComponent<GameLaunchButton>() ?? t.gameObject.AddComponent<GameLaunchButton>();
        gl.sceneName = scene; gl.width = w; gl.height = h; gl.exeLabel = exe;
    }

    // LauncherTransition 씬 오브젝트를 두고 DOSGothic 폰트를 연결(없으면 생성).
    static void EnsureTransition()
    {
        LauncherTransition tr = Object.FindFirstObjectByType<LauncherTransition>();
        if (tr == null)
        {
            var go = new GameObject("LauncherTransition");
            tr = go.AddComponent<LauncherTransition>();
        }
        if (tr.loadingFont == null)
            tr.loadingFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThePotato/TextMesh Pro/DOSGothic.ttf");
    }

    // 이름으로 자손 전체에서 찾기 (칩 위치가 좌/우 어디든 대응)
    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    // 상단 HUD 칩 연동 (§3). 칩 '이름'으로 찾으므로 좌/우 어디로 옮겨도 따라간다.
    //   Chip_TOIN(코인)=토인 · Chip_Basket(바구니)=런 덱 장수 · Chip_Storage(창고)=보유 장수
    static void WireHudStats(Transform content)
    {
        var canvas = content.parent;   // LauncherCanvas
        BindStat(canvas, "Chip_TOIN", HudStat.Stat.Toin);
        BindStat(canvas, "Chip_Basket", HudStat.Stat.DeckCount);
        BindStat(canvas, "Chip_Storage", HudStat.Stat.CollectionCount);
        BindStat(canvas, "Chip_Stage", HudStat.Stat.BestStage);
        BindStat(canvas, "Chip_Profile", HudStat.Stat.Nickname);
    }

    static void BindStat(Transform canvas, string chipName, HudStat.Stat stat)
    {
        var chip = FindDeep(canvas, chipName);
        if (chip == null) { Debug.LogWarning($"[TatoGames] {chipName} 없음 — HUD 연동 생략"); return; }

        // 예전 ToinDisplay 등 사라진 스크립트 잔재 정리
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(chip.gameObject);

        var labelT = chip.Find("Count");
        Text label;
        if (labelT == null)
        {
            var rt = NewRect("Count", chip);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(44, 0); rt.offsetMax = new Vector2(-14, 0);  // 아이콘 우측 여백
            label = rt.gameObject.AddComponent<Text>();
            label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
            label.fontSize = 26; label.color = Color.white;
            label.alignment = TextAnchor.MiddleRight;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
        }
        else label = labelT.GetComponent<Text>();
        label.text = "0";

        var hs = chip.GetComponent<HudStat>() ?? chip.gameObject.AddComponent<HudStat>();
        hs.stat = stat;
        hs.label = label;
    }

    // Launcher 씬을 Build Settings에 등록 (전투 '나가기'에서 로드하려면 필요)
    static void RegisterLauncherScene()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ══════════════════════════════════════════════════════ 스크롤 헬퍼 ═══════
    /// <summary>패널 안에 ScrollRect(+Viewport+Content+Scrollbar)을 만들고 Content Rect를 반환.</summary>
    static RectTransform MakeScroll(Transform panel, string name, bool horizontal,
                                    float l, float b, float r, float t)
    {
        // 외곽(스크롤 영역)
        var view = NewRect(name, panel);
        SetStretch(view, l, b, r, t);
        var sr = view.gameObject.AddComponent<ScrollRect>();
        var bg = view.gameObject.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0);        // 드래그 감지용 투명 그래픽
        sr.horizontal = horizontal; sr.vertical = !horizontal;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 24;

        // 뷰포트 (마스크) — 스크롤바 자리(12px) 확보
        var vp = NewRect("Viewport", view);
        if (horizontal) SetStretch(vp, 0, 12, 0, 0);  // 하단 스크롤바
        else            SetStretch(vp, 0, 0, 12, 0);  // 우측 스크롤바
        vp.gameObject.AddComponent<RectMask2D>();

        // 콘텐츠
        var content = NewRect("Content", vp);
        if (horizontal)
        {
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
        }
        else
        {
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
        }
        content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;

        sr.viewport = vp; sr.content = content;

        // 스크롤바
        var sb = MakeScrollbar(view, horizontal);
        if (horizontal) { sr.horizontalScrollbar = sb; sr.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent; }
        else            { sr.verticalScrollbar = sb;   sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent; }

        return content;
    }

    static Scrollbar MakeScrollbar(Transform scrollView, bool horizontal)
    {
        var rt = NewRect(horizontal ? "Scrollbar H" : "Scrollbar V", scrollView);
        if (horizontal)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(-12, 10); rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(10, 0); rt.anchoredPosition = Vector2.zero;
        }
        var track = rt.gameObject.AddComponent<Image>();
        track.color = new Color(1, 1, 1, 0.05f);
        var sb = rt.gameObject.AddComponent<Scrollbar>();

        var area = NewRect("Sliding Area", rt); SetStretch(area, 1, 1, 1, 1);
        var handle = NewRect("Handle", area); SetStretch(handle, 0, 0, 0, 0);
        var hImg = handle.gameObject.AddComponent<Image>();
        hImg.color = new Color(1, 1, 1, 0.35f);

        sb.handleRect = handle; sb.targetGraphic = hImg;
        sb.direction = horizontal ? Scrollbar.Direction.LeftToRight : Scrollbar.Direction.BottomToTop;
        return sb;
    }

    // ══════════════════════════════════════════════════════ 공통 헬퍼 ═════════
    static Transform FindContent()
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            var t = root.transform.Find("Content");
            if (t != null) return t;
        }
        return null;
    }

    static void ClearContent(Transform panel)
    {
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            var c = panel.GetChild(i);
            if (c.name == "Title") continue;
            Object.DestroyImmediate(c.gameObject);
        }
    }

    static Sprite S(string rel)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(Base + rel + ".png");
        if (sp == null) Debug.LogWarning($"[TatoGames] 스프라이트 없음: {Base}{rel}.png");
        return sp;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    /// <summary>anchorMin(0,0)~Max(1,1) 스트레치 + 가장자리 여백(left,bottom,right,top).</summary>
    static void SetStretch(RectTransform rt, float l, float b, float r, float t)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    /// <summary>센터 앵커로 이미지 배치(원본 크기). 레이아웃 그룹 밖 요소용.</summary>
    static Image Img(Transform parent, string name, Sprite sp, float x, float y)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sp; img.raycastTarget = false;
        if (sp != null) img.SetNativeSize();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        return img;
    }

    /// <summary>레이아웃 그룹의 자식 이미지(원본 크기). 위치는 그룹이 결정.</summary>
    static Image ImgInLayout(Transform parent, string name, Sprite sp)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sp; img.raycastTarget = false;
        if (sp != null) img.SetNativeSize();
        return img;
    }

    /// <summary>레이아웃 그룹의 자식 버튼(원본 크기). 위치는 그룹이 결정.</summary>
    static Button BtnInLayout(Transform parent, string name, Sprite idle, Sprite hover, Sprite pressed)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = idle;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        ApplySwap(btn, idle, hover, pressed);
        if (idle != null) img.SetNativeSize();
        return btn;
    }

    /// <summary>센터 앵커 버튼. w/h 미지정 시 idle 원본 크기. 레이아웃 그룹 밖 요소용.</summary>
    static Button Btn(Transform parent, string name, Sprite idle, Sprite hover, Sprite pressed,
                      float x, float y, float w = 0, float h = 0)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = idle;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        ApplySwap(btn, idle, hover, pressed);
        if (w > 0) rt.sizeDelta = new Vector2(w, h);
        else if (idle != null) img.SetNativeSize();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        return btn;
    }

    static void ApplySwap(Button btn, Sprite idle, Sprite hover, Sprite pressed)
    {
        if (hover == null && pressed == null) return;
        btn.transition = Selectable.Transition.SpriteSwap;
        var ss = btn.spriteState;
        ss.highlightedSprite = hover != null ? hover : idle;
        ss.pressedSprite = pressed != null ? pressed : (hover != null ? hover : idle);
        ss.selectedSprite = idle;
        btn.spriteState = ss;
    }
}
