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
        [Tooltip("카드 원본을 못 찾았을 때만 쓰는 대체 그림")]
        public Sprite cardArt;
        [Tooltip("전투 화면과 같은 카드 아트(프레임·코스트 배지 등). 비면 단색 카드로 그린다")]
        public BattleTheme theme;

        [Tooltip("등급 필터 버튼 (전체/일반/희귀/초월/전설)")]
        public List<RarityFilter> filters = new();

        [Tooltip("런 덱 장수 표시 라벨(선택)")]
        public Text deckCountLabel;

        [Tooltip("카드 이름 검색")]
        public InputField searchField;

        [Tooltip("정렬 드롭다운 — SortLabels 순서와 같다")]
        public Dropdown sortDropdown;

        /// <summary>정렬 기준. 드롭다운 항목 순서와 1:1로 맞춘다.</summary>
        public enum SortMode
        {
            RecentFirst,   // 얻은 순 (최신)
            OldestFirst,   // 얻은 순 (오래된)
            Name,          // 이름순
            RarityDesc,    // 희귀도 높은 순
            ExpirySoon,    // 기한 짧은 순 — 곧 썩는 것부터
            DeckFirst,     // 덱에 든 것 먼저
        }

        /// <summary>드롭다운에 표시할 이름 (SortMode 순서 그대로).</summary>
        public static readonly string[] SortLabels =
        {
            "얻은 순 (최신)",
            "얻은 순 (오래된)",
            "이름순",
            "희귀도 높은 순",
            "기한 짧은 순",
            "덱에 든 것 먼저",
        };

        SortMode sortMode = SortMode.RecentFirst;
        string searchQuery = "";

        static readonly Color OkColor = new(1f, 0.85f, 0.5f);
        static readonly Color WarnColor = new(1f, 0.45f, 0.4f);
        static readonly Color LockColor = new(0.65f, 0.7f, 0.8f);

        bool showAll = true;
        CardType? typeFilter;   // null = 전체 타입
        Rarity activeRarity;
        int matchCount;

        void Start()
        {
            SetupFilters();
            SetupSearchSort();
            SetupTypeFilter();
            Rebuild();
        }

        // 다른 탭(상점 판매 등)이나 보상으로 보유 카드가 바뀌면 바로 다시 그린다.
        // 예전엔 Start에서 한 번만 그려서, 상점에서 게임을 팔아 사라진 카드가 창고에 계속 보였다.
        void OnEnable()
        {
            PlayerData.Changed += Rebuild;
            Rebuild();
        }

        void OnDisable() => PlayerData.Changed -= Rebuild;

        void SetupSearchSort()
        {
            if (searchField != null)
            {
                // 입력할 때마다 즉시 걸러준다
                searchField.onValueChanged.AddListener(q => { searchQuery = q ?? ""; Rebuild(); });
            }

            if (sortDropdown != null)
            {
                sortDropdown.ClearOptions();
                sortDropdown.AddOptions(new List<string>(SortLabels));
                sortDropdown.value = (int)sortMode;
                sortDropdown.RefreshShownValue();
                sortDropdown.onValueChanged.AddListener(v => { sortMode = (SortMode)v; Rebuild(); });
            }
        }

        /// <summary>이름에 검색어가 들어있나 (대소문자 무시). 카드 데이터가 없으면 id로 대조).</summary>
        bool MatchesSearch(CardInstance inst, CardData card)
        {
            if (string.IsNullOrWhiteSpace(searchQuery)) return true;
            string q = searchQuery.Trim();
            string name = card != null ? card.displayName : inst.cardId;
            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return !string.IsNullOrEmpty(inst.cardId) &&
                   inst.cardId.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 정렬. 한글 이름은 유니코드 순서가 곧 가나다 순이라 Ordinal 비교로 충분하다.
        /// 기한은 짧은 것부터(곧 썩는 카드를 먼저 처리하게) — 귀속은 기한이 없어 맨 뒤로 간다.
        /// </summary>
        List<CardInstance> SortInstances(List<CardInstance> list, Dictionary<string, CardData> byId)
        {
            CardData Data(CardInstance c) => byId.TryGetValue(c.cardId, out var d) ? d : null;
            string Name(CardInstance c) { var d = Data(c); return d != null ? d.displayName : c.cardId; }
            int Rarity(CardInstance c) { var d = Data(c); return d != null ? (int)d.rarity : -1; }

            switch (sortMode)
            {
                case SortMode.OldestFirst:
                    list.Sort((a, b) => a.instanceId.CompareTo(b.instanceId)); break;
                case SortMode.Name:
                    list.Sort((a, b) =>
                    {
                        int c = string.CompareOrdinal(Name(a), Name(b));
                        return c != 0 ? c : a.instanceId.CompareTo(b.instanceId);
                    });
                    break;
                case SortMode.RarityDesc:
                    list.Sort((a, b) =>
                    {
                        int c = Rarity(b).CompareTo(Rarity(a));
                        if (c != 0) return c;
                        c = string.CompareOrdinal(Name(a), Name(b));
                        return c != 0 ? c : a.instanceId.CompareTo(b.instanceId);
                    });
                    break;
                case SortMode.ExpirySoon:
                    list.Sort((a, b) =>
                    {
                        int c = a.RunsLeftToPlay.CompareTo(b.RunsLeftToPlay);
                        return c != 0 ? c : a.instanceId.CompareTo(b.instanceId);
                    });
                    break;
                case SortMode.DeckFirst:
                {
                    var deck = new HashSet<int>(PlayerData.DeckIds());
                    list.Sort((a, b) =>
                    {
                        int c = deck.Contains(b.instanceId).CompareTo(deck.Contains(a.instanceId));
                        return c != 0 ? c : b.instanceId.CompareTo(a.instanceId);
                    });
                    break;
                }
                default:   // RecentFirst
                    list.Sort((a, b) => b.instanceId.CompareTo(a.instanceId)); break;
            }
            return list;
        }

        // ── 등급 필터 ──
        // ── 타입 필터 (공격 / 방어 / 스킬 / 뿌리) ──
        // 등급 필터 바로 아래 줄에 코드로 만든다 (전용 아트가 오기 전까지 단색 버튼).
        // 등급 필터와 겹쳐서 걸린다 — 예: 희귀 + 방어.
        static readonly (string label, CardType? type)[] TypeFilters =
        {
            ("전체", null), ("공격", CardType.Attack), ("방어", CardType.Defense),
            ("스킬", CardType.Skill), ("뿌리", CardType.Root),
        };
        static readonly Color TypeIdle = new(0.20f, 0.21f, 0.25f, 0.95f);
        static readonly Color TypeActive = new(0.62f, 0.45f, 0.16f, 1f);
        readonly List<(Image bg, CardType? type)> typeButtons = new();

        void SetupTypeFilter()
        {
            // 등급 필터 줄(FilterBar)을 기준으로 바로 아래에 둔다
            var rarityBar = filters.Find(f => f != null && f.button != null)?.button.transform.parent as RectTransform;
            if (rarityBar == null || rarityBar.parent == null) return;

            var bar = new GameObject("TypeFilterBar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(rarityBar.parent, false);
            bar.anchorMin = bar.anchorMax = rarityBar.anchorMin;
            bar.pivot = rarityBar.pivot;
            bar.anchoredPosition = rarityBar.anchoredPosition + new Vector2(0, -44);
            bar.sizeDelta = new Vector2(420, 30);
            var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            foreach (var (label, type) in TypeFilters)
            {
                var go = new GameObject("Type_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(bar, false);
                ((RectTransform)go.transform).sizeDelta = new Vector2(70, 30);
                var bg = go.GetComponent<Image>();
                go.GetComponent<Button>().targetGraphic = bg;
                var t = MakeTagText(go.transform, 17);
                t.text = label; t.color = Color.white;
                var local = type;
                go.GetComponent<Button>().onClick.AddListener(() => { typeFilter = local; ApplyTypeVisual(); Rebuild(); });
                typeButtons.Add((bg, type));
            }
            ApplyTypeVisual();
        }

        void ApplyTypeVisual()
        {
            foreach (var (bg, type) in typeButtons)
                bg.color = type == typeFilter ? TypeActive : TypeIdle;
        }

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
            // Destroy는 프레임 끝에 실행된다 — 먼저 떼어내야 그리드가 사라질 칸까지 자리를 잡지 않는다
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var c = content.GetChild(i);
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }

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

            // 등급 필터 → 이름 검색 → 정렬 순으로 거른다
            var shown = new List<CardInstance>();
            foreach (var inst in PlayerData.Instances())
            {
                byId.TryGetValue(inst.cardId, out var card);
                if (!showAll && (card == null || card.rarity != activeRarity)) continue;
                if (typeFilter != null && (card == null || card.type != typeFilter)) continue;
                if (!MatchesSearch(inst, card)) continue;
                shown.Add(inst);
            }

            foreach (var inst in SortInstances(shown, byId))
            {
                byId.TryGetValue(inst.cardId, out var card);
                BuildCell(inst, card);
            }

            matchCount = shown.Count;

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
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                deckCountLabel.text = $"\"{searchQuery.Trim()}\" 검색 결과 {matchCount}장  ·  런 덱 {PlayerData.DeckSize()}장";
                deckCountLabel.color = OkColor;
                return;
            }
            deckCountLabel.text = $"런 덱  {PlayerData.DeckSize()}장  (최소 {PlayerData.MinDeck} · 최대 {PlayerData.MaxDeck})";
            deckCountLabel.color = OkColor;
        }

        // ── 카드 칸 (그리드 셀 166×300) ──
        // 위: 전투 화면과 같은 카드(코스트·이름·효과) 166×232 · 아래 68px: 상태 태그 / 덱 토글 / 판매
        static readonly Vector2 CellCardSize = new(166, 232);

        // 같은 카드·같은 강화·같은 상태는 런타임 카드를 하나만 만든다 (검색할 때마다 다시 그리므로)
        readonly Dictionary<string, CardData> shownCache = new();

        CardData ShownCard(CardInstance inst, CardData card)
        {
            // 창고는 "다음 런 기준" — 다음 런에 싹이면 싹 효과로 보여준다 (썩을 카드는 원래 효과 그대로 어둡게)
            var state = inst.NextRunState == CardState.Sprouted ? CardState.Sprouted : CardState.Fresh;
            string key = $"{card.id}|{inst.upgrade}|{state}";
            if (!shownCache.TryGetValue(key, out var shown) || shown == null)
                shownCache[key] = shown = CardUpgrade.Build(card, inst.upgrade, state);
            return shown;
        }

        void BuildCell(CardInstance inst, CardData card)
        {
            string cardName = card != null ? card.displayName : inst.cardId;

            var cell = new GameObject($"Card_{inst.instanceId}_{cardName}", typeof(RectTransform));
            cell.transform.SetParent(content, false);   // 크기는 GridLayoutGroup이 지정

            if (card != null)
            {
                var view = CardView.Create(cell.transform, labelFont, CellCardSize);
                var vrt = (RectTransform)view.transform;
                vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0.5f, 1);
                vrt.anchoredPosition = Vector2.zero;
                // 클릭 기능은 없다. 다 쓴 카드(다음 런에 썩음)는 어둡게
                view.Bind(ShownCard(inst, card), CardLibrary.ThemeOr(theme), !inst.IsSpent, null);
            }
            else
            {
                // 원본을 못 찾은 카드 — 이름만이라도 보이게
                var artGO = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artGO.transform.SetParent(cell.transform, false);
                var art = artGO.GetComponent<RectTransform>();
                art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 1);
                art.sizeDelta = CellCardSize; art.anchoredPosition = Vector2.zero;
                artGO.GetComponent<Image>().sprite = cardArt;
                var txt = MakeTagText(artGO.transform, 18);
                txt.text = cardName;
            }

            // 마우스를 올린 카드가 살짝 커진다. 그리드 여백(가로 40 · 세로 24) 안에서만 커져서 옆 칸을 가리지 않는다
            var hover = cell.AddComponent<HoverScale>();
            hover.scale = 1.06f;

            // 새로 얻은 카드 — 오른쪽 위 빨간 점. 마우스를 올리면 "봤다"로 치고 사라진다
            if (PlayerData.IsNew(inst.instanceId))
            {
                var dot = new GameObject("NewDot", typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(cell.transform, false);
                var drt = (RectTransform)dot.transform;
                drt.anchorMin = drt.anchorMax = new Vector2(1, 1);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.anchoredPosition = new Vector2(-6, -6);
                drt.sizeDelta = new Vector2(20, 20);
                var dimg = dot.GetComponent<Image>();
                dimg.sprite = UiKit.Circle();
                dimg.color = new Color(0.93f, 0.2f, 0.18f);
                dimg.raycastTarget = false;

                int id = inst.instanceId;
                hover.onEnter = () =>
                {
                    PlayerData.MarkSeen(id);
                    if (dot != null) Destroy(dot);
                    hover.onEnter = null;
                };
            }

            // 아래 줄: 왼쪽 = 상태(귀속 / n런 남음 / 싹 / 썩음), 오른쪽 = 덱 토글 + 판매
            if (inst.bound) BuildBoundTag(cell.transform);
            else BuildStateTag(cell.transform, inst);

            // 다 쓴 카드(다음 런에 썩음)는 덱 토글 대신 판매 버튼을 단다
            if (inst.IsSpent) BuildSellButton(cell.transform, inst);
            else
            {
                BuildDeckToggle(cell.transform, inst);
                if (!inst.bound) BuildSellButton(cell.transform, inst, compact: true);
            }
        }

        /// <summary>
        /// 아래 줄 왼쪽: 감자 상태 — 생(남은 런 수) / 싹(라스트 찬스) / 썩음 (§8.1).
        /// 나이는 런이 시작될 때 먹으므로, 창고에서는 <b>다음 런 기준</b>으로 보여준다.
        /// (지금 상태를 그대로 쓰면 이미 끝난 런의 값이라 한 칸 뒤처진다)
        /// </summary>
        void BuildStateTag(Transform cell, CardInstance inst)
        {
            var tagGO = new GameObject("State", typeof(RectTransform), typeof(Image));
            tagGO.transform.SetParent(cell, false);
            var trt = tagGO.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 0);
            trt.sizeDelta = new Vector2(92, 28); trt.anchoredPosition = new Vector2(2, 36);

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

        /// <summary>
        /// 카드 판매. 썩은 카드는 고정 3토인, 멀쩡한 카드는 희귀도별 가격.
        /// compact = 덱 토글 옆에 작게 붙는 형태(멀쩡한 카드용).
        /// </summary>
        void BuildSellButton(Transform cell, CardInstance inst, bool compact = false)
        {
            var goSell = new GameObject("Sell", typeof(RectTransform), typeof(Image));
            goSell.transform.SetParent(cell, false);
            var trt = goSell.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1, 0);
            // 멀쩡한 카드는 덱 토글 아래, 다 쓴 카드는 덱 토글 자리에
            trt.sizeDelta = compact ? new Vector2(70, 26) : new Vector2(70, 28);
            trt.anchoredPosition = compact ? new Vector2(-2, 4) : new Vector2(-2, 36);
            var img = goSell.GetComponent<Image>();

            int value = PlayerData.SellValue(inst);
            // 런에 쓰고 있는 카드는 팔 수 없다 — 미리 잠가서 눌러보고 거절당하지 않게
            bool locked = PlayerData.RunInProgress && PlayerData.IsInDeck(inst.instanceId);
            img.color = locked ? new Color(0.24f, 0.24f, 0.28f, 0.9f)
                               : new Color(0.45f, 0.32f, 0.12f, 0.95f);

            var t = MakeTagText(goSell.transform, compact ? 15 : 17);
            t.text = locked ? "사용 중" : $"판매 +{value}";
            t.color = locked ? new Color(0.6f, 0.62f, 0.68f) : OkColor;

            if (locked) { img.raycastTarget = false; return; }

            var btn = goSell.AddComponent<Button>();
            btn.targetGraphic = img;
            int id = inst.instanceId;
            btn.onClick.AddListener(() =>
            {
                if (PlayerData.TrySellCard(id, out int refund, out string reason))
                {
                    Rebuild();
                    UpdateDeckLabel($"카드를 팔았습니다 (+{refund} 토인)");
                }
                else UpdateDeckLabel(reason);
            });
        }

        /// <summary>아래 줄 오른쪽: 이 한 장을 덱에 넣기/빼기 (최소·최대 제한 적용).</summary>
        void BuildDeckToggle(Transform cell, CardInstance inst)
        {
            var tagGO = new GameObject("DeckToggle", typeof(RectTransform), typeof(Image));
            tagGO.transform.SetParent(cell, false);
            var trt = tagGO.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1, 0);
            trt.sizeDelta = new Vector2(70, 28); trt.anchoredPosition = new Vector2(-2, 36);
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

        /// <summary>아래 줄 왼쪽: 귀속 표시(시작덱) — 덱에선 뺄 수 있지만 팔 수는 없다.</summary>
        void BuildBoundTag(Transform cell)
        {
            var tagGO = new GameObject("Bound", typeof(RectTransform), typeof(Image));
            tagGO.transform.SetParent(cell, false);
            var trt = tagGO.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 0);
            trt.sizeDelta = new Vector2(92, 28); trt.anchoredPosition = new Vector2(2, 36);
            tagGO.GetComponent<Image>().color = new Color(0.35f, 0.30f, 0.18f, 0.95f);

            var t = MakeTagText(tagGO.transform, 16);
            t.text = "귀속 · 무기한"; t.color = OkColor;
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
