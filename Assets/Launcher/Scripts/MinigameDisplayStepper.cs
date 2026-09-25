using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 미니게임 타이틀 화면 구석의 해상도 조절 위젯 — `◀  1024×768  ▶`.
    ///
    /// 미니게임 해상도는 런처 설정에 노출하지 않고 <b>그 게임 안에서만</b> 바꾼다.
    /// 조작은 좌우 화살표 두 개로 끝나서 UX가 가볍다.
    ///
    /// 씬에 미리 만들어 둘 필요 없이 <see cref="DisplayBootstrap"/>이 자동으로 띄운다.
    /// </summary>
    public class MinigameDisplayStepper : MonoBehaviour
    {
        DisplayTarget target;
        Text valueText;

        public static void Spawn(DisplayTarget target)
        {
            // 씬을 다시 로드하면 또 만들어지므로 중복 방지
            if (Object.FindFirstObjectByType<MinigameDisplayStepper>() != null) return;

            var canvasGO = new GameObject("DisplayStepper",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;   // 타이틀 UI 위에

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var w = canvasGO.AddComponent<MinigameDisplayStepper>();
            w.target = target;
            w.Build(canvasGO.transform);
        }

        void Build(Transform parent)
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Arial" }, 20);

            // 우측 하단 고정
            var rootGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            rootGO.transform.SetParent(parent, false);
            var rt = rootGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(230, 44);
            rt.anchoredPosition = new Vector2(-16, 16);
            rootGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            valueText = MakeText(rootGO.transform, font, 19, TextAnchor.MiddleCenter);
            var vrt = valueText.rectTransform;
            vrt.anchorMin = new Vector2(0, 0); vrt.anchorMax = new Vector2(1, 1);
            vrt.offsetMin = new Vector2(44, 0); vrt.offsetMax = new Vector2(-44, 0);

            MakeArrow(rootGO.transform, font, "Prev", "◀", new Vector2(0, 0.5f), new Vector2(24, 0), -1);
            MakeArrow(rootGO.transform, font, "Next", "▶", new Vector2(1, 0.5f), new Vector2(-24, 0), +1);

            Refresh();
        }

        void MakeArrow(Transform parent, Font font, string name, string glyph,
                       Vector2 anchor, Vector2 pos, int delta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = new Vector2(38, 38);
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);

            var t = MakeText(go.transform, font, 20, TextAnchor.MiddleCenter);
            t.text = glyph;
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => { DisplaySettings.Step(target, delta); Refresh(); });
        }

        static Text MakeText(Transform parent, Font font, int size, TextAnchor anchor)
        {
            var go = new GameObject("T", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor;
            t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void Refresh()
        {
            if (valueText == null) return;
            int i = DisplaySettings.IndexOf(target);
            int last = DisplaySettings.SizesFor(target).Length - 1;
            valueText.text = DisplaySettings.Describe(target, i);
            // 양끝이면 화살표를 흐리게 — 더 갈 곳이 없다는 표시
            valueText.color = Color.white;
            SetArrowDim("Prev", i <= 0);
            SetArrowDim("Next", i >= last);
        }

        void SetArrowDim(string name, bool dim)
        {
            var t = transform.GetChild(0).Find(name);
            if (t == null) return;
            var img = t.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, dim ? 0.04f : 0.12f);
            var txt = t.GetComponentInChildren<Text>();
            if (txt != null) txt.color = new Color(1f, 1f, 1f, dim ? 0.3f : 1f);
        }
    }
}
