using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartStage : MonoBehaviour
{
    public int mutationStage;

    public GameObject[] Potato;

    void Awake()
    {
        // 씬이 로드될 때 `OnSceneLoaded` 실행
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 구독을 풀지 않으면 이 씬이 사라진 뒤에도(런처·전투 씬에서도) 매번 불린다
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"🎮 {scene.name} 씬이 로드됨!");

        // 데이터 불러오기
        RoundStateManager.Instance.LoadState(out int PoisonStage);

        // 저장된 데이터를 현재 상태에 반영
        mutationStage = PoisonStage;
        Debug.Log($"불러오기 완료ss: 단계 {mutationStage}");

        if (mutationStage == 0)
        {
            Destroy(Potato[1]);
            Debug.Log("1단계제거");
            Destroy(Potato[2]);
            Debug.Log("2단계제거");
            Destroy(Potato[3]);
            Debug.Log("3단계제거");
        }

        else if (mutationStage == 1)
        {
            Destroy(Potato[0]);
            Debug.Log("0단계제거");
            Destroy(Potato[2]);
            Debug.Log("2단계제거");
            Destroy(Potato[3]);
            Debug.Log("3단계제거");
        }

        else if (mutationStage == 2)
        {
            Destroy(Potato[0]);
            Debug.Log("0단계제거");
            Destroy(Potato[1]);
            Debug.Log("1단계제거");
            Destroy(Potato[3]);
            Debug.Log("3단계제거");
        }

        else if (mutationStage == 3)
        {
            Destroy(Potato[0]);
            Debug.Log("0단계제거");
            Destroy(Potato[1]);
            Debug.Log("1단계제거");
            Destroy(Potato[2]);
            Debug.Log("2단계제거");
        }

    }
    
}
