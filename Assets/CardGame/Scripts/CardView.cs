using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 슬더스식 카드 1장. 프레임 / 일러스트 / 코스트 / 이름 / 설명이 각각 슬롯이라
    /// 아트가 들어오면 그대로 보인다. 일러스트는 CardData.artwork를 쓴다.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        public Image frame;
        public Image art;
        public Image costBadge;
        public Text costText;
        public Text nameText;
        public Text descText;
        public Button button;

        public static CardView Create(Transform parent, Font font, Vector2 size)
        {
            var rt = UiKit.Rect("Card", parent);
            rt.sizeDelta = size;
            var view = rt.gameObject.AddComponent<CardView>();

            // 프레임(카드 배경) — 클릭 대상
            var frame = rt.gameObject.AddComponent<Image>();
            frame.raycastTarget = true;
            view.frame = frame;
            view.button = rt.gameObject.AddComponent<Button>();
            view.button.targetGraphic = frame;

            float w = size.x, h = size.y;

            // 일러스트 (상단 약 45%)
            view.art = UiKit.Img("Art", rt);
            UiKit.Place(view.art.rectTransform, new Vector2(0.5f, 1),
                        new Vector2(0, -12), new Vector2(w - 24, h * 0.45f));
            view.art.preserveAspect = true;

            // 이름 (일러스트 아래)
            view.nameText = UiKit.Label("Name", rt, font, 17);
            UiKit.Place(view.nameText.rectTransform, new Vector2(0.5f, 1),
                        new Vector2(0, -(12 + h * 0.45f) - 4), new Vector2(w - 16, 24));

            // 설명 (하단)
            view.descText = UiKit.Label("Desc", rt, font, 15);
            UiKit.Place(view.descText.rectTransform, new Vector2(0.5f, 0),
                        new Vector2(0, 10), new Vector2(w - 18, h * 0.32f));

            // 코스트 배지 (좌상단)
            view.costBadge = UiKit.Img("CostBadge", rt);
            UiKit.Place(view.costBadge.rectTransform, new Vector2(0, 1),
                        new Vector2(16, -16), new Vector2(34, 34));
            view.costText = UiKit.Label("CostText", view.costBadge.rectTransform, font, 19);
            UiKit.Stretch(view.costText.rectTransform);

            return view;
        }

        public void Bind(CardData card, BattleTheme theme, bool playable, UnityAction onClick)
        {
            if (card == null) return;
            var t = theme;

            UiKit.Apply(frame, t != null ? t.FrameFor(card.type) : null,
                        playable ? (t != null ? t.cardColor : new Color(0.18f, 0.2f, 0.24f))
                                 : (t != null ? t.cardDisabledColor : new Color(0.11f, 0.12f, 0.14f)));
            // 아트가 있어도 사용 불가면 어둡게 (§14.4 — 불투명도는 Color로)
            if (frame.sprite != null)
                frame.color = playable ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);

            var artSprite = card.artwork != null ? card.artwork : (t != null ? t.cardArtFallback : null);
            UiKit.Apply(art, artSprite, new Color(0, 0, 0, 0.25f));
            if (art.sprite != null && !playable) art.color = new Color(0.6f, 0.6f, 0.65f, 1f);

            UiKit.Apply(costBadge, t != null ? t.costBadge : null,
                        t != null ? t.accentColor : new Color(1f, 0.78f, 0.2f));
            costText.text = card.cost.ToString();
            costText.color = costBadge.sprite != null ? Color.white : new Color(0.1f, 0.1f, 0.12f);

            nameText.text = card.displayName;
            descText.text = CardText.Describe(card);

            button.interactable = playable;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(onClick);
        }
    }
}
