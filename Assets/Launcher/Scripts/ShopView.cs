using System.Collections.Generic;
using System.Linq;
using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 상점 — <b>구매와 판매만</b> 한다. 설치·삭제는 미니게임 탭 담당.
    ///
    /// 대표 타일(tato.exe)은 판매 불가·위치 고정이라 에디터 빌더가 만들고,
    /// 여기서는 그 옆 그리드에 <b>미니게임 타일만</b> 그린다.
    /// 보유 중인 게임을 앞에 둬서, 내가 가진 것부터 눈에 들어오게 한다.
    ///
    /// 판매는 구매가의 30%를 돌려주는 대신 <b>그 게임의 카드가 사라진다</b>(§13.2).
    /// 되돌릴 수 없으므로 두 번 눌러야 실행된다.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [Header("타일 아트")]
        public Sprite buyIdle, buyHover, buyPressed;
        public Font labelFont;

        [Header("배치")]
        public RectTransform tileRoot;      // 타일이 들어갈 레이아웃
        public Vector2 tileSize = new(234, 294);
        public Vector2 buttonSize = new(171, 61);
        public float buttonY = -76.5f;

        [System.Serializable]
        public class TileArt { public string gameId; public Sprite sprite; }
        public List<TileArt> tileArts = new();

        public Text noticeLabel;

        string pendingSell;

        void OnEnable()
        {
            StorageData.Changed += Rebuild;
            PlayerData.Changed += Rebuild;
            pendingSell = null;
            Rebuild();
        }

        void OnDisable()
        {
            StorageData.Changed -= Rebuild;
            PlayerData.Changed -= Rebuild;
        }

        public void Rebuild()
        {
            if (tileRoot == null) return;

            // Destroy는 프레임 끝에 실행돼서, 그 전에 레이아웃을 다시 계산하면
            // 없어질 타일까지 자리를 차지한다. 부모에서 먼저 떼어내 즉시 빠지게 한다.
            for (int i = tileRoot.childCount - 1; i >= 0; i--)
            {
                var c = tileRoot.GetChild(i);
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }

            // 보유 중인 게임을 먼저, 그 다음 미보유. 같은 묶음 안에서는 싼 것부터
            var ordered = StorageData.MiniGames
                .OrderBy(g => StorageData.IsOwned(g.id) ? 0 : 1)
                .ThenBy(g => g.price)
                .ToList();

            foreach (var g in ordered) BuildTile(g);

            // 전체 저장공간은 미니게임 탭 담당(설치·삭제가 거기서 일어난다).
            // 상점은 게임별 용량만 타일에 적는다.
            if (noticeLabel != null && string.IsNullOrEmpty(noticeLabel.text))
                Notice($"보유 토인 {PlayerData.Toin}");

            // 타일을 런타임에 채우므로, 그리드와 그 위 가로 레이아웃을 다시 계산해야
            // 대표 타일(tato.exe)과 겹치지 않는다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(tileRoot);
            if (tileRoot.parent is RectTransform parent)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }

        void BuildTile(GameEntry g)
        {
            var art = tileArts.Find(a => a != null && a.gameId == g.id);

            var tileGO = new GameObject("Shop_" + g.id, typeof(RectTransform), typeof(Image));
            tileGO.transform.SetParent(tileRoot, false);
            var rt = tileGO.GetComponent<RectTransform>();
            rt.sizeDelta = tileSize;
            var img = tileGO.GetComponent<Image>();
            img.sprite = art != null ? art.sprite : null;
            img.raycastTarget = false;

            bool owned = StorageData.IsOwned(g.id);
            img.color = owned ? new Color(0.62f, 0.62f, 0.68f, 1f) : Color.white;  // 보유한 건 차분하게

            var btn = MakeButton(tileGO.transform, "Btn_Shop", 0f, buttonY);
            var caption = MakeText(btn.transform, "Text", "", 19, TextAnchor.MiddleCenter);
            var stretch = caption.rectTransform;
            stretch.anchorMin = Vector2.zero; stretch.anchorMax = Vector2.one;
            stretch.offsetMin = stretch.offsetMax = Vector2.zero;

            var info = MakeText(tileGO.transform, "Info", "", 17, TextAnchor.LowerCenter);
            info.rectTransform.anchorMin = info.rectTransform.anchorMax =
                info.rectTransform.pivot = new Vector2(0.5f, 0f);
            info.rectTransform.sizeDelta = new Vector2(220, 46);
            info.rectTransform.anchoredPosition = new Vector2(0, 8);

            // ── 상태별 표시 ──
            if (!owned)
            {
                bool afford = PlayerData.Toin >= g.price;
                caption.text = afford ? $"구매 {g.price}" : "토인 부족";
                btn.interactable = afford;
                info.text = $"{g.displayName}\n{g.sizeMb}MB · {g.price} 토인";
                info.color = new Color(1f, 0.85f, 0.55f);
                btn.onClick.AddListener(() =>
                {
                    pendingSell = null;
                    Notice(StorageData.TryPurchase(g.id, out string why)
                        ? $"{g.displayName} 구매 완료 (−{g.price} 토인)" : why, warn: why != null);
                });
            }
            else if (!g.CanSell)
            {
                caption.text = "기본 제공";
                btn.interactable = false;
                info.text = $"{g.displayName}\n{g.sizeMb}MB · 판매 불가";
                info.color = new Color(0.8f, 0.8f, 0.85f);
            }
            else
            {
                bool confirming = pendingSell == g.id;
                caption.text = confirming ? "정말 판매?" : $"판매 +{g.SellPrice}";
                int risk = StorageData.CardsAtRisk(g.id);
                info.text = $"{g.displayName}\n되팔기 {g.SellPrice} 토인 (구매가의 30%)";
                info.color = new Color(1f, 0.75f, 0.6f);
                btn.onClick.AddListener(() =>
                {
                    if (!confirming)
                    {
                        pendingSell = g.id;
                        Notice(risk > 0
                            ? $"판매하면 이 게임에서 얻은 카드 {risk}장이 사라집니다 — 한 번 더 누르세요"
                            : "정말 판매할까요? 한 번 더 누르세요", warn: true);
                        Rebuild();
                        return;
                    }
                    pendingSell = null;
                    if (StorageData.TrySell(g.id, out int refund, out int lost, out string why))
                        Notice($"{g.displayName} 판매 (+{refund} 토인)" +
                               (lost > 0 ? $", 카드 {lost}장 소멸" : ""), warn: true);
                    else Notice(why, warn: true);
                });
            }
        }

        void Notice(string msg, bool warn = false)
        {
            if (noticeLabel == null || msg == null) return;
            noticeLabel.text = msg;
            noticeLabel.color = warn ? new Color(1f, 0.55f, 0.45f) : new Color(0.75f, 0.9f, 1f);
        }

        Button MakeButton(Transform parent, string name, float x, float y)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = buttonSize;
            rt.anchoredPosition = new Vector2(x, y);
            var img = go.GetComponent<Image>();
            img.sprite = buyIdle;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (buyHover != null || buyPressed != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var ss = btn.spriteState;
                ss.highlightedSprite = buyHover != null ? buyHover : buyIdle;
                ss.pressedSprite = buyPressed != null ? buyPressed : buyIdle;
                ss.selectedSprite = buyIdle;
                btn.spriteState = ss;
            }
            return btn;
        }

        Text MakeText(Transform parent, string name, string text, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text; t.font = labelFont; t.fontSize = size;
            t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
    }
}
