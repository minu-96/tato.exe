using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Total_Coins : MonoBehaviour
{
    public static Total_Coins Instance;
    private int coins = 0;
    //private int beforecoin = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 데이터 유지
            coins = PlayerPrefs.GetInt("TotalCoin", 0);
        }

        else
        {
            Destroy(gameObject); // 중복된 인스턴스 제거
        }
    }

    public void SaveTotalCoin(int amount)
    {
        //coins -= beforecoin;
        coins += amount;
        Debug.Log($"저장 코인 갯수 {coins}");
        PlayerPrefs.SetInt("TotalCoin", coins);
        PlayerPrefs.Save();
        //beforecoin = coins;

    }

    public void RetryGameCoin(int amount)
    {
        coins -= amount;
        Debug.Log($"저장 코인 갯수 {coins}");
        PlayerPrefs.SetInt("TotalCoin", coins);
        PlayerPrefs.Save();
    }

    public void ZeroCoin()
    {
        coins = 0;
    }
}
