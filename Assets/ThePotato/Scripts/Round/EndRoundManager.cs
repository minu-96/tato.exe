using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndRoundManager : MonoBehaviour
{
    public static EndRoundManager Instance;

    public GameObject endRoundPanel; // 라운드 종료 화면 (UI 패널)
    public int currentRound = 0; // 현재 라운드 번호

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // 중복된 인스턴스 제거
        }
    }

    private void Start()
    {
        endRoundPanel.SetActive(false); // 패널 비활성화
    }

    public void EndRound(GameObject EndRoundPanel)
    {
        Debug.Log("1!");
        EndRoundPanel.SetActive(true);
        
        Debug.Log("2");
        //roundText.text = "Round " + currentRound + " Completed!"; // 메시지 업데이트
        // UI 활성화
        currentRound++; // 라운드 번호 증가
        Debug.Log("3");
        Time.timeScale = 0f; // 일시 정지
        Debug.Log("4");
    }

    public void ResetRound()
    {
        currentRound = 0;
    }
    
}