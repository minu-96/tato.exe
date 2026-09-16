using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Change_Scene_Start : MonoBehaviour
{
    private int resetlevel = 0;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ChangeSceneStart(string GameStartScene)
    {
        Coin_Value.instance.SetCoinValue();
        ScoreManager.instance.SetScore();
        RoundStateManager.Instance.SaveState(resetlevel);
        EndRoundManager.Instance.ResetRound();
        SceneManager.LoadScene(GameStartScene);
    }
}
