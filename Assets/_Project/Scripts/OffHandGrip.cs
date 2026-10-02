using UnityEngine;

/// <summary>
/// 양손 무기의 반대 손을 손잡이 지점에 붙이는 표시용 IK(10/2, 우두머리 대검). 판정·수치와 무관하다.
/// Animator 기본 층의 IK Pass가 켜져 있어야 한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class OffHandGrip : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private AvatarIKGoal goal = AvatarIKGoal.LeftHand;
    [Range(0f, 1f)] [SerializeField] private float weight = 1f;

    private Animator animator;

    private void Awake() => animator = GetComponent<Animator>();

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || target == null) return;
        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKPosition(goal, target.position);
    }
}
