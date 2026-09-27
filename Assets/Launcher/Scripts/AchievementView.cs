using System.Collections.Generic;
using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 업적 탭 — 카드 그리드를 실제 업적 목록으로 채운다.
    /// 카드 모양에 업적 이름·조건·진행도를 쓰고, 달성/미달성은 슬리브 스프라이트와 밝기로 구분한다.
    /// </summary>
    public class AchievementView : MonoBehaviour
    {
        [Tooltip("업적 카드가 들어갈 그리드")]
        public RectTransform content;

        [Tooltip("(안 씀) 목업용 견본 카드 — 글자가 그려져 있어 업적 칸 바탕으로 쓰지 않는다")]
        public Sprite cardSprite;
        public Sprite sleeveAcquired;
        public Sprite sleeveLocked;
        public Font labelFont;

        [Tooltip("상단 진행도 표시(선택)")]
        public Text progressLabel;

        bool placed;

        void OnEnable()
        {
            PlaceProgress();
            Rebuild();
        }

        /// <summary>
        /// 진행도 글자를 그리드 위 오른쪽 줄로 옮기고 그리드를 그만큼 내린다.
        /// 예전엔 화면 가운데(−300, −150)에 놓여 두 번째 줄 업적 카드 위에 겹쳐 떠 있었다.
        /// </summary>
        void PlaceProgress()
        {
            if (placed || progressLabel == null || content == null) return;
            placed = true;

            var scroll = content.parent != null ? content.parent.parent as RectTransform : null;   // Content → Viewport → Scroll
            if (scroll != null && scroll.anchorMin == Vector2.zero && scroll.anchorMax == Vector2.one)
                scroll.offsetMax = new Vector2(scroll.offsetMax.x, Mathf.Min(scroll.offsetMax.y, -212f));

            var rt = progressLabel.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -174f);
            rt.sizeDelta = new Vector2(420f, 32f);
            progressLabel.alignment = TextAnchor.MiddleRight;
            progressLabel.fontSize = 20;
            progressLabel.color = new Color(1f, 0.85f, 0.5f);
        }

        public void Rebuild()
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var c = content.GetChild(i);
                c.SetParent(null, false);   // Destroy는 프레임 끝이라 먼저 떼어야 그리드가 바로 다시 선다
                Destroy(c.gameObject);
            }

            int index = 0;
            foreach (var a in Achievements.All)
                BuildCell(a, Achievements.IsUnlocked(a), index++);

            if (progressLabel != null)
                progressLabel.text = $"업적  {Achievements.UnlockedCount} / {Achievements.All.Count}";
        }

        static readonly Vector2 CellSize = new(166, 250);

        /// <summary>
        /// 업적 칸 — 카드 모양(프레임·이름 띠·설명 칸)에 업적 이름·조건을 쓰고, 그림 창에 달성 표시나 진행도.
        /// 예전엔 견본 카드 그림(card.png — "밭의 생존자 · 6 피해"가 그려진 목업용 카드) 위에 글자를 겹쳐 써서,
        /// 모든 업적이 같은 공격 카드처럼 보이고 제목이 그림 속 카드 이름과 겹쳤다.
        /// </summary>
        void BuildCell(Achievement a, bool unlocked, int index)
        {
            var cell = new GameObject($"Achieve_{a.id}", typeof(RectTransform));
            cell.transform.SetParent(content, false);   // 크기는 GridLayoutGroup이 결정

            var view = CardView.Create(cell.transform, labelFont, CellSize);
            var vrt = (RectTransform)view.transform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0.5f, 1f);
            vrt.anchoredPosition = Vector2.zero;
            // 프레임 색은 돌려 가며 — 한 가지 색만 늘어서 보이지 않게
            view.BindPlain(a.title, a.desc, (CardType)(index % 4), CardLibrary.ThemeOr(null), unlocked);

            // 그림 창 — 달성 표시 또는 진행도
            int target = Mathf.Max(1, a.target);
            int cur = Mathf.Clamp(a.current != null ? a.current() : 0, 0, target);
            var mark = UiKit.Label("Status", view.window.rectTransform, labelFont, unlocked ? 30 : 26);
            UiKit.Stretch(mark.rectTransform, 4, 16, 4, 4);
            mark.fontStyle = FontStyle.Bold;
            mark.text = unlocked ? "달성!" : $"{cur} / {target}";
            mark.color = unlocked ? new Color(1f, 0.85f, 0.35f) : new Color(0.72f, 0.74f, 0.8f);
            UiKit.Outline(mark, 2f);

            if (!unlocked && target > 1)
            {
                // 진행 막대
                var barBg = UiKit.Img("ProgressBg", view.window.rectTransform);
                var brt = barBg.rectTransform;
                brt.anchorMin = new Vector2(0.14f, 0f); brt.anchorMax = new Vector2(0.86f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.anchoredPosition = new Vector2(0f, 10f); brt.sizeDelta = new Vector2(0f, 6f);
                barBg.color = new Color(0f, 0f, 0f, 0.5f);
                var fill = UiKit.Img("Fill", brt);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2((float)cur / target, 1f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                fill.color = new Color(1f, 0.78f, 0.2f);
            }

            // 슬리브 — 달성은 맑게, 미달성은 뿌옇게 (목업의 카드 슬리브)
            var sleeveGO = new GameObject("Sleeve", typeof(RectTransform), typeof(Image));
            sleeveGO.transform.SetParent(cell.transform, false);
            var srt = sleeveGO.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var sImg = sleeveGO.GetComponent<Image>();
            sImg.sprite = unlocked ? sleeveAcquired : sleeveLocked;
            sImg.raycastTarget = false;
            if (sImg.sprite == null) sleeveGO.SetActive(false);

            TooltipTrigger.On(view.hit, a.title,
                unlocked ? $"{a.desc}\n<color=#FFC845>달성했어요!</color>" : $"{a.desc}\n진행 {cur} / {target}");
        }
    }
}
