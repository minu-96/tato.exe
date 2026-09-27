using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 누를 수 없는 버튼을 반투명하게. 스프라이트 교체 방식 버튼은 비활성 모습이 따로 없어서
    /// 눌리지 않는 순간에도 멀쩡해 보인다(턴 종료·대장간 버튼). UiKit.ApplyButton이 붙인다.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class ButtonDim : MonoBehaviour
    {
        [Range(0f, 1f)] public float disabledAlpha = 0.5f;

        Selectable sel;
        CanvasGroup group;
        bool? last;

        void Awake()
        {
            sel = GetComponent<Selectable>();
            if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable() => last = null;   // 켜질 때 한 번은 반드시 맞춘다

        void LateUpdate()
        {
            if (sel == null || group == null) return;
            bool on = sel.IsInteractable();
            if (last == on) return;
            last = on;
            group.alpha = on ? 1f : disabledAlpha;
        }
    }
}
