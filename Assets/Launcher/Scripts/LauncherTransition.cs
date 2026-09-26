using System.Collections;
using System.Collections.Generic;
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

        /// <summary>런처 씬 이름과 런처가 쓰는 창 크기 (Launcher.unity의 CanvasScaler 기준 해상도).</summary>
        public const string LauncherScene = "Launcher";
        public const int LauncherWidth = 1280;
        public const int LauncherHeight = 900;

        [Tooltip("로딩 화면 폰트(DOSGothic 권장). 비우면 OS 폰트로 대체")]
        public Font loadingFont;

        const float FadeTime = 0.3f;
        const float MinShowTime = 0.7f;   // 최소 표시 시간 — 번쩍임 방지
        const float TipShowTime = 1.8f;   // 팁이 있으면 읽을 시간만큼 더 보여준다
        const int BarSlots = 16;

        CanvasGroup group;
        Text label;
        Text bar;
        Text notice;   // 미니게임 실행 시 팁 한 줄 (보상 조건 힌트)
        bool busy;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 씬 인스턴스가 없어도 호출되면 임시 인스턴스를 만든다(폰트 없이).
        /// width·height·fullscreen 은 남겨두었지만 <b>더 이상 해상도를 바꾸지 않는다</b> —
        /// 화면 크기는 DisplaySettings가 앱 전역으로 관리한다.
        /// </summary>
        public static void Request(string sceneName, int width, int height, bool fullscreen, string exeLabel)
        {
            Ensure().Begin(sceneName, width, height, fullscreen, exeLabel, sweepPersistent: false);
        }

        /// <summary>
        /// 미니게임·전투 → 런처 복귀. 나갈 때와 같은 DOS 연출을 쓰되
        ///   · 창 크기를 런처 기준(1280×900)으로 되돌리고
        ///   · 미니게임이 남긴 DontDestroyOnLoad 오브젝트를 정리한다.
        /// 런처 씬을 로드할 수 없으면(미니게임 단독 빌드 등) 기존처럼 앱을 종료한다.
        /// </summary>
        public static void ReturnToLauncher()
        {
            if (SceneManager.GetActiveScene().name == LauncherScene) return;

            if (!Application.CanStreamedLevelBeLoaded(LauncherScene))
            {
                Debug.LogWarning($"[TatoGames] '{LauncherScene}' 씬이 Build Settings에 없음 — 앱 종료로 대체");
                QuitApplication();
                return;
            }

            // 런처로 "돌아가는" 것도 런처를 실행하는 것 — 로딩 문구는 tato.exe
            Ensure().Begin(LauncherScene, LauncherWidth, LauncherHeight, false, "tato.exe",
                           sweepPersistent: true);
        }

        /// <summary>런처 없이 단독 실행 중일 때의 종료 (에디터에서는 플레이 정지).</summary>
        public static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static LauncherTransition Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("LauncherTransition");
                Instance = go.AddComponent<LauncherTransition>();
            }
            return Instance;
        }

        /// <summary>
        /// ESC = 런처로. 미니게임에 나가기 버튼이 없는 화면에서도 빠져나올 수 있게(데모용 비상구).
        /// 단, ESC를 자기 용도로 쓰는 씬은 건드리지 않는다 — 예전엔 일시정지하려고 ESC를 누르면
        /// 일시정지와 동시에 런처로 튕겨서 그 판(과 보상)이 날아갔다. 그 씬들은 일시정지 메뉴 → 타이틀 → 나가기로 나온다.
        /// </summary>
        void Update()
        {
            if (busy) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene == LauncherScene || SceneHandlesEscape(scene)) return;
            ReturnToLauncher();
        }

        /// <summary>ESC를 직접 쓰는 씬 — 늘어나라 인게임(일시정지), 밭의 생존자 라운드(일시정지)·메뉴(타이틀로).</summary>
        static bool SceneHandlesEscape(string scene) =>
            scene == "SnakeInGame" || scene.StartsWith("InGame") || scene == "GameMenu1";

        public void Begin(string sceneName, int width, int height, bool fullscreen, string exeLabel,
                          bool sweepPersistent = false)
        {
            if (busy) return;
            EnsureOverlay();
            StartCoroutine(Run(sceneName, width, height, fullscreen, exeLabel, sweepPersistent));
        }

        IEnumerator Run(string scene, int w, int h, bool fullscreen, string exe, bool sweep)
        {
            busy = true;
            group.blocksRaycasts = true;
            label.text = $"Launching {exe} ...";
            bar.text = Bar(0f);
            // 미니게임을 켤 때는 그 게임의 팁(보상 조건 힌트)을 띄운다.
            // 돌아올 때의 보상 요약은 런처에 뜨는 팝업(RewardSummaryPopup)이 맡는다 — 로딩 화면은 금방 지나가서 못 읽었다
            var entry = sweep ? null : StorageData.FindByScene(scene);
            string tip = entry != null && !string.IsNullOrEmpty(entry.tip) ? entry.tip : "";
            notice.text = tip.Length > 0 ? "TIP  " + tip : "";
            float minShow = tip.Length > 0 ? TipShowTime : MinShowTime;

            // ① 페이드 인
            yield return Fade(0f, 1f, FadeTime);

            // ①' 화면이 가려진 뒤 미니게임이 남긴 상주 오브젝트를 멈춘다 (복귀할 때만)
            var stale = sweep ? SuspendPersistentObjects() : null;

            // ② 완전히 가린 뒤, 가려는 씬이 쓰는 해상도로 맞춘다.
            // 게임마다 아트 비율이 달라 대상별로 따로 기억한다(DisplaySettings).
            // 화면이 가려진 동안 바꾸므로 창이 튀는 게 안 보인다.
            DisplaySettings.ApplyFor(DisplayBootstrap.TargetOf(scene));

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
                bar.text = Bar(Mathf.Min(loadP, elapsed / minShow));
                if (op.progress >= 0.9f && elapsed >= minShow) break;
                yield return null;
            }
            bar.text = Bar(1f);
            op.allowSceneActivation = true;
            yield return op;

            // ⑤' 새 씬이 뜬 뒤에 파괴 — 옛 씬이 살아있는 동안 참조가 끊기지 않게
            if (stale != null)
                foreach (var go in stale)
                    if (go != null) Destroy(go);

            // ⑥ 페이드 아웃
            yield return Fade(1f, 0f, FadeTime);
            group.blocksRaycasts = false;
            busy = false;
        }

        /// <summary>
        /// 미니게임이 씬 밖에 띄워둔 DontDestroyOnLoad 오브젝트를 멈춘다(자기 자신 제외).
        /// 그냥 두면 런처로 돌아온 뒤에도 살아남아서:
        ///   · AspectRatioController(모아모아) — Update에서 16:9를 강제해 런처 창(1280×900)을 계속 되돌린다
        ///   · TimeManager(밭의 생존자)      — 사라진 씬의 Text를 참조해 매 프레임 예외를 낸다
        ///   · Total_Coins · RoundStateManager — 지난 판의 코인·라운드 상태를 다음 판으로 끌고 간다
        /// 먼저 비활성화해 Update를 끊고, 새 씬이 올라온 뒤에 파괴한다(위 ⑤').
        /// </summary>
        List<GameObject> SuspendPersistentObjects()
        {
            Time.timeScale = 1f;        // 게임오버에서 0으로 멈춘 채 나갔을 수 있다
            AudioListener.pause = false;

            var stale = new List<GameObject>();
            try
            {
                // 이 컴포넌트 자신이 DontDestroyOnLoad라, 그 씬의 루트 = 상주 오브젝트 전부
                foreach (var go in gameObject.scene.GetRootGameObjects())
                {
                    if (go == gameObject) continue;
                    go.SetActive(false);
                    stale.Add(go);
                }
            }
            catch (System.Exception e)
            {
                // 정리에 실패해도 전환 자체는 끝까지 가야 한다 (검은 오버레이에 갇히지 않도록)
                Debug.LogWarning($"[TatoGames] 상주 오브젝트 정리 실패 — 전환은 계속: {e.Message}");
            }
            return stale;
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

            // 팁 (미니게임 실행 때만, 평소엔 빈 줄)
            notice = MakeText("Notice", canvasGO.transform, font, 22, TextAnchor.MiddleCenter);   // 팁 한 줄
            var nrt = notice.rectTransform;
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.sizeDelta = new Vector2(1100, 50);
            nrt.anchoredPosition = new Vector2(0, -110);
            notice.color = new Color(0.65f, 1f, 0.7f, 1f);
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
