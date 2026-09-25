using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 업적 탭 — 카드 그리드를 실제 업적 목록으로 채운다.
    /// 달성/미달성은 슬리브 스프라이트로 구분하고, 카드 위에 제목·설명을 얹는다.
    /// </summary>
    public class AchievementView : MonoBehaviour
    {
        [Tooltip("업적 카드가 들어갈 그리드")]
        public RectTransform content;

        public Sprite cardSprite;
        public Sprite sleeveAcquired;
        public Sprite sleeveLocked;
        public Font labelFont;

        [Tooltip("상단 진행도 표시(선택)")]
        public Text progressLabel;

        void OnEnable() => Rebuild();

        public void Rebuild()
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            foreach (var a in Achievements.All)
                BuildCell(a, Achievements.IsUnlocked(a));

            if (progressLabel != null)
                progressLabel.text = $"업적  {Achievements.UnlockedCount} / {Achievements.All.Count}";
        }

        void BuildCell(Achievement a, bool unlocked)
        {
            var cell = new GameObject($"Achieve_{a.id}", typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(content, false);   // 크기는 GridLayoutGroup이 결정
            var bg = cell.GetComponent<Image>();
            bg.sprite = cardSprite;
            bg.raycastTarget = false;
            // 미달성은 어둡게
            bg.color = unlocked ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f);

            var sleeveGO = new GameObject("Sleeve", typeof(RectTransform), typeof(Image));
            sleeveGO.transform.SetParent(cell.transform, false);
            var srt = sleeveGO.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var sImg = sleeveGO.GetComponent<Image>();
            sImg.sprite = unlocked ? sleeveAcquired : sleeveLocked;
            sImg.raycastTarget = false;

            Text(cell.transform, "Title", a.title, 20, TextAnchor.UpperCenter,
                 unlocked ? new Color(1f, 0.88f, 0.55f) : new Color(0.75f, 0.75f, 0.8f),
                 new Vector2(0, -14), new Vector2(150, 52));

            Text(cell.transform, "Desc", a.desc, 15, TextAnchor.LowerCenter,
                 unlocked ? new Color(0.92f, 0.92f, 0.95f) : new Color(0.6f, 0.6f, 0.66f),
                 new Vector2(0, 14), new Vector2(150, 70));
        }

        void Text(Transform parent, string name, string text, int size, TextAnchor anchor,
                  Color color, Vector2 pos, Vector2 size2)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            bool top = anchor == TextAnchor.UpperCenter;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size2;
            var t = go.GetComponent<Text>();
            t.text = text; t.font = labelFont; t.fontSize = size;
            t.alignment = anchor; t.color = color; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
