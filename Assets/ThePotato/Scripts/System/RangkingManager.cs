using UnityEngine.UI;
using UnityEngine; // Unity의 기본 기능 (GameObject, Transform 등)
using System.Collections; // 코루틴 및 IEnumerator 관련 기능
using System.Collections.Generic; // List, Dictionary 같은 컬렉션 사용 시 필요
using Unity.VisualScripting;

public class RangkingManager : MonoBehaviour
{
    public Text rankingText;

    private void Start()
    {
        ShowRanking();
    }

    void ShowRanking()
    {
        //rankingText.text = "\n";
        for (int i = 0; i < 10; i++)
        {
            int score = PlayerPrefs.GetInt("HighScore" + i, 0);
            rankingText.text += $"{i + 1}위: {score} 점\n";
        }
    }
}
/*

public static ScoreManager instance;
public int currentScore = 0;

private void Awake()
{
    if (instance == null)
    {
        instance = this;
    }
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
        scores[i] = PlayerPrefs.GetInt("Coins" + i, 0);
    }

    // 새로운 점수 추가
    scores[highScoresCount - 1] = currentScore;

    // 내림차순 정렬
    System.Array.Sort(scores);
    System.Array.Reverse(scores);

    // 다시 저장
    for (int i = 0; i < highScoresCount; i++)
    {
        PlayerPrefs.SetInt("Coins" + i, scores[i]);
    }

    PlayerPrefs.Save();
}
*/