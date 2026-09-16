using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Unity.VisualScripting;
using System.Collections.Generic; // List, Dictionary 같은 컬렉션 사용 시 필요

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;
    private float startTime;
    private bool isRunning = false;
    public Text timeText; // UI에 표시할 Text

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 데이터 유지
        }
        else
        {
            Destroy(gameObject); // 중복된 인스턴스 제거
        }
    }

    void Start()
    {
        StartTimer(); // 타이머 시작
    }

    void Update()
    {
        if (isRunning)
        {
            float elapsedTime = Time.time - startTime;
            timeText.text = FormatTime(elapsedTime);
        }
    }

    // 타이머 시작
    public void StartTimer()
    {
        startTime = Time.time;
        isRunning = true;
    }

    // 타이머 정지 후 시간 반환
    public string StopTimer()
    {
        isRunning = false;
        float finalTime = Time.time - startTime;
        return FormatTime(finalTime);
    }

    // 시간을 "10m 42s 379ms" 형식으로 변환
    private string FormatTime(float timeInSeconds)
    {
        int minutes = (int)(timeInSeconds / 60);
        int seconds = (int)(timeInSeconds % 60);
        int milliseconds = (int)((timeInSeconds - (minutes * 60) - seconds) * 1000);

        return string.Format("{0}m {1}s {2}ms", minutes, seconds, milliseconds);
    }
}
