using System.Collections;
using UnityEngine;

public class CameraIntro : MonoBehaviour
{
    [Header("시작 오프셋 (위에서 내려오는 거리)")]
    public float startOffsetY = 20f;

    [Header("내려오는 시간 (초)")]
    public float duration = 1.2f;

    [Header("이징 커브")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 endPos;

    void Awake()
    {
        endPos = transform.position;
        transform.position = new Vector3(endPos.x, endPos.y + startOffsetY, endPos.z);
    }

    void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SetIntroPlaying(true);

        StartCoroutine(MoveDown());
    }

    IEnumerator MoveDown()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.LerpUnclamped(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;

        if (GameManager.Instance != null)
            GameManager.Instance.SetIntroPlaying(false);
    }
}
