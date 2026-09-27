using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Keys = TatoGames.CardGame.TutorialOverlay.Keys;
using Step = TatoGames.CardGame.TutorialOverlay.Step;
using Side = TatoGames.CardGame.TutorialOverlay.Side;

namespace TatoGames.CardGame
{
    /// <summary>
    /// MVP 전투 1판 코어. 턴제(플레이어 ↔ 적), 에너지·드로우·블록, 상태이상 6종,
    /// 방어 2종 분리(§9). 데이터는 전부 SO(CardData/EnemyData, 외부화). UI는 코드로 만든다
    /// — 아트는 BattleTheme 슬롯으로 들어온다(§14.5).
    ///
    /// 화면 구성(1280×720 기준, 아래→위):
    ///   배경 · 적/플레이어 패널 · 에너지 · 덱 카운터 · 손패 · 턴 종료 · 결과(패배/클리어) ·
    ///   보상 · 맵 · 대장간 · 상단 바(스테이지·토인·덱·도움말·나가기) · 알림 배너 · 말풍선 · 튜토리얼
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Header("데이터 (SO)")]
        public CardData[] starterDeck;   // 시작덱 8장 (에디터에서 할당)
        public EnemyData enemyData;
        [Tooltip("맵에 나올 수 있는 적 전부. 스테이지 배정은 각 적의 stages에서 정한다")]
        public EnemyData[] enemyPool;
        [Tooltip("전투 보상 후보 풀 — 전투 출처 카드만. §12.1 (카드 원본 조회는 CardLibrary가 한다)")]
        public CardData[] rewardPool;

        [Header("플레이어 기본 수치 (§9)")]
        public int playerMaxHp = 36;
        public int maxEnergy = 2;
        public int drawCount = 5;
        public int handLimit = 8;

        [Header("UI")]
        public Font uiFont;
        [Tooltip("전투 화면 아트 모음 — 스프라이트를 넣으면 코드 수정 없이 반영")]
        public BattleTheme theme;

        // ── 런타임 상태 ──
        Combatant player, enemy;
        // 카드 더미는 "어느 보유 카드에서 왔는지"까지 들고 있어야 전투를 저장·복원할 수 있다
        readonly List<BattleCard> drawPile = new();
        readonly List<BattleCard> hand = new();
        readonly List<BattleCard> discard = new();
        int enemyIndex;
        int energy;
        bool over;
        bool transformed;   // 감자벌레 2페이즈 진입 여부
        string startNotice = "";   // 런 시작 알림(썩음) — 첫 턴이 끝나면 지운다

        // ── UI 참조 ──
        CombatantPanel enemyPanel, playerPanel;
        Text message, energyText, pileText;
        RectTransform energyOrb;
        Image backgroundImage;
        RectTransform handRow;
        Button endTurnBtn;
        GameObject resultLayer;     // 패배·런 클리어 — 어둡게 덮고 가운데 문구 + 버튼
        GameObject restartBtn;
        GameObject exitBtn;
        GameObject rewardPanel;
        Text rewardTitle;
        RectTransform rewardRow;
        Button rewardSkip;
        GameObject mapPanel;
        Text mapInfo;
        RectTransform mapArea;     // 노드 그래프가 그려지는 영역
        bool stageIntro;           // 새 스테이지 첫 노드 진입 대기
        GameObject forgePanel;     // 대장간
        Text forgeInfo, forgeHint;
        RectTransform forgeGrid, forgeActionRow, forgeScroll;
        int forgeSelected = -1;
        int forgeConfirmRemove = -1;   // 제거는 되돌릴 수 없어서 두 번 눌러야 한다
        readonly Dictionary<int, CardView> forgeViews = new();
        CardView forgePopup;           // 강화 버튼에 마우스를 올리면 결과 카드를 미리 보여준다

        // 상단 바 · 알림
        RectTransform topBar;
        Text topStage, topToin, topDeck;
        Image bannerBg;
        Text bannerText;
        float bannerHideAt = -1f;
        bool bannerSticky;

        // ── 배치 기준 (1280×720) ──
        const float TopBarH = 44f;
        const float HandY = 16f;                                   // 손패 아래끝
        static readonly Vector2 HandCardSize = new(150, 210);
        static readonly Vector2 RewardCardSize = new(180, 250);
        static readonly Vector2 ForgeCardSize = new(102, 142);
        const float PanelBottomY = HandY + 210f + 10f;             // 적·플레이어 패널은 손패 윗변 바로 위
        const float HandWidth = 980f;
        const float HandSpacing = 8f;
        const float BannerMaxW = 600f;

        // 마우스를 올린 카드가 얼마나 커지나 — 작은 카드일수록 크게 키워 글자가 읽히게
        const float HandHoverScale = 1.2f;
        const float RewardHoverScale = 1.1f;
        const float ForgeHoverScale = 1.4f;

        // 떠오르는 숫자 색
        static readonly Color DamageColor = new(1f, 0.42f, 0.36f);
        static readonly Color HealColor = new(0.5f, 1f, 0.55f);
        static readonly Color BlockColor = new(0.55f, 0.8f, 1f);
        static readonly Color WardColor = new(0.8f, 0.65f, 1f);
        static readonly Color Accent = new(1f, 0.78f, 0.2f);

        void Start()
        {
            if (uiFont == null)
                uiFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 22);
            theme = CardLibrary.ThemeOr(theme);   // 씬 연결이 빠져도 아트가 나오게
            BuildUI();

            // §7 시간축 = 런 수 — 나이는 <b>새 런을 시작할 때만</b> 먹는다.
            // 중단했던 런을 이어서 하는 건 같은 런이므로 세지 않는다.
            //
            // 나가기로 나이를 회피할 구멍은 '덱 잠금'이 막는다 — 런이 진행 중이면 덱을 못 바꾸므로
            // 중간에 나갔다 오는 것으로 얻을 게 없다. 새 런은 죽거나 클리어해야만 시작된다.
            if (!RunState.Active)
            {
                RunState.StartRun(playerMaxHp, enemyPool);
                TatoGames.Launcher.Achievements.Bump(TatoGames.Launcher.Achievements.RunsStarted);
                int rotted = PlayerData.AgeAll();
                if (rotted > 0)
                    TatoGames.Launcher.Achievements.Bump(TatoGames.Launcher.Achievements.CardsRotted, rotted);
                if (rotted > 0)
                    startNotice = $"카드 {rotted}장이 수명이 다해 썩었어요 — " +
                                  $"감자창고에서 장당 {PlayerData.RottenSellPrice}토인에 팔 수 있어요";
            }
            EnterNode();
        }

        void OnEnable() => PlayerData.Changed += UpdateTopBar;
        void OnDisable() => PlayerData.Changed -= UpdateTopBar;

        void Update()
        {
            // 알림 배너 — 정해진 시간이 지나면 흐려지며 사라진다
            if (bannerBg == null || !bannerBg.gameObject.activeSelf || bannerSticky) return;
            float left = bannerHideAt - Time.unscaledTime;
            if (left <= 0f) { bannerBg.gameObject.SetActive(false); return; }
            float a = Mathf.Clamp01(left / 0.35f);
            bannerBg.color = new Color(0.05f, 0.05f, 0.07f, 0.85f * a);
            var c = bannerText.color; c.a = a; bannerText.color = c;
        }

        // ══════════════════════════════════════════════ 런 진행 (§11.3) ════════
        /// <summary>
        /// 현재 노드로 진입 — 전투/보스면 전투, 휴식이면 회복, 대장간이면 대장간, 끝이면 클리어.
        /// 런처에 나갔다 돌아와도 여기로 들어온다. 그래서 "이미 끝낸 노드"와 "하던 전투"를 구분해야
        /// 같은 전투·휴식을 반복하거나 전투를 처음부터 다시 하는 구멍이 생기지 않는다.
        /// </summary>
        void EnterNode()
        {
            var node = RunState.Current;
            if (node == null) { ShowRunClear(); return; }

            // 새 스테이지 첫 노드를 고를 차례
            if (RunState.StageIntro) { stageIntro = true; ShowIdleField(); ShowMap(null); return; }

            // 이미 끝낸 노드 — 받다 만 보상이 있으면 그걸 다시 띄우고, 아니면 맵부터
            if (RunState.NodeCleared)
            {
                if (BattleSave.TryLoadReward(out var pending)) { ResumeReward(node, pending); return; }
                if (node.type == NodeType.Boss) { ShowIdleField(); AfterNodeCleared(); return; }   // 보스 다음은 맵이 아니라 다음 스테이지
                ShowIdleField(); ShowMap(null); return;
            }

            if (node.type == NodeType.Rest)
            {
                // 실제로 오른 만큼만 알린다 — 체력이 거의 차 있으면 +29가 아니다
                int before = Mathf.Clamp(RunState.PlayerHp, 0, playerMaxHp);
                RunState.PlayerHp = Mathf.Min(playerMaxHp, before + RunState.RestHeal);
                int healed = RunState.PlayerHp - before;
                RunState.NodeCleared = true;   // 다시 들어와도 또 회복하지 않는다
                ShowIdleField();
                ShowMap(healed > 0
                    ? $"휴식 — 체력 +{healed} 회복  ({RunState.PlayerHp}/{playerMaxHp})"
                    : $"휴식 — 체력이 이미 가득 차 있어요  ({RunState.PlayerHp}/{playerMaxHp})");
                return;
            }

            if (node.type == NodeType.Forge) { ShowIdleField(); ShowForge(); return; }

            var found = FindEnemy(node.enemyId);
            if (found != null) enemyData = found;

            // 하던 전투가 저장돼 있으면 그 자리에서 이어서
            if (BattleSave.TryLoadBattle(out var saved) && saved.enemyId == node.enemyId) ResumeBattle(saved);
            else StartBattle();
        }

        /// <summary>
        /// 전투 밖(맵·대장간·휴식)일 때 뒤에 깔리는 화면 — 플레이어 체력만 보이고 적은 숨긴다.
        /// 전투를 거치지 않고 맵부터 열면 패널이 빈 흰 사각형으로 남기 때문에 필요하다.
        /// </summary>
        void ShowIdleField()
        {
            over = true;
            drawPile.Clear(); hand.Clear(); discard.Clear();
            player = new Combatant { name = "플레이어", hp = Mathf.Clamp(RunState.PlayerHp, 0, playerMaxHp), maxHp = playerMaxHp };
            enemy = null;
            if (enemyPanel != null) enemyPanel.gameObject.SetActive(false);
            if (endTurnBtn != null) endTurnBtn.interactable = false;
            if (resultLayer != null) resultLayer.SetActive(false);
            Refresh();
        }

        /// <summary>현재 스테이지 배경으로 교체 (보스 클리어 후 전환).</summary>
        void UpdateBackground()
        {
            if (backgroundImage == null) return;
            var sp = theme != null ? theme.BackgroundFor(RunState.Stage) : null;
            UiKit.Apply(backgroundImage, sp,
                        theme != null ? theme.panelColor : new Color(0.09f, 0.10f, 0.13f));
            if (backgroundImage.sprite != null && theme != null)
                backgroundImage.color = theme.backgroundTint;
        }

        EnemyData FindEnemy(string id)
        {
            if (enemyPool == null || string.IsNullOrEmpty(id)) return null;
            foreach (var e in enemyPool)
                if (e != null && e.id == id) return e;
            Debug.LogWarning($"[TatoGames] 적을 찾지 못함: {id} — enemyPool 확인");
            return null;
        }

        // ══════════════════════════════════════════════ 전투 진행 ══════════════
        /// <summary>전투 화면으로 전환 — 맵·대장간·결과 화면을 내리고 적 패널을 올린다.</summary>
        void ShowBattleField()
        {
            if (mapPanel != null) mapPanel.SetActive(false);
            if (forgePanel != null) forgePanel.SetActive(false);
            if (rewardPanel != null) rewardPanel.SetActive(false);
            if (resultLayer != null) resultLayer.SetActive(false);
            if (endTurnBtn != null) endTurnBtn.interactable = true;
            if (enemyPanel != null) enemyPanel.gameObject.SetActive(true);
        }

        void StartBattle()
        {
            over = false;
            transformed = false;
            ShowBattleField();

            int hp = RunState.Active ? Mathf.Clamp(RunState.PlayerHp, 1, playerMaxHp) : playerMaxHp;
            player = new Combatant { name = "플레이어", hp = hp, maxHp = playerMaxHp };
            int scaledHp = ScaleHp(enemyData != null ? enemyData.hp : 15);
            enemy = new Combatant
            {
                name = enemyData != null ? enemyData.enemyName : "???",
                hp = scaledHp,
                maxHp = scaledHp,
                block = enemyData != null ? enemyData.blockStart : 0,
                ward = enemyData != null ? enemyData.wardStart : 0,
            };

            drawPile.Clear(); hand.Clear(); discard.Clear();
            BuildRunDeck();
            Shuffle(drawPile);
            enemyIndex = 0;

            StartPlayerTurn();
            AfterBattleShown();
        }

        /// <summary>
        /// 저장된 전투를 그 자리에서 이어간다 — 체력·방어·상태이상·적 행동 순서·덱 순서·손패 전부.
        /// 나갔다 오는 것으로 얻을 게 없다(예전엔 전투 시작 체력으로 새로 시작해 공짜 재도전이 됐다).
        /// </summary>
        void ResumeBattle(BattleSave.BattleState s)
        {
            over = false;
            ShowBattleField();

            player = BattleSave.Restore(s.player);
            enemy = BattleSave.Restore(s.enemy);
            transformed = s.transformed;
            enemyIndex = s.enemyIndex;
            energy = s.energy;
            startNotice = s.notice ?? "";

            drawPile.Clear(); hand.Clear(); discard.Clear();
            var owned = PlayerData.Instances().ToDictionary(c => c.instanceId);
            RestorePile(s.draw, drawPile, owned);
            RestorePile(s.hand, hand, owned);
            RestorePile(s.discard, discard, owned);

            Refresh();
            AfterBattleShown();
        }

        /// <summary>전투 화면이 뜬 직후 — 런 시작 알림과 첫 전투 안내.</summary>
        void AfterBattleShown()
        {
            if (!string.IsNullOrEmpty(startNotice)) Banner(startNotice, sticky: true);
            // 안내를 닫자마자 이 전투의 상황 힌트(적의 블록 등)를 확인한다 — 다음 행동까지 기다리지 않게
            Tutor(Keys.BattleBasics, BasicsSteps(), onClosed: CheckHints);
        }

        void RestorePile(List<BattleSave.CardRef> refs, List<BattleCard> pile, Dictionary<int, CardInstance> owned)
        {
            if (refs == null) return;
            foreach (var r in refs)
            {
                var data = FindCard(r.cardId);
                if (data == null) continue;
                owned.TryGetValue(r.instanceId, out var inst);
                pile.Add(new BattleCard
                {
                    data = inst != null ? CardUpgrade.Build(data, inst.upgrade, inst.State) : data,
                    instanceId = r.instanceId,
                    cardId = r.cardId,
                });
            }
        }

        /// <summary>지금 전투 상태를 저장한다. 플레이어가 입력을 기다리는 모든 순간(카드 사용 후·턴 시작)에 불린다.</summary>
        void SaveBattle()
        {
            if (!RunState.Active || player == null || enemy == null) return;
            BattleSave.SaveBattle(new BattleSave.BattleState
            {
                enemyId = RunState.Current?.enemyId,
                enemyIndex = enemyIndex,
                energy = energy,
                transformed = transformed,
                notice = startNotice,
                player = BattleSave.Capture(player),
                enemy = BattleSave.Capture(enemy),
                draw = BattleSave.Refs(drawPile),
                hand = BattleSave.Refs(hand),
                discard = BattleSave.Refs(discard),
            });
        }

        /// <summary>
        /// 런 덱 = 감자창고에서 덱에 넣어둔 보유 카드 (§10.7 2-존. 시작덱도 뺄 수 있다).
        /// 카드 원본은 CardLibrary(전 카드 39종)에서 찾는다 — 예전엔 starterDeck + rewardPool에서만 찾아서,
        /// 보상 풀을 전투 출처로 좁힌 뒤 <b>미니게임 카드가 전투 덱에서 조용히 빠졌다.</b>
        /// </summary>
        void BuildRunDeck()
        {
            // 대장간 강화와 감자 상태(생/싹)를 함께 얹은 런타임 복제본이 들어간다 (§8.1 · §10.10)
            foreach (var inst in PlayerData.DeckInstances())
            {
                var card = FindCard(inst.cardId);
                if (card == null) { Debug.LogWarning($"[TatoGames] 카드 원본 없음: {inst.cardId} — 덱에서 제외"); continue; }
                drawPile.Add(new BattleCard
                {
                    data = CardUpgrade.Build(card, inst.upgrade, inst.State),
                    instanceId = inst.instanceId,
                    cardId = inst.cardId,
                });
            }

            // 안전장치: 저장이 비었거나 해석 실패 시 시작덱으로 폴백
            if (drawPile.Count == 0 && starterDeck != null)
                foreach (var c in starterDeck)
                    if (c != null) drawPile.Add(new BattleCard { data = c, instanceId = -1, cardId = c.id });
        }

        void StartPlayerTurn()
        {
            if (over) return;
            energy = maxEnergy;                 // 리필 (§9)
            player.OnTurnStart();               // 블록 소멸 · 중독 · 재생
            if (player.IsDead) { Lose(); return; }
            DrawCards(drawCount);
            Refresh();
        }

        public void OnEndTurn()
        {
            if (over) return;
            startNotice = "";                   // 첫 턴이 지나면 런 시작 알림을 내린다
            if (bannerSticky) HideBanner();
            player.OnTurnEnd();                 // 취약·약화 감소
            foreach (var c in hand) discard.Add(c);
            hand.Clear();

            EnemyTurn();
            if (over) return;
            if (player.IsDead) { Lose(); return; }
            StartPlayerTurn();
        }

        List<EnemyAction> ActivePattern() =>
            enemyData == null ? new List<EnemyAction>()
                              : transformed ? enemyData.phase2Pattern : enemyData.pattern;

        void EnemyTurn()
        {
            var pattern = ActivePattern();
            if (enemyData == null || pattern.Count == 0) return;

            enemy.OnTurnStart();
            if (enemy.IsDead) { Banner($"{enemy.name}{CardText.Josa(enemy.name, "이", "가")} 중독으로 쓰러졌어요!"); Win(); return; }

            var act = pattern[Mathf.Clamp(enemyIndex, 0, pattern.Count - 1)];
            string result = "";
            switch (act.kind)
            {
                case EnemyActionKind.Attack:
                {
                    int hits = Mathf.Max(1, act.hits);
                    int raw = 0, lost = 0;
                    for (int i = 0; i < hits && !player.IsDead; i++)
                    {
                        int dmg = player.ModifyIncoming(enemy.ModifyOutgoing(ScaleAtk(act.amount)));
                        raw += dmg;
                        lost += player.TakeAttack(dmg);
                    }
                    int absorbed = raw - lost;
                    result = lost <= 0 ? $"블록으로 {raw} 모두 막음"
                           : absorbed > 0 ? $"{lost} 피해 (블록으로 {absorbed} 막음)"
                           : $"{lost} 피해";
                    break;
                }
                case EnemyActionKind.Block:
                {
                    int before = enemy.TotalBlock;
                    enemy.GainBlock(ScaleAtk(act.amount), temporary: false);   // 적 블록도 누적
                    result = $"블록 +{enemy.TotalBlock - before}";
                    break;
                }
                case EnemyActionKind.Debuff:
                    result = player.TryApplyDebuff(act.status, act.amount)
                        ? $"{CardText.Kor(act.status)} {act.amount} 걸림"
                        : $"{CardText.Kor(act.status)} — 효과 방어로 막음";
                    break;
                case EnemyActionKind.Buff:
                    enemy.Add(act.status, act.amount);
                    result = $"{CardText.Kor(act.status)} +{act.amount}";
                    break;
            }

            // 되받아치기 — 이번 적 턴에 받은 피해의 절반을 되돌려준다 (블록 무시)
            int reflected = player.ConsumeReflect();
            if (reflected > 0) result += $"  ·  반사 {reflected}";

            string label = string.IsNullOrEmpty(act.label) ? "" : $" · {act.label}";
            Banner($"{enemy.name}{label} — {result}", 3f);

            if (reflected > 0)
            {
                enemy.TakeLoss(reflected);
                if (enemy.IsDead) { Win(); return; }
            }

            enemy.OnTurnEnd();
            AdvanceEnemy();
        }

        // 장별 스케일 (§11.6) — 2·3단계 적이 더 단단하고 더 아프다
        int ScaleHp(int v) =>
            Mathf.Max(1, Mathf.RoundToInt(v * (RunState.Active ? RunState.HpScale : 1f)));
        int ScaleAtk(int v) =>
            Mathf.Max(0, Mathf.RoundToInt(v * (RunState.Active ? RunState.AtkScale : 1f)));

        void AdvanceEnemy()
        {
            int count = ActivePattern().Count;
            enemyIndex++;
            if (enemyIndex >= count)
                enemyIndex = enemyData.patternLoop ? 0 : count - 1;
        }

        /// <summary>공격 방어를 깨뜨렸을 때 2페이즈 변신(감자벌레). 새 체력·패턴으로 전환.</summary>
        void TryTransform(int blockBefore)
        {
            if (transformed || enemyData == null || !enemyData.transformOnBlockBreak) return;
            if (blockBefore <= 0 || enemy.TotalBlock > 0) return;   // 서 있던 방어를 0으로 깬 순간만
            transformed = true;
            enemyIndex = 0;
            string before = enemy.name;
            enemy.name = string.IsNullOrEmpty(enemyData.phase2Name) ? enemy.name : enemyData.phase2Name;
            enemy.maxHp = ScaleHp(Mathf.Max(1, enemyData.phase2Hp));
            enemy.hp = enemy.maxHp;   // 껍질 속 본체 (감자벌레: 10)
            enemy.block = 0; enemy.tempBlock = 0; enemy.ward = 0;

            // 체력이 갑자기 가득 차는 이유를 알려준다 — 안 그러면 버그처럼 보인다
            snapEnemyRef = null;
            Banner($"{before}의 블록이 무너졌어요! {enemy.name}{CardText.Josa(enemy.name, "이", "가")} 본모습을 드러냈어요 (체력 {enemy.maxHp})", 3.5f);
        }

        // ══════════════════════════════════════════════ 카드 플레이 ════════════
        void PlayCard(BattleCard played)
        {
            var card = played?.data;
            if (over || card == null) return;
            if (energy < card.cost) return;
            energy -= card.cost;

            int blockBefore = enemy.TotalBlock;
            foreach (var e in card.effects)
                ResolveEffect(e, self: player, opponent: enemy);

            hand.Remove(played);
            // 소멸(§10.3) — 이번 전투에서 다시 안 나온다. 버림 더미로 가지 않으므로
            // 덱을 다 돌아도 재사용할 수 없다. 강한 1회성 효과의 대가.
            if (card.keyword != CardKeyword.Exhaust) discard.Add(played);

            TryTransform(blockBefore);       // 방어 깨지면 변신(사망 판정보다 먼저)
            if (enemy.IsDead) { Win(); return; }
            Refresh();
        }

        void ResolveEffect(CardEffect e, Combatant self, Combatant opponent)
        {
            var target = e.target == TargetType.Self ? self : opponent;
            switch (e.type)
            {
                case EffectType.Damage:
                    int hits = Mathf.Max(1, e.hits);
                    for (int i = 0; i < hits && !opponent.IsDead; i++)
                    {
                        int raw = opponent.ModifyIncoming(self.ModifyOutgoing(e.value));
                        opponent.TakeAttack(raw);
                    }
                    break;
                case EffectType.Block:
                    self.GainBlock(e.value, temporary: false);    // 누적 블록
                    break;
                case EffectType.TemporaryBlock:
                    // 철벽처럼 한 번에 크게 주는 블록 — 누적되지 않고 다음 턴 시작에 사라진다
                    self.GainBlock(e.value, temporary: true);
                    break;
                case EffectType.EffectDefense:
                    self.ward += e.value;
                    break;
                case EffectType.ApplyStatus:
                    if (target == self) self.Add(e.status, e.value);      // 버프/자기부여
                    else if (!target.TryApplyDebuff(e.status, e.value))   // 디버프(효과방어 검사)
                        Banner($"{target.name}의 효과 방어가 {CardText.Kor(e.status)}{CardText.Josa(CardText.Kor(e.status), "을", "를")} 막았어요", 2.4f);
                    break;
                case EffectType.DrawPerRemainingEnergy:
                    DrawCards(energy * Mathf.Max(1, e.value));   // 근사: 즉시 처리(정식은 턴 종료 시)
                    break;
                case EffectType.ReflectHalfDamage:
                    // 되받아치기 — 다음 적 턴에 받은 피해(막아낸 몫 포함)의 절반을 되돌려준다
                    self.ArmReflect();
                    break;
                case EffectType.DisableBlockThisTurn:
                    // 돌진의 부작용 — 이번 턴에는 더 이상 블록을 얻을 수 없다
                    self.blockDisabled = true;
                    break;
                case EffectType.AmplifyPoisonPerTurn:
                    // 뿌리내림 — 이 적이 중독으로 받는 피해 +value (전투가 끝날 때까지 지속)
                    target.poisonAmp += e.value;
                    break;
                case EffectType.ApplyStatusForTurns:
                    // 곰팡이 정원 — duration턴 동안 매 턴 status +value
                    target.AddPeriodic(e.status, e.value, e.duration);
                    break;

                // ── 메인 게임(전투 출처) 카드 ──
                case EffectType.Heal:
                    self.Heal(e.value);
                    break;
                case EffectType.Draw:
                    DrawCards(e.value);
                    break;
                case EffectType.GainEnergy:
                    energy += e.value;
                    break;
                case EffectType.DamageFromBlock:
                {
                    // 흙 던지기 — 쌓아둔 방어를 그대로 두고 그 비율만큼 때린다
                    int fromBlock = self.TotalBlock * e.value / 100;
                    if (fromBlock > 0)
                        opponent.TakeAttack(opponent.ModifyIncoming(self.ModifyOutgoing(fromBlock)));
                    break;
                }
                case EffectType.DamageConsumingBlock:
                {
                    // 흙사태 — 방어를 헐어 그만큼 때린다. 방어 빌드의 피니셔
                    int spent = self.SpendBlock(e.value);
                    if (spent > 0)
                        opponent.TakeAttack(opponent.ModifyIncoming(self.ModifyOutgoing(spent)));
                    break;
                }
            }
        }

        void DrawCards(int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (hand.Count >= handLimit) break;
                if (drawPile.Count == 0)
                {
                    if (discard.Count == 0) break;   // 덱·버림 모두 비면 중단
                    drawPile.AddRange(discard);
                    discard.Clear();
                    Shuffle(drawPile);
                }
                var top = drawPile[drawPile.Count - 1];
                drawPile.RemoveAt(drawPile.Count - 1);
                hand.Add(top);
            }
        }

        void Win()
        {
            over = true;
            TatoGames.Launcher.Achievements.Bump(TatoGames.Launcher.Achievements.BattlesWon);
            if (RunState.IsBossNode)
                TatoGames.Launcher.Achievements.Bump(TatoGames.Launcher.Achievements.BossesKilled);
            if (RunState.Active)
            {
                RunState.PlayerHp = player.hp;   // 체력은 노드를 넘어 유지
                RunState.NodeCleared = true;     // 나갔다 와도 이 전투를 다시 하지 않는다
            }
            BattleSave.ClearBattle();
            Refresh();
            if (endTurnBtn != null) endTurnBtn.interactable = false;
            RebuildHand();
            OfferReward();
        }

        void Lose()
        {
            over = true; Refresh();
            // §10.7 "복사가 아니라 이동" · §13.2 손실 채널 — 인게임 덱 소멸(귀속 제외)
            int lost = PlayerData.DestroyRunDeck();
            message.text = lost > 0
                ? $"패배…\n<size=22>런 덱의 카드 {lost}장이 사라졌어요 (시작 카드는 남아요)</size>"
                : "패배…\n<size=22>런이 여기서 끝났어요</size>";
            RunState.End();          // 런 종료 — 런·전투·보상 저장을 전부 지운다
            EndControls();
            Tutor(Keys.BattleDefeat, DefeatSteps());
        }

        /// <summary>노드 클리어 후: 보스였으면 런 종료, 아니면 맵으로.</summary>
        void AfterNodeCleared(string note = null)
        {
            if (RunState.IsBossNode)
            {
                if (!RunState.HasNextStage) { ShowRunClear(); return; }
                RunState.NextStage(enemyPool);   // 새 스테이지 맵 생성 (그 스테이지 적으로)
                UpdateBackground();       // 배경 전환 (감자밭 → 뿌리층 → 깊은 토양)
                stageIntro = true;
                string head = $"보스 격파!   {RunState.StageName} 진입 — 적이 더 단단하고 아파져요";
                ShowMap(note == null ? head : $"{head}\n{note}");
                return;
            }
            ShowMap(note);
        }

        void ShowRunClear()
        {
            TatoGames.Launcher.Achievements.Bump(TatoGames.Launcher.Achievements.RunsCleared);
            RunState.End();
            if (mapPanel != null) mapPanel.SetActive(false);
            message.text = "런 클리어!\n<size=22>세 스테이지의 보스를 모두 쓰러뜨렸어요. 덱의 카드는 그대로 남아요</size>";
            EndControls();
        }

        // ══════════════════════════════════════════════════ 보상 (§12.1) ══════
        /// <summary>
        /// 보상을 굴리고 저장한 뒤 보여준다. 저장해 두기 때문에 보상 화면에서 나갔다 와도
        /// 같은 토인·같은 후보가 다시 뜬다 — 다시 굴려서 더 좋은 카드를 노리는 것도 막힌다.
        /// </summary>
        void OfferReward()
        {
            int toin = RewardToin();
            bool boss = enemyData != null && enemyData.tier == EnemyTier.Boss;
            var cards = PickRewardCards(boss ? BossRewardCount : NormalRewardCount, boss);

            if (cards.Count == 0)   // 후보 없으면 토인만 지급
            {
                PlayerData.AddToin(toin);
                AfterNodeCleared($"승리!   +{toin} 토인");
                return;
            }

            if (RunState.Active)
                BattleSave.SaveReward(new BattleSave.RewardState
                {
                    toin = toin, boss = boss, cardIds = cards.Select(c => c.id).ToList(),
                });
            ShowReward(toin, boss, cards);
        }

        /// <summary>받다 만 보상을 다시 띄운다 (보상 화면에서 나갔다 돌아온 경우).</summary>
        void ResumeReward(RunNode node, BattleSave.RewardState r)
        {
            var found = FindEnemy(node.enemyId);
            if (found != null) enemyData = found;
            ShowIdleField();

            var cards = r.cardIds.Select(FindCard).Where(c => c != null).ToList();
            if (cards.Count == 0)
            {
                BattleSave.ClearReward();
                PlayerData.AddToin(r.toin);
                AfterNodeCleared();
                return;
            }
            ShowReward(r.toin, r.boss, cards);
        }

        void ShowReward(int toin, bool boss, List<CardData> cards)
        {
            for (int i = rewardRow.childCount - 1; i >= 0; i--)
                Destroy(rewardRow.GetChild(i).gameObject);

            rewardTitle.text = boss
                ? $"보스 격파! 보상\n<size=21>+{toin} 토인  ·  초월·전설 카드 {cards.Count}장 중 1장을 골라요</size>"
                : $"승리! 전투 보상\n<size=21>+{toin} 토인  ·  카드 {cards.Count}장 중 1장을 골라요</size>";
            foreach (var card in cards)
            {
                var local = card;
                var view = CardView.Create(rewardRow, uiFont, RewardCardSize);
                view.name = "Reward_" + card.id;
                view.Bind(card, theme, true, () => PickReward(local, toin));
                AttachHover(view, card, true, RewardCardSize, RewardHoverScale);
            }

            // 카드를 안 받아도 된다 — 덱이 얇은 게 나을 때도 있고, 받은 카드는 지면 같이 사라진다
            rewardSkip.onClick.RemoveAllListeners();
            rewardSkip.onClick.AddListener(() => SkipReward(toin));
            SetButtonText(rewardSkip, $"카드 안 받기  (+{toin} 토인만)");

            rewardPanel.SetActive(true);
            Tutor(Keys.BattleReward, RewardSteps());
        }

        void PickReward(CardData card, int toin)
        {
            BattleSave.ClearReward();   // 지급보다 먼저 지운다 — 도중에 꺼져도 두 번 받지 않게
            PlayerData.AddToin(toin);
            // 덱에 자리가 있으면 덱으로, 가득 찼으면(최대 30장) 감자창고로 (희귀도로 수명 결정)
            bool inDeck = PlayerData.AddCard(card.id, card.rarity);

            rewardPanel.SetActive(false);
            // 바로 맵이 덮으므로 무엇을 받았는지는 맵 상단 문구로 띄운다
            AfterNodeCleared(inDeck
                ? $"획득: {card.displayName} — 런 덱에 들어갔어요  (+{toin} 토인)"
                : $"획득: {card.displayName} — 덱이 가득 차서({PlayerData.MaxDeck}장) 감자창고로 보냈어요  (+{toin} 토인)");
        }

        void SkipReward(int toin)
        {
            BattleSave.ClearReward();
            PlayerData.AddToin(toin);
            rewardPanel.SetActive(false);
            AfterNodeCleared($"카드 보상을 건너뛰었어요  (+{toin} 토인)");
        }

        int RewardToin()
        {
            var tier = enemyData != null ? enemyData.tier : EnemyTier.Normal;
            return tier switch
            {
                EnemyTier.Boss  => Random.Range(15, 26),   // 15~25
                EnemyTier.Elite => Random.Range(12, 16),   // 12~15
                _               => Random.Range(3, 7),     // 3~6
            };
        }

        /// <summary>
        /// 보상 희귀도 가중 (§12.1). [밸런싱 대상]
        ///
        /// <b>초월·전설은 보스에서만 나온다.</b> 일반·엘리트는 일반/희귀만 준다.
        /// 보스 보상은 3장이 아니라 2장 중 1택이라, 등급이 높은 대신 선택지가 좁다.
        /// </summary>
        public static readonly (Rarity rarity, int weight)[] NormalRewardWeights =
        {
            (Rarity.Common, 60),
            (Rarity.Rare,   40),
        };

        public static readonly (Rarity rarity, int weight)[] BossRewardWeights =
        {
            (Rarity.Transcendent, 75),
            (Rarity.Legendary,    25),
        };

        public const int NormalRewardCount = 3;
        public const int BossRewardCount = 2;

        /// <summary>가중치 표에서 희귀도 한 단계를 뽑는다.</summary>
        static Rarity RollRarity((Rarity rarity, int weight)[] table)
        {
            int total = 0;
            foreach (var w in table) total += w.weight;
            int roll = Random.Range(0, total);
            foreach (var w in table)
            {
                if (roll < w.weight) return w.rarity;
                roll -= w.weight;
            }
            return table[0].rarity;
        }

        /// <summary>보상 후보에서 희귀도 가중으로 중복 없이 count장.</summary>
        List<CardData> PickRewardCards(int count, bool boss)
        {
            var table = boss ? BossRewardWeights : NormalRewardWeights;
            var chosen = new List<CardData>();
            if (rewardPool == null) return chosen;

            // 보스는 초월·전설만, 일반·엘리트는 일반·희귀만 후보로 둔다
            var allowed = new HashSet<Rarity>(table.Select(w => w.rarity));
            var pool = rewardPool.Where(c => c != null && allowed.Contains(c.rarity)).ToList();
            // 해당 등급 카드가 아예 없으면(초기 로스터 등) 전체에서 고른다
            if (pool.Count == 0) pool = rewardPool.Where(c => c != null).ToList();

            while (chosen.Count < count && chosen.Count < pool.Count)
            {
                var want = RollRarity(table);
                var tierPool = pool.Where(c => c.rarity == want && !chosen.Contains(c)).ToList();
                // 그 등급에 남은 카드가 없으면 아무거나 (초월·전설은 장수가 적어 자주 비어 있다)
                if (tierPool.Count == 0) tierPool = pool.Where(c => !chosen.Contains(c)).ToList();
                if (tierPool.Count == 0) break;
                chosen.Add(tierPool[Random.Range(0, tierPool.Count)]);
            }
            return chosen;
        }

        /// <summary>패배·런 클리어 — 화면을 어둡게 덮고 가운데에 결과와 버튼을 띄운다.</summary>
        void EndControls()
        {
            if (endTurnBtn != null) endTurnBtn.interactable = false;
            if (rewardPanel != null) rewardPanel.SetActive(false);
            if (forgePanel != null) forgePanel.SetActive(false);
            if (resultLayer != null) resultLayer.SetActive(true);
            HideBanner();
            RebuildHand();
            UpdateTopBar();
        }

        void ExitToLauncher()
        {
            // 보상(토인·카드)은 PlayerData(PlayerPrefs)에 저장돼 있어 런처에서 이어진다.
            // 전환은 런처와 같은 경로를 써서 창 크기(1280×900)까지 되돌린다.
            TatoGames.Launcher.LauncherTransition.ReturnToLauncher();
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ══════════════════════════════════════════════════ UI ════════════════
        void BuildUI()
        {
            // ── 배경 (아트 슬롯) ──
            backgroundImage = UiKit.Img("Background", transform);
            UiKit.Stretch(backgroundImage.rectTransform);
            UpdateBackground();

            // ── 적(오른쪽) · 플레이어(왼쪽) — 손패 바로 위에 선다 ──
            // 화면 아래 기준으로 붙인다. 위 기준이면 화면 비율(16:10 등)에 따라
            // 손패(아래 기준)와 사이가 벌어진다.
            enemyPanel = CombatantPanel.Create(transform, uiFont, new Vector2(240, 240), withIntent: true);
            UiKit.Place(enemyPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0),
                        new Vector2(250, PanelBottomY), enemyPanel.GetComponent<RectTransform>().sizeDelta);

            playerPanel = CombatantPanel.Create(transform, uiFont, new Vector2(200, 200), withIntent: false);
            UiKit.Place(playerPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0),
                        new Vector2(-330, PanelBottomY), playerPanel.GetComponent<RectTransform>().sizeDelta);

            // ── 에너지 오브 (좌하단) ──
            var orb = UiKit.Img("EnergyOrb", transform);
            UiKit.Place(orb.rectTransform, new Vector2(0, 0), new Vector2(70, 120), new Vector2(84, 84));
            UiKit.Apply(orb, theme != null ? theme.energyOrb : null, Accent);
            energyOrb = orb.rectTransform;
            energyText = UiKit.Label("EnergyText", orb.rectTransform, uiFont, 26);
            UiKit.Stretch(energyText.rectTransform);
            energyText.fontStyle = FontStyle.Bold;
            if (orb.sprite != null) UiKit.Outline(energyText, 2f, new Color(0.35f, 0.18f, 0.02f, 0.95f));
            else energyText.color = new Color(0.1f, 0.1f, 0.12f);
            TooltipTrigger.On(orb, "에너지",
                $"카드를 쓰려면 카드 왼쪽 위 숫자만큼 에너지가 필요해요.\n내 턴이 시작될 때마다 {maxEnergy}로 다시 차요 (남은 에너지는 넘어가지 않아요).");

            // ── 덱/버림 카운터 ──
            pileText = UiKit.Label("PileText", transform, uiFont, 17, TextAnchor.LowerLeft);
            UiKit.Place(pileText.rectTransform, new Vector2(0, 0), new Vector2(20, 24), new Vector2(150, 26));
            UiKit.Outline(pileText);
            TooltipTrigger.On(pileText, "덱 · 버림",
                "덱: 앞으로 뽑을 카드 / 버림: 쓴 카드와 턴이 끝날 때 버린 손패.\n덱이 비면 버림 더미를 섞어서 다시 뽑아요. 〈소멸〉 카드는 이번 전투에서 사라져요.");

            // ── 손패 (하단 중앙) ──
            handRow = UiKit.Rect("Hand", transform);
            handRow.anchorMin = handRow.anchorMax = new Vector2(0.5f, 0);
            handRow.pivot = new Vector2(0.5f, 0);
            handRow.anchoredPosition = new Vector2(30, HandY); handRow.sizeDelta = new Vector2(HandWidth, 240);
            var hlg = handRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = HandSpacing; hlg.childAlignment = TextAnchor.LowerCenter;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            // 오른쪽, 손패 윗변 바로 위. 버튼 pivot이 가운데라 x = −(여백 24 + 폭 절반 85).
            // (예전 (−24, 130)은 절반이 화면 밖이었고, 손패가 6장을 넘으면 카드와 겹쳤다)
            endTurnBtn = MakeButton("EndTurn", "턴 종료", new Vector2(1, 0), new Vector2(-109, PanelBottomY + 30f),
                                    new Vector2(170, 60), OnEndTurn);
            UiKit.ApplyButton(endTurnBtn, theme);
            SetButtonFont(endTurnBtn, 22);

            BuildResultLayer();
            BuildRewardPanel();
            BuildMapPanel();
            BuildForgePanel();

            // 상단 바·알림은 맵·대장간 위에도 보여야 하므로 패널들 다음에 만든다
            BuildTopBar();
            BuildBanner();

            // 아이콘 설명 말풍선 — 맨 마지막에 만들어 모든 패널보다 위에 그린다
            UiTooltip.Init(transform, uiFont);
        }

        /// <summary>패배·런 클리어 화면 — 어둡게 덮고 결과 문구 + 새 런 / 나가기.</summary>
        void BuildResultLayer()
        {
            var dim = UiKit.Img("Result", transform, raycast: true);
            UiKit.Stretch(dim.rectTransform);
            dim.color = new Color(0f, 0f, 0f, 0.66f);
            resultLayer = dim.gameObject;

            message = UiKit.Label("Message", dim.transform, uiFont, 34);
            UiKit.Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1000, 130));
            message.color = theme != null ? theme.accentColor : Accent;
            message.supportRichText = true;
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.lineSpacing = 1.2f;
            UiKit.Outline(message, 2f);

            var restart = MakeButton("Restart", "새 런 시작", new Vector2(0.5f, 0.5f), new Vector2(-125, -50), new Vector2(220, 58),
                                     () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex), dim.transform);
            UiKit.ApplyButton(restart, theme);
            TooltipTrigger.On(restart.targetGraphic, "새 런 시작",
                "바로 새 런을 시작해요. 새 런을 시작하면 모든 카드가 한 살 먹어요.\n덱을 바꾸고 싶으면 먼저 런처의 감자창고에서 편성하세요.");
            restartBtn = restart.gameObject;

            var exit = MakeButton("Exit", "런처로 나가기", new Vector2(0.5f, 0.5f), new Vector2(125, -50), new Vector2(220, 58),
                                  ExitToLauncher, dim.transform);
            UiKit.ApplyButton(exit, theme);
            exitBtn = exit.gameObject;

            resultLayer.SetActive(false);
        }

        void BuildRewardPanel()
        {
            var panel = UiKit.Img("RewardPanel", transform, raycast: true);
            UiKit.Stretch(panel.rectTransform);
            panel.color = new Color(0, 0, 0, 0.8f);
            rewardPanel = panel.gameObject;

            rewardTitle = UiKit.Label("RewardTitle", panel.transform, uiFont, 30);
            UiKit.Place(rewardTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(900, 90));
            rewardTitle.color = Accent;
            rewardTitle.supportRichText = true;
            rewardTitle.lineSpacing = 1.15f;

            rewardRow = UiKit.Rect("RewardRow", panel.transform);
            UiKit.Place(rewardRow, new Vector2(0.5f, 0.5f), new Vector2(0, -35), new Vector2(780, 270));
            var rhlg = rewardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rhlg.spacing = 20; rhlg.childAlignment = TextAnchor.MiddleCenter;
            rhlg.childControlWidth = rhlg.childControlHeight = false;
            rhlg.childForceExpandWidth = rhlg.childForceExpandHeight = false;

            rewardSkip = MakeButton("RewardSkip", "카드 안 받기", new Vector2(0.5f, 0.5f), new Vector2(0, -222),
                                    new Vector2(290, 48), () => { }, panel.transform);
            UiKit.ApplyButton(rewardSkip, theme);
            SetButtonFont(rewardSkip, 18);
            TooltipTrigger.On(rewardSkip.targetGraphic, "카드 안 받기",
                "토인만 받고 카드는 받지 않아요.\n덱이 얇을수록 좋은 카드가 자주 나오고, 덱에 넣은 카드는 패배하면 사라져요.");

            rewardPanel.SetActive(false);
        }

        void BuildMapPanel()
        {
            var panel = UiKit.Img("MapPanel", transform, raycast: true);
            UiKit.Stretch(panel.rectTransform);
            panel.color = new Color(0, 0, 0, 0.82f);
            mapPanel = panel.gameObject;

            mapInfo = UiKit.Label("MapInfo", panel.transform, uiFont, 24);
            UiKit.Place(mapInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1100, 100));
            mapInfo.color = Accent;
            mapInfo.supportRichText = true;
            mapInfo.lineSpacing = 1.15f;
            mapInfo.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 노드 그래프 영역 (연결선 + 노드를 직접 그린다)
            mapArea = UiKit.Rect("MapArea", panel.transform);
            UiKit.Place(mapArea, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(MapW, MapH));

            mapPanel.SetActive(false);
        }

        void BuildForgePanel()
        {
            var panel = UiKit.Img("ForgePanel", transform, raycast: true);
            UiKit.Stretch(panel.rectTransform);
            panel.color = new Color(0, 0, 0, 0.88f);
            forgePanel = panel.gameObject;

            forgeInfo = UiKit.Label("ForgeInfo", panel.transform, uiFont, 22);
            UiKit.Place(forgeInfo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -(TopBarH + 8)), new Vector2(1000, 64));
            forgeInfo.color = theme != null ? theme.accentColor : Accent;
            forgeInfo.supportRichText = true;
            forgeInfo.lineSpacing = 1.15f;

            // 카드가 많으면(최대 30장) 한 화면에 안 들어간다 — 스크롤 안에 넣는다.
            // 예전엔 4줄째가 아래 버튼·안내 글씨를 덮었다.
            forgeScroll = UiKit.Rect("ForgeScroll", panel.transform);
            UiKit.Place(forgeScroll, new Vector2(0.5f, 1), new Vector2(0, -(TopBarH + 80)), new Vector2(1010, 402));
            var drag = forgeScroll.gameObject.AddComponent<Image>();
            drag.color = new Color(1, 1, 1, 0.03f);          // 휠·드래그를 받는 바탕
            var sr = forgeScroll.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 32;

            var viewport = UiKit.Rect("Viewport", forgeScroll);
            UiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            forgeGrid = UiKit.Rect("ForgeGrid", viewport);
            forgeGrid.anchorMin = new Vector2(0, 1); forgeGrid.anchorMax = new Vector2(1, 1);
            forgeGrid.pivot = new Vector2(0.5f, 1);
            forgeGrid.anchoredPosition = Vector2.zero; forgeGrid.sizeDelta = Vector2.zero;
            var fgrid = forgeGrid.gameObject.AddComponent<GridLayoutGroup>();
            fgrid.cellSize = ForgeCardSize;
            fgrid.spacing = new Vector2(8, 10);
            fgrid.padding = new RectOffset(8, 8, 10, 10);
            fgrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            fgrid.constraintCount = 9;
            fgrid.childAlignment = TextAnchor.UpperCenter;
            var ffit = forgeGrid.gameObject.AddComponent<ContentSizeFitter>();
            ffit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = viewport; sr.content = forgeGrid;

            forgeHint = UiKit.Label("ForgeHint", panel.transform, uiFont, 19);
            UiKit.Place(forgeHint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1000, 30));
            forgeHint.supportRichText = true;

            forgeActionRow = UiKit.Rect("ForgeActions", panel.transform);
            UiKit.Place(forgeActionRow, new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(1000, 52));
            var fhlg = forgeActionRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            fhlg.spacing = 14; fhlg.childAlignment = TextAnchor.MiddleCenter;
            fhlg.childControlWidth = fhlg.childControlHeight = false;
            fhlg.childForceExpandWidth = fhlg.childForceExpandHeight = false;

            var leave = MakeButton("ForgeLeave", "대장간 나가기", new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(230, 50),
                                   LeaveForge, panel.transform);
            UiKit.ApplyButton(leave, theme);

            forgePanel.SetActive(false);
        }

        /// <summary>
        /// 상단 바 — 스테이지·진행 / 토인 · 런 덱 / 도움말 · 나가기.
        /// 전투 중에 지금 몇 번째 노드인지, 토인이 얼마인지 볼 곳이 없었다.
        /// </summary>
        void BuildTopBar()
        {
            var bar = UiKit.Img("TopBar", transform);
            bar.color = new Color(0.04f, 0.04f, 0.06f, 0.8f);
            topBar = bar.rectTransform;
            topBar.anchorMin = new Vector2(0, 1); topBar.anchorMax = new Vector2(1, 1);
            topBar.pivot = new Vector2(0.5f, 1);
            topBar.anchoredPosition = Vector2.zero; topBar.sizeDelta = new Vector2(0, TopBarH);

            // 왼쪽: 스테이지
            var left = BarGroup("Left", new Vector2(0, 0.5f), new Vector2(12, 0), TextAnchor.MiddleLeft);
            var stageIcon = BarIcon(left, theme != null ? theme.stageIcon : null);
            topStage = BarText(left, 19, new Color(0.92f, 0.92f, 0.95f));
            string stageTip = $"런은 스테이지 {RunState.StageCount}개로 이뤄져 있어요. 스테이지마다 노드 10개,\n마지막 노드는 보스예요. 보스를 이기면 다음 스테이지로 가요.";
            TooltipTrigger.On(topStage, "스테이지 · 진행", stageTip);
            if (stageIcon != null) TooltipTrigger.On(stageIcon, "스테이지 · 진행", stageTip);

            // 오른쪽: 토인 · 덱 · ? · 나가기
            var right = BarGroup("Right", new Vector2(1, 0.5f), new Vector2(-10, 0), TextAnchor.MiddleRight);
            var toinIcon = BarIcon(right, theme != null ? theme.toinIcon : null);
            topToin = BarText(right, 19, Accent);
            const string toinTip = "게임의 돈이에요. 전투에서 벌고, 대장간 강화·제거와 런처 상점에서 써요.";
            TooltipTrigger.On(topToin, "토인", toinTip);
            if (toinIcon != null) TooltipTrigger.On(toinIcon, "토인", toinTip);

            BarSpacer(right, 10);
            var deckIcon = BarIcon(right, theme != null ? theme.deckIcon : null);
            topDeck = BarText(right, 19, new Color(0.9f, 0.9f, 0.93f));
            const string deckTip = "이번 런에 가져온 카드 수예요.\n전투에서 지면 덱에 든 카드가 사라져요 (시작 카드는 남아요).";
            TooltipTrigger.On(topDeck, "런 덱", deckTip);
            if (deckIcon != null) TooltipTrigger.On(deckIcon, "런 덱", deckTip);

            BarSpacer(right, 14);
            var help = BarButton(right, "?", 44, ShowHelp);
            TooltipTrigger.On(help.targetGraphic, "도움말", "지금 화면의 안내를 다시 보여줘요.");
            var leave = BarButton(right, "나가기", 104, ExitToLauncher);
            TooltipTrigger.On(leave.targetGraphic, "런처로 나가기",
                "언제 나가도 진행은 저장돼요. 런처에서 GAME START를 누르면 이 자리에서 이어져요.");
        }

        RectTransform BarGroup(string name, Vector2 anchor, Vector2 pos, TextAnchor align)
        {
            var g = UiKit.Rect(name, topBar);
            g.anchorMin = g.anchorMax = anchor;
            g.pivot = anchor;
            g.anchoredPosition = pos;
            g.sizeDelta = new Vector2(620, TopBarH - 6);
            var hlg = g.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6; hlg.childAlignment = align;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            return g;
        }

        Image BarIcon(Transform parent, Sprite sp)
        {
            if (sp == null) return null;
            var img = UiKit.Img("Icon", parent);
            img.sprite = sp; img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(28, 28);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 28;
            return img;
        }

        Text BarText(Transform parent, int size, Color color)
        {
            var t = UiKit.Label("Text", parent, uiFont, size, TextAnchor.MiddleLeft);
            t.rectTransform.sizeDelta = new Vector2(0, TopBarH - 6);
            t.color = color;
            UiKit.Outline(t, 1f);
            return t;
        }

        static void BarSpacer(Transform parent, float width)
        {
            var s = UiKit.Rect("Space", parent);
            s.sizeDelta = new Vector2(width, 10);
            var le = s.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
        }

        Button BarButton(Transform parent, string caption, float width, UnityEngine.Events.UnityAction onClick)
        {
            var b = MakeButton("Btn_" + caption, caption, new Vector2(0.5f, 0.5f), Vector2.zero,
                               new Vector2(width, 34), onClick, parent);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            UiKit.ApplyButton(b, theme);
            SetButtonFont(b, 17);
            return b;
        }

        void UpdateTopBar()
        {
            if (topStage == null) return;
            if (RunState.Active)
            {
                int cols = RunState.Columns.Count;
                string node = stageIntro ? "첫 노드를 고르세요"
                            : cols > 0 ? $"노드 {Mathf.Min(RunState.Col + 1, cols)} / {cols}" : "";
                topStage.text = $"{RunState.StageName}    {node}";
            }
            else topStage.text = "런 종료";
            topToin.text = PlayerData.Toin.ToString();
            topDeck.text = $"{PlayerData.DeckInstances().Count}장";
        }

        /// <summary>상단 바 아래 왼쪽의 알림 한 줄 — 적의 행동, 변신, 런 시작 알림 등.</summary>
        void BuildBanner()
        {
            bannerBg = UiKit.Img("Banner", transform);
            var rt = bannerBg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -(TopBarH + 8));
            bannerBg.color = new Color(0.05f, 0.05f, 0.07f, 0.85f);
            var ol = bannerBg.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.45f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            bannerText = UiKit.Label("Text", rt, uiFont, 19, TextAnchor.MiddleLeft);
            UiKit.Stretch(bannerText.rectTransform, 14, 6, 14, 6);
            bannerText.supportRichText = true;
            bannerBg.gameObject.SetActive(false);
        }

        /// <summary>알림을 띄운다. sticky면 직접 내릴 때까지 남는다.</summary>
        void Banner(string msg, float seconds = 2.6f, bool sticky = false)
        {
            if (bannerBg == null) return;
            if (string.IsNullOrEmpty(msg)) { HideBanner(); return; }

            var rt = bannerBg.rectTransform;
            bannerText.text = msg;
            bannerText.color = Color.white;
            bannerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = Mathf.Min(bannerText.preferredWidth, BannerMaxW);
            bannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            rt.sizeDelta = new Vector2(w + 28f, 40f);
            float h = bannerText.preferredHeight;   // 폭을 정한 뒤에 줄바꿈된 높이를 잰다
            rt.sizeDelta = new Vector2(w + 28f, Mathf.Max(36f, h + 12f));

            bannerBg.color = new Color(0.05f, 0.05f, 0.07f, 0.85f);
            bannerBg.gameObject.SetActive(true);
            bannerSticky = sticky;
            bannerHideAt = Time.unscaledTime + seconds;
        }

        void HideBanner()
        {
            bannerSticky = false;
            if (bannerBg != null) bannerBg.gameObject.SetActive(false);
        }

        // ══════════════════════════════════════════════════ 맵 (§11.3) ═════════
        const float MapW = 1160f, MapH = 300f;

        /// <summary>노드 그래프를 그린다 — 열마다 노드가 갈라지고, 연결선으로 이어진다.</summary>
        void ShowMap(string note)
        {
            if (mapPanel == null) return;
            if (rewardPanel != null) rewardPanel.SetActive(false);

            for (int i = mapArea.childCount - 1; i >= 0; i--)
                Destroy(mapArea.GetChild(i).gameObject);

            var cols = RunState.Columns;
            if (cols.Count == 0) return;

            // 새 스테이지 첫 진입이면 0열 노드가 목적지, 아니면 현재 노드의 연결선 끝이 목적지
            int targetCol = stageIntro ? 0 : RunState.Col + 1;
            var reachable = stageIntro ? new List<int> { 0 } : RunState.Reachable;

            // ── 연결선 먼저(노드 뒤에 깔리도록) ──
            for (int c = 0; c < cols.Count - 1; c++)
                for (int r = 0; r < cols[c].Count; r++)
                    foreach (int nr in cols[c][r].next)
                    {
                        bool live = !stageIntro && c == RunState.Col && r == RunState.Row
                                    && reachable.Contains(nr);
                        DrawEdge(NodePos(cols, c, r), NodePos(cols, c + 1, nr),
                                 live ? new Color(1f, 0.78f, 0.2f, 0.95f) : new Color(1f, 1f, 1f, 0.16f),
                                 live ? 5f : 3f);
                    }

            // ── 노드 ──
            bool lastStage = !RunState.HasNextStage;
            for (int c = 0; c < cols.Count; c++)
                for (int r = 0; r < cols[c].Count; r++)
                {
                    var n = cols[c][r];
                    bool isCurrent = !stageIntro && c == RunState.Col && r == RunState.Row;
                    bool isPast = c < RunState.Col && !stageIntro;
                    bool isPickable = c == targetCol && reachable.Contains(r);

                    var cell = UiKit.Img($"N{c}_{r}", mapArea, raycast: true);
                    UiKit.Place(cell.rectTransform, new Vector2(0.5f, 0.5f),
                                NodePos(cols, c, r), new Vector2(96, 66));
                    cell.color =
                        isCurrent ? new Color(0.85f, 0.62f, 0.15f, 0.95f)
                        : isPickable ? (n.type == NodeType.Boss ? new Color(0.55f, 0.2f, 0.2f, 0.98f)
                                                                : new Color(0.24f, 0.42f, 0.55f, 0.98f))
                        : isPast ? new Color(0.2f, 0.42f, 0.26f, 0.9f)
                                 : new Color(0.20f, 0.20f, 0.24f, 0.85f);

                    string enemyName = FindEnemy(n.enemyId)?.enemyName ?? "";
                    string sub = n.type switch
                    {
                        NodeType.Rest => $"+{RunState.RestHeal}",
                        NodeType.Forge => "강화·제거",
                        _ => enemyName,
                    };
                    var t = MakeStretchText(cell.transform, 15);
                    t.text = $"{RunState.Label(n)}\n{sub}";
                    bool dim = !isPickable && !isCurrent && !isPast;
                    if (dim) t.color = new Color(1, 1, 1, 0.5f);

                    // 노드 아이콘 (BattleTheme에 있으면) — 왼쪽에 두고 글자는 오른쪽으로 민다
                    var icon = theme != null ? theme.NodeIconFor(n.type) : null;
                    if (icon != null)
                    {
                        var ic = UiKit.Img("Icon", cell.transform);
                        UiKit.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(38, 38));
                        ic.sprite = icon; ic.preserveAspect = true;
                        ic.color = dim ? new Color(1, 1, 1, 0.5f) : Color.white;
                        t.rectTransform.offsetMin = new Vector2(40, 0);
                    }

                    // 노드에 마우스를 올리면 무엇을 하는 곳인지 — 처음 보면 '대장간'이 뭔지 모른다
                    var (tipTitle, tipBody) = NodeTip(n, enemyName, lastStage);
                    if (isCurrent) tipBody += "\n<color=#FFC845>지금 있는 곳</color>";
                    else if (isPickable) tipBody += "\n<color=#9FD6FF>클릭해서 이동</color>";
                    TooltipTrigger.On(cell, tipTitle, tipBody);

                    if (!isPickable) continue;
                    int pick = r;
                    var btn = cell.gameObject.AddComponent<Button>();
                    btn.targetGraphic = cell;
                    btn.onClick.AddListener(() => OnChooseNode(pick));
                }

            string head = stageIntro
                ? $"{RunState.StageName}   ·   체력 {RunState.PlayerHp}/{playerMaxHp}"
                : $"{RunState.StageName}   ·   노드 {Mathf.Min(RunState.Col + 1, cols.Count)} / {cols.Count}   ·   체력 {RunState.PlayerHp}/{playerMaxHp}";
            string pickMsg = reachable.Count > 1 ? "\n<color=#9FD6FF>파란 노드 중 갈 길을 고르세요</color>"
                                                 : "\n<color=#9FD6FF>파란 노드를 눌러 진행하세요</color>";
            mapInfo.text = (string.IsNullOrEmpty(note) ? head : $"{note}\n{head}") + pickMsg;

            mapPanel.SetActive(true);
            UpdateTopBar();
            Tutor(Keys.BattleMap, MapSteps());
        }

        static (string title, string body) NodeTip(RunNode n, string enemyName, bool lastStage) => n.type switch
        {
            NodeType.Rest => ("휴식", $"체력을 {RunState.RestHeal} 회복해요.\n체력은 전투가 끝나도 이어지니, 휴식이 유일한 회복 수단이에요."),
            NodeType.Forge => ("대장간", "토인으로 덱의 카드를 강화하거나 제거해요.\n강화는 카드당 1번이에요."),
            NodeType.Boss => ($"보스 · {enemyName}",
                              lastStage ? "마지막 보스! 이기면 런 클리어예요.\n보상: 초월·전설 카드 2장 중 1장"
                                        : "이기면 다음 스테이지로 가요.\n보상: 초월·전설 카드 2장 중 1장"),
            _ => ($"전투 · {enemyName}", "이기면 토인과 카드 보상(3장 중 1장)을 받아요."),
        };

        static Vector2 NodePos(List<List<RunNode>> cols, int c, int r)
        {
            float stepX = MapW / cols.Count;
            float x = -MapW / 2f + stepX / 2f + c * stepX;
            float y = cols[c].Count == 1 ? 0f : (r == 0 ? 78f : -78f);
            return new Vector2(x, y);
        }

        /// <summary>두 노드를 잇는 선 — 회전시킨 얇은 사각형.</summary>
        void DrawEdge(Vector2 a, Vector2 b, Color color, float thickness)
        {
            var line = UiKit.Img("Edge", mapArea);
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            Vector2 d = b - a;
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(d.magnitude, thickness);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.color = color;
        }

        void OnChooseNode(int row)
        {
            mapPanel.SetActive(false);
            if (stageIntro) { stageIntro = false; RunState.StageIntro = false; }   // 새 스테이지 0열로 그대로 진입
            else RunState.MoveTo(row);
            EnterNode();
        }

        // ══════════════════════════════════════════════ 대장간 (§11.2) ═════════
        void ShowForge()
        {
            if (forgePanel == null) { ShowMap(null); return; }
            if (mapPanel != null) mapPanel.SetActive(false);
            forgeSelected = -1;
            forgeConfirmRemove = -1;
            RebuildForge();
            forgePanel.SetActive(true);
            Tutor(Keys.BattleForge, ForgeSteps());
        }

        void RebuildForge()
        {
            forgeInfo.text =
                $"대장간   ·   보유 토인 <color=#FFFFFF>{PlayerData.Toin}</color>\n" +
                $"<size=18><color=#D8D8DD>강화 {PlayerData.UpgradeCost} 토인 (카드당 1번 · 수치 강화 또는 코스트 −1)   ·   " +
                $"제거 {PlayerData.RemoveCost} 토인</color></size>";

            for (int i = forgeGrid.childCount - 1; i >= 0; i--)
                Destroy(forgeGrid.GetChild(i).gameObject);
            forgeViews.Clear();

            foreach (var inst in PlayerData.DeckInstances())
            {
                var data = FindCard(inst.cardId);
                if (data == null) continue;
                var shown = CardUpgrade.Build(data, inst.upgrade, inst.State);
                int id = inst.instanceId;
                var view = CardView.Create(forgeGrid, uiFont, ForgeCardSize);
                view.name = $"Forge_{id}";
                view.Bind(shown, theme, true, () => SelectForge(id));
                AttachHover(view, shown, true, ForgeCardSize, ForgeHoverScale);
                forgeViews[id] = view;
            }

            RebuildForgeActions();
            UpdateTopBar();
        }

        void SelectForge(int id)
        {
            forgeSelected = id;
            forgeConfirmRemove = -1;
            RebuildForgeActions();
        }

        /// <summary>고른 카드에 금색 테두리 — 예전엔 아래 글자만 바뀌어서 무엇을 골랐는지 잘 안 보였다.</summary>
        void HighlightForge()
        {
            foreach (var kv in forgeViews)
            {
                var v = kv.Value;
                if (v == null) continue;
                var mark = v.transform.Find("Selected");
                bool sel = kv.Key == forgeSelected;
                if (sel && mark == null)
                {
                    var img = UiKit.Img("Selected", v.transform);
                    UiKit.Stretch(img.rectTransform, -5, -5, -5, -5);
                    img.color = Accent;
                    img.transform.SetAsFirstSibling();       // 카드 그림 뒤 — 가장자리만 금색으로 보인다
                }
                else if (!sel && mark != null) Destroy(mark.gameObject);
            }
        }

        void RebuildForgeActions()
        {
            for (int i = forgeActionRow.childCount - 1; i >= 0; i--)
                Destroy(forgeActionRow.GetChild(i).gameObject);
            HideForgePopup();

            var inst = PlayerData.Instances().FirstOrDefault(c => c.instanceId == forgeSelected);
            if (inst == null)
            {
                forgeSelected = -1;
                forgeHint.text = "강화하거나 제거할 카드를 고르세요";
                forgeHint.color = Color.white;
                HighlightForge();
                return;
            }
            HighlightForge();

            var data = FindCard(inst.cardId);
            string cardName = (data != null ? data.displayName : inst.cardId) + CardUpgrade.Suffix(inst.upgrade);
            int toin = PlayerData.Toin;
            bool canUpgrade = !inst.IsUpgraded && toin >= PlayerData.UpgradeCost;
            bool canRemove = toin >= PlayerData.RemoveCost;

            forgeHint.color = Color.white;
            forgeHint.text = inst.IsUpgraded
                ? $"선택: <b>{cardName}</b> — 이미 강화한 카드예요 (카드당 1번)"
                : $"선택: <b>{cardName}</b> — 버튼에 마우스를 올리면 강화 결과를 미리 볼 수 있어요";
            if (!canUpgrade && !inst.IsUpgraded && !canRemove)
            {
                forgeHint.text = $"선택: <b>{cardName}</b> — 토인이 부족해요 (강화 {PlayerData.UpgradeCost} · 제거 {PlayerData.RemoveCost})";
                forgeHint.color = new Color(1f, 0.6f, 0.5f);
            }

            int id = inst.instanceId;
            if (!inst.IsUpgraded && data != null)
            {
                var plus = AddForgeButton($"수치 강화  ({PlayerData.UpgradeCost})", () => DoUpgrade(id, UpgradeKind.Plus), canUpgrade);
                ForgePreviewOnHover(plus, CardUpgrade.Build(data, UpgradeKind.Plus, inst.State));
                var minus = AddForgeButton($"코스트 −1  ({PlayerData.UpgradeCost})", () => DoUpgrade(id, UpgradeKind.Minus), canUpgrade);
                ForgePreviewOnHover(minus, CardUpgrade.Build(data, UpgradeKind.Minus, inst.State));
            }

            bool confirming = forgeConfirmRemove == id;
            var remove = AddForgeButton(confirming ? "정말 제거할까요?" : $"제거  ({PlayerData.RemoveCost})", () =>
            {
                if (forgeConfirmRemove != id) { forgeConfirmRemove = id; RebuildForgeActions(); return; }
                DoRemove(id);
            }, canRemove);
            TooltipTrigger.On(remove.targetGraphic, "카드 제거",
                "이 카드를 영원히 없애요 (감자창고에서도 사라져요).\n약한 카드를 빼면 좋은 카드가 더 자주 나와요. 덱은 최소 8장이 필요해요.");

            AddForgeButton("취소", () => { forgeSelected = -1; forgeConfirmRemove = -1; RebuildForgeActions(); }, true);
        }

        Button AddForgeButton(string caption, UnityEngine.Events.UnityAction act, bool interactable)
        {
            var b = MakeButton("FA", caption, Vector2.zero, Vector2.zero, new Vector2(200, 48), act, forgeActionRow);
            UiKit.ApplyButton(b, theme);
            SetButtonFont(b, 18);
            b.interactable = interactable;
            return b;
        }

        /// <summary>강화 버튼에 마우스를 올리면 강화된 카드를 버튼 위에 크게 보여준다.</summary>
        void ForgePreviewOnHover(Button b, CardData result)
        {
            var relay = b.gameObject.AddComponent<CardHoverRelay>();
            var rt = (RectTransform)b.transform;
            relay.onEnter = () => ShowForgePopup(result, rt);
            relay.onExit = HideForgePopup;
        }

        void ShowForgePopup(CardData card, RectTransform near)
        {
            if (card == null || near == null) return;
            if (forgePopup == null)
            {
                forgePopup = CardView.Create(forgePanel.transform, uiFont, RewardCardSize);
                forgePopup.name = "ForgePreview";
                forgePopup.hit.raycastTarget = false;
            }
            forgePopup.Bind(card, theme, true, null);
            var rt = (RectTransform)forgePopup.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            var c = new Vector3[4];
            near.GetWorldCorners(c);
            rt.position = (c[1] + c[2]) * 0.5f + Vector3.up * (10f * rt.lossyScale.y);
            rt.SetAsLastSibling();
            forgePopup.gameObject.SetActive(true);
        }

        void HideForgePopup()
        {
            if (forgePopup != null) forgePopup.gameObject.SetActive(false);
        }

        void DoUpgrade(int id, UpgradeKind kind)
        {
            if (PlayerData.TryUpgrade(id, kind, out string reason))
            {
                forgeSelected = -1;
                RebuildForge();
                forgeHint.text = "강화했어요!";
                forgeHint.color = new Color(0.6f, 1f, 0.6f);
            }
            else { forgeHint.text = reason; forgeHint.color = new Color(1f, 0.6f, 0.5f); }
        }

        void DoRemove(int id)
        {
            forgeConfirmRemove = -1;
            if (PlayerData.TryRemoveCard(id, out string reason))
            {
                forgeSelected = -1;
                RebuildForge();
                forgeHint.text = "카드를 제거했어요";
                forgeHint.color = new Color(0.6f, 1f, 0.6f);
            }
            else { RebuildForgeActions(); forgeHint.text = reason; forgeHint.color = new Color(1f, 0.6f, 0.5f); }
        }

        void LeaveForge()
        {
            HideForgePopup();
            RunState.NodeCleared = true;   // 나간 뒤 다시 들어오면 맵부터
            forgePanel.SetActive(false);
            ShowMap(null);
        }

        /// <summary>
        /// id로 카드 원본 찾기. CardLibrary(Resources, 전 카드)가 기준이고, 없으면 씬에 배선된 목록을 본다.
        /// (보상 풀은 전투 출처만 들고 있어서 그것만으로는 미니게임 카드를 못 찾는다)
        /// </summary>
        CardData FindCard(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var fromLib = CardLibrary.Load()?.Find(id);
            if (fromLib != null) return fromLib;
            if (starterDeck != null) foreach (var c in starterDeck) if (c != null && c.id == id) return c;
            if (rewardPool != null) foreach (var c in rewardPool) if (c != null && c.id == id) return c;
            return null;
        }

        Text MakeStretchText(Transform parent, int size)
        {
            var t = UiKit.Label("T", parent, uiFont, size);
            UiKit.Stretch(t.rectTransform);
            return t;
        }

        void Refresh()
        {
            if (enemy != null)
            {
                enemyPanel.Bind(enemy, theme, EnemyArt());
                var intent = CurrentIntent();
                enemyPanel.SetIntent(over ? null : intent, theme, intent != null ? IntentAmount(intent) : 0);
            }
            playerPanel.Bind(player, theme, theme != null ? theme.playerPortrait : null);

            energyText.text = $"{energy}/{maxEnergy}";
            pileText.text = $"덱 {drawPile.Count}   버림 {discard.Count}";
            RebuildHand();
            UpdateTopBar();
            EmitDeltas();
            if (!over) SaveBattle();   // 입력을 기다리는 순간마다 저장 — 언제 나가도 그 자리에서 이어진다
            if (!over) CheckHints();
        }

        /// <summary>적 그림 — 2페이즈로 변신했고 변신 그림이 있으면 그걸 쓴다.</summary>
        Sprite EnemyArt()
        {
            if (enemyData == null) return null;
            return transformed && enemyData.phase2Artwork != null ? enemyData.phase2Artwork : enemyData.artwork;
        }

        /// <summary>
        /// 인텐트에 띄울 수치 — 실제로 들어올 값. 공격은 장별 배율 + 적의 힘·약화 + 플레이어의 취약까지 반영
        /// (예전엔 배율만 반영해서 `약 올리기`를 써도 예고 수치가 그대로였다).
        /// </summary>
        int IntentAmount(EnemyAction a) => a.kind switch
        {
            EnemyActionKind.Attack => player.ModifyIncoming(enemy.ModifyOutgoing(ScaleAtk(a.amount))),
            EnemyActionKind.Block => ScaleAtk(a.amount),
            _ => a.amount,
        };

        /// <summary>적이 다음 턴에 할 행동(인텐트).</summary>
        EnemyAction CurrentIntent()
        {
            var pattern = ActivePattern();
            if (enemyData == null || pattern == null || pattern.Count == 0) return null;
            return pattern[Mathf.Clamp(enemyIndex, 0, pattern.Count - 1)];
        }

        void RebuildHand()
        {
            for (int i = handRow.childCount - 1; i >= 0; i--)
                Destroy(handRow.GetChild(i).gameObject);

            FitHandSpacing();
            var ctx = HandContext();
            foreach (var bc in hand)
            {
                var local = bc;
                var card = bc.data;
                bool playable = !over && energy >= card.cost;
                var view = CardView.Create(handRow, uiFont, HandCardSize);
                ((RectTransform)view.transform).pivot = new Vector2(0.5f, 0f);   // 커질 때 화면 밖이 아니라 위로 자라게
                view.name = "Card_" + card.id;
                view.Bind(card, theme, playable, () => PlayCard(local), ctx);
                AttachHover(view, card, playable, HandCardSize, HandHoverScale, ctx);
            }
        }

        /// <summary>
        /// 손패가 많으면 카드끼리 겹쳐 폭 안에 넣는다 (최대 8장).
        /// 예전엔 7장부터 화면 밖으로 삐져나갔다. 마우스를 올리면 확대본이 맨 위에 떠서 가려진 카드도 읽힌다.
        /// </summary>
        void FitHandSpacing()
        {
            var hlg = handRow.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) return;
            int n = hand.Count;
            float w = HandCardSize.x;
            float spacing = HandSpacing;
            if (n > 1 && n * w + (n - 1) * HandSpacing > HandWidth)
                spacing = (HandWidth - n * w) / (n - 1);
            hlg.spacing = spacing;
        }

        /// <summary>
        /// 손패 카드에 "지금 쓰면 실제로 들어갈 수치"를 넣는 문맥 — ResolveEffect와 같은 계산을 쓴다.
        /// 피해: 내 힘·약화 → 적 취약. 블록: 내 민첩, 이번 턴 블록 불가면 0.
        /// </summary>
        CardText.Context HandContext()
        {
            if (over || player == null) return null;
            return new CardText.Context
            {
                damage = b => enemy != null ? enemy.ModifyIncoming(player.ModifyOutgoing(b))
                                            : player.ModifyOutgoing(b),
                block = b => player.blockDisabled ? 0 : Mathf.Max(0, b + player.Get(StatusType.Dexterity)),
                currentBlock = player.TotalBlock,
            };
        }

        // ── 피격 숫자 ──
        // 전투는 한 번에 계산된다. 달라진 체력·블록·효과 방어를 지난 화면과 비교해 숫자로 띄운다.
        struct Snap { public int hp, block, ward; }
        Combatant snapPlayerRef, snapEnemyRef;
        Snap snapPlayer, snapEnemy;

        void EmitDeltas()
        {
            Emit(player, ref snapPlayerRef, ref snapPlayer, playerPanel);
            Emit(enemy, ref snapEnemyRef, ref snapEnemy, enemyPanel);
        }

        static void Emit(Combatant c, ref Combatant seen, ref Snap s, CombatantPanel panel)
        {
            if (c == null || panel == null) { seen = c; return; }
            var now = new Snap { hp = c.hp, block = c.TotalBlock, ward = c.ward };
            if (seen == c && panel.isActiveAndEnabled)       // 같은 전투의 같은 대상일 때만 (새 전투·변신 직후는 제외)
            {
                int dh = now.hp - s.hp, db = now.block - s.block, dw = now.ward - s.ward;
                if (dh < 0) { panel.Float($"-{-dh}", DamageColor, 0); panel.Flash(); }
                else if (dh > 0) panel.Float($"+{dh}", HealColor, 0);
                if (db != 0) panel.Float(db > 0 ? $"블록 +{db}" : $"블록 {db}", BlockColor, 1);
                if (dw != 0) panel.Float(dw > 0 ? $"효과 방어 +{dw}" : $"효과 방어 {dw}", WardColor, 2);
            }
            seen = c;
            s = now;
        }

        // ── 카드 마우스오버 확대 ──
        // 카드 자체를 키우면 옆 카드(나중 형제)가 그 위를 덮어 그린다. 그래서 같은 모양의
        // 확대본을 맨 위에 겹쳐 그린다. 확대본은 클릭을 받지 않으므로 아래 원래 카드가 그대로 눌린다.
        // 확대본 옆에는 카드에 나오는 용어(소멸·취약…) 설명을 띄운다.
        readonly Dictionary<Vector2, CardView> previews = new();   // 카드 크기별로 하나씩 재사용
        CardView previewShown;
        CardView previewOwner;

        void AttachHover(CardView view, CardData card, bool playable, Vector2 size, float scale,
                         CardText.Context battle = null)
        {
            var relay = view.gameObject.AddComponent<CardHoverRelay>();
            relay.onEnter = () => ShowPreview(view, card, playable, size, scale, battle);
            relay.onExit = () => HidePreview(view);
        }

        void ShowPreview(CardView source, CardData card, bool playable, Vector2 size, float scale,
                         CardText.Context battle = null)
        {
            if (source == null || card == null) return;
            if (!previews.TryGetValue(size, out var pv) || pv == null)
            {
                pv = CardView.Create(transform, uiFont, size);
                pv.name = "HoverPreview";
                pv.hit.raycastTarget = false;     // 마우스는 계속 원래 카드 위에 있는 것으로 친다
                previews[size] = pv;
            }
            if (previewShown != null && previewShown != pv) previewShown.gameObject.SetActive(false);

            pv.Bind(card, theme, playable, null, battle);   // 확대본도 손패와 같은 실제 수치
            var src = (RectTransform)source.transform;
            var rt = (RectTransform)pv.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = src.pivot;
            rt.sizeDelta = size;
            rt.position = src.position;              // 원래 카드와 정확히 같은 자리에서
            rt.localScale = Vector3.one * scale;     // pivot 기준으로 커진다
            rt.SetAsLastSibling();                   // 보상·대장간 패널보다도 위
            pv.gameObject.SetActive(true);

            previewShown = pv;
            previewOwner = source;

            // 확대본 옆에 용어 설명 (등급·종류 + 소멸·취약 같은 말)
            string gloss = CardText.Glossary(card);
            UiTooltip.ShowBeside(source, rt, CardText.CardKind(card), gloss);
        }

        void HidePreview(CardView source)
        {
            if (previewOwner != source) return;      // 이미 다른 카드로 옮겨갔으면 건드리지 않는다
            if (previewShown != null) previewShown.gameObject.SetActive(false);
            previewShown = null;
            previewOwner = null;
            UiTooltip.Hide(source);
        }

        // ══════════════════════════════════════════════ 튜토리얼 ═══════════════
        /// <summary>
        /// 처음 보는 화면에 안내를 띄운다. 한 프레임 뒤에 띄운다 — 방금 만든 카드·노드가 자리를 잡은 뒤여야
        /// 설명할 곳을 정확히 뚫을 수 있다.
        /// </summary>
        void Tutor(string key, List<Step> steps, bool force = false, System.Action onClosed = null)
        {
            if (!force && TutorialOverlay.Seen(key)) return;
            if (!isActiveAndEnabled) return;
            StartCoroutine(TutorNextFrame(key, steps, force, onClosed));
        }

        IEnumerator TutorNextFrame(string key, List<Step> steps, bool force, System.Action onClosed)
        {
            yield return null;
            TutorialOverlay.Run(transform, uiFont, key, steps, force, onClosed);
        }

        /// <summary>상단 바 `?` — 지금 떠 있는 화면의 안내를 다시 보여준다.</summary>
        void ShowHelp()
        {
            TutorialOverlay.CloseAll();
            if (forgePanel != null && forgePanel.activeSelf) Tutor(Keys.BattleForge, ForgeSteps(), true);
            else if (rewardPanel != null && rewardPanel.activeSelf) Tutor(Keys.BattleReward, RewardSteps(), true);
            else if (mapPanel != null && mapPanel.activeSelf) Tutor(Keys.BattleMap, MapSteps(), true);
            else Tutor(Keys.BattleBasics, BasicsSteps(), true);
        }

        /// <summary>전투 중 처음 겪는 상황에 한 줄 안내 — 첫 전투 안내를 본 뒤부터.</summary>
        void CheckHints()
        {
            if (over || enemy == null || player == null) return;
            if (!TutorialOverlay.Seen(Keys.BattleBasics)) return;

            if (enemy.TotalBlock > 0 && !TutorialOverlay.Seen(Keys.HintEnemyBlock))
                Tutor(Keys.HintEnemyBlock, new List<Step>
                {
                    new("적의 블록", "적에게 <b>블록</b>이 있어요. 내 공격은 블록부터 깎고, 남은 만큼만 체력이 줄어요.\n" +
                                    "<b>중독</b>은 블록을 무시하고 바로 체력을 깎아요.",
                        () => enemyPanel.BlockRect, Side.Left),
                });

            if (enemy.ward > 0 && !TutorialOverlay.Seen(Keys.HintEnemyWard))
                Tutor(Keys.HintEnemyWard, new List<Step>
                {
                    new("적의 효과 방어", "적에게 <b>효과 방어</b>가 있어요. 내가 거는 취약·약화·중독을 1건씩 막아요.\n" +
                                        "약한 디버프로 먼저 벗겨내면 중요한 디버프가 들어가요.",
                        () => enemyPanel.WardRect, Side.Left),
                });

            if ((player.status.Count > 0 || enemy.status.Count > 0) && !TutorialOverlay.Seen(Keys.HintStatus))
            {
                var row = player.status.Count > 0 ? playerPanel.statusRow : enemyPanel.statusRow;
                Tutor(Keys.HintStatus, new List<Step>
                {
                    new("상태이상", "상태이상이 생겼어요. 아이콘 오른쪽 숫자는 남은 턴(또는 세기)이에요.\n" +
                                  "<b>아이콘에 마우스를 올리면</b> 무슨 효과인지 알려줘요.",
                        () => row),
                });
            }
        }

        List<Step> BasicsSteps() => new()
        {
            new("첫 전투!", "카드로 적의 체력을 0으로 만들면 이겨요.\n<b>내 턴</b>에 카드를 쓰고 턴을 끝내면 <b>적의 턴</b>이 와요.\n" +
                            "이 화면에서 알아둘 것만 짧게 볼게요."),
            new("적의 다음 행동", "적이 <b>다음 턴에 할 행동</b>이에요. 칼은 공격, 방패는 방어예요.\n" +
                                 "숫자는 실제로 들어올 피해예요. 공격이 오면 블록으로 막을 준비를!\n아이콘에 마우스를 올리면 설명이 나와요.",
                () => enemyPanel.intentRow, Side.Left),
            new("에너지", $"카드를 쓰는 데 필요해요. 내 턴마다 <b>{maxEnergy}</b>로 다시 차요.\n카드 왼쪽 위 숫자가 그 카드의 비용이에요.",
                () => energyOrb, Side.Right),
            new("손패", "매 턴 5장을 뽑아요. 카드를 <b>클릭</b>하면 바로 사용돼요.\n" +
                       "마우스를 올리면 크게 보이고, 옆에 용어 설명이 나와요.\n" +
                       $"숫자가 <color={CardText.UpColor}>초록</color>이면 버프로 강해진 값, <color={CardText.DownColor}>빨강</color>이면 약해진 값이에요.",
                () => handRow, Side.Above),
            new("블록", "방어 카드를 쓰면 <b>블록</b>이 생겨요. 공격 피해를 먼저 막아줘요.\n" +
                       "이 게임의 블록은 <b>턴이 지나도 사라지지 않고 쌓여요!</b>\n체력은 전투가 끝나도 이어지고 <b>휴식</b>에서만 회복돼요.",
                () => (RectTransform)playerPanel.transform, Side.Right),
            new("턴 종료", "쓸 카드를 다 썼으면 턴 종료. 남은 손패는 버려지고 적이 예고한 행동을 해요.",
                () => (RectTransform)endTurnBtn.transform, Side.Left),
            new("상단 바", "스테이지 진행 · 토인 · 런 덱 장수예요.\n<b>?</b>를 누르면 이 안내를 다시 볼 수 있고, <b>나가기</b>로 언제든 런처에 갈 수 있어요 (진행은 저장돼요).\n" +
                          "<color=#FF9A8A>전투에서 지면 덱에 넣은 카드가 사라져요</color> (시작 카드는 남아요).",
                () => topBar, Side.Below),
        };

        List<Step> RewardSteps() => new()
        {
            new("전투 보상", "카드 1장을 골라요. 고른 카드는 바로 <b>런 덱</b>에 들어가요 (덱이 가득 차면 감자창고로).\n" +
                            "카드에 마우스를 올리면 등급과 용어 설명이 나와요.",
                () => rewardRow, Side.Below),
            new("안 받아도 돼요", "마음에 드는 카드가 없으면 <b>카드 안 받기</b>. 토인은 그대로 받아요.\n덱이 얇으면 좋은 카드가 더 자주 손에 와요.",
                () => (RectTransform)rewardSkip.transform, Side.Above),
        };

        List<Step> MapSteps() => new()
        {
            new("맵", "노드를 하나씩 골라 오른쪽으로 나아가요. <b>선으로 이어진 파란 노드</b>만 갈 수 있어요.\n" +
                     "노드에 마우스를 올리면 무엇이 있는지 알려줘요.",
                () => mapArea, Side.Below),
            new("노드 종류", "<b>전투</b> 토인·카드 보상   <b>대장간</b> 카드 강화·제거\n<b>휴식</b> 체력 회복   <b>보스</b> 이기면 다음 스테이지\n" +
                            $"스테이지 {RunState.StageCount}개의 보스를 모두 이기면 런 클리어예요!",
                () => mapArea, Side.Below),
            new("현재 상태", "지금 스테이지·진행 노드·체력이에요. 체력은 휴식에서만 회복되니 아껴 쓰세요.",
                () => mapInfo.rectTransform, Side.Below),
        };

        List<Step> ForgeSteps() => new()
        {
            new("대장간", "덱의 카드를 골라 <b>강화</b>하거나 <b>제거</b>해요. 토인이 들어요.",
                () => forgeScroll, Side.Below),
            new("강화", "카드당 1번, 둘 중 하나를 골라요.\n<b>수치 강화</b>: 피해·블록 +3, 상태이상 +1\n<b>코스트 −1</b>: 에너지를 1 덜 써요\n" +
                       "강화 버튼에 마우스를 올리면 결과 카드를 미리 보여줘요.",
                () => forgeInfo.rectTransform, Side.Below),
            new("제거", "제거한 카드는 영원히 사라져요 (감자창고에서도).\n약한 카드를 빼면 강한 카드가 더 자주 나와요. 덱은 최소 8장이 필요해요.",
                () => forgeInfo.rectTransform, Side.Below),
        };

        List<Step> DefeatSteps() => new()
        {
            new("런이 끝났어요", "체력이 0이 되면 런이 끝나고, <b>덱에 넣었던 카드는 사라져요</b> (시작 카드는 남아요).\n" +
                                "런처의 <b>미니게임</b>으로 카드를 다시 모으고, <b>감자창고</b>에서 덱을 짜서 다시 도전하세요.\n" +
                                "새 런을 시작할 때마다 모든 카드가 한 살 먹는다는 것도 잊지 마세요!",
                () => (RectTransform)restartBtn.transform, Side.Below),
        };

        // ── UI 헬퍼 ──
        Button MakeButton(string name, string caption, Vector2 anchor, Vector2 pos, Vector2 size,
                          UnityEngine.Events.UnityAction onClick, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent != null ? parent : transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.2f, 0.24f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<Text>();
            txt.font = uiFont; txt.fontSize = 20; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter; txt.text = caption;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow;
            return btn;
        }

        static void SetButtonText(Button b, string caption)
        {
            var t = b != null ? b.GetComponentInChildren<Text>(true) : null;
            if (t != null) t.text = caption;
        }

        static void SetButtonFont(Button b, int size)
        {
            var t = b != null ? b.GetComponentInChildren<Text>(true) : null;
            if (t != null) t.fontSize = size;
        }
    }
}
