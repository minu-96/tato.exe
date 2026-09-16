using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Set_WayPoints : MonoBehaviour
{
    public Vector2 areaMin; //랜덤 위치 최소값
    public Vector2 areaMax; //랜덤 위치 최대값


    // Start is called before the first frame update
    void Start()
    {
        transform.position = new Vector2(Random.Range(areaMin.x, areaMax.x), Random.Range(areaMin.y, areaMax.y));
        StartCoroutine(ResetPosition());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private IEnumerator ResetPosition()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // 일정 시간 대기
            transform.position = new Vector2(Random.Range(areaMin.x, areaMax.x), Random.Range(areaMin.y, areaMax.y));
        }
    }
}

//농부가 너무 빠르게 위치를 바꾸게 되고 가깝게 생성되었을경우에 위치에 머물러 있는 것을 수정해야할듯
