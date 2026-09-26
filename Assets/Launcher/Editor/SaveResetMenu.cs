using TatoGames.CardGame;
using TatoGames.Launcher;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 테스트용 세이브 초기화. 메뉴: TatoGames ▸ Reset Save.
///
/// tato.exe가 쓰는 키만 골라서 지운다 — 토인·카드·덱·런(전투·보상 포함)·구매/설치·업적·최고 스테이지.
/// 해상도 설정과 미니게임 자체 기록(밭의 생존자 코인·랭킹)은 남긴다.
/// <b>PlayerPrefs.DeleteAll()은 쓰지 않는다</b> — 한 게임의 초기화가 다른 게임 세이브까지 지운 적이 있다.
/// </summary>
public static class SaveResetMenu
{
    [MenuItem("TatoGames/Reset Save (세이브 초기화)")]
    public static void ResetSave()
    {
        if (!EditorUtility.DisplayDialog("세이브 초기화",
                "토인·보유 카드·덱·진행 중인 런·구매/설치·업적·최고 스테이지를 지웁니다.\n" +
                "해상도 설정과 미니게임 자체 기록은 남습니다.\n\n되돌릴 수 없습니다.",
                "초기화", "취소"))
            return;

        PlayerData.Reset();          // 토인 · 카드 인스턴스 · 덱
        RunState.ClearSave();        // 런 · 전투 · 보상
        StorageData.ResetOwnership();
        Achievements.ResetAll();
        PlayerPrefs.DeleteKey(RunState.BestStageKey);
        PlayerPrefs.DeleteKey(TatoReward.PendingKey);
        PlayerPrefs.Save();
        Debug.Log("[TatoGames] 세이브 초기화 완료 — 다음 실행 때 시작덱 8장으로 시작합니다");
    }
}
