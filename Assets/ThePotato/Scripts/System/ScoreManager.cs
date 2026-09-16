using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance;
    public int currentScore = 0;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void SetScore()
    {
        currentScore = 0;
    }

    public void AddScore(int amount)
    {
        currentScore += amount;
    }

    public void SaveScore()
    {
        int highScoresCount = 10; // 저장할 랭킹 개수
        int[] scores = new int[highScoresCount];

        // 기존 랭킹 데이터 불러오기
        for (int i = 0; i < highScoresCount; i++)
        {
            scores[i] = PlayerPrefs.GetInt("HighScore" + i, 0);
        }

        // 새로운 점수 추가
        scores[highScoresCount - 1] = currentScore;

        // 내림차순 정렬
        System.Array.Sort(scores);
        System.Array.Reverse(scores);

        // 다시 저장
        for (int i = 0; i < highScoresCount; i++)
        {
            PlayerPrefs.SetInt("HighScore" + i, scores[i]);
        }

        PlayerPrefs.Save();
    }

    
}
