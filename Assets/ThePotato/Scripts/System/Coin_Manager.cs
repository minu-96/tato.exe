using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Coin_Manager : MonoBehaviour
{
    public Text text;
    private int coinValue = 0;


    void Update()
    {
        coinValue = PlayerPrefs.GetInt("TotalCoin", 0);
        text.text = $"{coinValue}";
    }

}
