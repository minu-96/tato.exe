using System.Collections.Generic;
using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 감자창고: 보유 카드를 인스턴스 단위로 표시하고 런 덱을 구성한다(§8.2 · §10.7).
    /// 같은 카드를 여러 장 가져도 각각 별개 셀 → 한 장씩 넣고 뺄 수 있다.
    /// 덱은 최소 8 · 최대 30장. 시작덱도 뺄 수 있고, 시작덱의 특권은 '귀속(판매 불가)'뿐이다.
    /// 등급 필터(전체/일반/희귀/초월/전설)로 표시를 거른다.
    /// </summary>
    public class CollectionView : MonoBehaviour
    {
        [System.Serializable]
        public class RarityFilter
        {
            public Button button;
            public bool all;        // true면 전체(등급 무관)
            public Rarity rarity;   // all=false일 때 이 등급만
            [System.NonSerialized] public Sprite idleSprite;
        }

        [Tooltip("스크롤 콘텐츠(GridLayoutGroup)")]
        public RectTransform content;
        [Tooltip("전 카드 SO(id 조회용)")]
        public CardData[] allCards;
        public Font labelFont;
        public Sprite cardArt;

        [Tooltip("등급 필터 버튼 (전체/일반/희귀/초월/전설)")]
        public List<RarityFilter> filters = new();

        [Tooltip("런 덱 장수 표시 라벨(선택)")]
        public Text deckCountLabel;

        static readonly Color OkColor = new(1f, 0.85f, 0.5f);
        static readonly Color WarnColor = new(1f, 0.45f, 0.4f);
        static readonly Color LockColor = new(0.65f, 0.7f, 0.8f);

        bool showAll = true;
        Rarity activeRarity;

        void Start()
        {
            SetupFilters();
            Rebuild();
        }

        // ── 등급 필터 ──
        void SetupFilters()
        {
            foreach (var f in filters)
            {
                if (f == null || f.button == null) continue;
                var img = f.button.targetGraphic as Image;
                f.idleSprite = img != null ? img.sprite : null;
                var local = f;
                f.button.onClick.AddListener(() => SetFilter(local));
            }
            var allF = filters.Find(x => x != null && x.all);
            if (allF != null) ApplyActiveVisual(allF);
        }

        public void SetFilter(RarityFilter f)
        {
            showAll = f.all;
            activeRarity = f.rarity;
            ApplyActiveVisual(f);
            Rebuild();
        }

        void ApplyActiveVisual(RarityFilter active)
        {
            foreach (var f in filters)
            {
                if (f == null || f.button == null) continue;
                var img = f.button.targetGraphic as Image;
                if (img == null) continue;
                img.sprite = (f == active) ? f.button.spriteState.pressedSprite : f.idleSprite;
            }
        }

        // ── 목록 ──
        public void Rebuild()
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            var byId = new Dictionary<string, CardData>();
            if (allCards != null)
                foreach (var c in allCards)
                    if (c != null && !byId.ContainsKey(c.id)) byId[c.id] = c;

            // 인스펙터 배선(allCards)은 카드를 추가하면 쉽게 낡는다.
            // CardLibrary(Resources)로 빠진 것을 메워서, 새 카드도 이름·등급이 제대로 나온다.
            var lib = CardLibrary.Load();
            if (lib != null)
                foreach (var c in lib.cards)
                    if (c != null && !string.IsNullOrEmpty(c.id) && !byId.ContainsKey(c.id)) byId[c.id] = c;

            foreach (var inst in PlayerData.Instances())
            {
                byId.TryGetValue(inst.cardId, out var card);
                if (!showAll && (card == null || card.rarity != activeRarity)) continue;
                BuildCell(inst, card);
            }

            UpdateDeckLabel();
        }

        void UpdateDeckLabel(string warning = null)
        {
            if (deckCountLabel == null) return;
            if (warning != null)
            {
                deckCountLabel.text = warning;
                deckCountLabel.color = WarnColor;
                return;
            }
            if (PlayerData.RunInProgress)
            {
                // 런이 진행 중이면 편성이 잠긴다 — 이유를 먼저 보여줘서 눌러보고 거절당하지 않게
                deckCountLabel.text = $"런 진행 중 — 덱 편성 잠김  (현재 {PlayerData.DeckSize()}장)";
                deckCountLabel.color = LockColor;
                return;
            }
            deckCountLabel.text = $"런 덱  {PlayerData.DeckSize()}장  (최소 {PlayerData.MinDeck} · 최대 {PlayerData.MaxDeck})";
            deckCountLabel.color = OkColor;
        }

        void BuildCell(CardInstance inst, CardData card)
        {
            string cardName = card != null ? card.displayName : inst.cardId;

            var cell = new GameObject($"Card_{inst.instanceId}_{cardName}", typeof(RectTransform));
            cell.transform.SetParent(content, false);   // 크기는 GridLayoutGroup이 지정

            var artGO = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGO.transform.SetParent(cell.transform, false);
            var art = artGO.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0.5f, 1); art.anchorMax = new Vector2(0.5f, 1); art.pivot = new Vector2(0.5f, 1);
            art.sizeDelta = new Vector2(166, 250); art.anchoredPosition = Vector2.zero;
            var img = artGO.GetComponent<Image>(); img.sprite = cardArt; img.raycastTarget = false;

            var lblGO = new GameObject("Name", typeof(RectTransform), typeof(Text));
            lblGO.transform.SetParent(cell.transform, false);
            var lrt = lblGO.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0);
            lrt.sizeDelta = new Vector2(0, 46); lrt.anchoredPosition = Vector2.zero;
            var txt = lblGO.GetComponent<Text>();
            txt.text = cardName;
            txt.font = labelFont; txt.fontSize = 22; txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white; txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 다 쓴 카드(다음 런에 썩음)는 덱 토글 대신 판매 버튼을 단다
            if (inst.IsSpent) BuildSellButton(cell.transform, inst);
            else BuildDeckToggle(cell.transform, inst);

            if (inst.bound) BuildBoundTag(cell.transform);
            else BuildStateTag(cell.transform, inst);

            // 다 쓴 카드는 확실히 구분되게 어둡게
            if (inst.IsSpent) img.color = new Color(0.45f, 0.40f, 0.35f, 1f);
        }

        /// <summary>
        /// 좌하단: 감자 상태 — 생(남은 런 수) / 싹(라스트 찬스) / 썩음 (§8.1).
        /// 나이는 런이 시작될 때 먹으므로, 창고에서는 <b>다음 런 기준</b>으로 보여준다.
        /// (지금 상태를 그대로 쓰면 이미 끝난 런의 값이라 한 칸 뒤처진다)
        /// </summary>
        void BuildStateTag(Transform cell, CardInstance inst)
        {
            var tagGO = new GameObject("State", typeof(RectTransform), typeof(Image));
            tagGO.transform.SetParent(cell, false);
            var trt = tagGO.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 0);
            trt.sizeDelta = new Vector2(96, 28); trt.anchoredPosition = new Vector2(6, 52);

            var t = MakeTagText(tagGO.transform, 16);
            switch (inst.NextRunState)
            {
                case CardState.Sprouted:
                    tagGO.GetComponent<Image>().color = new Color(0.20f, 0.45f, 0.22f, 0.95f);
                    t.text = "싹 · 마지막"; t.color = new Color(0.75f, 1f, 0.75f);
                    break;
                case CardState.Rotten:
                    tagGO.GetComponent<Image>().color = new Color(0.34f, 0.24f, 0.16f, 0.95f);
                    t.text = "썩음"; t.color = new Color(1f, 0.65f, 0.5f);
                    break;
                default:
                    tagGO.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.24f, 0.9f);
                    t.text = $"{inst.RunsLeftToPlay}런 남음"; t.color = new Color(0.8f, 0.85f, 0.9f);
                    break;
            }
        }

        /// <summary>다 쓴 카드 판매 — 희귀도 무관 고정가.</summary>
        void BuildSellButton(Transform cell, CardInstance inst)
        {
            var goSell = new GameObject("Sell", typeof(RectTransform), typeof(Image));
            goSell.transform.SetParent(cell, false);
            var trt = goSell.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1, 1);
            trt.sizeDelta = new Vector2(88, 30); trt.anchoredPosition = new Vector2(-6, -6);
            var img = goSell.GetComponent<Image>();
            img.color = new Color(0.45f, 0.32f, 0.12f, 0.95f);

            var t = MakeTagText(goSell.transform, 17);
            t.text = $"판매 +{PlayerData.RottenSellPrice}";
            t.color = OkColor;

            var btn = goSell.AddComponent<Button>();
            btn.targetGraphic = img;
            int id = inst.instanceId;
            btn.onClick.AddListener(() =>
            {
                if (PlayerData.TrySellRotten(id, out string reason)) Rebuild();
                else UpdateDeckLabel(reason);
            });
        }

        /// <summary>우상단: 이 한 장을 덱에 넣기/빼기 (최소·최대 제한 적용).</summary>
        void BuildDeckToggle(Transform cell, CardInstance inst)
        {
            var tagGO = new GameObject("DeckToggle", typeof(RectTransform), typeof(Image));
            tagGO.transform.SetParent(cell, false);
            var trt = tagGO.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1, 1);
            trt.sizeDelta = new Vector2(66, 30); trt.anchoredPosition = new Vector2(-6, -6);
            var tImg = tagGO.GetComponent<Image>();

            var tTxt = MakeTagText(tagGO.transform, 18);

            bool inDeck = PlayerData.IsInDeck(inst.instanceId);
            bool locked = PlayerData.RunInProgress;

            tImg.color = locked ? new Color(0.22f, 0.23f, 0.27f, 0.9f)
                       : inDeck ? new Color(0.16f, 0.42f, 0.24f, 0.95f)
                                : new Color(0.28f, 0.28f, 0.31f, 0.95f);
            tTxt.text = locked ? (inDeck ? "덱 🔒" : "—") : (inDeck ? "덱 ✓" : "덱 +");
            tTxt.color = locked ? new Color(0.6f, 0.62f, 0.68f) : Color.white;

            if (locked) { tImg.raycastTarget = false; return; }   // 런 중에는 누를 수 없다

            var btn = tagGO.AddComponent<Button>();
            btn.targetGraphic = tImg;
            int id = inst.instanceId;
            bool want = !inDeck;
            btn.onClick.AddListener(() =>
            {
                if (PlayerData.TrySetInDeck(id, want, out string reason)) Rebuild();
                else UpdateDeckLabel(reason);
            });
        }

        /// <summary>좌상단: 귀속 표시(시작덱) — 덱에선 뺄 수 있지만 팔 수는 없다.</summary>
        void BuildBoundTag(Transform cell)
        {
            var tagGO = new GameObject("Bound", typeof(RectTransform), typeof(Image));
            tagGO.transform.SetParent(cell, false);
            var trt = tagGO.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 1);
            trt.sizeDelta = new Vector2(52, 26); trt.anchoredPosition = new Vector2(6, -6);
            tagGO.GetComponent<Image>().color = new Color(0.35f, 0.30f, 0.18f, 0.95f);

            var t = MakeTagText(tagGO.transform, 16);
            t.text = "귀속"; t.color = OkColor;
        }

        Text MakeTagText(Transform parent, int size)
        {
            var go = new GameObject("T", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = labelFont; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
    }
}
