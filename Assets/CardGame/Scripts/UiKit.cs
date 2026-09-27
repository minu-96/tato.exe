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

        /// <summary>돌 버튼 아트(밝은 황토색) 위의 글자색 — 흰 글자는 묻혀서 읽히지 않는다.</summary>
        public static readonly Color ButtonText = new(0.29f, 0.16f, 0.05f, 1f);

        /// <summary>
        /// 버튼에 Idle/Hover/Pressed 스프라이트를 적용(없으면 색 전환).
        /// 누를 수 없을 때는 어두운 그림 + 반투명(ButtonDim) — SpriteSwap은 비활성 모습이 따로 없어서
        /// 예전엔 '턴 종료'가 눌리지 않는 순간에도 똑같이 보였다.
        /// </summary>
        public static void ApplyButton(Button btn, BattleTheme theme)
        {
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            var label = btn.GetComponentInChildren<Text>(true);
            if (theme != null && theme.buttonIdle != null)
            {
                Apply(img, theme.buttonIdle, theme.cardColor);
                btn.transition = Selectable.Transition.SpriteSwap;
                var ss = btn.spriteState;
                ss.highlightedSprite = theme.buttonHover != null ? theme.buttonHover : theme.buttonIdle;
                ss.pressedSprite = theme.buttonPressed != null ? theme.buttonPressed : theme.buttonIdle;
                ss.selectedSprite = theme.buttonIdle;
                ss.disabledSprite = theme.buttonPressed != null ? theme.buttonPressed : theme.buttonIdle;
                btn.spriteState = ss;
                FitSlicedBorders(img, 0.8f);
                if (label != null) { label.color = ButtonText; label.fontStyle = FontStyle.Bold; }
            }
            else if (theme != null)
            {
                Apply(img, null, theme.cardColor);
                btn.transition = Selectable.Transition.ColorTint;
            }
            if (!btn.TryGetComponent(out ButtonDim _)) btn.gameObject.AddComponent<ButtonDim>();
        }

        /// <summary>
        /// 9-slice 테두리(바위 장식 등)가 칸보다 두꺼우면 비율대로 줄인다.
        /// fill = 테두리가 차지해도 되는 비율 (0.8이면 위아래 테두리 합이 높이의 80%까지).
        /// 줄이지 않으면 Unity가 한 방향만 눌러서 모서리 장식이 찌그러진다.
        /// </summary>
        public static float FitSlicedBorders(Image img, float fill)
        {
            if (img == null || img.sprite == null || img.type != Image.Type.Sliced) return 1f;
            var b = img.sprite.border;                       // x=왼 y=아래 z=오른 w=위
            var size = img.rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = img.rectTransform.sizeDelta;
            float v = (b.y + b.w) / Mathf.Max(1f, size.y * fill);
            float h = (b.x + b.z) / Mathf.Max(1f, size.x * fill);
            float m = Mathf.Max(1f, v, h);
            img.pixelsPerUnitMultiplier = m;
            return m;
        }

        /// <summary>글자 테두리 — 그림 위의 글자가 배경에 묻히지 않게.</summary>
        public static Outline Outline(Graphic g, float distance = 1.5f, Color? color = null)
        {
            if (g == null) return null;
            if (!g.TryGetComponent(out Outline o)) o = g.gameObject.AddComponent<Outline>();
            o.effectColor = color ?? new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(distance, -distance);
            return o;
        }

        static Sprite circle;

        /// <summary>
        /// 흰 원 스프라이트 (알림 점 등). 기본 UI 원형 리소스는 에디터 전용이라 빌드에서 못 쓰므로 코드로 한 번 그려 둔다.
        /// </summary>
        public static Sprite Circle()
        {
            if (circle != null) return circle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255);   // 가장자리 1px 부드럽게
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            circle.name = "UiKit_Circle";
            return circle;
        }

        static Sprite pill;

        /// <summary>
        /// 끝이 둥근 알약 모양 — 원 스프라이트에 9-slice 테두리를 줘서 가로로 늘려도 양끝이 반원으로 남는다.
        /// height를 주면 반지름이 높이의 절반이 되도록 맞춘다.
        /// </summary>
        public static void ApplyPill(Image img, float height)
        {
            if (img == null) return;
            if (pill == null)
            {
                var c = Circle();
                pill = Sprite.Create(c.texture, new Rect(0, 0, c.texture.width, c.texture.height),
                                     new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                     new Vector4(31, 31, 31, 31));
                pill.name = "UiKit_Pill";
            }
            img.sprite = pill;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 31f / Mathf.Max(1f, height * 0.5f);
        }

        // ── 런처 공용: 글자 없는 알약 버튼 · 어두운 입력칸/드롭다운 ──
        // 런처 버튼 그림(GAME START · PURCHASE · 필터 '전체')은 글자가 그림에 박혀 있어서,
        // 다른 글자를 얹으면 두 글자가 겹친다. 다른 문구가 필요한 곳은 이 알약을 쓴다.
        public static readonly Color FieldBg = new(0.16f, 0.18f, 0.22f, 1f);
        public static readonly Color FieldText = new(0.9f, 0.91f, 0.94f, 1f);
        public static readonly Color FieldHint = new(0.55f, 0.57f, 0.62f, 1f);
        public static readonly Color PillGold = new(0.88f, 0.66f, 0.26f, 1f);

        /// <summary>글자 없는 알약 버튼을 새로 만든다.</summary>
        public static Button PillButton(Transform parent, string name, Font font, string caption, Vector2 size,
                                        Color bg, Color fg, int fontSize = 18)
        {
            var img = Img(name, parent, raycast: true);
            img.rectTransform.sizeDelta = size;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = Label("Text", img.rectTransform, font, fontSize);
            Stretch(t.rectTransform);
            t.text = caption;
            StylePillButton(btn, bg, fg);
            return btn;
        }

        /// <summary>있는 버튼을 알약 모양으로 바꾼다 (그림에 박힌 글자를 없앤다).</summary>
        public static void StylePillButton(Button btn, Color bg, Color fg)
        {
            if (btn == null) return;
            if (btn.targetGraphic is Image img)
            {
                var h = img.rectTransform.rect.height > 0 ? img.rectTransform.rect.height : img.rectTransform.sizeDelta.y;
                ApplyPill(img, h);
                img.color = bg;
            }
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;
            foreach (var t in btn.GetComponentsInChildren<Text>(true)) { t.color = fg; t.fontStyle = FontStyle.Bold; }
            if (!btn.TryGetComponent(out ButtonDim _)) btn.gameObject.AddComponent<ButtonDim>();
        }

        /// <summary>
        /// Unity 기본 입력칸을 어두운 테마로. 배경 그림이 없으면(흰 상자) 어두운 알약으로 바꾼다.
        /// </summary>
        public static void StyleDarkInput(InputField field, string placeholder)
        {
            if (field == null) return;
            if (field.targetGraphic is Image img && img.sprite == null)
            {
                ApplyPill(img, img.rectTransform.rect.height > 0 ? img.rectTransform.rect.height : 36f);
                img.color = FieldBg;
                Outline(img, 1f, new Color(1f, 1f, 1f, 0.18f));
            }
            if (field.textComponent != null) { field.textComponent.color = FieldText; field.textComponent.fontSize = 16; }
            if (field.placeholder is Text ph)
            {
                ph.text = placeholder;
                ph.color = FieldHint;
                ph.fontSize = 16;
                ph.fontStyle = FontStyle.Normal;
            }
            // 글자 커서·선택 색도 밝게
            field.caretColor = FieldText;
            field.customCaretColor = true;
            field.selectionColor = new Color(1f, 0.78f, 0.2f, 0.35f);
        }

        /// <summary>
        /// Unity 기본 드롭다운을 어두운 테마로 — 밝은 글자, 열린 목록도 어둡게,
        /// 그림 없는 화살표·체크 표시(흰 네모)는 숨긴다.
        /// replaceBackground면 닫힌 상자 그림도 어두운 알약으로 바꾼다(글자가 박힌 그림을 빌려 쓴 경우).
        /// </summary>
        public static void StyleDarkDropdown(Dropdown dd, int fontSize, bool replaceBackground)
        {
            if (dd == null) return;
            if (dd.targetGraphic is Image bg && (replaceBackground || bg.sprite == null))
            {
                ApplyPill(bg, bg.rectTransform.rect.height > 0 ? bg.rectTransform.rect.height : 40f);
                bg.color = FieldBg;
                Outline(bg, 1f, new Color(1f, 1f, 1f, 0.2f));
            }

            // 닫힌 상자의 글자 — 오른쪽은 화살표 자리
            if (dd.captionText != null)
            {
                dd.captionText.color = FieldText;
                dd.captionText.fontSize = fontSize;
                dd.captionText.alignment = TextAnchor.MiddleLeft;
                var crt = dd.captionText.rectTransform;
                crt.offsetMin = new Vector2(14f, crt.offsetMin.y);
                crt.offsetMax = new Vector2(-30f, crt.offsetMax.y);
            }

            // 화살표 — 그림이 없으면 흰 네모가 되므로 글자(▼)로 대신한다. 닫힌 상자 그림에 화살표가 있으면 숨긴다
            var arrow = dd.transform.Find("Arrow");
            if (arrow != null && arrow.TryGetComponent(out Image aimg) && aimg.sprite == null)
            {
                bool artHasChevron = dd.targetGraphic is Image b2 && b2.sprite != null && !replaceBackground;
                aimg.color = new Color(0, 0, 0, 0);
                if (!artHasChevron && arrow.Find("Glyph") == null)
                {
                    var g = Label("Glyph", arrow, dd.captionText != null ? dd.captionText.font : null, fontSize - 4);
                    Stretch(g.rectTransform);
                    g.text = "▼";
                    g.color = FieldHint;
                }
            }

            // 열린 목록
            var tpl = dd.template;
            if (tpl != null)
            {
                if (tpl.TryGetComponent(out Image timg)) { timg.sprite = null; timg.color = new Color(0.12f, 0.13f, 0.16f, 0.98f); }
                var item = tpl.Find("Viewport/Content/Item");
                if (item != null)
                {
                    // 기본 항목 칸은 20px — 글자를 키우면 줄 높이가 칸보다 커져 글자가 통째로 잘려 안 보였다
                    if (item is RectTransform irt0)
                        irt0.sizeDelta = new Vector2(irt0.sizeDelta.x, Mathf.Max(irt0.sizeDelta.y, fontSize + 14));
                    var check = item.Find("Item Checkmark");
                    if (check != null && check.TryGetComponent(out Image cimg) && cimg.sprite == null)
                        check.gameObject.SetActive(false);
                    if (item.TryGetComponent(out Toggle tg) && tg.targetGraphic is Image ibg)
                    {
                        ibg.color = Color.white;
                        var cb = tg.colors;
                        cb.normalColor = new Color(1f, 1f, 1f, 0f);
                        cb.highlightedColor = new Color(1f, 1f, 1f, 0.14f);
                        cb.pressedColor = new Color(1f, 1f, 1f, 0.22f);
                        cb.selectedColor = new Color(1f, 1f, 1f, 0.08f);
                        cb.colorMultiplier = 1f;
                        tg.colors = cb;
                    }
                }
                if (dd.itemText != null)
                {
                    dd.itemText.color = FieldText;
                    dd.itemText.fontSize = fontSize;
                    dd.itemText.alignment = TextAnchor.MiddleLeft;
                    dd.itemText.verticalOverflow = VerticalWrapMode.Overflow;
                    var irt = dd.itemText.rectTransform;
                    irt.offsetMin = new Vector2(12f, irt.offsetMin.y);
                }
                // 목록 높이도 항목 수에 맞게 (기본 150은 3개짜리 목록에 비해 남는다)
                if (dd.options != null && dd.options.Count > 0)
                {
                    float rows = Mathf.Min(dd.options.Count, 6);
                    tpl.sizeDelta = new Vector2(tpl.sizeDelta.x, rows * (fontSize + 14) + 10f);
                }
                var sb = tpl.Find("Scrollbar");
                if (sb != null)
                {
                    if (sb.TryGetComponent(out Image sbg)) sbg.color = new Color(1f, 1f, 1f, 0.05f);
                    var handle = sb.Find("Sliding Area/Handle");
                    if (handle != null && handle.TryGetComponent(out Image himg)) himg.color = new Color(1f, 1f, 1f, 0.3f);
                }
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
