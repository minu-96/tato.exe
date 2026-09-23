using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// UI 조립 공용 헬퍼. Apply()가 아트 적용의 핵심 —
    /// 스프라이트를 넣으면 흰 틴트 + (Border가 있으면) 9-slice로 자동 전환되고,
    /// 비어 있으면 폴백 단색으로 그린다. 덕분에 아트를 하나씩 채워 넣을 수 있다(§14.4).
    /// </summary>
    public static class UiKit
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Image Img(string name, Transform parent, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(string name, Transform parent, Font font, int size,
                                 TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor;
            t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>스프라이트가 있으면 아트로, 없으면 단색으로. Border가 있으면 9-slice.</summary>
        public static void Apply(Image img, Sprite sprite, Color fallback)
        {
            if (img == null) return;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }
            else
            {
                img.sprite = null;
                img.color = fallback;
                img.type = Image.Type.Simple;
            }
        }

        /// <summary>버튼에 Idle/Hover/Pressed 스프라이트를 적용(없으면 색 전환).</summary>
        public static void ApplyButton(Button btn, BattleTheme theme)
        {
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            if (theme != null && theme.buttonIdle != null)
            {
                Apply(img, theme.buttonIdle, theme.cardColor);
                btn.transition = Selectable.Transition.SpriteSwap;
                var ss = btn.spriteState;
                ss.highlightedSprite = theme.buttonHover != null ? theme.buttonHover : theme.buttonIdle;
                ss.pressedSprite = theme.buttonPressed != null ? theme.buttonPressed : theme.buttonIdle;
                ss.selectedSprite = theme.buttonIdle;
                btn.spriteState = ss;
            }
            else if (theme != null)
            {
                Apply(img, null, theme.cardColor);
                btn.transition = Selectable.Transition.ColorTint;
            }
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }
    }
}
