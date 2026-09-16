using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Eat_Item : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Eat");
        if (collision.gameObject.CompareTag("Potato"))
        {
            Destroy(gameObject);
        }
    }
}
