using UnityEngine;

/// <summary>Drives an AICar along a <see cref="ScriptedPath"/> (speed through AICar's external speed control).</summary>
public class ScriptedPathDriver : MonoBehaviour
{
    public float DefaultSpeedKmh = 30f;
    public float MaxAcceleration = 2f, MaxDeceleration = 3f;
    [Header("State (read only)")]
    public float Speed;
    public float Distance;
    public bool Finished;

    ScriptedPath _path;
    AICar _car;
    float _start;
    Vector3 _last;

    public void Init(ScriptedPath path, float initialSpeed = 0f)
    {
        _path = path;
        _car = GetComponent<AICar>();
        _car.ExternalSpeedControl = true;
        Speed = initialSpeed;
        _start = Time.time;
        _last = transform.position;
        Distance = 0f;
    }

    void FixedUpdate()
    {
        if (_path == null || _car == null || !_car.enabled) return;
        float dt = Time.fixedDeltaTime;
        var p = transform.position;
        var step = p - _last; step.y = 0;
        Distance += Vector3.Dot(step, transform.forward);
        _last = p;
        float target;
        if (_path.HasTimes)
        {
            // replay: track where the recording was at this time
            float t = Time.time - _start;
            float s = _path.DistanceAt(t), sNext = _path.DistanceAt(t + 0.5f);
            target = Mathf.Max(0f, (sNext - s) / 0.5f + 0.8f * (s - Distance));
        }
        else target = _path.TargetSpeed(Distance, DefaultSpeedKmh / 3.6f, MaxDeceleration);
        if (Distance >= _path.Length - 0.5f) target = 0f;
        float a = Mathf.Clamp((target - Speed) / dt, -MaxDeceleration * (_path.HasTimes ? 2f : 1f), MaxAcceleration * (_path.HasTimes ? 2f : 1f));
        Speed = Mathf.Max(0f, Speed + a * dt);
        Finished = Distance >= _path.Length - 0.5f && Speed < 0.05f;
        _car.ExternalSpeed = Speed;
        _car.ExternalAcceleration = a;
    }
}
