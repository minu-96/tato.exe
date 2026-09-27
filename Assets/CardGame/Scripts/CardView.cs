using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 슬더스식 카드 1장. 프레임 / 일러스트 / 코스트 / 이름 / 설명이 각각 슬롯이라
    /// 아트가 들어오면 그대로 보인다. 일러스트는 CardData.artwork를 쓴다.
    ///
    /// 그리는 순서(아래→위): 판정용 바탕 → 창 바탕 → 일러스트 → <b>프레임</b> → 이름·설명 → 코스트 배지.
    /// 프레임 아트는 일러스트 자리가 투명한 "창"이라, 프레임을 일러스트 위에 덮어야
    /// 창 테두리 장식이 일러스트에 가려지지 않는다.
    ///
    /// 각 칸의 위치는 프레임 아트(360×500)에서 잰 비율이다 — 카드 크기(손패 150×210 · 보상 180×250 ·
    /// 대장간 102×142 · 감자창고 166×232)가 달라도 같은 자리에 들어간다.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        public Image hit;         // 클릭·마우스 판정 (프레임 아트가 있으면 투명, 없으면 단색 카드 바탕)
        public Image window;      // 일러스트 창 바탕 (일러스트가 투명하거나 비율이 안 맞을 때 보이는 면)
        public Image art;
        public Image frame;       // 프레임 아트 — 일러스트 위에 덮는다
        public Image costBadge;
        public Text costText;
        public Text nameText;
        public Text descText;
        public Button button;
        public Image rarityPill;  // 일러스트 창 오른쪽 위 등급 표시 (작은 카드는 생략)
        public Text rarityText;

        // ── 프레임 아트(360×500)에서 잰 영역 — (왼쪽, 위, 오른쪽, 아래)를 카드 폭·높이에 대한 비율로 ──
        static readonly Rect WindowArea = Area(45, 48, 320, 254);    // 투명한 일러스트 창
        static readonly Rect NameArea   = Area(54, 262, 311, 301);   // 밝은 이름 띠
        static readonly Rect DescArea   = Area(50, 332, 314, 456);   // 어두운 설명 칸 (안쪽 여백 포함)
        const float BadgeSize = 0.2f;                                // 배지 지름 = 카드 폭의 20% (180폭 → 36)
        static readonly Vector2 BadgeCenter = new(0.1f, 0.08f);      // 왼쪽 위 모서리에 걸친다

        static Rect Area(float l, float t, float r, float b) =>
            Rect.MinMaxRect(l / 360f, t / 500f, r / 360f, b / 500f);

        /// <summary>area는 위에서부터 잰 비율 — UI 앵커(아래에서부터)로 바꿔 붙인다.</summary>
        static void Fit(RectTransform rt, Rect area)
        {
            rt.anchorMin = new Vector2(area.xMin, 1f - area.yMax);
            rt.anchorMax = new Vector2(area.xMax, 1f - area.yMin);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static CardView Create(Transform parent, Font font, Vector2 size)
        {
            var rt = UiKit.Rect("Card", parent);
            rt.sizeDelta = size;
            var view = rt.gameObject.AddComponent<CardView>();

            // 판정용 바탕 — 클릭·마우스를 받는다
            view.hit = rt.gameObject.AddComponent<Image>();
            view.hit.raycastTarget = true;
            view.button = rt.gameObject.AddComponent<Button>();

            view.window = UiKit.Img("Window", rt);
            Fit(view.window.rectTransform, WindowArea);

            view.art = UiKit.Img("Art", rt);
            Fit(view.art.rectTransform, WindowArea);
            view.art.preserveAspect = true;

            view.frame = UiKit.Img("Frame", rt);
            UiKit.Stretch(view.frame.rectTransform);
            view.button.targetGraphic = view.frame;   // 누름·마우스 색 변화는 프레임에
            // 사용 불가 표시는 Bind가 색으로 직접 한다 — 버튼 기본값(반투명)을 쓰면 프레임이 비쳐 보인다
            var colors = view.button.colors;
            colors.disabledColor = Color.white;
            view.button.colors = colors;

            view.nameText = UiKit.Label("Name", rt, font, 17);
            Fit(view.nameText.rectTransform, NameArea);

            view.descText = UiKit.Label("Desc", rt, font, 15);
            Fit(view.descText.rectTransform, DescArea);

            // 이름·설명은 칸 안에서 줄바꿈하고, 넘치면 글자를 줄인다
            FitInside(view.nameText, 9);
            FitInside(view.descText, 8);

            // 코스트 배지 — 왼쪽 위 모서리. 카드 크기에 맞춰 같이 커진다
            float bs = size.x * BadgeSize;
            view.costBadge = UiKit.Img("CostBadge", rt);
            var brt = view.costBadge.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(BadgeCenter.x, 1f - BadgeCenter.y);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(bs, bs);
            view.costText = UiKit.Label("CostText", brt, font, Mathf.Max(10, Mathf.RoundToInt(bs * 0.56f)));
            UiKit.Stretch(view.costText.rectTransform);
            view.costText.fontStyle = FontStyle.Bold;

            // 등급 표시 — 프레임에 등급 칸이 없어서, 보스 보상(초월·전설)도 카드만 봐서는 등급을 알 수 없었다.
            // 대장간처럼 작은 카드는 글자가 읽히지 않아 생략한다.
            if (size.x >= 130f)
            {
                view.rarityPill = UiKit.Img("Rarity", rt);
                var prt = view.rarityPill.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(WindowArea.xMax, 1f - WindowArea.yMin);
                prt.pivot = new Vector2(1f, 1f);
                prt.anchoredPosition = new Vector2(-3f, -3f);
                prt.sizeDelta = new Vector2(size.x * 0.25f, size.x * 0.12f);
                view.rarityPill.color = new Color(0.06f, 0.05f, 0.04f, 0.78f);
                view.rarityText = UiKit.Label("Text", prt, font, Mathf.Max(10, Mathf.RoundToInt(size.x * 0.078f)));
                UiKit.Stretch(view.rarityText.rectTransform);
                view.rarityText.fontStyle = FontStyle.Bold;
            }

            return view;
        }

        static void FitInside(Text t, int minSize)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = minSize;
            t.resizeTextMaxSize = t.fontSize;
        }

        // 프레임 아트 위의 글자색 — 이름 띠는 밝고 설명 칸은 어둡다
        static readonly Color NameOnFrame = new(0.30f, 0.19f, 0.08f);
        static readonly Color CostOnBadge = new(0.36f, 0.18f, 0.03f);
        static readonly Color WindowBack = new(0.20f, 0.16f, 0.12f, 1f);

        public void Bind(CardData card, BattleTheme theme, bool playable, UnityAction onClick,
                         CardText.Context battle = null)
        {
            if (card == null) return;
            var t = theme;
            var frameSprite = t != null ? t.FrameFor(card.type) : null;
            bool framed = frameSprite != null;

            // 프레임 아트가 있으면 프레임이 카드 모양을 그리고, 없으면 판정용 바탕이 단색 카드가 된다
            var cardColor = playable ? (t != null ? t.cardColor : new Color(0.18f, 0.2f, 0.24f))
                                     : (t != null ? t.cardDisabledColor : new Color(0.11f, 0.12f, 0.14f));
            hit.sprite = null;
            hit.color = framed ? new Color(0, 0, 0, 0) : cardColor;   // 투명해도 판정은 받는다

            frame.gameObject.SetActive(framed);
            if (framed)
            {
                UiKit.Apply(frame, frameSprite, Color.white);
                // 사용 불가면 어둡게 (§14.4 — 불투명도는 Color로)
                frame.color = playable ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
            }

            window.color = framed ? (playable ? WindowBack : WindowBack * 0.6f + new Color(0, 0, 0, 0.4f))
                                  : new Color(0, 0, 0, 0.25f);

            var artSprite = card.artwork != null ? card.artwork : (t != null ? t.cardArtFallback : null);
            UiKit.Apply(art, artSprite, new Color(0, 0, 0, 0));
            if (art.sprite != null && !playable) art.color = new Color(0.6f, 0.6f, 0.65f, 1f);

            UiKit.Apply(costBadge, t != null ? t.costBadge : null,
                        t != null ? t.accentColor : new Color(1f, 0.78f, 0.2f));
            costText.text = card.cost.ToString();
            costText.color = costBadge.sprite != null ? CostOnBadge : new Color(0.1f, 0.1f, 0.12f);

            nameText.text = card.displayName;
            nameText.color = framed ? NameOnFrame : Color.white;
            // 전투 중(손패)이면 힘·약화·취약·민첩이 반영된 실제 수치 — 달라진 숫자만 색이 바뀐다
            descText.supportRichText = true;
            descText.text = battle != null ? CardText.Describe(card, battle) : CardText.Describe(card);
            descText.color = Color.white;

            if (rarityPill != null)
            {
                rarityText.text = CardText.RarityName(card.rarity);
                var rc = CardText.RarityColor(card.rarity);
                rarityText.color = playable ? rc : rc * 0.65f;
            }

            button.interactable = playable;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(onClick);
        }

        /// <summary>
        /// 카드 모양(프레임·이름 띠·설명 칸)만 빌려 임의의 글을 쓴다 — 코스트·등급·일러스트 없음 (업적 칸 등).
        /// bright가 아니면 어둡게.
        /// </summary>
        public void BindPlain(string title, string desc, CardType frameType, BattleTheme theme, bool bright)
        {
            var frameSprite = theme != null ? theme.FrameFor(frameType) : null;
            bool framed = frameSprite != null;

            hit.sprite = null;
            hit.color = framed ? new Color(0, 0, 0, 0)
                               : (bright ? (theme != null ? theme.cardColor : new Color(0.18f, 0.2f, 0.24f))
                                         : (theme != null ? theme.cardDisabledColor : new Color(0.11f, 0.12f, 0.14f)));
            frame.gameObject.SetActive(framed);
            if (framed)
            {
                UiKit.Apply(frame, frameSprite, Color.white);
                frame.color = bright ? Color.white : new Color(0.66f, 0.66f, 0.7f, 1f);
            }
            window.color = framed ? (bright ? WindowBack : WindowBack * 0.6f + new Color(0, 0, 0, 0.4f))
                                  : new Color(0, 0, 0, 0.25f);
            UiKit.Apply(art, null, new Color(0, 0, 0, 0));

            costBadge.gameObject.SetActive(false);
            if (rarityPill != null) rarityPill.gameObject.SetActive(false);

            // 이름은 어둡게 하지 않는다 — 흐린 프레임 위에서 이름까지 흐리면 읽히지 않았다
            nameText.text = title;
            nameText.color = framed ? NameOnFrame : Color.white;
            descText.supportRichText = true;
            descText.text = desc;
            descText.color = bright ? Color.white : new Color(0.7f, 0.7f, 0.74f);

            button.interactable = false;
            button.onClick.RemoveAllListeners();
        }
    }
}
