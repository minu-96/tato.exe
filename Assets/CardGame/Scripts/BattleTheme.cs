using UnityEngine;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 전투 화면 아트 집결지. 여기 슬롯에 스프라이트를 넣으면 코드 수정 없이 반영된다(§14.5 분담).
    /// 비워두면 단색 폴백으로 그려지므로 아트를 하나씩 채워 넣을 수 있다.
    /// 9-slice가 필요한 프레임·바·버튼은 Sprite Editor에서 Border를 지정해두면 자동으로 늘어난다(§14.4).
    /// </summary>
    [CreateAssetMenu(menuName = "TatoGames/Battle Theme", fileName = "BattleTheme")]
    public class BattleTheme : ScriptableObject
    {
        [Header("배경")]
        [Tooltip("스테이지별 배경 — [0]감자밭 [1]뿌리층 [2]깊은토양. 비면 background 사용")]
        public Sprite[] stageBackgrounds = new Sprite[3];
        public Sprite background;
        public Color backgroundTint = Color.white;

        public Sprite BackgroundFor(int stage) =>
            (stageBackgrounds != null && stage >= 0 && stage < stageBackgrounds.Length
             && stageBackgrounds[stage] != null) ? stageBackgrounds[stage] : background;

        [Header("플레이어")]
        [Tooltip("플레이어(감자) 초상화 — 전투 화면 왼쪽")]
        public Sprite playerPortrait;

        [Header("맵 노드 아이콘 (비우면 글자만)")]
        public Sprite nodeBattle;
        public Sprite nodeRest;
        public Sprite nodeForge;
        public Sprite nodeBoss;

        public Sprite NodeIconFor(NodeType t) => t switch
        {
            NodeType.Rest => nodeRest,
            NodeType.Forge => nodeForge,
            NodeType.Boss => nodeBoss,
            _ => nodeBattle,
        };

        [Header("카드 프레임 (타입별 · 없으면 cardFrame 사용)")]
        public Sprite cardFrame;
        public Sprite cardFrameAttack;
        public Sprite cardFrameDefense;
        public Sprite cardFrameSkill;
        public Sprite cardFrameRoot;
        [Tooltip("카드 일러스트가 없을 때 쓸 기본 그림")]
        public Sprite cardArtFallback;
        public Sprite costBadge;

        [Header("체력 / 방어")]
        public Sprite barBackground;
        public Sprite barFill;
        public Sprite blockIcon;   // 공격 방어
        public Sprite wardIcon;    // 효과 방어

        [Header("에너지")]
        public Sprite energyOrb;

        [Header("인텐트 아이콘 (적 설계 §2.4)")]
        public Sprite intentAttack;
        public Sprite intentBlock;
        public Sprite intentDebuff;
        public Sprite intentBuff;

        [Header("상태이상 아이콘 (§9 6종)")]
        public Sprite iconVulnerable;
        public Sprite iconWeak;
        public Sprite iconPoison;
        public Sprite iconStrength;
        public Sprite iconDexterity;
        public Sprite iconRegen;

        [Header("버튼 (SpriteSwap)")]
        public Sprite buttonIdle;
        public Sprite buttonHover;
        public Sprite buttonPressed;

        [Header("폴백 색상 (아트 없을 때)")]
        public Color panelColor = new(0.14f, 0.15f, 0.19f, 0.92f);
        public Color cardColor = new(0.18f, 0.20f, 0.24f, 1f);
        public Color cardDisabledColor = new(0.11f, 0.12f, 0.14f, 1f);
        public Color accentColor = new(1f, 0.78f, 0.2f, 1f);
        public Color hpColor = new(0.78f, 0.25f, 0.25f, 1f);
        public Color blockColor = new(0.35f, 0.62f, 0.85f, 1f);
        public Color wardColor = new(0.65f, 0.45f, 0.85f, 1f);

        public Sprite FrameFor(CardType t) => t switch
        {
            CardType.Attack => cardFrameAttack != null ? cardFrameAttack : cardFrame,
            CardType.Defense => cardFrameDefense != null ? cardFrameDefense : cardFrame,
            CardType.Skill => cardFrameSkill != null ? cardFrameSkill : cardFrame,
            CardType.Root => cardFrameRoot != null ? cardFrameRoot : cardFrame,
            _ => cardFrame,
        };

        public Sprite IconFor(StatusType s) => s switch
        {
            StatusType.Vulnerable => iconVulnerable,
            StatusType.Weak => iconWeak,
            StatusType.Poison => iconPoison,
            StatusType.Strength => iconStrength,
            StatusType.Dexterity => iconDexterity,
            StatusType.Regen => iconRegen,
            _ => null,
        };

        public Sprite IntentFor(EnemyActionKind k) => k switch
        {
            EnemyActionKind.Attack => intentAttack,
            EnemyActionKind.Block => intentBlock,
            EnemyActionKind.Debuff => intentDebuff,
            EnemyActionKind.Buff => intentBuff,
            _ => null,
        };

        public static Color StatusColor(StatusType s) => s switch
        {
            StatusType.Vulnerable => new Color(0.95f, 0.55f, 0.3f),
            StatusType.Weak => new Color(0.6f, 0.55f, 0.8f),
            StatusType.Poison => new Color(0.55f, 0.8f, 0.4f),
            StatusType.Strength => new Color(0.95f, 0.45f, 0.4f),
            StatusType.Dexterity => new Color(0.45f, 0.8f, 0.85f),
            StatusType.Regen => new Color(0.5f, 0.9f, 0.6f),
            _ => Color.white,
        };
    }
}
