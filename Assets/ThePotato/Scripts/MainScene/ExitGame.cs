using TatoGames.Launcher;
using UnityEngine;

public class ExitGame : MonoBehaviour
{
    /// <summary>
    /// 버튼 클릭 시 호출. 나가기 = 런처로 복귀 (창 크기도 런처 기준으로 되돌아가고,
    /// 이 게임이 씬 밖에 띄워둔 상주 오브젝트 — 코인·타이머·라운드 상태 — 도 정리된다).
    /// 런처가 빌드에 없으면 LauncherTransition이 알아서 앱 종료로 대체한다.
    /// </summary>
    public void QuitGame()
    {
        LauncherTransition.ReturnToLauncher();
    }
}
