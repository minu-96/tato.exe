using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Retry_Button_Pause : MonoBehaviour
{
    private int amount = 0;
    public void RetryGame(string InGame0)
    {
        Time.timeScale = 1f;
        
        //Coin_Value.instance.LoadCoin(out amount);
        //Total_Coins.Instance.RetryGameCoin(amount);
        ScoreManager.instance.SetScore();
        RoundStateManager.Instance.SaveState(amount);
        EndRoundManager.Instance.ResetRound();
        Coin_Value.instance.SetCoinValue();
        SceneManager.LoadScene(InGame0);
    }
}
