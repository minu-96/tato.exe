using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class Eat_Coin : MonoBehaviour
{
    public int scoreValue = 1; // 코인을 먹으면 증가하는 점수
    public int coinValue = 1;
    

    private void Start()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Potato")) // 플레이어와 충돌했을 때
        {
            
            ScoreManager.instance.AddScore(scoreValue); // 점수 추가
            Coin_Value.instance.AddCoin(coinValue); // 코인 추가
            Set_Coin_Point.Instance.AddCoinUI();
            Destroy(gameObject); // 코인 제거
        }
    }

    
}
