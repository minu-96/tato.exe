using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Close_UI : MonoBehaviour
{
    public GameObject ui; // UI 창 (Canvas)
    public Pause_Game pauseGameScript; // Pause_Game 스크립트 참조

    public void CloseUI()
    {
        if (ui != null)
        {
            ui.SetActive(false); // UI 닫기

            // pauseGame이 닫혔으면 타임스케일 복구
            if (pauseGameScript != null && !pauseGameScript.pauseGame.activeSelf)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
