using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 런처 → 게임 씬 전환. 창 크기가 바뀌는 경우의 시퀀스를 담당한다.
    ///
    ///   ① 오버레이 페이드 인(0.3s) — 화면을 완전히 가림
    ///   ② 가려진 뒤 Screen.SetResolution (창이 튀는 게 안 보임)
    ///   ③ 2~3프레임 대기(리사이즈 완료)
    ///   ④ LoadSceneAsync(allowSceneActivation=false)로 대기
    ///   ⑤ 최소 표시 시간(0.7s) 경과 + 로드 완료 → 씬 활성화
    ///   ⑥ 페이드 아웃
    ///
    /// 오버레이는 DontDestroyOnLoad 캔버스(별도 씬 아님)라 씬 교체 중에도 살아있고,
    /// 앵커가 화면 전체라 해상도가 바뀌어도 알아서 늘어난다. 로딩 화면 언어는 기획서의
    /// "부팅 DOS 연출"과 통일 — Launching &lt;exe&gt; ... [████░░░].
    /// </summary>
    public class LauncherTransition : MonoBehaviour
    {
        public static LauncherTransition Instance { get; private set; }

        [Tooltip("로딩 화면 폰트(DOSGothic 권장). 비우면 OS 폰트로 대체")]
        public Font loadingFont;

        const float FadeTime = 0.3f;
        const float MinShowTime = 0.7f;   // 최소 표시 시간 — 번쩍임 방지
        const int BarSlots = 16;

        CanvasGroup group;
        Text label;
        Text bar;
        bool busy;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>씬 인스턴스가 없어도 호출되면 임시 인스턴스를 만든다(폰트 없이).</summary>
        public static void Request(string sceneName, int width, int height, bool fullscreen, string exeLabel)
        {
            if (Instance == null)
            {
                var go = new GameObject("LauncherTransition");
                Instance = go.AddComponent<LauncherTransition>();
            }
            Instance.Begin(sceneName, width, height, fullscreen, exeLabel);
        }

        public void Begin(string sceneName, int width, int height, bool fullscreen, string exeLabel)
        {
            if (busy) return;
            EnsureOverlay();
            StartCoroutine(Run(sceneName, width, height, fullscreen, exeLabel));
        }

        IEnumerator Run(string scene, int w, int h, bool fullscreen, string exe)
        {
            busy = true;
            group.blocksRaycasts = true;
            label.text = $"Launching {exe} ...";
            bar.text = Bar(0f);

            // ① 페이드 인
            yield return Fade(0f, 1f, FadeTime);

            // ② 완전히 가린 뒤 해상도 변경
            if (w > 0 && h > 0)
                Screen.SetResolution(w, h, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

            // ③ 2~3프레임 대기(리사이즈 완료 확인)
            yield return null; yield return null; yield return null;

            // ④ 비동기 로드 — 활성화 보류
            float start = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(scene);
            if (op == null)
            {
                Debug.LogError($"[TatoGames] 씬 로드 실패: '{scene}' (Build Settings 등록 확인)");
                yield return Fade(1f, 0f, FadeTime);
                group.blocksRaycasts = false; busy = false; yield break;
            }
            op.allowSceneActivation = false;

            // ⑤ 진행 표시 + 최소 표시 시간
            while (true)
            {
                float loadP = Mathf.Clamp01(op.progress / 0.9f);
                float elapsed = Time.unscaledTime - start;
                bar.text = Bar(Mathf.Min(loadP, elapsed / MinShowTime));
                if (op.progress >= 0.9f && elapsed >= MinShowTime) break;
                yield return null;
            }
            bar.text = Bar(1f);
            op.allowSceneActivation = true;
            yield return op;

            // ⑥ 페이드 아웃
            yield return Fade(1f, 0f, FadeTime);
            group.blocksRaycasts = false;
            busy = false;
        }

        string Bar(float t)
        {
            int fill = Mathf.RoundToInt(Mathf.Clamp01(t) * BarSlots);
            return "[" + new string('█', fill) + new string('░', BarSlots - fill) + "]";
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            float e = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, e / dur);
                yield return null;
            }
            group.alpha = to;
        }

        // ── 오버레이 UI를 코드로 조립(한 번만) ──
        void EnsureOverlay()
        {
            if (group != null) return;

            var font = loadingFont != null
                ? loadingFont
                : Font.CreateDynamicFontFromOSFont(
                    new[] { "DungGeunMo", "DOSGothic", "Malgun Gothic", "맑은 고딕", "Consolas", "Arial" }, 32);

            var canvasGO = new GameObject("Overlay",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;   // 항상 최상단
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGO.GetComponent<CanvasGroup>();
            group.alpha = 0f; group.blocksRaycasts = false;

            // 검은 배경(화면 전체)
            var bg = NewChild("BG", canvasGO.transform).AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.05f, 1f);
            Stretch(bg.rectTransform);

            // 실행 문구
            label = MakeText("Label", canvasGO.transform, font, 34, TextAnchor.MiddleCenter);
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(1000, 60);
            lrt.anchoredPosition = new Vector2(0, 20);

            // 진행 바
            bar = MakeText("Bar", canvasGO.transform, font, 34, TextAnchor.MiddleCenter);
            var brt = bar.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(1000, 60);
            brt.anchoredPosition = new Vector2(0, -40);
            bar.color = new Color(1f, 0.78f, 0.2f, 1f);   // 감자색 노랑
        }

        static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static Text MakeText(string name, Transform parent, Font font, int size, TextAnchor anchor)
        {
            var t = NewChild(name, parent).AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor;
            t.color = Color.white; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
