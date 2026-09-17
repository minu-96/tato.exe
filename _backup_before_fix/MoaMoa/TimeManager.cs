using UnityEngine;

public class TimeManager : MonoBehaviour
{
    [Header("Time Settings")]
    [SerializeField] private float maxTime = 30f;

    [Header("References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private GameManager gameManager;

    private float currentTime;
    private bool isRunning;

    private void Update()
    {
        if (!isRunning)
            return;

        currentTime -= Time.deltaTime;
        currentTime = Mathf.Max(currentTime, 0f);

        uiManager.UpdateTimer(currentTime, maxTime);

        if (currentTime <= 0f)
        {
            isRunning = false;
            gameManager.EndGameFromTimer();
        }
    }

    public void StartTimer()
    {
        currentTime = maxTime;
        isRunning = true;
        uiManager.UpdateTimer(currentTime, maxTime);
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public void ResetTimer()
    {
        currentTime = maxTime;
        uiManager.UpdateTimer(currentTime, maxTime);
    }

    public float GetCurrentTime()
    {
        return currentTime;
    }

    public float GetMaxTime()
    {
        return maxTime;
    }

    public void AddTime(float amount)
    {
        currentTime += amount;
        currentTime = Mathf.Min(currentTime, maxTime);
        uiManager.UpdateTimer(currentTime, maxTime);
    }

    public void ReduceTime(float amount)
    {
        currentTime -= amount;
        currentTime = Mathf.Max(currentTime, 0f);
        uiManager.UpdateTimer(currentTime, maxTime);

        if (currentTime <= 0f)
        {
            isRunning = false;
            gameManager.EndGameFromTimer();
        }
    }
}