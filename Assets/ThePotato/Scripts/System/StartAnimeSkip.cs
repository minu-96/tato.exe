using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartAnimeSkip : MonoBehaviour
{
    public string InGame;

    // Update is called once per frame
    void Update()
    {
        
        if(Input.GetKeyDown(KeyCode.Space))
        {
            AnimeSkip(InGame);
        }
    }

    public void AnimeSkip(string InGame0)
    {
        SceneManager.LoadScene(InGame0);
    }
}
