using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 게임 실행 버튼. 대상 씬·가정 해상도·로딩 라벨을 들고, 클릭 시 LauncherTransition에
    /// 넘긴다. 미니게임마다 창 해상도가 다르므로(늘어나라 1280×720 / 모아모아·밭의생존자
    /// 1920×1080 추정) 값은 인스펙터에서 게임별로 확정한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class GameLaunchButton : MonoBehaviour
    {
        [Tooltip("Build Settings에 등록된 씬 이름")]
        public string sceneName;

        [Tooltip("이 게임이 가정하는 창 해상도 — 참고용. 실제 화면 크기는 설정 탭(DisplaySettings)이 전역 관리한다")]
        public int width = 1280;
        public int height = 720;
        public bool fullscreen = false;

        [Tooltip("로딩 화면에 표시할 실행 파일명 (예: 밭의_생존자.exe)")]
        public string exeLabel = "tato.exe";

        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Launch);
        }

        public void Launch()
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning($"[TatoGames] {name}: sceneName 미설정 — 전환 취소 (인스펙터에서 대상 씬 지정)");
                return;
            }

            // 설치되지 않은 프로그램은 실행할 수 없다 (§4 저장공간)
            var entry = StorageData.FindByScene(sceneName);
            if (entry != null && !StorageData.IsInstalled(entry.id))
            {
                Debug.Log($"[TatoGames] {entry.displayName} 미설치 — 상점에서 설치하세요");
                return;
            }
            LauncherTransition.Request(sceneName, width, height, fullscreen, exeLabel);
        }
    }
}
