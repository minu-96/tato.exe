using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Chane_Scene_MainMenu : MonoBehaviour
{
    public void ChangeScene1(string TestScene0)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(TestScene0);
    }
}