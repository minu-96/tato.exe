using UnityEngine;
using UnityEngine.EventSystems;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 마우스 올림/내림을 콜백으로 넘겨준다. 오브젝트가 사라질 때도(손패 재구성 등)
    /// 내림으로 처리해서, 가리키던 카드가 없어졌는데 확대 표시만 남는 일이 없게 한다.
    /// (전투 화면 카드 확대 미리보기 — BattleController.AttachHover)
    /// </summary>
    public class CardHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action onEnter;
        public System.Action onExit;

        public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
        public void OnPointerExit(PointerEventData e) => onExit?.Invoke();
        void OnDisable() => onExit?.Invoke();
    }
}
