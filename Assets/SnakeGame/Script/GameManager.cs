using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject applePrefab;
    private GameObject currentApple;

    public Vector2Int mapMin = new Vector2Int(-9, -9);
    public Vector2Int mapMax = new Vector2Int(9, 9);

    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI scoreText1;
    public GameObject gameOverPanel;
    public GameObject pausePanel;
    private int score = 0;
    private bool isPaused = false;
    public bool IsIntroPlaying { get; private set; } = false;

    public void SetIntroPlaying(bool value) { IsIntroPlaying = value; }

    void Awake() { Instance = this; }

    void Start()
    {
        gameOverPanel.SetActive(false);
        pausePanel.SetActive(false);
        UpdateScoreUI();
        SpawnApple();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameOverPanel.activeSelf) return;
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
    }

    public void SpawnApple()
    {
        if (currentApple != null) Destroy(currentApple);

        List<Vector2> occupied = new List<Vector2>();
        SnakeHead head = FindObjectOfType<SnakeHead>();
        if (head != null) occupied = head.GetOccupiedPositions();

        List<Vector2> available = new List<Vector2>();
        for (int x = mapMin.x; x <= mapMax.x; x++)
            for (int y = mapMin.y; y <= mapMax.y; y++)
            {
                Vector2 p = new Vector2(x, y);
                if (!occupied.Contains(p)) available.Add(p);
            }

        if (available.Count == 0) { Debug.Log("게임 클리어!"); return; }

        Vector2 pos = available[Random.Range(0, available.Count)];
        currentApple = Instantiate(applePrefab, pos, Quaternion.identity);
    }

    public void AddScore() { score++; UpdateScoreUI(); }
    void UpdateScoreUI() { scoreText.text = "x " + score;
        scoreText1.text = "x " + score; }

    public void GameOver()
    {
        gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void GoTitle()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Title");
    }
}