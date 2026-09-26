using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// MVP 전투 1판 코어. 턴제(플레이어 ↔ 적), 에너지·드로우·블록, 상태이상 6종,
    /// 방어 2종 분리(§9). 데이터는 전부 SO(CardData/EnemyData, 외부화). UI는 코드로 만든
    /// 기능용 프로그래머 아트 — 디자인은 추후 교체(§14.5).
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
        Image backgroundImage;
        RectTransform handRow;
        Button endTurnBtn;
        GameObject restartBtn;
        GameObject exitBtn;
        GameObject rewardPanel;
        Text rewardTitle;
        RectTransform rewardRow;
        GameObject mapPanel;
        Text mapInfo;
        RectTransform mapArea;     // 노드 그래프가 그려지는 영역
        bool stageIntro;           // 새 스테이지 첫 노드 진입 대기
        GameObject forgePanel;     // 대장간
        Text forgeInfo, forgeHint;
        RectTransform forgeGrid, forgeActionRow;
        int forgeSelected = -1;

        // ── 배치 기준 (1280×720) ──
        const float HandY = 16f;                                   // 손패 아래끝
        static readonly Vector2 HandCardSize = new(150, 210);
        static readonly Vector2 RewardCardSize = new(180, 250);
        static readonly Vector2 ForgeCardSize = new(102, 142);
        const float PanelBottomY = HandY + 210f + 10f;             // 적·플레이어 패널은 손패 윗변 바로 위

        // 마우스를 올린 카드가 얼마나 커지나 — 작은 카드일수록 크게 키워 글자가 읽히게
        const float HandHoverScale = 1.2f;
        const float RewardHoverScale = 1.1f;
        const float ForgeHoverScale = 1.4f;

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
                    startNotice = $"카드 {rotted}장이 썩었습니다 — " +
                                  $"감자창고에서 장당 {PlayerData.RottenSellPrice}토인에 팔 수 있어요";
            }
            EnterNode();
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
                RunState.PlayerHp = Mathf.Min(playerMaxHp, RunState.PlayerHp + RunState.RestHeal);
                RunState.NodeCleared = true;   // 다시 들어와도 또 회복하지 않는다
                ShowIdleField();
                ShowMap($"휴식 — 체력 +{RunState.RestHeal}  (현재 {RunState.PlayerHp}/{playerMaxHp})");
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
        /// <summary>전투 화면으로 전환 — 맵·대장간·종료 버튼을 내리고 적 패널을 올린다.</summary>
        void ShowBattleField()
        {
            if (mapPanel != null) mapPanel.SetActive(false);
            if (forgePanel != null) forgePanel.SetActive(false);
            if (rewardPanel != null) rewardPanel.SetActive(false);
            if (restartBtn != null) restartBtn.SetActive(false);
            if (exitBtn != null) exitBtn.SetActive(false);
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
            if (enemy.IsDead) { Win(); return; }

            var act = pattern[Mathf.Clamp(enemyIndex, 0, pattern.Count - 1)];
            switch (act.kind)
            {
                case EnemyActionKind.Attack:
                    int hits = Mathf.Max(1, act.hits);
                    for (int i = 0; i < hits && !player.IsDead; i++)
                    {
                        int raw = player.ModifyIncoming(enemy.ModifyOutgoing(ScaleAtk(act.amount)));
                        player.TakeAttack(raw);
                    }
                    break;
                case EnemyActionKind.Block:
                    enemy.GainBlock(ScaleAtk(act.amount), temporary: false);   // 적 블록도 누적
                    break;
                case EnemyActionKind.Debuff:
                    player.TryApplyDebuff(act.status, act.amount);
                    break;
                case EnemyActionKind.Buff:
                    enemy.Add(act.status, act.amount);
                    break;
            }

            // 되받아치기 — 이번 적 턴에 받은 피해의 절반을 되돌려준다 (블록 무시)
            int reflected = player.ConsumeReflect();
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
            enemy.name = string.IsNullOrEmpty(enemyData.phase2Name) ? enemy.name : enemyData.phase2Name;
            enemy.maxHp = ScaleHp(Mathf.Max(1, enemyData.phase2Hp));
            enemy.hp = enemy.maxHp;   // 껍질 속 본체 (감자벌레: 10)
            enemy.block = 0; enemy.tempBlock = 0; enemy.ward = 0;
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
                    else target.TryApplyDebuff(e.status, e.value);         // 디버프(효과방어 검사)
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
            message.text = "승리!";
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
                ? $"패배…  인게임 덱의 카드 {lost}장이 소멸했습니다 (귀속 제외)"
                : "패배…  런이 여기서 끝났습니다";
            RunState.End();          // 런 종료 — 런·전투·보상 저장을 전부 지운다
            EndControls();
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
                string head = $"보스 격파!   {RunState.StageName} 진입";
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
            message.text = "런 클리어!  보스를 쓰러뜨렸습니다.";
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
                message.text = $"승리!   +{toin} 토인";
                AfterNodeCleared();
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
                ? $"보스 보상\n+{toin} 토인 · 초월/전설 카드 {cards.Count}장 중 1장 선택"
                : $"전투 보상\n+{toin} 토인 · 카드 {cards.Count}장 중 1장 선택";
            foreach (var card in cards)
            {
                var local = card;
                var view = CardView.Create(rewardRow, uiFont, RewardCardSize);
                view.name = "Reward_" + card.id;
                view.Bind(card, theme, true, () => PickReward(local, toin));
                AttachHover(view, card, true, RewardCardSize, RewardHoverScale);
            }
            rewardPanel.SetActive(true);
        }

        void PickReward(CardData card, int toin)
        {
            BattleSave.ClearReward();   // 지급보다 먼저 지운다 — 도중에 꺼져도 두 번 받지 않게
            PlayerData.AddToin(toin);
            // 덱에 자리가 있으면 덱으로, 가득 찼으면(최대 30장) 감자창고로 (희귀도로 수명 결정)
            bool inDeck = PlayerData.AddCard(card.id, card.rarity);

            rewardPanel.SetActive(false);
            message.text = $"획득: {card.displayName}   (+{toin} 토인)";
            // 바로 맵이 덮으므로 덱이 찼다는 안내는 맵 상단 문구로 띄운다
            AfterNodeCleared(inDeck ? null
                : $"{card.displayName} — 덱이 가득 차서({PlayerData.MaxDeck}장) 감자창고로 보냈어요");
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

        void EndControls()
        {
            if (endTurnBtn != null) endTurnBtn.interactable = false;
            ShowEndButtons();
            RebuildHand();
        }

        void ShowEndButtons()
        {
            if (restartBtn != null) restartBtn.SetActive(true);
            if (exitBtn != null) exitBtn.SetActive(true);
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
            UiKit.Apply(orb, theme != null ? theme.energyOrb : null,
                        theme != null ? theme.accentColor : new Color(1f, 0.78f, 0.2f));
            energyText = UiKit.Label("EnergyText", orb.rectTransform, uiFont, 26);
            UiKit.Stretch(energyText.rectTransform);
            energyText.color = orb.sprite != null ? Color.white : new Color(0.1f, 0.1f, 0.12f);

            // ── 덱/버림 카운터 ──
            pileText = UiKit.Label("PileText", transform, uiFont, 17, TextAnchor.LowerLeft);
            UiKit.Place(pileText.rectTransform, new Vector2(0, 0), new Vector2(20, 24), new Vector2(420, 26));

            message = UiKit.Label("Message", transform, uiFont, 30);
            UiKit.Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(760, 60));
            message.color = theme != null ? theme.accentColor : new Color(1f, 0.78f, 0.2f);

            // ── 손패 (하단 중앙) ──
            handRow = UiKit.Rect("Hand", transform);
            handRow.anchorMin = handRow.anchorMax = new Vector2(0.5f, 0);
            handRow.pivot = new Vector2(0.5f, 0);
            handRow.anchoredPosition = new Vector2(30, HandY); handRow.sizeDelta = new Vector2(980, 240);
            var hlg = handRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.childAlignment = TextAnchor.LowerCenter;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            // 오른쪽, 손패 윗변 바로 위. 버튼 pivot이 가운데라 x = −(여백 24 + 폭 절반 85).
            // (예전 (−24, 130)은 절반이 화면 밖이었고, 손패가 6장을 넘으면 카드와 겹쳤다)
            endTurnBtn = MakeButton("EndTurn", "턴 종료", new Vector2(1, 0), new Vector2(-109, PanelBottomY + 30f),
                                    new Vector2(170, 60), OnEndTurn);
            UiKit.ApplyButton(endTurnBtn, theme);

            restartBtn = MakeButton("Restart", "다시하기", new Vector2(0.5f, 0.5f), new Vector2(-110, -70), new Vector2(200, 56),
                                    () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)).gameObject;
            restartBtn.SetActive(false);

            exitBtn = MakeButton("Exit", "나가기 (런처로)", new Vector2(0.5f, 0.5f), new Vector2(110, -70), new Vector2(200, 56),
                                 ExitToLauncher).gameObject;
            exitBtn.SetActive(false);

            // 보상 패널 (기본 숨김) — 승리 시 활성화
            var panelGO = new GameObject("RewardPanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(transform, false);
            var prt = panelGO.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            panelGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.78f);
            rewardPanel = panelGO;

            rewardTitle = MakeText("RewardTitle", new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(820, 90),
                                   TextAnchor.MiddleCenter, panelGO.transform);
            rewardTitle.fontSize = 28; rewardTitle.color = new Color(1f, 0.78f, 0.2f);

            var rrGO = new GameObject("RewardRow", typeof(RectTransform));
            rrGO.transform.SetParent(panelGO.transform, false);
            rewardRow = rrGO.GetComponent<RectTransform>();
            rewardRow.anchorMin = rewardRow.anchorMax = new Vector2(0.5f, 0.5f); rewardRow.pivot = new Vector2(0.5f, 0.5f);
            rewardRow.anchoredPosition = new Vector2(0, -40); rewardRow.sizeDelta = new Vector2(780, 270);
            var rhlg = rrGO.AddComponent<HorizontalLayoutGroup>();
            rhlg.spacing = 16; rhlg.childAlignment = TextAnchor.MiddleCenter;
            rhlg.childControlWidth = rhlg.childControlHeight = false;
            rhlg.childForceExpandWidth = rhlg.childForceExpandHeight = false;

            rewardPanel.SetActive(false);

            // ── 맵 패널 (노드 진행, 기본 숨김) ──
            var mapGO = new GameObject("MapPanel", typeof(RectTransform), typeof(Image));
            mapGO.transform.SetParent(transform, false);
            var mrt = mapGO.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.offsetMin = mrt.offsetMax = Vector2.zero;
            mapGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.82f);
            mapPanel = mapGO;

            mapInfo = MakeText("MapInfo", new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(900, 90),
                               TextAnchor.MiddleCenter, mapGO.transform);
            mapInfo.fontSize = 26; mapInfo.color = new Color(1f, 0.78f, 0.2f);

            // 노드 그래프 영역 (연결선 + 노드를 직접 그린다)
            mapArea = UiKit.Rect("MapArea", mapGO.transform);
            UiKit.Place(mapArea, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(MapW, MapH));

            mapPanel.SetActive(false);

            // ── 대장간 패널 (§11.2 · 기본 숨김) ──
            var fgGO = new GameObject("ForgePanel", typeof(RectTransform), typeof(Image));
            fgGO.transform.SetParent(transform, false);
            var frt = fgGO.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            fgGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.88f);
            forgePanel = fgGO;

            forgeInfo = UiKit.Label("ForgeInfo", fgGO.transform, uiFont, 24);
            UiKit.Place(forgeInfo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1000, 70));
            forgeInfo.color = theme != null ? theme.accentColor : new Color(1f, 0.78f, 0.2f);

            forgeGrid = UiKit.Rect("ForgeGrid", fgGO.transform);
            forgeGrid.anchorMin = forgeGrid.anchorMax = new Vector2(0.5f, 1);
            forgeGrid.pivot = new Vector2(0.5f, 1);
            forgeGrid.anchoredPosition = new Vector2(0, -104);
            forgeGrid.sizeDelta = new Vector2(980, 420);
            var fgrid = forgeGrid.gameObject.AddComponent<GridLayoutGroup>();
            fgrid.cellSize = new Vector2(102, 142);
            fgrid.spacing = new Vector2(6, 6);
            fgrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            fgrid.constraintCount = 9;
            fgrid.childAlignment = TextAnchor.UpperCenter;

            forgeHint = UiKit.Label("ForgeHint", fgGO.transform, uiFont, 19);
            UiKit.Place(forgeHint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 148), new Vector2(950, 30));

            forgeActionRow = UiKit.Rect("ForgeActions", fgGO.transform);
            UiKit.Place(forgeActionRow, new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(950, 52));
            var fhlg = forgeActionRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            fhlg.spacing = 14; fhlg.childAlignment = TextAnchor.MiddleCenter;
            fhlg.childControlWidth = fhlg.childControlHeight = false;
            fhlg.childForceExpandWidth = fhlg.childForceExpandHeight = false;

            MakeButton("ForgeLeave", "대장간 나가기", new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(220, 48),
                       LeaveForge, fgGO.transform);

            forgePanel.SetActive(false);

            // 전투 중에도 언제든 런처로 (런은 유지되어 다음에 이어서 진행)
            MakeButton("Leave", "나가기", new Vector2(0, 1), new Vector2(110, -178), new Vector2(140, 44),
                       ExitToLauncher);
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
            for (int c = 0; c < cols.Count; c++)
                for (int r = 0; r < cols[c].Count; r++)
                {
                    var n = cols[c][r];
                    bool isCurrent = !stageIntro && c == RunState.Col && r == RunState.Row;
                    bool isPast = c < RunState.Col && !stageIntro;
                    bool isPickable = c == targetCol && reachable.Contains(r);

                    var cell = UiKit.Img($"N{c}_{r}", mapArea, raycast: isPickable);
                    UiKit.Place(cell.rectTransform, new Vector2(0.5f, 0.5f),
                                NodePos(cols, c, r), new Vector2(96, 66));
                    cell.color =
                        isCurrent ? new Color(0.85f, 0.62f, 0.15f, 0.95f)
                        : isPickable ? (n.type == NodeType.Boss ? new Color(0.55f, 0.2f, 0.2f, 0.98f)
                                                                : new Color(0.24f, 0.42f, 0.55f, 0.98f))
                        : isPast ? new Color(0.2f, 0.42f, 0.26f, 0.9f)
                                 : new Color(0.20f, 0.20f, 0.24f, 0.85f);

                    string sub = n.type switch
                    {
                        NodeType.Rest => $"+{RunState.RestHeal}",
                        NodeType.Forge => "강화·제거",
                        _ => FindEnemy(n.enemyId)?.enemyName ?? "",
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

                    if (!isPickable) continue;
                    int pick = r;
                    var btn = cell.gameObject.AddComponent<Button>();
                    btn.targetGraphic = cell;
                    btn.onClick.AddListener(() => OnChooseNode(pick));
                }

            int shown = Mathf.Min(RunState.Col + 1, cols.Count);
            string head = $"{RunState.StageName}   ·   노드 {shown} / {cols.Count}   ·   체력 {RunState.PlayerHp}/{playerMaxHp}";
            string pickMsg = reachable.Count > 1 ? "\n갈 길을 선택하세요" : "\n이어서 진행하세요";
            mapInfo.text = (string.IsNullOrEmpty(note) ? head : $"{note}\n{head}") + pickMsg;

            mapPanel.SetActive(true);
        }

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
            RebuildForge();
            forgePanel.SetActive(true);
        }

        void RebuildForge()
        {
            forgeInfo.text =
                $"대장간   ·   보유 토인 {PlayerData.Toin}\n" +
                $"강화 {PlayerData.UpgradeCost} 토인 (카드당 1회)   ·   제거 {PlayerData.RemoveCost} 토인";

            for (int i = forgeGrid.childCount - 1; i >= 0; i--)
                Destroy(forgeGrid.GetChild(i).gameObject);

            foreach (var inst in PlayerData.DeckInstances())
            {
                var data = FindCard(inst.cardId);
                if (data == null) continue;
                var shown = CardUpgrade.Build(data, inst.upgrade, inst.State);
                int id = inst.instanceId;
                var view = CardView.Create(forgeGrid, uiFont, ForgeCardSize);
                view.name = $"Forge_{id}";
                view.Bind(shown, theme, true, () => { forgeSelected = id; RebuildForgeActions(); });
                AttachHover(view, shown, true, ForgeCardSize, ForgeHoverScale);
            }

            RebuildForgeActions();
        }

        void RebuildForgeActions()
        {
            for (int i = forgeActionRow.childCount - 1; i >= 0; i--)
                Destroy(forgeActionRow.GetChild(i).gameObject);

            var inst = PlayerData.Instances().FirstOrDefault(c => c.instanceId == forgeSelected);
            if (inst == null)
            {
                forgeSelected = -1;
                forgeHint.text = "강화하거나 제거할 카드를 고르세요";
                return;
            }

            var data = FindCard(inst.cardId);
            forgeHint.text = $"선택: {(data != null ? data.displayName : inst.cardId)}{CardUpgrade.Suffix(inst.upgrade)}";

            int id = inst.instanceId;
            if (!inst.IsUpgraded)
            {
                AddForgeButton($"강화 +  ({PlayerData.UpgradeCost})", () => DoUpgrade(id, UpgradeKind.Plus));
                AddForgeButton($"강화 −  ({PlayerData.UpgradeCost})", () => DoUpgrade(id, UpgradeKind.Minus));
            }
            AddForgeButton($"제거  ({PlayerData.RemoveCost})", () => DoRemove(id));
            AddForgeButton("취소", () => { forgeSelected = -1; RebuildForgeActions(); });
        }

        void AddForgeButton(string caption, UnityEngine.Events.UnityAction act)
        {
            var b = MakeButton("FA", caption, Vector2.zero, Vector2.zero, new Vector2(196, 48), act, forgeActionRow);
            UiKit.ApplyButton(b, theme);
        }

        void DoUpgrade(int id, UpgradeKind kind)
        {
            if (PlayerData.TryUpgrade(id, kind, out string reason)) { forgeSelected = -1; RebuildForge(); }
            else forgeHint.text = reason;
        }

        void DoRemove(int id)
        {
            if (PlayerData.TryRemoveCard(id, out string reason)) { forgeSelected = -1; RebuildForge(); }
            else forgeHint.text = reason;
        }

        void LeaveForge()
        {
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
            var go = new GameObject("T", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = uiFont; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void Refresh()
        {
            if (enemy != null)
            {
                enemyPanel.Bind(enemy, theme, EnemyArt());
                var intent = CurrentIntent();
                enemyPanel.SetIntent(intent, theme, intent != null ? IntentAmount(intent) : 0);
            }
            playerPanel.Bind(player, theme, theme != null ? theme.playerPortrait : null);

            energyText.text = $"{energy}/{maxEnergy}";
            pileText.text = $"덱 {drawPile.Count}    버림 {discard.Count}";
            if (!over) message.text = startNotice;
            RebuildHand();
            if (!over) SaveBattle();   // 입력을 기다리는 순간마다 저장 — 언제 나가도 그 자리에서 이어진다
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

            foreach (var bc in hand)
            {
                var local = bc;
                var card = bc.data;
                bool playable = !over && energy >= card.cost;
                var view = CardView.Create(handRow, uiFont, HandCardSize);
                ((RectTransform)view.transform).pivot = new Vector2(0.5f, 0f);   // 커질 때 화면 밖이 아니라 위로 자라게
                view.name = "Card_" + card.id;
                view.Bind(card, theme, playable, () => PlayCard(local));
                AttachHover(view, card, playable, HandCardSize, HandHoverScale);
            }
        }

        // ── 카드 마우스오버 확대 ──
        // 카드 자체를 키우면 옆 카드(나중 형제)가 그 위를 덮어 그린다. 그래서 같은 모양의
        // 확대본을 맨 위에 겹쳐 그린다. 확대본은 클릭을 받지 않으므로 아래 원래 카드가 그대로 눌린다.
        readonly Dictionary<Vector2, CardView> previews = new();   // 카드 크기별로 하나씩 재사용
        CardView previewShown;
        CardView previewOwner;

        void AttachHover(CardView view, CardData card, bool playable, Vector2 size, float scale)
        {
            var relay = view.gameObject.AddComponent<CardHoverRelay>();
            relay.onEnter = () => ShowPreview(view, card, playable, size, scale);
            relay.onExit = () => HidePreview(view);
        }

        void ShowPreview(CardView source, CardData card, bool playable, Vector2 size, float scale)
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

            pv.Bind(card, theme, playable, null);
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
        }

        void HidePreview(CardView source)
        {
            if (previewOwner != source) return;      // 이미 다른 카드로 옮겨갔으면 건드리지 않는다
            if (previewShown != null) previewShown.gameObject.SetActive(false);
            previewShown = null;
            previewOwner = null;
        }

        // ── UI 헬퍼 ──
        Text MakeText(string name, Vector2 anchor, Vector2 pos, Vector2 size, TextAnchor align, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent != null ? parent : transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = uiFont; t.fontSize = 22; t.color = Color.white; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

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
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow;
            return btn;
        }
    }
}
