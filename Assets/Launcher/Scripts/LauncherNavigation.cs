using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TatoGames.Launcher
{
    /// <summary>
    /// 사이드바 버튼 ↔ 콘텐츠 패널 토글.
    /// navButtons[i]를 누르면 panels[i]만 보이고 나머지는 숨긴다.
    /// (홈/미니게임/감자창고/상점/패치노트/업적/설정 순)
    /// </summary>
    public class LauncherNavigation : MonoBehaviour
    {
        public List<Button> navButtons = new();
        public List<GameObject> panels = new();

        [Tooltip("시작 시 보여줄 화면 인덱스 (0 = 홈)")]
        public int startIndex = 0;

        void Start()
        {
            for (int i = 0; i < navButtons.Count; i++)
            {
                int idx = i; // 클로저 캡처 주의
                if (navButtons[i] != null)
                    navButtons[i].onClick.AddListener(() => Show(idx));
            }
            Show(startIndex);
        }

        public void Show(int index)
        {
            for (int i = 0; i < panels.Count; i++)
                if (panels[i] != null)
                    panels[i].SetActive(i == index);
        }
    }
}
