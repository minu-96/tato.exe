using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Exit : MonoBehaviour
{
    public string menu;

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitScene(menu);
        }
    }

    public void ExitScene(string menu)
    {
        SceneManager.LoadScene(menu);
    }
}
