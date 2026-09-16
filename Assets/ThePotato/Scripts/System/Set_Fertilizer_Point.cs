using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Set_Fertilizer_Point : MonoBehaviour
{
    public Vector2 areaMin; //랜덤 위치 최소값
    public Vector2 areaMax; //랜덤 위치 최대값
    public GameObject fertillizerPrefab; // 생성할 오브젝트 프리팹
    public Transform FertillizerTransform; // 부모가 될 Hole 오브젝트
    public float spawnInterval = 3f; // 생성 간격 (초)
    public int spawnCount = 1; // 한 번에 생성할 개수
    
    private void Start()
    {
        StartCoroutine(SpawnFertillizer()); // 코루틴 실행

        StartCoroutine(ReSpawnFertillizer());

    }
    private Vector3 GetRandomPosition()
    {
        float randomX = Random.Range(areaMin.x, areaMax.x);
        float randomY = Random.Range(areaMin.y, areaMax.y);
        return new Vector3(randomX, randomY, 0f) + FertillizerTransform.position;
    }

    private IEnumerator SpawnFertillizer()
    {
        yield return new WaitForSeconds(spawnInterval); // 일정 시간 대기

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 randomPosition = GetRandomPosition(); // 랜덤 위치 계산
            GameObject spawnedObject = Instantiate(fertillizerPrefab, randomPosition, Quaternion.identity);

            if (FertillizerTransform != null)
            {
                spawnedObject.transform.SetParent(FertillizerTransform);
            }
        }

    }

    private IEnumerator ReSpawnFertillizer()
    {
        yield return new WaitForSeconds(spawnInterval);

        for (int j = 1; j > 0; j++)
        {
            yield return new WaitForSeconds(spawnInterval * 3); // 일정 시간 대기

            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 randomPosition = GetRandomPosition(); // 랜덤 위치 계산
                GameObject spawnedObject = Instantiate(fertillizerPrefab, randomPosition, Quaternion.identity);

                if (FertillizerTransform != null)
                {
                    spawnedObject.transform.SetParent(FertillizerTransform);
                }
            }
        }
    }

}
