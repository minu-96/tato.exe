using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider timerSlider;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;

    [Header("Restart Confirm")]
    [SerializeField] private GameObject confirmPanel;     // 비활성 상태로 둠
    [SerializeField] private GameManager gameManager;

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (confirmPanel != null)  confirmPanel.SetActive(false);
    }

    public void UpdateScore(int score) { scoreText.text = "" + score; }

    public void UpdateTimer(float currentTime, float maxTime)
    {
        timerSlider.maxValue = maxTime;
        timerSlider.value = currentTime;
    }

    public void ShowGameOver(int finalScore)
    {
        gameOverPanel.SetActive(true);
        finalScoreText.text = "Score : " + finalScore;
    }

    public void HideGameOver() { gameOverPanel.SetActive(false); }

    // ── 다시하기 버튼 흐름 ──
    public void OnRestartButtonClicked()       // 다시하기 버튼 OnClick
    {
        confirmPanel.SetActive(true);
    }

    public void OnConfirmYes()                  // "예" 버튼
    {
        confirmPanel.SetActive(false);
        gameManager.RestartCurrentGame();
    }

    public void OnConfirmNo()                   // "아니오" 버튼
    {
        confirmPanel.SetActive(false);
    }
}