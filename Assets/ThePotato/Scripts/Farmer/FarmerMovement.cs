using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FarmerMovement : MonoBehaviour
{

    private GameObject target; // 오브젝트 1의 게임 오브젝트

    public Transform[] waypoints; // 경로 점(waypoint)을 저장하는 배열
    public float holeSpeed = 1f; // 구덩이 속도
    public float defaultSpeed = 1f; // 기본 이동 속도
    public float speed = 2f; // 이동 속도
    public float chaseSpeed = 4f; // 추적 시 속도
    public float detectionRange = 5f; // 감자를 감지할 범위
    public Transform potato; // 감자의 Transform

    private int currentWaypointIndex = 0; // 현재 이동 중인 waypoint
    private bool isChasing = false; // 추적 중인지 여부

    public GameObject exclamationMark;

    public AudioClip alertSound;
    private AudioSource audioSource;

    private int currentStage;

    // 감자가 이미 잡혔는가 — 농부가 여럿이거나 씬이 넘어가기 전에 트리거가 한 번 더 와도
    // 보상·씬 전환이 한 번만 일어나게. 모든 농부가 같이 보도록 static, 라운드 씬이 뜰 때 초기화.
    private static bool potatoCaught;


    private void Start()
    {
        potatoCaught = false;
        RoundStateManager.Instance.LoadState(out int PoisonStage);
        currentStage = PoisonStage;

        audioSource = GetComponent<AudioSource>();

        // 씬에서 오브젝트 1 찾기
        target = GameObject.FindWithTag("Potato");
    }

    void Update()
    {
        // 씬에서 오브젝트 1 찾기
        target = GameObject.FindWithTag("Potato");

            if (isChasing)
            {
                // 감자를 추적
                ChasePotato();
            }
            else
            {
                // 정해진 경로를 따라 이동
                MoveAlongWaypoints();

                // 감자 감지
                DetectPotato();
            }

    }

    void MoveAlongWaypoints()
    {
        // 현재 목표 Waypoint까지 이동
        Transform targetWaypoint = waypoints[currentWaypointIndex];
        Vector2 direction = (targetWaypoint.position - transform.position).normalized;
        transform.position = Vector2.MoveTowards(transform.position, targetWaypoint.position, speed * Time.deltaTime);


        // 목표 Waypoint에 도달했는지 확인
        if (Vector2.Distance(transform.position, targetWaypoint.position) < 0.1f)
        {
            // 다음 Waypoint로 이동
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Length)
            {
                currentWaypointIndex = 0; // Waypoint 반복
            }
        }
    }

    void DetectPotato()
    {

        if (target != null)
        {
            float distance = Vector2.Distance(transform.position, target.transform.position);

            if (distance < detectionRange)
            {
                isChasing = true; // 추적 시작
                audioSource.PlayOneShot(alertSound);
                exclamationMark.SetActive(true); // 느낌표 활성화

                
            }
        }

    }

    void ChasePotato()
    {

        // 타겟 방향으로 이동
        Vector2 direction = (target.transform.position - transform.position).normalized;
        transform.position = Vector2.MoveTowards(transform.position, target.transform.position, speed * Time.deltaTime);

        if (target != null)
        {
            float distance = Vector2.Distance(transform.position, target.transform.position);

            // 감자를 놓쳤는지 확인
            if (distance > detectionRange)
            {
                isChasing = false; // 추적 종료
                exclamationMark.SetActive(false); // 느낌표 ㅂㅣ활성화
            }
        }

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Potato"))
        {
            if (potatoCaught) return;
            potatoCaught = true;

            // 생존한 라운드에 따라 '밭의 생존자' 출처 카드를 수급한다 (§8).
            // 라운드는 InGame0~6 씬으로 관리되므로, InGameN에서 잡혔다면 끝까지 버틴 라운드는 N개다
            // (예전엔 N+1이라 잡힌 라운드까지 생존으로 쳤다 — 7라운드에서 잡혀도 완주와 같은 보상).
            // 씬을 넘기기 전에 불러야 활성 씬이 InGameN 이다.
            TatoGames.CardGame.TatoReward.Grant(
                TatoGames.CardGame.AcquireSource.FieldSurvivor,
                TatoGames.CardGame.TatoReward.SceneNumber());

            if(currentStage >= 3)
            {
                Coin_Value.instance.SaveCoin();
                Debug.Log("감자가 던져졌습니다! 게임 종료!");
                SceneManager.LoadScene("GameOver2");
                ScoreManager.instance.SaveScore();
            }
            else
            {
                Debug.Log("감자가 잡혔습니다! 게임 종료!");
                SceneManager.LoadScene("GameOver0");
                ScoreManager.instance.SaveScore();
            }
            
        }

        else if (collision.gameObject.CompareTag("Hole"))
        {
            speed = holeSpeed;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Hole"))
        {
            speed = defaultSpeed;
        }
    }
}

    

