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

            BuildDeckToggle(cell.transform, inst);
            if (inst.bound) BuildBoundTag(cell.transform);
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
            tImg.color = inDeck ? new Color(0.16f, 0.42f, 0.24f, 0.95f) : new Color(0.28f, 0.28f, 0.31f, 0.95f);
            tTxt.text = inDeck ? "덱 ✓" : "덱 +";
            tTxt.color = Color.white;

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
