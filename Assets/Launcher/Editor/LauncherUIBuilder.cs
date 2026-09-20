using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TatoGames.Launcher;

/// <summary>
/// 런처 셸 UI(목업 8번)를 코드로 조립. 메뉴: TatoGames ▸ Build Launcher Shell.
/// 배경 + 사이드바(로고·나비 7개) + 헤더(칩 5개) + 콘텐츠(화면 7개, 이름 타이틀)를
/// Assets/Resorces/Launcher/Main 아트로 배치하고 Launcher.unity 씬으로 저장한다.
/// 기준 해상도 1280×900. 픽셀 정밀 배치가 아니라 에디터에서 미세조정하는 1차 셸.
/// </summary>
public static class LauncherUIBuilder
{
    const string Base = "Assets/Resorces/Launcher/Main/";
    const string ScenePath = "Assets/Launcher/Scenes/Launcher.unity";

    [MenuItem("TatoGames/Build Launcher Shell")]
    public static void Build()
    {
        // 현재 열린 씬에 저장 안 된 변경이 있으면 사용자에게 물어봄 (실수 방지)
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        FixSpriteImports();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGO = new GameObject("LauncherCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGO.GetComponent<RectTransform>();

        // ── 배경 (풀스크린) ──
        Stretch(AddImage("Background", root, S("background")).rectTransform);

        // ── 사이드바 (좌측 200px) ──
        var sidebar = NewRect("Sidebar", root);
        sidebar.anchorMin = new Vector2(0, 0); sidebar.anchorMax = new Vector2(0, 1);
        sidebar.pivot = new Vector2(0, 0.5f); sidebar.sizeDelta = new Vector2(200, 0);
        sidebar.anchoredPosition = Vector2.zero;

        var logo = AddImage("Logo", sidebar, S("Header/LOGO"));
        TopLeft(logo.rectTransform, 30, 24, 130, 45);

        var nav = NewRect("Nav", sidebar);
        nav.anchorMin = new Vector2(0, 1); nav.anchorMax = new Vector2(1, 1); nav.pivot = new Vector2(0.5f, 1);
        nav.anchoredPosition = new Vector2(0, -90); nav.sizeDelta = new Vector2(0, 420);
        var vlg = nav.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter; vlg.spacing = 10;
        vlg.childControlWidth = vlg.childControlHeight = false;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;

        var navButtons = new List<Button>
        {
            AddButton("Btn_Home",      nav, S("Sidebar/Button_hoom_Idle"),         S("Sidebar/Button_Hover")),
            AddButton("Btn_Minigame",  nav, S("Sidebar/Button_MiniGame_Idle"),     S("Sidebar/Button_Hover")),
            AddButton("Btn_Storage",   nav, S("Sidebar/Button_TATOstorage_Idle"),  S("Sidebar/Button_Hover")),
            AddButton("Btn_Shop",      nav, S("Sidebar/Button_store_Idle"),        S("Sidebar/Button_Hover")),
            AddButton("Btn_Patchnote", nav, S("Sidebar/Button_patchnote_Idle"),    S("Sidebar/Button_Hover")),
            AddButton("Btn_Achieve",   nav, S("Sidebar/Button_achievements_Idle"), S("Sidebar/Button_Hover")),
        };

        var setBtn = AddButton("Btn_Setting", sidebar, S("Sidebar/Button_setting_Idle"), S("Sidebar/Button_Hover"));
        var setRT = setBtn.GetComponent<RectTransform>();
        setRT.anchorMin = setRT.anchorMax = new Vector2(0.5f, 0); setRT.pivot = new Vector2(0.5f, 0);
        setRT.anchoredPosition = new Vector2(0, 30);
        navButtons.Add(setBtn);

        // ── 헤더 (상단 바) ──
        var leftHud = NewRect("Header_Left", root);
        leftHud.anchorMin = leftHud.anchorMax = new Vector2(0, 1); leftHud.pivot = new Vector2(0, 1);
        leftHud.anchoredPosition = new Vector2(212, -16); leftHud.sizeDelta = new Vector2(600, 68);
        var hlgL = leftHud.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlgL.childAlignment = TextAnchor.UpperLeft; hlgL.spacing = 12;
        hlgL.childControlWidth = hlgL.childControlHeight = false;
        hlgL.childForceExpandWidth = hlgL.childForceExpandHeight = false;
        AddImage("Chip_Stage", leftHud, S("Header/UI_Stage"), true);
        AddImage("Chip_Basket", leftHud, S("Header/UI_TATObasket"), true);
        AddImage("Chip_Storage", leftHud, S("Header/UI_TATOstorage"), true);

        var rightHud = NewRect("Header_Right", root);
        rightHud.anchorMin = rightHud.anchorMax = new Vector2(1, 1); rightHud.pivot = new Vector2(1, 1);
        rightHud.anchoredPosition = new Vector2(-20, -16); rightHud.sizeDelta = new Vector2(400, 68);
        var hlgR = rightHud.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlgR.childAlignment = TextAnchor.UpperRight; hlgR.spacing = 12;
        hlgR.childControlWidth = hlgR.childControlHeight = false;
        hlgR.childForceExpandWidth = hlgR.childForceExpandHeight = false;
        AddImage("Chip_TOIN", rightHud, S("Header/UI_TOIN"), true);
        AddImage("Chip_Profile", rightHud, S("Header/UI_Profile"), true);

        // ── 콘텐츠 영역 (사이드바 우측 · 헤더 아래) ──
        var content = NewRect("Content", root);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(200, 0); content.offsetMax = new Vector2(0, -80);

        var panels = new List<GameObject>
        {
            AddPanel("Panel_Home",      content, S("Home/name")),
            AddPanel("Panel_Minigame",  content, S("Minigame/name")),
            AddPanel("Panel_Storage",   content, S("TATOstorage/name")),
            AddPanel("Panel_Shop",      content, S("Shop/name")),
            AddPanel("Panel_Patchnote", content, S("patchnote/name")),
            AddPanel("Panel_Achieve",   content, S("Achievement/name")),
            AddPanel("Panel_Setting",   content, S("Settings/name")),
        };

        // 홈 화면: 중앙 로고 + GAME START 버튼 (목업 2번)
        var homeLogo = AddImage("Home_Logo", panels[0].transform, S("Home/LOGO"), true);
        Center(homeLogo.rectTransform, 0, 60);
        var startBtn = AddButton("Btn_GameStart", panels[0].transform,
                                  S("Home/Button_start/Idle"), S("Home/Button_start/Hover"),
                                  S("Home/Button_start/Pressed"), setSize: false);
        var startRT = startBtn.GetComponent<RectTransform>();
        var startSprite = S("Home/Button_start/Idle");
        Center(startRT, 0, -140);
        if (startSprite != null) startRT.sizeDelta = new Vector2(startSprite.rect.width, startSprite.rect.height);

        // ── 네비게이션 컴포넌트 ──
        var navComp = canvasGO.AddComponent<LauncherNavigation>();
        navComp.navButtons = navButtons;
        navComp.panels = panels;

        Directory.CreateDirectory("Assets/Launcher/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[TatoGames] 런처 셸 생성 완료 → {ScenePath} (Play로 사이드바 클릭 시 화면 전환)");
    }

    // ── 스프라이트 임포트: Sprite / Single 로 통일 ──
    static void FixSpriteImports()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resorces/Launcher" });
        int fixedCount = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            bool changed = false;
            if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
            if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (changed) { ti.SaveAndReimport(); fixedCount++; }
        }
        Debug.Log($"[TatoGames] 런처 스프라이트 임포트 정리: {fixedCount}개 → Sprite/Single");
    }

    // ── 헬퍼 ──
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

    static Image AddImage(string name, Transform parent, Sprite sp, bool nativeSize = false)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sp;
        img.raycastTarget = false;
        if (nativeSize && sp != null) img.SetNativeSize();
        return img;
    }

    static Button AddButton(string name, Transform parent, Sprite idle, Sprite hover,
                            Sprite pressed = null, bool setSize = true)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = idle;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (hover != null)
        {
            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = btn.spriteState;
            ss.highlightedSprite = hover;
            ss.pressedSprite = pressed != null ? pressed : hover;
            ss.selectedSprite = idle;
            btn.spriteState = ss;
        }
        if (setSize)
        {
            rt.sizeDelta = new Vector2(160, 48);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 160; le.preferredHeight = 48;
        }
        return btn;
    }

    static GameObject AddPanel(string name, Transform content, Sprite title)
    {
        var rt = NewRect(name, content);
        Stretch(rt);
        if (title != null)
        {
            // name.png = 1080×160, 콘텐츠 폭 전체를 채우는 화면 타이틀 바 → 상단 꽉 차게
            var t = AddImage("Title", rt, title, true);
            TopLeft(t.rectTransform, 0, 0);
        }
        return rt.gameObject;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void TopLeft(RectTransform rt, float x, float y, float w = 0, float h = 0)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        if (w > 0) rt.sizeDelta = new Vector2(w, h);
    }

    static void Center(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }
}
