using System.Collections.Generic;
using System.Linq;
using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 사이드바 버튼 ↔ 콘텐츠 패널 토글.
    /// navButtons[i]를 누르면 panels[i]만 보이고 나머지는 숨긴다.
    /// (홈/미니게임/감자창고/상점/패치노트/업적/설정 순)
    ///
    /// 사이드바 버튼 그림은 "아이콘+글자"(Idle)와 "옅은 하이라이트 판"(Hover)이 따로 온다.
    /// 예전엔 마우스를 올리면 Idle을 Hover로 <b>갈아끼워서 아이콘과 글자가 사라졌고</b>,
    /// 지금 어느 탭인지 표시도 없었다. 그래서 실행할 때 버튼을 두 겹으로 다시 짠다:
    ///   · 버튼 자신 = 하이라이트 판 (평소 투명 · 마우스 올리면 보임)
    ///   · 자식 Label = 아이콘+글자 (항상 보임)
    ///   · 선택된 탭 = 판을 켜두고 왼쪽에 주황 막대 (목업 Main.png)
    /// </summary>
    public class LauncherNavigation : MonoBehaviour
    {
        public List<Button> navButtons = new();
        public List<GameObject> panels = new();

        [Tooltip("시작 시 보여줄 화면 인덱스 (0 = 홈)")]
        public int startIndex = 0;

        /// <summary>탭이 바뀔 때 (패널 이름). 튜토리얼이 처음 연 탭을 안내한다.</summary>
        public event System.Action<string> TabShown;

        /// <summary>지금 보이는 패널 이름.</summary>
        public string CurrentPanel { get; private set; }

        static readonly Color ActiveBarColor = new(0.85f, 0.64f, 0.25f, 1f);

        readonly List<(GameObject bg, GameObject bar)> activeMarks = new();

        void Start()
        {
            SetupSidebar();

            for (int i = 0; i < navButtons.Count; i++)
            {
                int idx = i; // 클로저 캡처 주의
                if (navButtons[i] != null)
                    navButtons[i].onClick.AddListener(() => Show(idx));
            }

            // 아이콘 설명 말풍선 (HUD 칩·감자창고 카드 등) — 런처 캔버스에 하나
            UiTooltip.Init(transform, UiFont());

            // 처음 하는 사람을 위한 안내 (탭을 처음 열 때마다)
            if (!TryGetComponent(out LauncherTutorial _)) gameObject.AddComponent<LauncherTutorial>();

            Show(startIndex);
        }

        /// <summary>런처 글꼴 — 씬의 글자에서 가져온다 (없으면 OS 글꼴).</summary>
        public Font UiFont()
        {
            var t = GetComponentsInChildren<Text>(true).FirstOrDefault(x => x.font != null && x.font.name == "WinKor")
                    ?? GetComponentsInChildren<Text>(true).FirstOrDefault(x => x.font != null);
            return t != null ? t.font
                             : Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 20);
        }

        void SetupSidebar()
        {
            activeMarks.Clear();
            foreach (var b in navButtons)
            {
                if (b == null) { activeMarks.Add((null, null)); continue; }
                var img = b.targetGraphic as Image;
                var label = b.transform.Find("Label");
                if (img == null || label != null)
                {
                    activeMarks.Add((b.transform.Find("ActiveBg")?.gameObject, b.transform.Find("ActiveBar")?.gameObject));
                    continue;
                }

                var labelSprite = img.sprite;                        // 아이콘 + 글자
                var hoverSprite = b.spriteState.highlightedSprite;   // 옅은 하이라이트 판
                // 판 그림이 없으면 옅은 흰색으로 대신한다 (그림 없이 흰 사각형이 뜨지 않게)
                var plateColor = hoverSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);

                // 선택된 탭 바탕 — 하이라이트 판을 켜둔다 (버튼 판과 따로)
                var bg = NewImage("ActiveBg", b.transform, hoverSprite, plateColor);
                Stretch(bg.rectTransform);

                var lbl = NewImage("Label", b.transform, labelSprite, Color.white);
                Stretch(lbl.rectTransform);

                var bar = NewImage("ActiveBar", b.transform, null, ActiveBarColor);
                var brt = bar.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot = new Vector2(0f, 0.5f);
                brt.anchoredPosition = new Vector2(10f, 0f);
                brt.sizeDelta = new Vector2(3f, 30f);

                // 버튼 자신은 하이라이트 판 — 평소엔 투명, 마우스를 올리면 보인다
                img.sprite = hoverSprite;
                img.color = plateColor;
                b.transition = Selectable.Transition.ColorTint;
                var cb = b.colors;
                cb.normalColor = new Color(1f, 1f, 1f, 0f);
                cb.highlightedColor = Color.white;
                cb.pressedColor = new Color(1f, 1f, 1f, 0.75f);
                cb.selectedColor = new Color(1f, 1f, 1f, 0f);
                cb.disabledColor = new Color(1f, 1f, 1f, 0f);
                cb.colorMultiplier = 1f;
                cb.fadeDuration = 0.08f;
                b.colors = cb;

                bg.gameObject.SetActive(false);
                bar.gameObject.SetActive(false);
                activeMarks.Add((bg.gameObject, bar.gameObject));
            }
        }

        static Image NewImage(string name, Transform parent, Sprite sp, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sp;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>이름으로 탭 전환 (인덱스는 순서가 바뀌면 깨지므로 이름으로 찾는다).</summary>
        public bool ShowByName(string panelName)
        {
            for (int i = 0; i < panels.Count; i++)
                if (panels[i] != null && panels[i].name == panelName) { Show(i); return true; }
            Debug.LogWarning($"[TatoGames] 탭을 찾지 못함: {panelName}");
            return false;
        }

        public void Show(int index)
        {
            for (int i = 0; i < panels.Count; i++)
                if (panels[i] != null)
                    panels[i].SetActive(i == index);

            // 선택된 탭 표시 — 사이드바 버튼과 패널은 같은 순서
            for (int i = 0; i < activeMarks.Count; i++)
            {
                bool on = i == index;
                if (activeMarks[i].bg != null) activeMarks[i].bg.SetActive(on);
                if (activeMarks[i].bar != null) activeMarks[i].bar.SetActive(on);
            }

            CurrentPanel = index >= 0 && index < panels.Count && panels[index] != null ? panels[index].name : null;
            if (CurrentPanel != null) TabShown?.Invoke(CurrentPanel);
        }
    }
}
