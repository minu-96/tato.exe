using UnityEngine.UI;
using UnityEngine; // Unity의 기본 기능 (GameObject, Transform 등)
using System.Collections; // 코루틴 및 IEnumerator 관련 기능
using System.Collections.Generic; // List, Dictionary 같은 컬렉션 사용 시 필요

public class Coin_Value : MonoBehaviour
{
    public static Coin_Value instance;
    public int coinValue = 0;

    public AudioClip alertSound;
    private AudioSource audioSource;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void AddCoin(int amount)
    {
        audioSource.PlayOneShot(alertSound);
        coinValue += amount;
    }

    public void LoadCoin(out int value)
    {
        value = coinValue;
    }
    public void SetCoinValue()
    {
        coinValue = 0;
    }

    public void SaveCoin()
    {
        Total_Coins.Instance.SaveTotalCoin(coinValue);
    }
}
