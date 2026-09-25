using TatoGames.CardGame;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 상단 HUD 칩에 수치를 표시한다(§3). 게임에서 번 토인·모은 카드가 그대로 이어진다.
    /// 칩 종류만 바꿔 같은 컴포넌트를 재사용.
    /// </summary>
    public class HudStat : MonoBehaviour
    {
        public enum Stat
        {
            Toin,             // 토인 보유량
            DeckCount,        // 런 덱 장수
            CollectionCount,  // 감자창고 보유 장수
            BestStage,        // 최고 도달 스테이지
            Nickname,         // 플레이어 이름
        }

        /// <summary>닉네임 — 아직 입력 시스템이 없어 고정값. 나중에 설정에서 바꾸게 한다.</summary>
        public const string DefaultNickname = "멀쩡한 감자농부";

        public Stat stat = Stat.Toin;
        public Text label;

        void OnEnable()
        {
            PlayerData.Changed += Refresh;   // 덱 토글·보상 등으로 값이 바뀌면 즉시 갱신
            Refresh();
        }

        void OnDisable() => PlayerData.Changed -= Refresh;

        public void Refresh()
        {
            if (label == null) return;
            label.text = stat switch
            {
                Stat.DeckCount => PlayerData.DeckSize().ToString(),
                Stat.CollectionCount => PlayerData.Instances().Count.ToString(),
                Stat.BestStage => $"{RunState.BestStageReached} / {RunState.StageCount}",
                Stat.Nickname => DefaultNickname,
                _ => PlayerData.Toin.ToString(),
            };
        }
    }
}
