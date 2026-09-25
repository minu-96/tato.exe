using TatoGames.Launcher;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    /// <summary>
    /// 나가기 = 런처로 복귀 (창 크기도 런처 기준으로 되돌아간다).
    /// 런처가 빌드에 없으면 LauncherTransition이 알아서 앱 종료로 대체한다.
    /// </summary>
    public void Quit()
    {
        LauncherTransition.ReturnToLauncher();
    }

    public void StartGame(string scene)
    {
        SceneManager.LoadScene(scene);
    }
}
