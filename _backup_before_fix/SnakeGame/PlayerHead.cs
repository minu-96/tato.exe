using System.Collections.Generic;
using UnityEngine;

public class SnakeHead : MonoBehaviour
{
    [Header("이동")]
    public float moveInterval = 0.2f;
    public float lerpSpeed = 15f;

    [Header("프리팹")]
    public GameObject bodyPrefab;
    public GameObject tailPrefab;

    [Header("머리 스프라이트")]
    public Sprite spriteRight, spriteLeft, spriteUp, spriteDown;

    [Header("디텍터")]
    public Transform headDetector;

    private Vector2 direction = Vector2.right;
    private Vector2 nextDirection = Vector2.right;
    private float timer = 0f;
    private Vector2 targetPos;

    private SpriteRenderer sr;
    private List<SnakeSegment> segments = new List<SnakeSegment>();

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        targetPos = transform.position;

        // 꼬리 1개로 시작 (머리 바로 뒤 한 칸)
        Vector2 tailPos = targetPos - direction;
        SnakeSegment tail = Instantiate(tailPrefab, tailPos, Quaternion.identity)
            .GetComponent<SnakeSegment>();
        tail.Init(tailPos, true);
        segments.Add(tail);

        UpdateHeadSprite();
        UpdateDetector();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsIntroPlaying) return;

        GetInput();

        timer += Time.deltaTime;
        if (timer >= moveInterval)
        {
            timer = 0f;
            Move();
        }

        transform.position = Vector2.Lerp(
            transform.position, targetPos, Time.deltaTime * lerpSpeed);
    }

    // 머리 → 몸통들 → 꼬리 순서의 Transform 목록
public List<Transform> GetBodyTransforms()
{
    List<Transform> list = new List<Transform> { transform };
    foreach (var seg in segments)
        list.Add(seg.transform);
    return list;
}

    void GetInput()
    {
        if (Input.GetKey(KeyCode.UpArrow) && direction != Vector2.down)
            nextDirection = Vector2.up;
        else if (Input.GetKey(KeyCode.DownArrow) && direction != Vector2.up)
            nextDirection = Vector2.down;
        else if (Input.GetKey(KeyCode.LeftArrow) && direction != Vector2.right)
            nextDirection = Vector2.left;
        else if (Input.GetKey(KeyCode.RightArrow) && direction != Vector2.left)
            nextDirection = Vector2.right;
    }

void Move()
{
    direction = nextDirection;
    Vector2 headOldPos = targetPos;

    for (int i = segments.Count - 1; i >= 1; i--)
    {
        Vector2 followPos = segments[i - 1].GetTarget();
        Vector2 dir = (followPos - segments[i].GetTarget()).normalized;
        segments[i].SetTarget(followPos, dir);
    }
    if (segments.Count > 0)
    {
        Vector2 dir0 = (headOldPos - segments[0].GetTarget()).normalized;
        segments[0].SetTarget(headOldPos, dir0);
    }

    targetPos = headOldPos + direction;

    // ── 자기 몸통 충돌 판정 (추가) ──
    for (int i = 0; i < segments.Count; i++)
    {
        if (segments[i].GetTarget() == targetPos)
        {
            GameManager.Instance.GameOver();
            return; // 더 진행 안 함
        }
    }

    UpdateHeadSprite();
    UpdateDetector();
}

    // 사과 먹을 때 호출 — 꼬리 앞에 몸통 1개 삽입
    public void AddSegment()
    {
        SnakeSegment tail = segments[segments.Count - 1];
        Vector2 spawnPos = tail.GetTarget(); // 현재 꼬리 위치에 생성

        SnakeSegment body = Instantiate(bodyPrefab, spawnPos, Quaternion.identity)
            .GetComponent<SnakeSegment>();
        body.Init(spawnPos, false);

        segments.Insert(segments.Count - 1, body); // 꼬리 바로 앞에 삽입
    }

    void UpdateHeadSprite()
    {
        transform.rotation = Quaternion.identity;
        if (direction == Vector2.right) sr.sprite = spriteRight;
        else if (direction == Vector2.up) sr.sprite = spriteUp;
        else if (direction == Vector2.left) sr.sprite = spriteLeft;
        else if (direction == Vector2.down) sr.sprite = spriteDown;
    }

    void UpdateDetector()
    {
        if (headDetector != null)
            headDetector.localPosition = direction * 0.5f;
    }

    // 사과 스폰용 — 머리+모든 세그먼트의 칸 위치
    public List<Vector2> GetOccupiedPositions()
    {
        List<Vector2> list = new List<Vector2> { targetPos };
        foreach (var seg in segments)
            list.Add(seg.GetTarget());
        return list;
    }
}