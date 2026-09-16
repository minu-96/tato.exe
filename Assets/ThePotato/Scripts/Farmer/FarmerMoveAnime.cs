using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FarmerMoveAnime : MonoBehaviour
{
    private Animator animator;
    private Vector2 previousPosition;

    void Start()
    {
        animator = GetComponent<Animator>();
        previousPosition = transform.position;
    }

    void Update()
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = currentPosition - previousPosition;

        // 애니메이터 파라미터 설정
        animator.SetFloat("DX", direction.x);
        animator.SetFloat("DY", direction.y);
        animator.SetFloat("move", direction.sqrMagnitude);
        animator.SetFloat("LeftUp", direction.y + direction.x);
        animator.SetFloat("LeftDown", direction.x - direction.y);
        animator.SetFloat("RightUp", direction.y - direction.x);
        animator.SetFloat("RightDown", direction.y + direction.x);

        previousPosition = currentPosition;
    }

}
