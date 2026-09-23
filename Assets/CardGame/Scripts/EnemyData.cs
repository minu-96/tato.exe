using System.Collections.Generic;
using UnityEngine;

namespace TatoGames.CardGame
{
    public enum EnemyTier { Normal, Elite, Boss }   // 일반 / 엘리트 / 보스

    public enum EnemyActionKind { Attack, Block, Debuff, Buff }   // 공격 / 방어 / 디버프 / 버프

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
    }
}
