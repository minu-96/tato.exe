using UnityEngine;

public class HeadDetector : MonoBehaviour
{
    private SnakeHead head;

    void Start() { head = GetComponentInParent<SnakeHead>(); }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Wall"))
            GameManager.Instance.GameOver();
        else if (other.CompareTag("Apple"))
        {
            head.AddSegment();
            GameManager.Instance.SpawnApple();
            GameManager.Instance.AddScore();
        }
    }
}