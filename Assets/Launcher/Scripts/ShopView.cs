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
    ///
    /// 알림(구매 완료 등)은 오른쪽 아래 작은 상자로 잠깐 떴다 사라진다.
    /// 목업의 "지금 할인하고있음!!" 그림은 할인 기능이 없어서 숨긴다 — 항상 떠 있으면 거짓말이 된다.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [Header("타일 아트")]
        public Sprite buyIdle, buyHover, buyPressed;
        [Tooltip("보유 중 버튼(OWNED) — 비면 대표 타일의 OWNED 그림을 빌린다")]
        public Sprite ownedIdle;
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
        Image toast;          // 알림 상자
        float toastHideAt;
        bool prepared;

        // 그림 칸(타일 위쪽 사각형) 안쪽 윗줄 — 타일 가운데 기준
        const float PillY = 99f;
        const float ToastTime = 4f;

        void OnEnable()
        {
            Prepare();
            StorageData.Changed += Rebuild;
            PlayerData.Changed += Rebuild;
            pendingSell = null;
            HideToast();
            Rebuild();
        }

        void OnDisable()
        {
            StorageData.Changed -= Rebuild;
            PlayerData.Changed -= Rebuild;
        }

        void Update()
        {
            if (toast != null && toast.gameObject.activeSelf && Time.unscaledTime > toastHideAt) HideToast();
        }

        /// <summary>한 번만 — 빌더가 만든 화면 요소를 정리한다(씬을 다시 만들지 않아도 되게).</summary>
        void Prepare()
        {
            if (prepared) return;
            prepared = true;

            // 할인 기능이 없는데 "지금 할인하고있음!!"이 항상 떠 있었다
            var sale = transform.Find("Notification");
            if (sale != null) sale.gameObject.SetActive(false);

            // 대표 타일(tato.exe) — 아래 글자가 주황 장식 막대 위를 덮었다. 그림에 이미 '무료'가 적혀 있어
            // 이름·판매 불가 글자는 빼고 용량만 알약으로
            var featured = TatoGames.CardGame.TutorialOverlay.FindDeep(transform, "Featured_TATOEXE");
            // 보유 중 버튼 그림 — 빌더가 안 넘겼으면(예전 씬) 대표 타일의 OWNED 버튼에서 빌린다
            var ownedBtn = featured != null ? featured.Find("Btn_Owned") : null;
            if (ownedIdle == null && ownedBtn != null && ownedBtn.TryGetComponent(out Image oimg)) ownedIdle = oimg.sprite;
            // 대표 타일의 OWNED는 '비활성 버튼'이라 반투명했다 — 게임 타일의 OWNED와 같은 밝기로
            if (ownedBtn != null && ownedBtn.TryGetComponent(out Button ob)) ob.transition = Selectable.Transition.None;
            if (featured != null)
            {
                var info = featured.Find("Info");
                if (info != null) info.gameObject.SetActive(false);
                var core = StorageData.Find("tato");
                if (core != null && featured.Find("Size") == null)
                {
                    var pill = Pill(featured, "Size", $"{core.sizeMb}MB", new Vector2(-46f, featured.rect.height * 0.5f - 48f));
                    TooltipTrigger.On(pill, "tato.exe (본편)", $"저장공간 {core.sizeMb}MB · 항상 설치돼 있고 팔 수 없어요.");
                }
            }

            BuildToast();
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

            // 타일을 런타임에 채우므로, 그리드와 그 위 레이아웃(세로 스크롤 콘텐츠)을 다시 계산해야
            // 대표 타일(tato.exe)과 겹치지 않고 스크롤 길이도 타일 줄 수에 맞는다.
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

            // 버튼 그림(PURCHASE · OWNED)에는 글자가 박혀 있다 — 그 위에 다른 글자를 얹지 않는다.
            // 가격·판매는 그림 칸 위쪽 모서리의 알약으로 따로 보여준다 (예전엔 "기본 제공"이 PURCHASE 위에 겹쳤다)
            var btn = MakeButton(tileGO.transform, "Btn_Shop", 0f, buttonY);

            // 게임 이름은 타일 그림에 이미 있다 — 용량만 그림 칸 왼쪽 위에 작게 (아래 장식 점을 덮지 않게)
            var size = Pill(tileGO.transform, "Size", $"{g.sizeMb}MB", new Vector2(-46f, PillY));

            // ── 상태별 표시 ──
            if (!owned)
            {
                bool afford = PlayerData.Toin >= g.price;
                btn.interactable = afford;
                // 스프라이트 교체 방식이라 토인이 모자라 못 누를 때도 똑같이 보였다 — 흐리게
                btn.gameObject.AddComponent<ButtonDim>();
                var price = Pill(tileGO.transform, "Price", $"{g.price}토인", new Vector2(48f, PillY));
                price.GetComponentInChildren<Text>().color = afford ? new Color(1f, 0.82f, 0.4f) : new Color(1f, 0.55f, 0.48f);
                string buyTip = $"{g.price}토인으로 사요. 저장공간({g.sizeMb}MB)이 남으면 바로 설치돼요.\n" +
                                $"판매할 때는 {g.SellPrice}토인(30%)만 돌려받아요." +
                                (afford ? "" : $"\n<color=#FF9A8A>지금 토인 {PlayerData.Toin} — {g.price - PlayerData.Toin} 부족해요</color>");
                TooltipTrigger.On(btn.targetGraphic, $"{g.displayName} 구매", buyTip);
                TooltipTrigger.On(price, afford ? "가격" : "토인 부족", buyTip);
                TooltipTrigger.On(size, "용량", $"설치하면 저장공간 {g.sizeMb}MB를 써요. (남은 공간 {StorageData.FreeMb}MB)");
                btn.onClick.AddListener(() =>
                {
                    pendingSell = null;
                    Notice(StorageData.TryPurchase(g.id, out string why)
                        ? $"{g.displayName} 구매 완료 (−{g.price} 토인)" +
                          (StorageData.IsInstalled(g.id) ? " — 미니게임 탭에서 바로 실행할 수 있어요" : " — 공간이 부족해 설치는 안 했어요")
                        : why, warn: why != null);
                });
                return;
            }

            // 보유 중 — 대표 타일처럼 OWNED 그림 (누르는 버튼이 아니다)
            ShowOwned(btn);
            if (!g.CanSell)
            {
                TooltipTrigger.On(btn.targetGraphic, "기본 제공", "처음부터 가지고 있는 게임이에요. 팔 수 없어요.\n실행·설치·삭제는 미니게임 탭에서 해요.");
                TooltipTrigger.On(size, "기본 제공", $"처음부터 가지고 있는 게임이에요. (용량 {g.sizeMb}MB)");
                return;
            }

            TooltipTrigger.On(btn.targetGraphic, "보유 중", "가지고 있는 게임이에요. 실행·설치·삭제는 미니게임 탭에서 해요.\n팔려면 오른쪽 위 '판매'를 누르세요.");
            TooltipTrigger.On(size, "보유 중", $"가지고 있는 게임이에요. (용량 {g.sizeMb}MB)");

            // 판매 — 그림 칸 오른쪽 위 작은 버튼, 두 번 눌러야 팔린다
            bool confirming = pendingSell == g.id;
            int risk = StorageData.CardsAtRisk(g.id);
            var sell = Pill(tileGO.transform, "Btn_Sell", confirming ? "정말?" : $"판매 +{g.SellPrice}", new Vector2(44f, PillY),
                            new Vector2(66, 22));
            sell.color = confirming ? new Color(0.55f, 0.2f, 0.16f, 0.95f) : new Color(0.36f, 0.22f, 0.18f, 0.95f);
            sell.GetComponentInChildren<Text>().color = new Color(1f, 0.8f, 0.7f);
            var sellBtn = sell.gameObject.AddComponent<Button>();
            sellBtn.targetGraphic = sell;
            TooltipTrigger.On(sell, $"{g.displayName} 판매",
                $"{g.SellPrice}토인(구매가의 30%)을 돌려받아요.\n" +
                (risk > 0 ? $"<color=#FF9A8A>이 게임에서 얻은 카드 {risk}장이 사라져요.</color>" : "이 게임에서 얻은 카드는 없어요.") +
                "\n한 번 더 눌러야 팔려요.");
            sellBtn.onClick.AddListener(() =>
            {
                if (!confirming)
                {
                    pendingSell = g.id;
                    Notice(risk > 0
                        ? $"판매하면 이 게임에서 얻은 카드 {risk}장이 사라져요 — 한 번 더 누르세요"
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

        /// <summary>OWNED 그림으로 바꾸고 누를 수 없게 한다 (흐리게 하지 않는다 — 비활성이 아니라 '보유' 표시다).</summary>
        void ShowOwned(Button btn)
        {
            var img = btn.targetGraphic as Image;
            if (img != null && ownedIdle != null)
            {
                img.sprite = ownedIdle;
                img.SetNativeSize();
            }
            btn.transition = Selectable.Transition.None;
            btn.interactable = false;
        }

        // ══════════════════════════════════════════════ 알림 상자 ═════════════
        /// <summary>오른쪽 아래 알림 상자. 빌더가 만든 noticeLabel을 그 안으로 옮긴다.</summary>
        void BuildToast()
        {
            var go = new GameObject("Toast", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-40f, 36f);
            rt.sizeDelta = new Vector2(440f, 64f);
            toast = go.GetComponent<Image>();
            toast.color = new Color(0.10f, 0.11f, 0.14f, 0.96f);
            toast.raycastTarget = false;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 1f, 1f, 0.18f);
            ol.effectDistance = new Vector2(1f, -1f);

            if (noticeLabel == null)
                noticeLabel = MakeText(go.transform, "Label_Notice", "", 18, TextAnchor.MiddleCenter);
            var nrt = noticeLabel.rectTransform;
            noticeLabel.transform.SetParent(go.transform, false);
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.offsetMin = new Vector2(16f, 6f); nrt.offsetMax = new Vector2(-16f, -6f);
            noticeLabel.alignment = TextAnchor.MiddleCenter;
            noticeLabel.fontSize = 18;
            noticeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            noticeLabel.verticalOverflow = VerticalWrapMode.Overflow;
            noticeLabel.supportRichText = true;
            go.SetActive(false);
        }

        void HideToast()
        {
            if (toast != null) toast.gameObject.SetActive(false);
            if (noticeLabel != null) noticeLabel.text = "";
        }

        void Notice(string msg, bool warn = false)
        {
            if (noticeLabel == null || msg == null) return;
            noticeLabel.text = msg;
            noticeLabel.color = warn ? new Color(1f, 0.62f, 0.52f) : new Color(0.78f, 0.92f, 1f);
            if (toast != null)
            {
                toast.gameObject.SetActive(true);
                toast.transform.SetAsLastSibling();
                toastHideAt = Time.unscaledTime + ToastTime;
            }
        }

        // ══════════════════════════════════════════════ 조립 도우미 ═══════════
        Image Pill(Transform parent, string name, string text, Vector2 pos, Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size ?? new Vector2(58, 20);
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            UiKit.ApplyPill(img, rt.sizeDelta.y);
            img.color = new Color(0f, 0f, 0f, 0.45f);
            var t = MakeText(go.transform, "Text", text, 13, TextAnchor.MiddleCenter);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.color = new Color(0.8f, 0.84f, 0.9f);
            return img;
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
