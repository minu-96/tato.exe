using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 카드 자세히 보기 — 카드를 <b>우클릭</b>하면 화면을 어둡게 덮고 카드를 크게, 옆에 등급·종류·에너지와
    /// 카드에 나오는 용어(소멸·취약…) 설명을 보여준다. 아무 곳이나 클릭하거나 ESC를 누르면 닫힌다.
    /// 전투(손패·보상·대장간)와 감자창고가 같이 쓴다. 캔버스(화면)당 하나를 돌려 쓴다.
    ///
    /// 예전엔 마우스를 올리기만 해도 커진 카드 옆에 용어 설명이 붙어 다녀서 화면이 지저분했다.
    /// 이제 마우스를 올리면 카드만 커지고, 설명은 보고 싶을 때만 연다.
    /// 우클릭을 아직 안 써 본 사람에게는 커진 카드에 작은 꼬리표(우클릭: 자세히 보기)를 붙인다 — 한 번 열어 보면 사라진다.
    /// </summary>
    public class CardDetailView : MonoBehaviour, IPointerClickHandler
    {
        static readonly Vector2 CardSize = new(270, 375);   // 보상 카드(180×250)의 1.5배
        const float InfoWidth = 380f;
        const float Gap = 28f;
        static readonly Color Accent = new(1f, 0.78f, 0.2f);
        static readonly Color Muted = new(0.62f, 0.65f, 0.71f);

        static CardDetailView inst;
        static int escFrame = -1;

        CardView card;
        RectTransform info;
        Text nameText, kindText, termsText, noteText;

        public static bool IsOpen => inst != null && inst.gameObject.activeSelf;

        /// <summary>
        /// 이번 ESC를 이 창이 쓰는가 — 같은 ESC로 런처에 나가지 않게 (LauncherTransition의 비상구).
        /// 창이 먼저 닫히고 나서 확인해도 같은 프레임이면 쓴 것으로 친다.
        /// </summary>
        public static bool UsesEscape => IsOpen || escFrame == Time.frameCount;

        /// <summary>우클릭을 아직 안 써 봤나 — 그동안은 커진 카드에 꼬리표를 붙인다.</summary>
        public static bool HintNeeded => !TutorialOverlay.Seen(TutorialOverlay.Keys.HintCardDetail);

        /// <summary>
        /// 카드를 크게 보여준다. battle이 있으면 손패와 같은 실제 수치(힘·취약 등 반영)로.
        /// 안내(튜토리얼)가 떠 있으면 열지 않는다.
        /// </summary>
        public static void Show(Transform canvasRoot, Font font, BattleTheme theme, CardData data,
                                CardText.Context battle = null)
        {
            if (canvasRoot == null || data == null) return;
            if (TutorialOverlay.Busy) return;
            if (inst == null || inst.transform.parent != canvasRoot)
            {
                if (inst != null) Destroy(inst.gameObject);
                Build(canvasRoot, font);
            }
            if (HintNeeded) TutorialOverlay.MarkSeen(TutorialOverlay.Keys.HintCardDetail);

            inst.gameObject.SetActive(true);
            inst.transform.SetAsLastSibling();   // 카드 확대본·말풍선·패널보다 위
            inst.Bind(data, theme, battle);
        }

        public static void Hide()
        {
            if (inst != null) inst.gameObject.SetActive(false);
        }

        /// <summary>뒤 덮개 어디를 눌러도(왼쪽·오른쪽) 닫힌다 — 카드·설명 칸은 판정을 받지 않는다.</summary>
        public void OnPointerClick(PointerEventData e) => Hide();

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            escFrame = Time.frameCount;
            Hide();
        }

        void Bind(CardData data, BattleTheme theme, CardText.Context battle)
        {
            card.Bind(data, theme, true, null, battle);
            card.button.interactable = false;   // 마우스를 올려도 색이 바뀌지 않게 (누를 것이 아니다)

            nameText.text = data.displayName;
            string rarity = ColorUtility.ToHtmlStringRGB(CardText.RarityColor(data.rarity));
            kindText.text = $"<color=#{rarity}>{CardText.RarityName(data.rarity)}</color> · " +
                            $"{CardText.TypeName(data.type)} 카드  ·  에너지 {data.cost}";

            string gloss = CardText.Glossary(data);
            termsText.text = string.IsNullOrEmpty(gloss) ? "따로 설명할 용어가 없는 카드예요." : gloss;
            termsText.color = string.IsNullOrEmpty(gloss) ? Muted : Color.white;

            // 손패의 숫자 색이 무슨 뜻인지 — 실제로 달라진 숫자가 있을 때만
            bool changed = battle != null && CardText.Describe(data, battle) != CardText.Describe(data);
            noteText.gameObject.SetActive(changed);
            if (changed)
                noteText.text = "색이 바뀐 숫자는 지금 쓰면 실제로 들어가는 값이에요 — " +
                                $"<color={CardText.UpColor}>초록</color> 늘어남 · " +
                                $"<color={CardText.DownColor}>빨강</color> 줄어듦 · " +
                                $"<color={CardText.LiveColor}>노랑</color> 지금 블록에 따라 정해짐";

            LayoutRebuilder.ForceRebuildLayoutImmediate(info);
        }

        static void Build(Transform root, Font font)
        {
            var dim = UiKit.Img("CardDetail", root, raycast: true);
            UiKit.Stretch(dim.rectTransform);
            // 프로젝트가 Linear 색 공간이라 반투명 검정이 눈에는 옅게 보인다 — 0.82로는 뒤 화면이 절반쯤 그대로 보였다
            dim.color = new Color(0f, 0f, 0f, 0.9f);
            inst = dim.gameObject.AddComponent<CardDetailView>();

            // 가운데: 왼쪽 큰 카드 + 오른쪽 설명 칸 (설명 칸 윗변 = 카드 윗변)
            var group = UiKit.Rect("Group", dim.transform);
            UiKit.Place(group, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f),
                        new Vector2(CardSize.x + Gap + InfoWidth, CardSize.y));

            inst.card = CardView.Create(group, font, CardSize);
            var crt = (RectTransform)inst.card.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            inst.card.hit.raycastTarget = false;     // 카드를 눌러도 닫히게 — 판정은 뒤 덮개가 받는다
            // 카드가 커진 만큼 글자도 키운다 (배율로 키우면 글자가 흐려진다)
            Grow(inst.card.nameText, 26);
            Grow(inst.card.descText, 22);

            var panel = UiKit.Img("Info", group);
            inst.info = panel.rectTransform;
            inst.info.anchorMin = inst.info.anchorMax = inst.info.pivot = new Vector2(1f, 1f);
            inst.info.anchoredPosition = Vector2.zero;
            inst.info.sizeDelta = new Vector2(InfoWidth, 0f);
            panel.color = new Color(0.07f, 0.07f, 0.09f, 1f);   // 반투명이면(Linear) 뒤 배경이 비쳐 글이 흐려진다
            UiKit.Outline(panel, 1.5f, new Color(Accent.r, Accent.g, Accent.b, 0.7f));

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 14, 18);
            vlg.spacing = 8f;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // 폭 고정 → 글이 줄바꿈된다
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            inst.nameText = Line("Name", panel.transform, font, 28, Accent);
            inst.nameText.fontStyle = FontStyle.Bold;
            inst.kindText = Line("Kind", panel.transform, font, 17, new Color(0.85f, 0.87f, 0.9f));

            var rule = UiKit.Img("Rule", panel.transform);
            rule.color = new Color(1f, 1f, 1f, 0.14f);
            var le = rule.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 2f;

            inst.termsText = Line("Terms", panel.transform, font, 18, Color.white);
            inst.termsText.lineSpacing = 1.15f;
            inst.noteText = Line("Note", panel.transform, font, 15, Muted);

            // 닫는 법 — 카드 바로 아래 (화면 맨 아래에 두면 손패 글자와 겹쳐 읽기 어려웠다)
            var hint = UiKit.Label("Hint", group, font, 16);
            var hrt = hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -14f);
            hrt.sizeDelta = new Vector2(800f, 24f);
            hint.text = "아무 곳이나 클릭하거나 ESC를 누르면 닫혀요";
            hint.color = Muted;

            dim.gameObject.SetActive(false);
        }

        static void Grow(Text t, int size)
        {
            t.fontSize = size;
            t.resizeTextMaxSize = size;
        }

        static Text Line(string name, Transform parent, Font font, int size, Color color)
        {
            var t = UiKit.Label(name, parent, font, size, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.color = color;
            return t;
        }

        // ══════════════════════════════════════════ 꼬리표 ═════════════════════
        /// <summary>
        /// 커진 카드의 일러스트 아래쪽에 "우클릭: 자세히 보기" 꼬리표를 붙이거나 뗀다.
        /// 우클릭을 한 번 써 보면(HintNeeded = false) 다시 붙지 않는다.
        /// 카드가 배율로 커져 있어도 꼬리표는 화면에서 같은 크기로 보이게 배율을 되돌린다.
        /// </summary>
        public static void SetHint(Transform cardRoot, Font font, bool show)
        {
            if (cardRoot == null) return;
            var tag = cardRoot.Find("DetailHint");
            if (!show || !HintNeeded)
            {
                if (tag != null) tag.gameObject.SetActive(false);
                return;
            }
            if (tag == null) tag = BuildHint(cardRoot, font);
            float s = Mathf.Max(0.01f, cardRoot.localScale.x);
            tag.localScale = Vector3.one / s;
            tag.gameObject.SetActive(true);
            tag.SetAsLastSibling();
        }

        static Transform BuildHint(Transform cardRoot, Font font)
        {
            var bg = UiKit.Img("DetailHint", cardRoot);
            var rt = bg.rectTransform;
            // 일러스트 창 아래 가장자리 바로 위 — 코스트·등급·이름·설명을 가리지 않는 자리
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f - 254f / 500f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 4f);
            rt.sizeDelta = new Vector2(124f, 22f);
            UiKit.ApplyPill(bg, 22f);
            bg.color = new Color(0.07f, 0.07f, 0.09f, 0.92f);
            UiKit.Outline(bg, 1f, new Color(Accent.r, Accent.g, Accent.b, 0.6f));

            var t = UiKit.Label("Text", rt, font, 13);
            UiKit.Stretch(t.rectTransform);
            t.text = "우클릭: 자세히 보기";
            t.color = new Color(1f, 0.86f, 0.55f);
            return rt;
        }
    }
}
