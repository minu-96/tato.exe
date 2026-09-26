using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 패치노트 탭 — 버전별 변경 사항. 런처가 OS 셸이라는 컨셉을 살려
    /// 프로그램 업데이트 내역처럼 보여준다.
    ///
    /// 내용은 이 스크립트 안의 <see cref="Notes"/>에 있다. 빌드할 때마다 여기만 고치면 된다.
    /// </summary>
    public class PatchNoteView : MonoBehaviour
    {
        [Tooltip("스크롤 콘텐츠")]
        public RectTransform content;
        public Font labelFont;

        /// <summary>최신이 위. (버전, 날짜, 항목들)</summary>
        static readonly (string ver, string date, string[] lines)[] Notes =
        {
            ("v0.7", "2026-09-26", new[]
            {
                "스테이지마다 나오는 몬스터가 달라집니다",
                "전투 중에 나갔다 와도 그 자리에서 이어집니다 (보상 화면 포함)",
                "미니게임에서 얻은 카드는 감자창고로 들어갑니다 — 덱 편성은 직접",
                "덱이 가득 차면 전투 보상 카드가 감자창고로 갑니다",
                "카드에 마우스를 올리면 커집니다 (전투 · 감자창고)",
                "감자창고에서 카드의 코스트와 효과를 볼 수 있습니다",
                "감자창고에 타입 필터(공격·방어·스킬·뿌리) 추가",
                "새로 얻은 카드에 빨간 점이 붙습니다",
                "미니게임에서 돌아오면 얻은 카드를 희귀도별로 알려줍니다",
                "적의 공격 예고에 약화·취약이 반영됩니다",
                "미니게임 카드가 전투에 나오지 않던 문제 수정",
                "밭의 생존자를 완주하면 카드를 받습니다",
                "미니게임에서 ESC로 일시정지하면 런처로 튕기던 문제 수정",
            }),
            ("v0.6", "2026-09-25", new[]
            {
                "감자 상태(생/싹/썩음) 추가 — 카드에 수명이 생겼습니다",
                "미니게임 플레이로 카드를 수급합니다",
                "저장공간 시스템 — 미니게임을 골라서 설치합니다",
                "감자창고에 검색·정렬 추가",
                "게임별 해상도 설정 추가",
                "전투 출처 카드 20종 추가 (총 39종) — 초월·전설은 보스 보상",
            }),
            ("v0.5", "2026-09-24", new[]
            {
                "방어가 턴을 넘겨 누적됩니다",
                "미구현이던 카드 효과 5종 구현",
                "미니게임에서 런처로 돌아올 수 있습니다",
                "스테이지 난이도 재조정",
            }),
            ("v0.4", "2026-09-21", new[]
            {
                "런 구조(10노드·분기·3스테이지) 추가",
                "대장간·휴식 노드 추가",
                "전투 보상(토인 + 카드 3중 1택) 추가",
            }),
        };

        void OnEnable() => Rebuild();

        public void Rebuild()
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            foreach (var (ver, date, lines) in Notes)
            {
                Row($"{ver}   {date}", 26, new Color(1f, 0.85f, 0.5f), 44);
                var sb = new StringBuilder();
                foreach (var l in lines) sb.AppendLine("  · " + l);
                Row(sb.ToString().TrimEnd(), 19, new Color(0.9f, 0.9f, 0.93f), 26 * lines.Length + 12);
                Row("", 14, Color.clear, 16);   // 버전 사이 여백
            }
        }

        void Row(string text, int size, Color color, float height)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(content, false);
            var t = go.GetComponent<Text>();
            t.text = text; t.font = labelFont; t.fontSize = size;
            t.color = color; t.alignment = TextAnchor.UpperLeft; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height;
        }
    }
}
