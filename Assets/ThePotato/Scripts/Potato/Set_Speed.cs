using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Set_Speed : MonoBehaviour
{
    public static Set_Speed Instance;

    public float moveSpeed = 1f; // 캐릭터 이동 속도
    public float SpeedMultiple = 2f; //이동속도 배율
    public float time = 5f; //스피드가 올라가있는 시간

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // 중복된 인스턴스 제거
        }
    }

    
    public void SaveSpeed(float speed)
    {
        moveSpeed = speed;
        Debug.Log($"상태 저장하기: 속도 {speed}");
    }

    public IEnumerator SpeedUp(float speed)
    {
        moveSpeed *= SpeedMultiple;
        Debug.Log($"상태 불러오기: 속도 {moveSpeed}");

        yield return new WaitForSeconds(time); // 일정 시간 대기

        moveSpeed /= SpeedMultiple;
        Debug.Log($"상태 불러오기: 속도 {moveSpeed}");
    }

}
