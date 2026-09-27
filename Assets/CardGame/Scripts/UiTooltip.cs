using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 마우스를 올린 아이콘의 설명을 작게 띄우는 말풍선. 화면(캔버스)당 하나를 돌려 쓴다.
    /// 상태이상·방어·인텐트가 아트 아이콘으로 바뀌면서 글자가 사라졌기 때문에 필요하다.
    /// 아이콘 쪽에는 TooltipTrigger를 붙인다.
    /// </summary>
    public class UiTooltip : MonoBehaviour
    {
        const float Width = 280f;
        const float Gap = 6f;       // 아이콘과 말풍선 사이
        const float Margin = 4f;    // 화면 가장자리 여백

        static UiTooltip inst;

        RectTransform rt;
        Text text;
        Object owner;

        /// <summary>캔버스 루트에 말풍선을 하나 만든다. 씬을 다시 불러오면 새로 만든다.</summary>
        public static void Init(Transform canvasRoot, Font font)
        {
            if (inst != null) return;

            var go = new GameObject("Tooltip", typeof(RectTransform), typeof(Image),
                                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(canvasRoot, false);
            inst = go.AddComponent<UiTooltip>();
            inst.rt = go.GetComponent<RectTransform>();
            inst.rt.anchorMin = inst.rt.anchorMax = new Vector2(0.5f, 0.5f);
            inst.rt.pivot = new Vector2(0.5f, 0f);
            inst.rt.sizeDelta = new Vector2(Width, 0f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.07f, 0.07f, 0.09f, 0.96f);
            bg.raycastTarget = false;                       // 말풍선이 마우스를 가로채면 깜빡인다
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.78f, 0.2f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 8, 9);
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fit = go.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // 폭 고정 → 글이 줄바꿈된다
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            inst.text = UiKit.Label("Text", go.transform, font, 17, TextAnchor.UpperLeft);
            inst.text.horizontalOverflow = HorizontalWrapMode.Wrap;
            inst.text.verticalOverflow = VerticalWrapMode.Overflow;
            inst.text.supportRichText = true;
            inst.text.lineSpacing = 1.05f;

            go.SetActive(false);
        }

        /// <summary>anchor 위(자리가 없으면 아래)에 띄운다. who = 띄운 쪽 — 다른 아이콘이 끄지 못하게.</summary>
        public static void Show(Object who, RectTransform anchor, string title, string body)
        {
            if (!Prepare(who, anchor, title, body)) return;
            inst.PlaceNear(anchor);
        }

        /// <summary>anchor 오른쪽(자리가 없으면 왼쪽)에 띄운다 — 카드처럼 세로로 긴 대상 옆.</summary>
        public static void ShowBeside(Object who, RectTransform anchor, string title, string body)
        {
            if (!Prepare(who, anchor, title, body)) return;
            inst.PlaceBeside(anchor);
        }

        static bool Prepare(Object who, RectTransform anchor, string title, string body)
        {
            if (inst == null || anchor == null) return false;
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) return false;
            if (TutorialOverlay.Busy) return false;      // 안내가 떠 있으면 말풍선끼리 겹치지 않게

            inst.owner = who;
            inst.text.text = string.IsNullOrEmpty(title) ? body
                           : string.IsNullOrEmpty(body) ? $"<b><color=#FFC845>{title}</color></b>"
                           : $"<b><color=#FFC845>{title}</color></b>\n{body}";
            inst.gameObject.SetActive(true);
            inst.rt.SetAsLastSibling();                 // 카드 확대 미리보기·패널보다 위
            LayoutRebuilder.ForceRebuildLayoutImmediate(inst.rt);
            return true;
        }

        public static void Hide(Object who)
        {
            if (inst == null || inst.owner != who) return;   // 이미 다른 아이콘으로 옮겨갔으면 그대로
            inst.owner = null;
            inst.gameObject.SetActive(false);
        }

        /// <summary>
        /// 오버레이 캔버스라 월드 좌표 = 화면 픽셀. 아이콘 위쪽 가운데에 붙이고,
        /// 위가 모자라면 아래로 뒤집고, 좌우는 화면 안으로 민다.
        /// </summary>
        void PlaceNear(RectTransform anchor)
        {
            var c = new Vector3[4];
            anchor.GetWorldCorners(c);                  // 0 좌하, 1 좌상, 2 우상, 3 우하
            float s = rt.lossyScale.x;
            Vector2 size = rt.rect.size * s;

            Vector3 pos = (c[1] + c[2]) * 0.5f + Vector3.up * (Gap * s);
            rt.pivot = new Vector2(0.5f, 0f);
            if (pos.y + size.y > Screen.height - Margin)
            {
                rt.pivot = new Vector2(0.5f, 1f);
                pos = (c[0] + c[3]) * 0.5f - Vector3.up * (Gap * s);
            }

            float half = size.x * 0.5f;
            pos.x = Mathf.Clamp(pos.x, half + Margin, Screen.width - half - Margin);
            rt.position = pos;
        }

        /// <summary>anchor 오른쪽 위에 붙이고, 오른쪽이 모자라면 왼쪽으로. 세로는 화면 안으로.</summary>
        void PlaceBeside(RectTransform anchor)
        {
            var c = new Vector3[4];
            anchor.GetWorldCorners(c);                  // 0 좌하, 1 좌상, 2 우상, 3 우하
            float s = rt.lossyScale.x;
            Vector2 size = rt.rect.size * s;
            float gap = Gap * 2f * s;

            rt.pivot = new Vector2(0f, 1f);
            Vector3 pos = c[2] + Vector3.right * gap;
            if (pos.x + size.x > Screen.width - Margin)
            {
                rt.pivot = new Vector2(1f, 1f);
                pos = c[1] - Vector3.right * gap;
            }
            pos.y = Mathf.Clamp(pos.y, size.y + Margin, Screen.height - Margin);
            rt.position = pos;
        }
    }
}
