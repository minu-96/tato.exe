using UnityEngine;
using UnityEngine.EventSystems;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 마우스를 올리면 그 자리에서 살짝 커진다. 레이아웃 그룹은 localScale을 건드리지 않으므로
    /// 옆 칸 배치는 그대로다. 칸 사이 여백보다 작게 키워야 옆 칸에 가려지지 않는다.
    /// (전투 화면처럼 카드가 붙어 있는 곳은 BattleController의 확대 미리보기를 쓴다)
    /// </summary>
    public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float scale = 1.06f;

        /// <summary>마우스가 처음 올라왔을 때 한 번 더 할 일 (감자창고: 새 카드 빨간 점 지우기).</summary>
        public System.Action onEnter;

        public void OnPointerEnter(PointerEventData e)
        {
            transform.localScale = Vector3.one * scale;
            onEnter?.Invoke();
        }
        public void OnPointerExit(PointerEventData e) => transform.localScale = Vector3.one;
        void OnDisable() => transform.localScale = Vector3.one;
    }
}
