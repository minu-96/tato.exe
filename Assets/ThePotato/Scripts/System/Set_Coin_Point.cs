using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Set_Coin_Point : MonoBehaviour
{
    public static Set_Coin_Point Instance;

    public GameObject coinPrefab; // 생성할 코인 프리팹
    public float spawnInterval = 0f; // 코인 생성 주기 (초)
    public float coinLifetime = 0f; // 코인이 사라지는 시간 (초)
    public Vector2 spawnAreaMin; // 코인 생성 최소 좌표
    public Vector2 spawnAreaMax; // 코인 생성 최대 좌표

    public Text text;
    private int coinValue = 1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        Coin_Value.instance.LoadCoin(out coinValue);
        text.text = $"{coinValue}";

        StartCoroutine(SpawnCoins());
        SpawnCoin();
        
    }

    IEnumerator SpawnCoins()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnCoin();
        }
    }

    void SpawnCoin()
    {
        Vector2 spawnPosition = new Vector2(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y)
        );

        GameObject newCoin = Instantiate(coinPrefab, spawnPosition, Quaternion.identity);
        Destroy(newCoin, coinLifetime); // 일정 시간이 지나면 삭제
        
    }

    public void AddCoinUI()
    {
        Coin_Value.instance.LoadCoin(out coinValue);
        text.text = $"{coinValue}";
    }
}