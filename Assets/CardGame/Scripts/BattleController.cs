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
        [Tooltip("맵 노드가 id로 적을 찾을 풀 (스테이지1 전 적)")]
        public EnemyData[] enemyPool;
        [Tooltip("전투 보상 후보 풀 (시작덱 제외 카드). §12.1")]
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
        readonly List<CardData> drawPile = new();
        readonly List<CardData> hand = new();
        readonly List<CardData> discard = new();
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

        void Start()
        {
            if (uiFont == null)
                uiFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 22);
            BuildUI();

            // §7 시간축 = 런 수 — 나이는 <b>새 런을 시작할 때만</b> 먹는다.
            // 중단했던 런을 이어서 하는 건 같은 런이므로 세지 않는다.
            //
            // 나가기로 나이를 회피할 구멍은 '덱 잠금'이 막는다 — 런이 진행 중이면 덱을 못 바꾸므로
            // 중간에 나갔다 오는 것으로 얻을 게 없다. 새 런은 죽거나 클리어해야만 시작된다.
            if (!RunState.Active)
            {
                RunState.StartRun(playerMaxHp);
                int rotted = PlayerData.AgeAll();
                if (rotted > 0)
                    startNotice = $"카드 {rotted}장이 썩었습니다 — " +
                                  $"감자창고에서 장당 {PlayerData.RottenSellPrice}토인에 팔 수 있어요";
            }
            EnterNode();
        }

        // ══════════════════════════════════════════════ 런 진행 (§11.3) ════════
        /// <summary>현재 노드로 진입 — 전투/보스면 전투 시작, 휴식이면 회복, 끝이면 클리어.</summary>
        void EnterNode()
        {
            var node = RunState.Current;
            if (node == null) { ShowRunClear(); return; }

            if (node.type == NodeType.Rest)
            {
                RunState.PlayerHp = Mathf.Min(playerMaxHp, RunState.PlayerHp + RunState.RestHeal);
                ShowMap($"휴식 — 체력 +{RunState.RestHeal}  (현재 {RunState.PlayerHp}/{playerMaxHp})");
                return;
            }

            if (node.type == NodeType.Forge) { ShowForge(); return; }

            var found = FindEnemy(node.enemyId);
            if (found != null) enemyData = found;
            StartBattle();
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
        void StartBattle()
        {
            over = false;
            transformed = false;
            if (mapPanel != null) mapPanel.SetActive(false);
            if (forgePanel != null) forgePanel.SetActive(false);
            if (restartBtn != null) restartBtn.SetActive(false);
            if (exitBtn != null) exitBtn.SetActive(false);
            if (endTurnBtn != null) endTurnBtn.interactable = true;

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
        /// 런 덱 = 시작덱(귀속, 항상 포함) + 감자창고에서 덱에 넣어둔 획득 카드 (§10.7 2-존).
        /// id → CardData는 starterDeck + rewardPool이 전 카드를 커버하므로 별도 로더가 필요 없다.
        /// </summary>
        void BuildRunDeck()
        {
            var lib = new Dictionary<string, CardData>();
            Register(starterDeck);
            Register(rewardPool);

            // 덱은 전적으로 보유 인스턴스 구성에 따른다(시작덱도 뺄 수 있음).
            // 대장간에서 강화한 인스턴스는 런타임 복제본으로 들어간다(§10.10).
            // 감자 상태(생/싹)와 대장간 강화를 함께 올린다 (§8.1 · §10.10)
            foreach (var inst in PlayerData.DeckInstances())
                if (lib.TryGetValue(inst.cardId, out var card))
                    drawPile.Add(CardUpgrade.Build(card, inst.upgrade, inst.State));

            // 안전장치: 저장이 비었거나 해석 실패 시 시작덱으로 폴백
            if (drawPile.Count == 0 && starterDeck != null)
                foreach (var c in starterDeck) if (c != null) drawPile.Add(c);

            void Register(CardData[] arr)
            {
                if (arr == null) return;
                foreach (var c in arr)
                    if (c != null && !string.IsNullOrEmpty(c.id) && !lib.ContainsKey(c.id)) lib[c.id] = c;
            }
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
            transformed ? enemyData.phase2Pattern : enemyData.pattern;

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
        void PlayCard(CardData card)
        {
            if (over || card == null) return;
            if (energy < card.cost) return;
            energy -= card.cost;

            int blockBefore = enemy.TotalBlock;
            foreach (var e in card.effects)
                ResolveEffect(e, self: player, opponent: enemy);

            hand.Remove(card);
            discard.Add(card);

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
            if (RunState.Active) RunState.PlayerHp = player.hp;   // 체력은 노드를 넘어 유지
            Refresh();
            message.text = "승리!";
            if (endTurnBtn != null) endTurnBtn.interactable = false;
            RebuildHand();
            ShowReward();
        }

        void Lose()
        {
            over = true; Refresh();
            // §10.7 "복사가 아니라 이동" · §13.2 손실 채널 — 인게임 덱 소멸(귀속 제외)
            int lost = PlayerData.DestroyRunDeck();
            message.text = lost > 0
                ? $"패배…  인게임 덱의 카드 {lost}장이 소멸했습니다 (귀속 제외)"
                : "패배…  런이 여기서 끝났습니다";
            RunState.End();          // §13.7 중간 저장 없음
            EndControls();
        }

        /// <summary>노드 클리어 후: 보스였으면 런 종료, 아니면 맵으로.</summary>
        void AfterNodeCleared()
        {
            if (RunState.IsBossNode)
            {
                if (!RunState.HasNextStage) { ShowRunClear(); return; }
                RunState.NextStage();     // 새 스테이지 맵 생성
                UpdateBackground();       // 배경 전환 (감자밭 → 뿌리층 → 깊은 토양)
                stageIntro = true;
                ShowMap($"보스 격파!   {RunState.StageName} 진입");
                return;
            }
            ShowMap(null);
        }

        void ShowRunClear()
        {
            RunState.End();
            if (mapPanel != null) mapPanel.SetActive(false);
            message.text = "런 클리어!  보스를 쓰러뜨렸습니다.";
            EndControls();
        }

        // ══════════════════════════════════════════════════ 보상 (§12.1) ══════
        void ShowReward()
        {
            int toin = RewardToin();
            var cards = PickRewardCards(3);

            for (int i = rewardRow.childCount - 1; i >= 0; i--)
                Destroy(rewardRow.GetChild(i).gameObject);

            if (cards.Count == 0)   // 후보 없으면 토인만 지급
            {
                PlayerData.AddToin(toin);
                message.text = $"승리!   +{toin} 토인";
                AfterNodeCleared();
                return;
            }

            rewardTitle.text = $"전투 보상\n+{toin} 토인 · 카드 1장 선택";
            foreach (var card in cards)
            {
                var local = card;
                var view = CardView.Create(rewardRow, uiFont, new Vector2(180, 250));
                view.name = "Reward_" + card.id;
                view.Bind(card, theme, true, () => PickReward(local, toin));
            }
            rewardPanel.SetActive(true);
        }

        void PickReward(CardData card, int toin)
        {
            PlayerData.AddToin(toin);
            PlayerData.AddCard(card.id, card.rarity);   // 런처 컬렉션으로 이어짐 (희귀도로 수명 결정)

            rewardPanel.SetActive(false);
            message.text = $"획득: {card.displayName}   (+{toin} 토인)";
            AfterNodeCleared();
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

        // 기본덱 제외 후보에서 희귀도 가중(일반60/희귀40)으로 중복 없이 count장
        List<CardData> PickRewardCards(int count)
        {
            var chosen = new List<CardData>();
            if (rewardPool == null) return chosen;
            var pool = rewardPool.Where(c => c != null).ToList();
            while (chosen.Count < count && chosen.Count < pool.Count)
            {
                Rarity want = Random.value < 0.6f ? Rarity.Common : Rarity.Rare;
                var tierPool = pool.Where(c => c.rarity == want && !chosen.Contains(c)).ToList();
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

        static void Shuffle(List<CardData> list)
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

            // ── 적: 우측 상단 (슬더스 배치) ──
            enemyPanel = CombatantPanel.Create(transform, uiFont, new Vector2(240, 240), withIntent: true);
            UiKit.Place(enemyPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 1),
                        new Vector2(250, -40), enemyPanel.GetComponent<RectTransform>().sizeDelta);

            // ── 플레이어: 좌측 하단 ──
            playerPanel = CombatantPanel.Create(transform, uiFont, new Vector2(200, 200), withIntent: false);
            UiKit.Place(playerPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 1),
                        new Vector2(-330, -70), playerPanel.GetComponent<RectTransform>().sizeDelta);

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
            handRow.anchoredPosition = new Vector2(30, 16); handRow.sizeDelta = new Vector2(980, 240);
            var hlg = handRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.childAlignment = TextAnchor.LowerCenter;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            endTurnBtn = MakeButton("EndTurn", "턴 종료", new Vector2(1, 0), new Vector2(-24, 130), new Vector2(170, 60), OnEndTurn);
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
                    if (!isPickable && !isCurrent && !isPast) t.color = new Color(1, 1, 1, 0.5f);

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
            if (stageIntro) stageIntro = false;    // 새 스테이지 0열로 그대로 진입
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
                var view = CardView.Create(forgeGrid, uiFont, new Vector2(102, 142));
                view.name = $"Forge_{id}";
                view.Bind(shown, theme, true, () => { forgeSelected = id; RebuildForgeActions(); });
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
            forgePanel.SetActive(false);
            ShowMap(null);
        }

        /// <summary>id로 카드 원본 찾기 (시작덱 + 보상 풀이 전 카드를 커버).</summary>
        CardData FindCard(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
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
            enemyPanel.Bind(enemy, theme, enemyData != null ? enemyData.artwork : null);
            enemyPanel.SetIntent(CurrentIntent(), theme, RunState.Active ? RunState.AtkScale : 1f);
            playerPanel.Bind(player, theme, null);   // 플레이어 초상화는 캐릭터 아트 도착 시 연결

            energyText.text = $"{energy}/{maxEnergy}";
            pileText.text = $"덱 {drawPile.Count}    버림 {discard.Count}";
            if (!over) message.text = startNotice;
            RebuildHand();
        }

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

            foreach (var card in hand)
            {
                var local = card;
                bool playable = !over && energy >= card.cost;
                var view = CardView.Create(handRow, uiFont, new Vector2(150, 210));
                view.name = "Card_" + card.id;
                view.Bind(card, theme, playable, () => PlayCard(local));
            }
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
