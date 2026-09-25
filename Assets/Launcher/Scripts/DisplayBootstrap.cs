using UnityEngine;
using UnityEngine.SceneManagement;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 씬이 바뀔 때마다 그 씬에 맞는 화면 설정을 적용하고, 미니게임 타이틀에는
    /// 해상도 ◀▶ 위젯을 자동으로 띄운다.
    ///
    /// <b>씬 배선이 전혀 필요 없다.</b> 어떤 경로로 씬이 로드되든(런처 전환, 미니게임 내부
    /// 씬 이동, 다시하기) 항상 걸린다.
    /// </summary>
    public static class DisplayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            DisplaySettings.ApplyFor(TargetOf(SceneManager.GetActiveScene().name));
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var target = TargetOf(scene.name);
            DisplaySettings.ApplyFor(target);

            // 미니게임 타이틀에서만 해상도 조절 위젯을 붙인다 (인게임 중에는 방해되므로 제외)
            if (IsMinigameTitle(scene.name))
                MinigameDisplayStepper.Spawn(target);
        }

        /// <summary>씬 이름 → 화면 설정 단위. 모르는 씬은 런처 취급.</summary>
        public static DisplayTarget TargetOf(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return DisplayTarget.Launcher;

            if (sceneName == "Battle") return DisplayTarget.MainGame;
            if (sceneName.StartsWith("Snake")) return DisplayTarget.Neulteona;
            if (sceneName.StartsWith("MoaMoa")) return DisplayTarget.MoaMoa;

            // 밭의 생존자 — 씬 이름이 제각각이라 목록으로 판별
            if (sceneName.StartsWith("InGame") || sceneName.StartsWith("GameOver") ||
                sceneName.StartsWith("GameStart") || sceneName.StartsWith("GameMenu") ||
                sceneName == "Anime0")
                return DisplayTarget.FieldSurvivor;

            return DisplayTarget.Launcher;
        }

        static bool IsMinigameTitle(string sceneName) =>
            sceneName == "SnakeTitle" || sceneName == "MoaMoaTitle" || sceneName == "GameStart0";
    }
}
