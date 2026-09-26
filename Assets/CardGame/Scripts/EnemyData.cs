using System.Collections.Generic;
using UnityEngine;

namespace TatoGames.CardGame
{
    public enum EnemyTier { Normal, Elite, Boss }   // 일반 / 엘리트 / 보스

    public enum EnemyActionKind { Attack, Block, Debuff, Buff }   // 공격 / 방어 / 디버프 / 버프

    /// <summary>적이 나오는 스테이지. 여러 개를 켤 수 있다(인스펙터에서 체크).</summary>
    [System.Flags]
    public enum StageMask { None = 0, Stage1 = 1 << 0, Stage2 = 1 << 1, Stage3 = 1 << 2 }

    /// <summary>적 행동 한 스텝. 인텐트로 예고된다(적 설계 §2.4).</summary>
    [System.Serializable]
    public class EnemyAction
    {
        public string label;         // 인텐트 이름 (예: "내리치기")
        public EnemyActionKind kind = EnemyActionKind.Attack;
        public int amount;
        public int hits = 1;         // 다단히트 (돌진·연타)
        public StatusType status = StatusType.None;  // Debuff/Buff일 때
    }

    /// <summary>
    /// 적 원본 정의 (적 설계 §2.6). 전부 외부화. 패턴은 순서대로 돌며 loop.
    /// </summary>
    [CreateAssetMenu(menuName = "TatoGames/Enemy", fileName = "Enemy")]
    public class EnemyData : ScriptableObject
    {
        public string id;
        public string enemyName;
        public EnemyTier tier = EnemyTier.Normal;

        [Tooltip("이 적이 나오는 스테이지. 맵이 스테이지마다 여기가 켜진 적만 골라 배치한다.\n" +
                 "보스는 그 스테이지의 10번 노드에 나온다. 한 스테이지에 보스가 여럿이면 무작위")]
        public StageMask stages = StageMask.Stage1;

        /// <summary>stage는 0부터(0 = 1스테이지).</summary>
        public bool AppearsIn(int stage) => stage >= 0 && stage < 31 && (stages & (StageMask)(1 << stage)) != 0;

        [Tooltip("적 아트 (몬스터 시트에서 캐릭터만 크롭한 스프라이트를 연결)")]
        public Sprite artwork;

        [Min(1)] public int hp = 15;
        [Tooltip("전투 시작 공격 방어")] public int blockStart;
        [Tooltip("전투 시작 효과 방어(건수)")] public int wardStart;
        [Tooltip("매 턴 효과 방어 갱신 여부 — MVP 적은 전부 false")] public bool wardRefresh;

        public List<EnemyAction> pattern = new();
        public bool patternLoop = true;

        [Header("2페이즈 변신 (감자벌레류)")]
        [Tooltip("공격 방어가 0으로 깨지면 2페이즈로 변신")]
        public bool transformOnBlockBreak;
        public string phase2Name;
        [Tooltip("변신 후 새 체력 (감자벌레: 10)")] public int phase2Hp;
        public List<EnemyAction> phase2Pattern = new();
        [Tooltip("변신 후 그림 (비우면 artwork 그대로)")]
        public Sprite phase2Artwork;
    }
}
