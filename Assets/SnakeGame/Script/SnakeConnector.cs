using System.Collections.Generic;
using UnityEngine;

public class SnakeConnector : MonoBehaviour
{
    public SnakeHead head;
    public GameObject stemPrefab;
    public float thickness = 0.5f; // 줄기 두께 (스프라이트 y 스케일)

    private List<Transform> stems = new List<Transform>();

    void LateUpdate() // Lerp 이후의 '보이는 위치' 기준으로 갱신
    {
        // stemPrefab(줄기 프리팹)이나 head가 없으면 연결선 그리기를 건너뜀
        // (인스펙터 미할당 시 매 프레임 예외가 쏟아지는 것을 방지)
        if (stemPrefab == null || head == null) return;

        List<Transform> points = head.GetBodyTransforms();
        int needed = points.Count - 1; // 연결선 개수 = 세그먼트 사이 간격 수

        // 모자라면 생성
        while (stems.Count < needed)
            stems.Add(Instantiate(stemPrefab, transform).transform);

        // 남는 건 숨김 (안전장치)
        for (int i = needed; i < stems.Count; i++)
            stems[i].gameObject.SetActive(false);

        // 각 줄기를 두 점 사이에 배치
        for (int i = 0; i < needed; i++)
        {
            Vector3 a = points[i].position;
            Vector3 b = points[i + 1].position;
            Transform stem = stems[i];
            stem.gameObject.SetActive(true);

            // 중간 지점
            stem.position = (a + b) * 0.5f;

            // 두 점을 잇는 각도
            Vector2 diff = b - a;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            stem.rotation = Quaternion.Euler(0, 0, angle);

            // 거리만큼 x로 늘이기
            stem.localScale = new Vector3(diff.magnitude, thickness, 1f);
        }
    }
}