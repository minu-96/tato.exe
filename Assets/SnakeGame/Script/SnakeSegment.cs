using UnityEngine;

public class SnakeSegment : MonoBehaviour
{
    public float lerpSpeed = 15f;

    // 꼬리만 방향 스프라이트 사용 (몸통은 기본 스프라이트 유지)
    public Sprite spriteRight, spriteLeft, spriteUp, spriteDown;

    private Vector2 targetPos;
    private SpriteRenderer sr;
    private bool isTail = false;

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        transform.position = Vector2.Lerp(
            transform.position, targetPos, Time.deltaTime * lerpSpeed);
    }

    public void Init(Vector2 pos, bool tail)
    {
        targetPos = pos;
        transform.position = pos; // 시작 위치 즉시 고정 (원점에서 날아오는 글리치 방지)
        isTail = tail;
    }

    public void SetTarget(Vector2 pos, Vector2 dir)
    {
        targetPos = pos;
        if (isTail) UpdateSprite(dir);
    }

    public Vector2 GetTarget() => targetPos;

    void UpdateSprite(Vector2 dir)
    {
        if (dir == Vector2.right) sr.sprite = spriteRight;
        else if (dir == Vector2.left) sr.sprite = spriteLeft;
        else if (dir == Vector2.up) sr.sprite = spriteUp;
        else if (dir == Vector2.down) sr.sprite = spriteDown;
    }
}