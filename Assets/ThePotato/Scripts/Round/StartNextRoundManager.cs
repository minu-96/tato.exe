using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartNextRoundManager : MonoBehaviour
{
    public int mutationStage;
    public GameObject endRoundPanel; // 라운드 종료 화면 (UI 패널)

    public void StartNextRound(string scene)
    {

        // 데이터 불러오기
        RoundStateManager.Instance.LoadState(out int PoisonStage);
        //Coin_Value.instance.SaveCoin();

        // 저장된 데이터를 현재 상태에 반영
        mutationStage = PoisonStage;
        Debug.Log($"불러오기 완료: 단계 {mutationStage}");

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
