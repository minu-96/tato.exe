using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoundStateManager : MonoBehaviour
{
    public static RoundStateManager Instance; // 싱글톤 패턴

    // 저장할 데이터
    public int mutationStage; // 독감자 단계

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

    // 현재 상태 저장
    public void SaveState(int stage)
    {
        mutationStage = stage; // 기존 데이터를 복사

        Debug.Log($"상태 저장 완료: 단계 {stage}");
    }

    // 저장된 상태 불러오기
    public void LoadState(out int stage)
    {
        stage = mutationStage; // 복사된 데이터 반환

        Debug.Log($"상태 불러오기: 단계 {stage}");
    }
}
