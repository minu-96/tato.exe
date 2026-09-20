using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TatoGames.Launcher;

/// <summary>
/// 비어 있던 탭 패널(감자창고·상점·업적)을 현재 에셋 기준으로 채운다.
/// 메뉴: TatoGames ▸ Fill Tab Panels (Storage·Shop·Achieve).
///
/// LauncherUIBuilder.Build()와 달리 새 씬을 만들지 않고 기존 Launcher.unity를 열어
/// 대상 패널의 (Title 제외) 자식만 지우고 다시 채우므로, 사용자가 손으로 배치한
/// 홈·미니게임 패널은 건드리지 않는다. 여러 번 실행해도 결과가 같다(idempotent).
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

    [MenuItem("TatoGames/Fill Tab Panels (Storage·Shop·Achieve)")]
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

        BuildStorage(content.Find("Panel_Storage"));
        BuildShop(content.Find("Panel_Shop"));
        BuildAchieve(content.Find("Panel_Achieve"));

        EnsureTransition();
        WireLaunchButtons(content);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("[TatoGames] 완료 → 탭 배치 + 전환 시스템 + 실행 버튼 배선");
    }

    // ══════════════════════════════════════════════ 감자창고(컬렉션) ══════════
    // 상단 고정: 등급 필터 5 + 검색 + 정렬 / 하단: 세로 스크롤 카드 그리드
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
        foreach (var name in new[] { "All", "Common", "Rare", "Epic", "Legendary" })
            BtnInLayout(filterBar, "Filter_" + name,
                        S("TATOstorage/Filter/" + name + "_Idle"), null,
                        S("TATOstorage/Filter/" + name + "_Pressed"));

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
        ImgInLayout(toolBar, "SearchBox", S("TATOstorage/Search/Idle"));
        var sort = ImgInLayout(toolBar, "SortDropdown", S("TATOstorage/Sort/Options"));
        var openList = Img(sort.transform, "OpenList", S("TATOstorage/Sort/Dropdown"), 0f, -70f);
        openList.gameObject.SetActive(false); // 열린 목록은 기본 숨김

        // ── 세로 스크롤: "보유 카드"만 표시 ──
        // 지금 보유 = 시작덱 4종(귀속). 미니게임/보상 카드는 획득 시 컬렉션에 추가된다(§10.7).
        // 전용 카드 아트가 없어 card.png를 placeholder로 쓰고 이름 라벨로 구분한다.
        var grid = MakeScroll(panel, "Scroll_Cards", horizontal: false, l: 24, b: 24, r: 24, t: 236);
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(166, 300);   // 카드(250) + 이름 라벨(50)
        g.spacing = new Vector2(40, 24);
        g.padding = new RectOffset(8, 8, 8, 8);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 5;
        var fit = grid.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var labelFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/MoaMoa/Font/WinKor.ttf");
        foreach (var card in StarterDeck)
            OwnedCardCell(grid.transform, card.id, card.name, labelFont);
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
    }

    // ══════════════════════════════════════════════════════ 상점 ══════════════
    // 가로 스크롤: 대표타일(TATO.EXE) + 게임타일 2행 그리드 / 우하단 알림(고정)
    static void BuildShop(Transform panel)
    {
        if (panel == null) { Debug.LogWarning("[TatoGames] Panel_Shop 없음"); return; }
        ClearContent(panel);

        var content = MakeScroll(panel, "Scroll_Shop", horizontal: true, l: 24, b: 28, r: 24, t: 176);
        var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(4, 4, 0, 0);
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        var cfit = content.gameObject.AddComponent<ContentSizeFitter>();
        cfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 대표 타일 — 본편 tato.exe (OWNED 버튼 자리만 비어 있는 완성 아트)
        var featured = ImgInLayout(content, "Featured_TATOEXE", S("Shop/Tile/TATOEXE"));
        Btn(featured.transform, "Btn_Owned", S("Shop/Tile/Button/OWNED_Idle"), null, null, 0f, -188f);

        // 게임 타일 2행 그리드 (가로로 흐름)
        var gridGO = NewRect("Tiles", content);
        var gg = gridGO.gameObject.AddComponent<GridLayoutGroup>();
        gg.cellSize = new Vector2(234, 294);
        gg.spacing = new Vector2(16, 0);
        gg.startAxis = GridLayoutGroup.Axis.Vertical;          // 위→아래 먼저 채우고 다음 열
        gg.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        gg.constraintCount = 2;
        var gfit = gridGO.gameObject.AddComponent<ContentSizeFitter>();
        gfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        gfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for (int i = 0; i < 8; i++)
        {
            var tile = ImgInLayout(gridGO, "Shop_Tile_" + i, S("Minigame/Tile/poootato"));
            Btn(tile.transform, "Btn_Buy",
                S("Shop/Tile/Button/Idle"), S("Shop/Tile/Button/Hover"), S("Shop/Tile/Button/Pressed"),
                0f, -76.5f, 171f, 61f);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // 할인 알림 — 스크롤과 무관하게 우하단 고정
        Img(panel, "Notification", S("Shop/Notification"), 300f, -300f);
    }

    // ══════════════════════════════════════════════════════ 업적 ══════════════
    // 세로 스크롤: 카드 그리드, 각 카드에 슬리브(획득) 오버레이
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
        for (int i = 0; i < 15; i++)
        {
            var card = ImgInLayout(grid.transform, "Achieve_" + i, S("Achievement/card"));
            // 슬리브 프레임 오버레이 — 기본 획득(Acquired). 미획득 카드는 에디터에서
            // Sleeve 스프라이트를 Unacquired로 교체.
            Img(card.transform, "Sleeve", S("Achievement/Card_Sleeve/Acquired"), 0f, 0f);
        }
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
        // 홈: 본편 카드게임 — 메인 씬 미제작이라 씬 이름은 비워둠(인스펙터에서 지정)
        SetLaunch(content, "Panel_Home/Btn_GameStart", "", 1280, 720, "tato.exe");
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
