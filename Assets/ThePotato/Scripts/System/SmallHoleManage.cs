using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmallHoleManage : MonoBehaviour
{
    public GameObject holePrefab; // 구멍 프리팹
    public Transform potato; // 감자의 Transform
    public float spawnRange = 10f; // 구멍이 생성될 수 있는 최대 범위
    public float minDistanceFromPotato = 2f; // 감자로부터 최소 거리
    public int maxAttempts = 10; // 위치 찾기 최대 시도 횟수

    void Start()
    {
        StartCoroutine(SpawnHoles());
    }

    IEnumerator SpawnHoles()
    {
        while (true)
        {
            Vector2 spawnPosition = GetValidSpawnPosition();
            if (spawnPosition != Vector2.zero)
            {
                Instantiate(holePrefab, spawnPosition, Quaternion.identity);
            }

            yield return new WaitForSeconds(2f); // 2초마다 구멍 생성
        }
    }

    Vector2 GetValidSpawnPosition()
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomPosition = new Vector2(
                Random.Range(-spawnRange, spawnRange),
                Random.Range(-spawnRange, spawnRange)
            );

            // 감자와의 거리 검사
            if (Vector2.Distance(randomPosition, potato.position) >= minDistanceFromPotato)
            {
                return randomPosition;
            }
        }

        return Vector2.zero; // 유효한 위치를 찾지 못하면 (0,0) 반환
    }
}
