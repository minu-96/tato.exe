using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartNextRoundManager : MonoBehaviour
{
    public int mutationStage;
    public GameObject endRoundPanel; // 라운드 종료 화면 (UI 패널)

    /// <summary>전 라운드 수 (InGame0~6). 완주 보상의 기록값.</summary>
    const int FullClearRounds = 7;

    public void StartNextRound(string scene)
    {

        // 데이터 불러오기
        RoundStateManager.Instance.LoadState(out int PoisonStage);
        //Coin_Value.instance.SaveCoin();

        // 저장된 데이터를 현재 상태에 반영
        mutationStage = PoisonStage;
        Debug.Log($"불러오기 완료: 단계 {mutationStage}");

        // 마지막 라운드까지 버텨서 결과 화면(GameOver*)으로 가는 경우 = 완주.
        // 잡혔을 때만 카드를 주던 탓에 완주하면 오히려 0장이었다 → 완주 = 전 라운드(7) 생존으로 지급.
        if (scene.StartsWith("GameOver"))
            TatoGames.CardGame.TatoReward.Grant(TatoGames.CardGame.AcquireSource.FieldSurvivor, FullClearRounds);

        // UI 닫기 및 다음 라운드 준비
        endRoundPanel.SetActive(false); // 패널 비활성화
        SceneManager.LoadScene(scene);

        Time.timeScale = 1f; // 게임 재개

    }
    public void OverRound(string scene)
    {
        RoundStateManager.Instance.LoadState(out int PoisonStage);
        if (PoisonStage == 3)
        {
            SceneManager.LoadScene(scene);
        }
    }
}
