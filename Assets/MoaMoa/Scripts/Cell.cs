using System.Collections;
using UnityEngine;
using TMPro;

public class Cell : MonoBehaviour
{
    [Header("Apple Data")]
    public int value;
    public bool isSelected;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TextMeshPro numberText;

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.55f, 0.35f, 0.15f); // 진한 감자색
    [SerializeField] private Color poisonColor  = new Color(0.45f, 0.75f, 0.35f); // 독감자색

    public void Initialize(int newValue)
    {
        value = newValue;
        numberText.text = value.ToString();
        isSelected = false;
        ResetVisual();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        spriteRenderer.color = selected ? selectedColor : defaultColor;
    }

    // 드래그 합계가 10일 때만 호출
    public void SetPoison()
    {
        spriteRenderer.color = poisonColor;
    }

    public void ResetVisual()
    {
        isSelected = false;
        spriteRenderer.color = defaultColor;
    }

    // 합 10 성공 시 호출 — 날아가는 연출
    public void Remove()
    {
        StartCoroutine(FlyAwayRoutine());
    }

    private IEnumerator FlyAwayRoutine()
{
    float duration = 1.2f;
    float elapsed = 0f;

    // 초기 속도: 위로 튀어오르고 좌우로 살짝
    Vector3 velocity = new Vector3(
        Random.Range(-3f, 3f),   // 좌우
        Random.Range(7f, 10f),   // 위로 튀어오르는 힘
        0f
    );
    float gravity = -20f;        // 중력 (값 키우면 더 빨리 떨어짐)
    float spin = Random.Range(-720f, 720f);

    while (elapsed < duration)
    {
        // 중력 누적
        velocity.y += gravity * Time.deltaTime;

        // 위치 갱신
        transform.position += velocity * Time.deltaTime;

        // 회전
        transform.Rotate(0f, 0f, spin * Time.deltaTime);

        // 후반부에만 페이드아웃 (0.6초 지나면 서서히 사라짐)
        if (elapsed > 0.6f)
        {
            float fadeT = (elapsed - 0.6f) / (duration - 0.6f);
            Color c = spriteRenderer.color;
            c.a = 1f - fadeT;
            spriteRenderer.color = c;

            if (numberText != null)
            {
                Color tc = numberText.color;
                tc.a = 1f - fadeT;
                numberText.color = tc;
            }
        }

        elapsed += Time.deltaTime;
        yield return null;
    }
    Destroy(gameObject);
}
}