using UnityEngine;
using UnityEngine.EventSystems;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 이 오브젝트에 마우스를 올리면 UiTooltip으로 설명을 띄운다.
    /// 판정을 받으려면 같은 오브젝트의 Graphic(Image 등)이 raycastTarget = true여야 한다.
    /// 아이콘이 다시 그려지며 사라져도(OnDisable) 말풍선이 남지 않는다.
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string title;
        [TextArea] public string body;
        [Tooltip("위·아래 대신 옆에 띄운다 (카드처럼 세로로 긴 대상)")]
        public bool beside;

        public void Set(string t, string b) { title = t; body = b; }

        public void OnPointerEnter(PointerEventData e)
        {
            if (beside) UiTooltip.ShowBeside(this, (RectTransform)transform, title, body);
            else UiTooltip.Show(this, (RectTransform)transform, title, body);
        }

        public void OnPointerExit(PointerEventData e) => UiTooltip.Hide(this);
        void OnDisable() => UiTooltip.Hide(this);

        /// <summary>대상에 트리거를 붙이고(있으면 재사용) 판정을 켠다.</summary>
        public static TooltipTrigger On(UnityEngine.UI.Graphic g, string title, string body, bool beside = false)
        {
            if (g == null) return null;
            g.raycastTarget = true;
            if (!g.TryGetComponent(out TooltipTrigger t)) t = g.gameObject.AddComponent<TooltipTrigger>();
            t.Set(title, body);
            t.beside = beside;
            return t;
        }
    }
}
