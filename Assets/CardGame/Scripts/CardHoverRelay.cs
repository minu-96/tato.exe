using UnityEngine;
using UnityEngine.EventSystems;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 마우스 올림/내림·우클릭을 콜백으로 넘겨준다. 오브젝트가 사라질 때도(손패 재구성 등)
    /// 내림으로 처리해서, 가리키던 카드가 없어졌는데 확대 표시만 남는 일이 없게 한다.
    /// (카드 확대 미리보기 — BattleController.AttachHover · 우클릭 자세히 보기 — CardDetailView)
    /// 왼쪽 클릭은 같은 오브젝트의 Button이 받는다(Button은 왼쪽 버튼만 반응한다).
    /// </summary>
    public class CardHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public System.Action onEnter;
        public System.Action onExit;
        public System.Action onRightClick;

        public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
        public void OnPointerExit(PointerEventData e) => onExit?.Invoke();

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) onRightClick?.Invoke();
        }

        void OnDisable() => onExit?.Invoke();
    }
}
