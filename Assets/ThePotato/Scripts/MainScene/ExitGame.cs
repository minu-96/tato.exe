using UnityEngine;

public class ExitGame : MonoBehaviour
{
    // 버튼 클릭 시 호출될 함수
    public void QuitGame()
    {
        // 에디터 환경에서는 UnityEditor를 통해 에디터를 멈춥니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 빌드된 게임에서는 애플리케이션 종료
        Application.Quit();
#endif
    }
}