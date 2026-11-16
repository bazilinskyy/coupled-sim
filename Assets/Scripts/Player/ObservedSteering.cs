using System.Linq;
using UnityEngine;

/// <summary>
/// Turns the steering wheel of a car this agent does not drive itself (an AV, another
/// participant's car seen on this agent, an AI car with an interior) from how the car moves:
/// road-wheel angle = atan(wheelbase × yaw rate / speed) (bicycle model), times the steering
/// ratio. Added by <see cref="PlayerAvatar.Initialize"/>.
/// </summary>
public class ObservedSteering : MonoBehaviour
{
    public Transform SteeringWheel;
    [Tooltip("Largest road-wheel angle (degrees).")]
    public float MaxSteerAngle = 30f;
    [Tooltip("Steering wheel degrees per degree of road-wheel angle.")]
    public float SteeringRatio = 15f;
    [Tooltip("+1 or -1: the direction the wheel model turns for a right turn.")]
    public float Direction = -1f;

    float _wheelbase = 2.5f, _steer;
    Vector3 _lastPos;
    float _lastYaw;
    bool _started;

    public void Setup(Transform wheel, float maxSteerAngle, float direction)
    {
        SteeringWheel = wheel;
        MaxSteerAngle = maxSteerAngle > 1f ? maxSteerAngle : 30f;
        Direction = Mathf.Sign(direction);
        var wcs = GetComponentsInChildren<WheelCollider>(true);
        if (wcs.Length >= 2)
        {
            var zs = wcs.Select(w => transform.InverseTransformPoint(w.transform.position).z).ToArray();
            _wheelbase = Mathf.Max(1f, zs.Max() - zs.Min());
        }
    }

    /// <summary>Road-wheel angle (degrees, positive = right) estimated from the motion.</summary>
    public float SteerAngle => _steer;

    /// <summary>The curvature of the path is given (<see cref="Drive"/>, e.g. by a replay, from the logged path over a
    /// second) instead of estimated here frame by frame.</summary>
    [HideInInspector] public bool Driven;

    /// <summary>
    /// Turns the wheel for a path of this curvature (1/m, positive = to the right), or holds it where it is
    /// (null: standing still). The wheel follows within about 0.15 s.
    /// </summary>
    public void Drive(float? curvature, float dt)
    {
        Driven = true;
        if (SteeringWheel == null) return;
        if (curvature.HasValue && dt > 0f)
        {
            float target = Mathf.Clamp(Mathf.Atan(_wheelbase * curvature.Value) * Mathf.Rad2Deg, -MaxSteerAngle, MaxSteerAngle);
            _steer = Mathf.Lerp(_steer, target, 1f - Mathf.Exp(-dt * 6f));
        }
        SteeringWheel.localRotation = Quaternion.AngleAxis(_steer * SteeringRatio * Direction, Vector3.forward);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (Driven || SteeringWheel == null || dt <= 0f) return;
        var pos = transform.position;
        float yaw = transform.eulerAngles.y;
        if (!_started) { _started = true; _lastPos = pos; _lastYaw = yaw; return; }
        float v = Vector3.Dot(pos - _lastPos, transform.forward) / dt;
        float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) * Mathf.Deg2Rad / dt;
        _lastPos = pos;
        _lastYaw = yaw;
        // hold the wheel when (almost) stopped
        if (Mathf.Abs(v) > 0.5f)
        {
            float target = Mathf.Clamp(Mathf.Atan(_wheelbase * yawRate / v) * Mathf.Rad2Deg, -MaxSteerAngle, MaxSteerAngle);
            _steer = Mathf.Lerp(_steer, target, 1f - Mathf.Exp(-dt * 6f));
        }
        SteeringWheel.localRotation = Quaternion.AngleAxis(_steer * SteeringRatio * Direction, Vector3.forward);
    }
}
