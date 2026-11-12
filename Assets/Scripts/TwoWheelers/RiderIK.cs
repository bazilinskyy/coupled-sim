using UnityEngine;

/// <summary>
/// Puts a humanoid rider on a two-wheeler: hands on the grips, feet on the pedals (which turn with
/// the crank) or the footboard, knees pointing forward, head turned slightly with the steering.
/// Needs IK Pass on the rider's animator layer.
/// </summary>
[RequireComponent(typeof(Animator))]
public class RiderIK : MonoBehaviour
{
    public Transform LeftHand, RightHand, LeftFoot, RightFoot;
    [Tooltip("Hints the knees point to (in front of the rider).")]
    public Transform LeftKnee, RightKnee;
    [Tooltip("Where the rider looks (e.g. ahead of the vehicle).")]
    public Transform LookTarget;
    [Range(0f, 1f)] public float Weight = 1f;

    Animator _animator;

    void Awake() => _animator = GetComponent<Animator>();

    void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null) return;
        Set(AvatarIKGoal.LeftHand, LeftHand);
        Set(AvatarIKGoal.RightHand, RightHand);
        Set(AvatarIKGoal.LeftFoot, LeftFoot);
        Set(AvatarIKGoal.RightFoot, RightFoot);
        Hint(AvatarIKHint.LeftKnee, LeftKnee);
        Hint(AvatarIKHint.RightKnee, RightKnee);
        if (LookTarget != null)
        {
            _animator.SetLookAtWeight(0.8f * Weight, 0.1f, 0.7f, 1f, 0.5f);
            _animator.SetLookAtPosition(LookTarget.position);
        }
    }

    void Set(AvatarIKGoal goal, Transform t)
    {
        if (t == null) return;
        _animator.SetIKPositionWeight(goal, Weight);
        _animator.SetIKRotationWeight(goal, Weight * 0.7f);
        _animator.SetIKPosition(goal, t.position);
        _animator.SetIKRotation(goal, t.rotation);
    }

    void Hint(AvatarIKHint hint, Transform t)
    {
        if (t == null) return;
        _animator.SetIKHintPositionWeight(hint, Weight);
        _animator.SetIKHintPosition(hint, t.position);
    }
}
