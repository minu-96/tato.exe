using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pause_Game : MonoBehaviour
{
    public GameObject pauseGame;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (pauseGame.activeSelf) // 이미 UI가 켜져 있다면
            {
                ResumeGame();
            }
            else // UI가 꺼져 있다면
            {
                Pause();
            }
        }
    }

    public void Pause()
    {
        pauseGame.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        pauseGame.SetActive(false);
        Time.timeScale = 1f;
    }
}
