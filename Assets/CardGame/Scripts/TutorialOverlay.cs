using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 처음 하는 사람을 위한 안내. 화면을 어둡게 덮고 설명할 곳만 밝게 뚫은 뒤 말풍선으로 설명한다.
    ///
    /// - 한 번 본 안내는 다시 뜨지 않는다(PlayerPrefs `tato_tut_*`). 설정 탭 `튜토리얼 다시 보기`,
    ///   전투 화면 상단 `?` 버튼으로 다시 볼 수 있다.
    /// - 떠 있는 동안에는 뒤 화면을 누를 수 없다 — 설명 도중에 카드가 나가거나 탭이 바뀌지 않게.
    /// - 다른 안내가 떠 있으면 줄을 세웠다가 앞의 것이 닫힌 뒤에 띄운다.
    /// - Enter·Space = 다음, ←·Backspace = 이전.
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        public enum Side { Auto, Below, Above, Right, Left }

        public class Step
        {
            public string title;
            public string body;
            /// <summary>밝게 뚫을 곳. null이거나 꺼져 있으면 화면 전체를 어둡게 하고 말풍선을 가운데 띄운다.</summary>
            public System.Func<RectTransform> target;
            /// <summary>이 단계를 보여주기 직전에 할 일 (탭 전환 등).</summary>
            public System.Action onEnter;
            public Side side = Side.Auto;
            public float pad = 8f;

            public Step(string title, string body, System.Func<RectTransform> target = null,
                        Side side = Side.Auto, System.Action onEnter = null)
            {
                this.title = title; this.body = body; this.target = target;
                this.side = side; this.onEnter = onEnter;
            }
        }

        // ══════════════════════════════════════════════ 본 기록 ══════════════
        const string Prefix = "tato_tut_";

        /// <summary>안내 키 전부 — 초기화·다시 보기에서 쓴다. 새 안내를 만들면 여기에도 넣을 것.</summary>
        public static readonly string[] AllKeys =
        {
            Keys.LauncherIntro, Keys.LauncherStorage, Keys.LauncherMinigame, Keys.LauncherShop,
            Keys.BattleBasics, Keys.BattleReward, Keys.BattleMap, Keys.BattleForge, Keys.BattleDefeat,
            Keys.HintEnemyBlock, Keys.HintEnemyWard, Keys.HintStatus, Keys.HintCardDetail,
        };

        public static class Keys
        {
            public const string LauncherIntro = "launcher_intro";
            public const string LauncherStorage = "launcher_storage";
            public const string LauncherMinigame = "launcher_minigame";
            public const string LauncherShop = "launcher_shop";
            public const string BattleBasics = "battle_basics";
            public const string BattleReward = "battle_reward";
            public const string BattleMap = "battle_map";
            public const string BattleForge = "battle_forge";
            public const string BattleDefeat = "battle_defeat";
            public const string HintEnemyBlock = "hint_enemy_block";
            public const string HintEnemyWard = "hint_enemy_ward";
            public const string HintStatus = "hint_status";
            /// <summary>카드 우클릭(자세히 보기)을 한 번 써 봤나 — 전까지 커진 카드에 꼬리표가 붙는다.</summary>
            public const string HintCardDetail = "hint_card_detail";
        }

        public static bool Seen(string key) => PlayerPrefs.GetInt(Prefix + key, 0) != 0;

        public static void MarkSeen(string key)
        {
            PlayerPrefs.SetInt(Prefix + key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>모든 안내를 "안 봄"으로 되돌린다 (다시 보기 · 세이브 초기화).</summary>
        public static void ResetAll()
        {
            foreach (var k in AllKeys) PlayerPrefs.DeleteKey(Prefix + k);
            PlayerPrefs.Save();
        }

        // ══════════════════════════════════════════════ 실행 · 대기열 ══════════
        class Request
        {
            public Transform root; public Font font; public string key;
            public List<Step> steps; public System.Action onClosed;
        }

        static TutorialOverlay current;
        static readonly List<Request> queue = new();

        /// <summary>지금 안내가 떠 있거나 기다리는 중인가.</summary>
        public static bool Busy
        {
            get
            {
                queue.RemoveAll(r => r.root == null);   // 씬이 바뀌어 사라진 화면의 대기분은 버린다
                return current != null || queue.Count > 0;
            }
        }

        /// <summary>
        /// 안내를 띄운다. force가 아니면 이미 본 안내는 무시한다.
        /// 다른 안내가 떠 있으면 줄을 세운다(같은 키는 한 번만).
        /// </summary>
        public static void Run(Transform canvasRoot, Font font, string key, IList<Step> steps,
                               bool force = false, System.Action onClosed = null)
        {
            if (canvasRoot == null || steps == null || steps.Count == 0) return;
            if (!force && Seen(key)) return;
            if (current != null && current.key == key) return;
            if (queue.Exists(r => r.key == key)) return;

            var req = new Request
            {
                root = canvasRoot, font = font, key = key,
                steps = new List<Step>(steps), onClosed = onClosed,
            };
            if (current != null) { queue.Add(req); return; }
            Open(req);
        }

        /// <summary>떠 있는 안내와 대기열을 전부 닫는다(본 것으로 치지 않는다).</summary>
        public static void CloseAll()
        {
            queue.Clear();
            if (current != null) current.Close(markSeen: false);
        }

        static void Open(Request r)
        {
            var go = new GameObject("Tutorial", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(r.root, false);
            var t = go.AddComponent<TutorialOverlay>();
            t.key = r.key; t.steps = r.steps; t.onClosed = r.onClosed; t.font = r.font;
            t.Build();
            current = t;
            t.ShowStep(0);
        }

        static void OpenNext()
        {
            while (queue.Count > 0)
            {
                var r = queue[0];
                queue.RemoveAt(0);
                if (r.root == null) continue;              // 씬이 바뀌어 사라진 화면
                if (Seen(r.key)) continue;                 // 기다리는 사이에 봤다
                Open(r);
                return;
            }
        }

        // ══════════════════════════════════════════════ 화면 ══════════════════
        const float BubbleWidth = 440f;
        const float Gap = 14f;
        const float Margin = 12f;
        // 프로젝트가 Linear 색 공간이라 반투명 검정이 눈에는 더 옅게 보인다 — 0.72로는 뒤 화면이 거의 그대로 보였다
        static readonly Color Dim = new(0f, 0f, 0f, 0.86f);
        static readonly Color Accent = new(1f, 0.78f, 0.2f, 1f);

        string key;
        List<Step> steps;
        System.Action onClosed;
        Font font;
        int index;
        float age;
        bool closing;

        RectTransform root, holeBlock, bubble;
        readonly Image[] dims = new Image[4];
        readonly Image[] edges = new Image[4];
        Text titleText, bodyText, countText, nextLabel;
        Button prevBtn;
        CanvasGroup group;

        void Build()
        {
            root = (RectTransform)transform;
            UiKit.Stretch(root);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.SetAsLastSibling();
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;

            // 어둡게 — 설명할 곳을 뺀 네 면. 클릭도 막는다
            for (int i = 0; i < 4; i++)
            {
                dims[i] = UiKit.Img("Dim" + i, root, raycast: true);
                dims[i].color = Dim;
            }
            // 뚫린 곳도 클릭은 막는다 (투명)
            var hb = UiKit.Img("HoleBlock", root, raycast: true);
            hb.color = new Color(0, 0, 0, 0);
            holeBlock = hb.rectTransform;

            // 테두리 — 깜빡이며 시선을 끈다
            for (int i = 0; i < 4; i++)
            {
                edges[i] = UiKit.Img("Edge" + i, root);
                edges[i].color = Accent;
            }

            BuildBubble();
        }

        void BuildBubble()
        {
            var bg = UiKit.Img("Bubble", root, raycast: true);
            bg.color = new Color(0.09f, 0.09f, 0.11f, 0.98f);
            var ol = bg.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.8f);
            ol.effectDistance = new Vector2(2f, -2f);
            bubble = bg.rectTransform;
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.sizeDelta = new Vector2(BubbleWidth, 0f);

            var vlg = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 16, 14);
            vlg.spacing = 8;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = UiKit.Label("Title", bubble, font, 22, TextAnchor.UpperLeft);
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Accent;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;

            bodyText = UiKit.Label("Body", bubble, font, 18, TextAnchor.UpperLeft);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.supportRichText = true;
            bodyText.lineSpacing = 1.12f;
            bodyText.color = new Color(0.93f, 0.93f, 0.95f);

            // 아래 줄: 단계 표시 · 건너뛰기 · 이전 · 다음
            var foot = UiKit.Rect("Footer", bubble);
            var fle = foot.gameObject.AddComponent<LayoutElement>();
            fle.minHeight = fle.preferredHeight = 38;
            var hlg = foot.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            countText = UiKit.Label("Count", foot, font, 15, TextAnchor.MiddleLeft);
            countText.color = new Color(0.6f, 0.62f, 0.68f);
            var cle = countText.gameObject.AddComponent<LayoutElement>();
            cle.flexibleWidth = 1; cle.preferredHeight = 36;

            FooterButton(foot, "건너뛰기", 96, new Color(1, 1, 1, 0.06f), new Color(0.7f, 0.72f, 0.78f), Skip);
            prevBtn = FooterButton(foot, "이전", 76, new Color(1, 1, 1, 0.12f), Color.white, Prev);
            var next = FooterButton(foot, "다음", 112, Accent, new Color(0.18f, 0.12f, 0.04f), Next);
            nextLabel = next.GetComponentInChildren<Text>();
            nextLabel.fontStyle = FontStyle.Bold;
        }

        Button FooterButton(Transform parent, string caption, float width, Color bg, Color fg,
                            UnityEngine.Events.UnityAction onClick)
        {
            var img = UiKit.Img("Btn_" + caption, parent, raycast: true);
            img.color = bg;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = 36;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var t = UiKit.Label("Text", img.transform, font, 17);
            UiKit.Stretch(t.rectTransform);
            t.text = caption; t.color = fg;
            return btn;
        }

        void ShowStep(int i)
        {
            index = Mathf.Clamp(i, 0, steps.Count - 1);
            var s = steps[index];
            try { s.onEnter?.Invoke(); }
            catch (System.Exception e) { Debug.LogWarning($"[TatoGames] 튜토리얼 단계 준비 실패: {e.Message}"); }

            titleText.text = s.title ?? "";
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(s.title));
            bodyText.text = s.body ?? "";
            countText.text = steps.Count > 1 ? $"{index + 1} / {steps.Count}" : "";
            prevBtn.gameObject.SetActive(index > 0);
            nextLabel.text = index < steps.Count - 1 ? "다음" : "알겠어요";

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
            Layout();
        }

        void Next()
        {
            ClearSelection();
            if (index < steps.Count - 1) ShowStep(index + 1);
            else Close(markSeen: true);
        }

        void Prev()
        {
            ClearSelection();
            if (index > 0) ShowStep(index - 1);
        }

        void Skip()
        {
            ClearSelection();
            Close(markSeen: true);
        }

        // 버튼이 선택된 채로 남으면 Enter가 버튼과 단축키를 둘 다 눌러 두 칸씩 넘어간다
        static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void Close(bool markSeen)
        {
            if (closing) return;
            closing = true;
            if (markSeen && !string.IsNullOrEmpty(key)) MarkSeen(key);
            if (current == this) current = null;
            var done = onClosed;
            Destroy(gameObject);
            try { done?.Invoke(); }
            catch (System.Exception e) { Debug.LogWarning($"[TatoGames] 튜토리얼 종료 처리 실패: {e.Message}"); }
            OpenNext();
        }

        void OnDestroy()
        {
            // 씬이 바뀌어 사라진 경우 — 본 것으로 치지 않고 다음에 다시 띄운다
            if (current == this) current = null;
        }

        void LateUpdate()
        {
            if (steps == null || closing) return;
            age += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(age / 0.2f);

            Layout();

            float a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(age * 2.8f));
            foreach (var e in edges) { var c = e.color; c.a = a; e.color = c; }

            // 전환 연출 중에는 화면이 가려져 있으니 키 입력을 받지 않는다
            if (TatoGames.Launcher.LauncherTransition.Busy || age < 0.25f) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.RightArrow))
                Next();
            else if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.LeftArrow))
                Prev();
        }

        // ══════════════════════════════════════════════ 배치 ══════════════════
        /// <summary>
        /// 설명할 곳의 화면 사각형을 구해 네 면을 어둡게 덮고, 말풍선을 옆에 붙인다.
        /// 대상이 움직여도(손패 재배치 등) 매 프레임 다시 맞춘다.
        /// </summary>
        void Layout()
        {
            var s = steps[index];
            RectTransform target = null;
            try { target = s.target?.Invoke(); }
            catch { target = null; }
            bool has = target != null && target.gameObject.activeInHierarchy;

            Rect r = root.rect;
            Rect h;
            if (has)
            {
                var c = new Vector3[4];
                target.GetWorldCorners(c);
                Vector2 a = root.InverseTransformPoint(c[0]);
                Vector2 b = root.InverseTransformPoint(c[2]);
                h = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - s.pad, Mathf.Min(a.y, b.y) - s.pad,
                                    Mathf.Max(a.x, b.x) + s.pad, Mathf.Max(a.y, b.y) + s.pad);
                h = Rect.MinMaxRect(Mathf.Max(h.xMin, r.xMin), Mathf.Max(h.yMin, r.yMin),
                                    Mathf.Min(h.xMax, r.xMax), Mathf.Min(h.yMax, r.yMax));
                if (h.width <= 1f || h.height <= 1f) has = false;
            }
            else h = default;
            if (!has) h = new Rect(r.center, Vector2.zero);

            SetLocal(dims[0].rectTransform, Rect.MinMaxRect(r.xMin, h.yMax, r.xMax, r.yMax));   // 위
            SetLocal(dims[1].rectTransform, Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, h.yMin));   // 아래
            SetLocal(dims[2].rectTransform, Rect.MinMaxRect(r.xMin, h.yMin, h.xMin, h.yMax));   // 왼쪽
            SetLocal(dims[3].rectTransform, Rect.MinMaxRect(h.xMax, h.yMin, r.xMax, h.yMax));   // 오른쪽
            SetLocal(holeBlock, h);

            const float t = 3f;
            SetLocal(edges[0].rectTransform, Rect.MinMaxRect(h.xMin - t, h.yMax, h.xMax + t, h.yMax + t));
            SetLocal(edges[1].rectTransform, Rect.MinMaxRect(h.xMin - t, h.yMin - t, h.xMax + t, h.yMin));
            SetLocal(edges[2].rectTransform, Rect.MinMaxRect(h.xMin - t, h.yMin, h.xMin, h.yMax));
            SetLocal(edges[3].rectTransform, Rect.MinMaxRect(h.xMax, h.yMin, h.xMax + t, h.yMax));
            foreach (var e in edges) e.enabled = has;

            PlaceBubble(r, h, has, s.side);
        }

        void PlaceBubble(Rect r, Rect h, bool has, Side side)
        {
            Vector2 size = bubble.rect.size;
            Vector2 pos = r.center;

            if (has)
            {
                bool below = h.yMin - Gap - size.y >= r.yMin + Margin;
                bool above = h.yMax + Gap + size.y <= r.yMax - Margin;
                bool right = h.xMax + Gap + size.x <= r.xMax - Margin;
                bool left = h.xMin - Gap - size.x >= r.xMin + Margin;

                Side pick = side;
                if (pick == Side.Below && !below) pick = Side.Auto;
                if (pick == Side.Above && !above) pick = Side.Auto;
                if (pick == Side.Right && !right) pick = Side.Auto;
                if (pick == Side.Left && !left) pick = Side.Auto;
                if (pick == Side.Auto)
                    pick = below ? Side.Below : above ? Side.Above : right ? Side.Right : left ? Side.Left : Side.Auto;

                pos = pick switch
                {
                    Side.Below => new Vector2(h.center.x, h.yMin - Gap - size.y * 0.5f),
                    Side.Above => new Vector2(h.center.x, h.yMax + Gap + size.y * 0.5f),
                    Side.Right => new Vector2(h.xMax + Gap + size.x * 0.5f, h.center.y),
                    Side.Left => new Vector2(h.xMin - Gap - size.x * 0.5f, h.center.y),
                    _ => r.center,
                };
            }

            pos.x = Mathf.Clamp(pos.x, r.xMin + Margin + size.x * 0.5f, r.xMax - Margin - size.x * 0.5f);
            pos.y = Mathf.Clamp(pos.y, r.yMin + Margin + size.y * 0.5f, r.yMax - Margin - size.y * 0.5f);
            bubble.anchoredPosition = pos;
        }

        /// <summary>root 중심 기준 좌표의 사각형으로 놓는다.</summary>
        static void SetLocal(RectTransform t, Rect lr)
        {
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = Vector2.zero;
            t.anchoredPosition = lr.min;
            t.sizeDelta = new Vector2(Mathf.Max(0f, lr.width), Mathf.Max(0f, lr.height));
        }

        // ══════════════════════════════════════════════ 도우미 ═════════════════
        /// <summary>이름으로 자손을 찾는다 (경로가 바뀌어도 따라가게).</summary>
        public static RectTransform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root as RectTransform;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
