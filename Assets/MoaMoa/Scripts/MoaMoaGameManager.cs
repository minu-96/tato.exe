using System.Collections.Generic;
using TatoGames.Launcher;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MoaMoaGameManager : MonoBehaviour
{
    public enum GameState
    {
        Ready,
        Playing,
        GameOver
    }

    [Header("Game Rule")]
    [SerializeField] private int targetSum = 10;
    [SerializeField] private int scorePerApple = 1;

    [Header("References")]
    [SerializeField] private CellSpawner appleSpawner;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private DragSelectionBox selectionManager;
    [SerializeField] private MoaMoaTimeManager timeManager;

    private int currentScore;
    private GameState currentState = GameState.Ready;

    private void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        // Title 씬처럼 보드 참조가 없는 곳에서는 게임을 시작하지 않음
        if (appleSpawner == null || uiManager == null || timeManager == null)
            return;

        currentScore = 0;
        currentState = GameState.Playing;

        appleSpawner.SpawnBoard();
        uiManager.HideGameOver();
        uiManager.UpdateScore(currentScore);
        timeManager.StartTimer();
    }

    public bool CanInput()
    {
        return currentState == GameState.Playing;
    }

    public void EvaluateSelection(List<Cell> selectedApples)
    {
        if (currentState != GameState.Playing)
            return;

        if (selectedApples == null || selectedApples.Count == 0)
            return;

        int sum = CalculateSum(selectedApples);

        if (sum == targetSum)
        {
            HandleSuccess(selectedApples);
        }
        else
        {
            HandleFailure(selectedApples);
        }
    }

    private int CalculateSum(List<Cell> apples)
    {
        int sum = 0;

        foreach (Cell apple in apples)
        {
            if (apple != null)
            {
                sum += apple.value;
            }
        }

        return sum;
    }

    private void HandleSuccess(List<Cell> apples)
    {
        int removedCount = 0;

        foreach (Cell apple in apples)
        {
            if (apple != null)
            {
                appleSpawner.RemoveAppleFromList(apple);
                apple.Remove();
                removedCount++;
            }
        }

        AddScore(removedCount * scorePerApple);
    }

    private void HandleFailure(List<Cell> apples)
    {
        foreach (Cell apple in apples)
        {
            if (apple != null)
            {
                apple.ResetVisual();
            }
        }
    }

    private void AddScore(int amount)
    {
        currentScore += amount;
        uiManager.UpdateScore(currentScore);
    }

    public void EndGameFromTimer()
    {
        if (currentState == GameState.GameOver)
            return;

        currentState = GameState.GameOver;
        selectionManager.ClearSelection();
        timeManager.StopTimer();

        // 점수에 따라 '모아모아' 출처 카드를 수급한다 (§8). 위 GameOver 가드 덕분에 1판 1회만 실행된다.
        TatoGames.CardGame.TatoReward.Grant(TatoGames.CardGame.AcquireSource.MoaMoa, currentScore);

        uiManager.ShowGameOver(currentScore);
    }

    public void RestartGame(string scene)
    {
        SceneManager.LoadScene(scene);
    }

    /// <summary>
    /// 나가기 = 런처로 복귀 (창 크기도 런처 기준으로 되돌아간다).
    /// 런처가 빌드에 없으면 LauncherTransition이 알아서 앱 종료로 대체한다.
    /// </summary>
    public void QuitGame()
    {
        LauncherTransition.ReturnToLauncher();
    }

    // 현재 화면 유지하면서 보드/점수/타이머만 리셋
public void RestartCurrentGame()
{
    selectionManager.ClearSelection();
    appleSpawner.ClearBoard();

    currentScore = 0;
    currentState = GameState.Playing;

    appleSpawner.SpawnBoard();
    uiManager.UpdateScore(currentScore);
    uiManager.HideGameOver();

    timeManager.ResetTimer();
    timeManager.StartTimer();
}

}