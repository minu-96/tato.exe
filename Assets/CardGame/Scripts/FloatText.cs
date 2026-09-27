using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.CardGame
{
    /// <summary>
    /// 떠오르며 사라지는 숫자 (−5 피해, 블록 +5 …).
    /// 전투는 한 번에 계산되므로 이게 없으면 턴 종료를 누른 순간 체력만 조용히 줄어 있다 —
    /// 무엇이 얼마나 들어왔는지 눈으로 보이게 한다.
    /// </summary>
    public class FloatText : MonoBehaviour
    {
        const float Life = 1.1f;
        const float Rise = 56f;

        Text text;
        RectTransform rt;
        Vector2 start;
        float age;

        public static void Spawn(Transform parent, Font font, string msg, Color color, Vector2 localPos,
                                 int size = 30, float delay = 0f)
        {
            if (parent == null || string.IsNullOrEmpty(msg)) return;
            var t = UiKit.Label("Float", parent, font, size);
            t.text = msg;
            t.color = color;
            t.fontStyle = FontStyle.Bold;
            UiKit.Outline(t, 2f);
            var f = t.gameObject.AddComponent<FloatText>();
            f.text = t;
            f.rt = t.rectTransform;
            f.rt.anchorMin = f.rt.anchorMax = f.rt.pivot = new Vector2(0.5f, 0.5f);
            f.rt.sizeDelta = new Vector2(240, 40);
            f.start = localPos;
            f.rt.anchoredPosition = localPos;
            f.age = -delay;                       // 여러 개가 동시에 뜨면 조금씩 늦춰 겹치지 않게
            f.rt.SetAsLastSibling();
            if (delay > 0f) { var c = t.color; c.a = 0f; t.color = c; }
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            if (age < 0f) return;
            float k = Mathf.Clamp01(age / Life);
            rt.anchoredPosition = start + Vector2.up * (Rise * Mathf.Sqrt(k));
            var c = text.color;
            c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            text.color = c;
            if (age >= Life) Destroy(gameObject);
        }
    }
}
