using UnityEngine;
using UnityEngine.UI;
using System.Collections; // 코루틴 및 IEnumerator 관련 기능
using System.Collections.Generic; // List, Dictionary 같은 컬렉션 사용 시 필요
using System;

public class RulePageManager : MonoBehaviour
{
    public GameObject[] images;
    private int a = 0;
    private int b = 1;

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.RightArrow))
        {
            Asdd();
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Asdf();
        }
    }

    private void Asdf()
    {
        images[a].SetActive(true);
        images[b].SetActive(false);
    }
    private void Asdd()
    {
        images[b].SetActive(true);
        images[a].SetActive(false);
    }

}
