using UnityEngine.UI;
using UnityEngine; // Unity의 기본 기능 (GameObject, Transform 등)
using System.Collections; // 코루틴 및 IEnumerator 관련 기능
using System.Collections.Generic; // List, Dictionary 같은 컬렉션 사용 시 필요


public class UI_TIME : MonoBehaviour
{

    public Text timeText;
    public float updateInterval = 1f; //생성 시간
    private int hour = 8;
    private int minute = 0;
    public GameObject endRoundPanel;

    private void Start()
    {
        StartCoroutine(UpdateTime());
        timeText.text = $"{hour:D2}:{minute:D2}";

    }

    private IEnumerator UpdateTime()
    {
        while (hour < 18) // 18:00까지 반복
        {
            yield return new WaitForSeconds(updateInterval); // 1초 대기

            minute += 20; // 10분 증가
            if (minute >= 60) // 60분이 되면 시간 증가
            {
                minute = 0;
                hour++;
            }
            /*
            if (hour == 13)
            {
                AAA();
            }
            */
            timeText.text = $"{hour:D2}:{minute:D2}"; // 08:00 형식으로 표시
        }
        if (hour == 18)
        {
            AAA();
        }

    }

    private void AAA()
    {
        EndRoundManager.Instance.EndRound(endRoundPanel);
    }
}
