using UnityEngine;
using UnityEngine.UI;

public class Reset_Data : MonoBehaviour
{
    const int RankingCount = 10;   // ScoreManager 랭킹 칸 수와 같다

    private void Start()
    {
        Total_Coins.Instance.ZeroCoin();

        // 밭의 생존자 기록만 지운다. 예전엔 PlayerPrefs.DeleteAll()이라서,
        // 런처에 합쳐진 뒤로는 tato.exe 세이브(토인·카드·덱·런·업적) 전체가 같이 날아갔다.
        PlayerPrefs.DeleteKey("TotalCoin");
        for (int i = 0; i < RankingCount; i++)
        {
            PlayerPrefs.DeleteKey("HighScore" + i);
            PlayerPrefs.DeleteKey("Coins" + i);   // 옛 랭킹 키
        }
        PlayerPrefs.Save();
    }
}
