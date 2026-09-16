using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class Reset_Button : MonoBehaviour
{
    public GameObject resetData;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            resetData.SetActive(true);
            Debug.Log("reset");
            //resetData.SetActive(false);
            Debug.Log("resetcomplete");
        }
    }
}
