using System.Linq;
using System.Text;
using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 미니게임에서 돌아오면 런처 위에 뜨는 보상 요약 팝업.
    ///
    /// 런처를 떠나 있는 동안 한 모든 판을 합쳐서 <b>게임별로 몇 판 했고 희귀도별로 몇 장 받았는지</b>만 알린다.
    /// 카드 이름은 알리지 않는다 — 감자창고에서 빨간 점이 붙은 카드로 확인한다.
    /// 조건(몇 점에 몇 장)도 알리지 않는다 — 대략적인 힌트는 실행 로딩 화면의 팁이 맡는다.
    ///
    /// 씬 배선 없이 런처 씬이 뜰 때 자동으로 확인한다. 쌓인 보상이 없으면 아무것도 안 뜬다.
    /// </summary>
    public class RewardSummaryPopup : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != LauncherTransition.LauncherScene) return;
            var summary = TatoReward.TakePending();
            if (summary != null) Show(summary);
        }

        static readonly string[] RarityName = { "일반", "희귀", "초월", "전설" };
        static readonly string[] RarityColor = { "#E4E4E4", "#86BCFF", "#C9A2FF", "#FFD166" };

        public static void Show(TatoReward.PendingSummary summary)
        {
            var font = LauncherTransition.Instance != null && LauncherTransition.Instance.loadingFont != null
                ? LauncherTransition.Instance.loadingFont
                : Font.CreateDynamicFontFromOSFont(new[] { "DOSGothic", "Apple SD Gothic Neo", "Malgun Gothic", "Arial" }, 24);

            // 로딩 오버레이(32760)보다 아래 — 복귀 연출이 걷히면서 드러난다
            var canvasGO = new GameObject("RewardSummaryPopup", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(LauncherTransition.LauncherWidth, LauncherTransition.LauncherHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<RewardSummaryPopup>();

            // 뒤를 어둡게 + 클릭 막기
            var dim = Child<Image>("Dim", canvasGO.transform);
            Stretch(dim.rectTransform);
            dim.color = new Color(0, 0, 0, 0.6f);

            // 본문
            var sb = new StringBuilder();
            foreach (var e in summary.entries)
            {
                var game = StorageData.Catalog.FirstOrDefault(g => g.cardSource == e.source);
                string name = game != null ? game.displayName : e.source.ToString();
                sb.Append($"<b>{name}</b>  ·  {e.plays}판\n");
                if (e.Total == 0)
                {
                    sb.Append("    <color=#A8A8A8>카드 없음 — 조금만 더 하면 카드가 나와요</color>\n");
                    continue;
                }
                var parts = Enumerable.Range(0, 4)
                    .Where(r => e.byRarity != null && r < e.byRarity.Length && e.byRarity[r] > 0)
                    .Select(r => $"<color={RarityColor[r]}>{RarityName[r]} {e.byRarity[r]}장</color>");
                sb.Append("    ").Append(string.Join("   ", parts)).Append('\n');
            }

            int total = summary.TotalCards;
            int lines = summary.entries.Count * 2;
            float bodyH = lines * 34f;
            float panelH = 90f + bodyH + 60f + 76f;

            var panel = Child<Image>("Panel", canvasGO.transform);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(620, panelH);
            panel.color = new Color(0.10f, 0.11f, 0.13f, 0.97f);

            var title = Label("Title", panel.transform, font, 30, TextAnchor.MiddleCenter);
            Place(title.rectTransform, 0, -24, 560, 44);
            title.text = "미니게임 보상";
            title.color = new Color(1f, 0.78f, 0.2f);

            var body = Label("Body", panel.transform, font, 22, TextAnchor.UpperLeft);
            Place(body.rectTransform, 0, -84, 540, bodyH);
            body.text = sb.ToString().TrimEnd();

            var foot = Label("Foot", panel.transform, font, 19, TextAnchor.MiddleCenter);
            Place(foot.rectTransform, 0, -(90f + bodyH), 560, 50);
            foot.text = total > 0
                ? $"카드 {total}장이 감자창고에 들어갔어요\n<color=#FF6B5E>●</color> 빨간 점이 붙은 카드가 새 카드예요"
                : "이번에는 받은 카드가 없어요";
            foot.color = new Color(0.85f, 0.88f, 0.92f);

            // 확인 버튼
            var btnImg = Child<Image>("OK", panel.transform);
            var brt = btnImg.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0);
            brt.anchoredPosition = new Vector2(0, 20);
            brt.sizeDelta = new Vector2(180, 48);
            btnImg.color = new Color(0.62f, 0.45f, 0.16f);
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var bl = Label("Text", btnImg.transform, font, 22, TextAnchor.MiddleCenter);
            Stretch(bl.rectTransform);
            bl.text = "확인";
            btn.onClick.AddListener(() => Destroy(canvasGO));
        }

        // ── 조립 헬퍼 ──
        static T Child<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        static Text Label(string name, Transform parent, Font font, int size, TextAnchor anchor)
        {
            var t = Child<Text>(name, parent);
            t.font = font; t.fontSize = size; t.alignment = anchor;
            t.color = Color.white; t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>패널 위쪽 가운데 기준으로 놓는다 (y는 위에서부터 음수).</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
