//using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using System.Collections; // 코루틴 및 IEnumerator 관련 기능
using System.Collections.Generic; // List, Dictionary 같은 컬렉션 사용 시 필요

public class Set_UI_Layer : MonoBehaviour
{
    
    public Canvas[] imageCanvases; // 3개의 이미지 캔버스

    public void ShowImage(int index)
    {
        // 가장 높은 Sorting Order 찾기
        int maxOrder = 0;
        foreach (Canvas canvas in imageCanvases)
        {
            if (canvas.sortingOrder > maxOrder)
                maxOrder = canvas.sortingOrder;
        }

        // 선택한 이미지의 Sorting Order를 최상위로 변경
        imageCanvases[index].sortingOrder = maxOrder + 1;
    }
}
