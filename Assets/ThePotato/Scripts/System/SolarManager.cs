using UnityEngine.UI;
using UnityEngine; // Unity의 기본 기능 (GameObject, Transform 등)
using System.Collections; // 코루틴 및 IEnumerator 관련 기능
using System.Collections.Generic; // List, Dictionary 같은 컬렉션 사용 시 필요

public class SolarManager : MonoBehaviour
{
    public Slider mutationGauge; // 독감자 게이지 UI Slider
    public float mutationSpeed = 10f; // 게이지 상승 속도
    private bool isInSunlight = false; // 햇볕에 감자가 있는지 확인
    private float currentGauge = 0f; // 현재 게이지 값
    public Text stageText; // 단계 표시 텍스트
    public int mutationStage = 0; // 현재 단계 (예: 0 = 일반, 1 = 독감자 1단계 ...)

    public GameObject currentPotato; // 현재 플레이어 감자
    public GameObject PotatoPrefabs; // 마지막 단계에서 생성할 새 감자 프리팹
    public Transform spawnPoint; // 새 감자가 생성될 위치
    public GameObject Solar; //햇볕 오브젝트

    [SerializeField] private GameObject mutationEffectPrefab; // 이펙트 프리펩 연결
    [SerializeField] private Transform effectSpawnPoint; // 이펙트 생성 위치
    private Animator particleAnimator;

    void Start()
    {
        RoundStateManager.Instance.LoadState(out mutationStage);
        Debug.Log($"asdf{mutationStage}");

        if (mutationStage >= 3)
        {
            Destroy(Solar);
        }

        if (mutationStage < 1)
        {
            stageText.text = "0/3";
        }
        else if (mutationStage < 2)
        {
            stageText.text = "1/3"; // 단계 텍스트 업데이트
        }
        else if (mutationStage < 3)
        {
            stageText.text = "2/3";
        }
        else if (mutationStage < 4)
        {
            stageText.text = "3/3";
        }
        else
            stageText.text = "";
        //Debug.Log($"상태 저장 완료: 단계 {mutationStage}");
    }

    private void Update()
    {
        if (isInSunlight)
        {

            // 게이지 증가
            currentGauge += mutationSpeed * Time.deltaTime;
            mutationGauge.value = currentGauge;

            // 단계 변경
            if (currentGauge >= mutationGauge.maxValue)
            {
                MutatePotato(); // 감자를 독감자로 변환
            }

        }

        if (spawnPoint == null)
        {
            Debug.LogError("spawnPoint가 설정되지 않았습니다!");
        }

        

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {

        if (collision.CompareTag("Sunlight")) // 햇볕 트리거와 충돌
        {

            isInSunlight = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Sunlight")) // 햇볕 영역에서 벗어남
        {

            isInSunlight = false;
        }
    }

    private void MutatePotato()
    {

            mutationStage++; // 다음 단계로

        // 상태 저장
        RoundStateManager.Instance.SaveState(mutationStage);
        Debug.Log($"상태 저장 완료: 단계 {mutationStage}");

            currentGauge = 0f; // 게이지 초기화
            mutationGauge.value = 0;
            mutationGauge.maxValue *= 2; // 다음 단계 게이지 증가
            
            PlayMutationEffect();

        if (mutationStage == 1)
            stageText.text = "1/3"; // 단계 텍스트 업데이트
        if (mutationStage == 2)
            stageText.text = "2/3";
        if (mutationStage == 3)
            stageText.text = "3/3";
    }
    
    private void TransformToNextStage()
    {
        /* Debug.Log("단계가 변경됨: " + mutationStage);

        //yield return new WaitForSeconds(0.6f); // 1초 대기

        // 기존 캐릭터 제거
        if (currentPotato != null)
        {
            Destroy(currentPotato);
            Destroy(Solar);
        }

        // 새로운 캐릭터 생성
        currentPotato = Instantiate(PotatoPrefabs, spawnPoint.position, Quaternion.identity);
        */

        //햇빛 오브젝트 제거
        Destroy(Solar);

        Debug.Log("단계가 변경됨: " + mutationStage);

        if (currentPotato != null)
        {
            Debug.Log("기존 감자 삭제: " + currentPotato.name);
            Destroy(currentPotato);
        }
        else
        {
            Debug.LogWarning("삭제할 감자가 없습니다.");
        }

        if (PotatoPrefabs == null)
        {
            Debug.LogError("PotatoPrefabs가 설정되지 않았습니다!");
            return;
        }

        GameObject newPotato = Instantiate(PotatoPrefabs, spawnPoint.position, Quaternion.identity);
        if (newPotato != null)
        {
            Debug.Log("새 감자 생성됨: " + newPotato.name);
            currentPotato = newPotato;  // 새로운 감자를 현재 감자로 설정
        }
        else
        {
            Debug.LogError("새로운 감자 생성 실패!");
        }

        Debug.Log($"스폰 위치: {spawnPoint.position}");

        
    }

    private void PlayMutationEffect()
    {
        
        if (mutationEffectPrefab != null && effectSpawnPoint != null)
        {
            Vector3 spawnPosition = effectSpawnPoint.position;
            spawnPosition.z = 0; // Z값을 0으로 설정

            // 이펙트 생성 후 감자의 자식으로 설정
            GameObject effect = Instantiate(mutationEffectPrefab, effectSpawnPoint.position, Quaternion.identity);
            effect.transform.SetParent(currentPotato.transform); // 감자와 동기화

            particleAnimator = effect.GetComponent<Animator>();
            particleAnimator.transform.position = transform.position;

            // 애니메이션 재생
            particleAnimator.SetTrigger("Play");

            // 일정 시간이 지나면 이펙트 제거
            Destroy(effect, 0.5f);
            Debug.Log("변이 이펙트 재생");
            StartCoroutine(DelayedAction()); //코루틴 호출
        }
        else
        {
            Debug.LogWarning("MutationEffectPrefab 또는 EffectSpawnPoint가 설정되지 않았습니다.");
        }
        /*

        if (mutationEffectPrefab != null && effectSpawnPoint != null)
        {
            GameObject effect = Instantiate(mutationEffectPrefab, effectSpawnPoint.position, Quaternion.identity);
            effect.transform.SetParent(currentPotato.transform);

            Animator animator = effect.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("YourAnimationName");  // 애니메이션 재생
                StartCoroutine(DestroyAfterAnimation(animator, effect));
            }
            else
            {
                Destroy(effect, 0.5f);  // 기본 삭제 시간
            }

            Debug.Log("변이 이펙트 재생");
            StartCoroutine(DelayedAction());
        }
        else
        {
            Debug.LogWarning("MutationEffectPrefab 또는 EffectSpawnPoint가 설정되지 않았습니다.");
        }*/
    }

    private IEnumerator DestroyAfterAnimation(Animator animator, GameObject effect)
    {
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        Destroy(effect);
    }

    private IEnumerator DelayedAction()
    {
        yield return new WaitForSeconds(0.6f); // 1초 대기
        Debug.Log("1초 후 실행"); // 딜레이 후 실행
        TransformToNextStage();
    }

}