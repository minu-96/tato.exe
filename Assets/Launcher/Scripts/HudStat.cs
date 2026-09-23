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
        }

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
                _ => PlayerData.Toin.ToString(),
            };
        }
    }
}
