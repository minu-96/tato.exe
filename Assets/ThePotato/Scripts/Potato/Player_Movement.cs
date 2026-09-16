using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerMovement : MonoBehaviour
{

    public float moveSpeed = 5f; // 캐릭터 이동 속도

    public float speedDivide = 0.5f;

    private Rigidbody2D rb; // Rigidbody2D 참조
    private Vector2 move; // 입력값 저장

    private Vector2 lastDirection = Vector2.zero;

    private Animator animator;

    void Start()
    {
        // Rigidbody2D 컴포넌트 가져오기
        rb = GetComponent<Rigidbody2D>();

        animator = GetComponent<Animator>();

        Set_Speed.Instance.SaveSpeed(moveSpeed);
    }

    void Update()
    {
        moveSpeed = Set_Speed.Instance.moveSpeed;
        // 입력값 받아오기
         float movementx = Input.GetAxisRaw("Horizontal"); // A, D 또는 좌우 화살표
        float movementy = Input.GetAxisRaw("Vertical"); // W, S 또는 상하 화살표

        move = new Vector2(movementx, movementy).normalized;
        
        // Animator 파라미터 설정
        animator.SetFloat("DX", move.x);
        animator.SetFloat("DY", move.y);

        // 움직임이 있을 때 방향 업데이트
        if (move != Vector2.zero)
        {
            lastDirection = move; // 마지막 움직임 방향 저장
        }

        // 멈췄을 때 방향 유지
        if (move == Vector2.zero)
        {
            animator.SetFloat("DX", lastDirection.x);
            animator.SetFloat("DY", lastDirection.y);
        } 
    }

    void FixedUpdate()
    {
        // Rigidbody2D를 이용해 움직이기
        rb.linearVelocity = move * moveSpeed;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Eat");
        if (collision.gameObject.CompareTag("fertilizer"))
        {
            StartCoroutine(Set_Speed.Instance.SpeedUp(moveSpeed));
        }
    }
   

}