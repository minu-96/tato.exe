using System.Diagnostics;
using System.IO;
using UnityEngine;

public class GameExit : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void OnApplicationQuit()
    {
        LaunchLauncher();
    }

    void LaunchLauncher()
    {
#if UNITY_STANDALONE_OSX
        // dataPath = Game1.app/Contents
        // Parent 1번 = Game1.app
        // Parent 2번 = Games/
        // Parent 3번 = GameHub/  ← 런처 위치
        string dataPath = Application.dataPath;
        string gameHubDir = Directory.GetParent(dataPath).Parent.Parent.FullName;
        string launcherPath = Path.Combine(gameHubDir, "tato.app");

        if (Directory.Exists(launcherPath))
            Process.Start("open", "\"" + launcherPath + "\"");

#elif UNITY_STANDALONE_WIN
        string gameDir = Directory.GetParent(Application.dataPath).FullName;
        string launcherPath = Path.GetFullPath(
            Path.Combine(gameDir, "..", "..", "tato.exe")
        );

        if (File.Exists(launcherPath))
            Process.Start(launcherPath);
#endif
    }
}