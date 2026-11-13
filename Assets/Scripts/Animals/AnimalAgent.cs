using System.Collections.Generic;
using UnityEngine;

public enum AnimalSpecies { Dog, Husky, Cat, Pigeon, Fox, Deer, Horse }

/// <summary>
/// An animal moving around the world: it idles, wanders (now and then across the road), eats, and
/// flees from approaching vehicles and (for wild animals and pigeons) from people; pigeons fly off.
/// The behaviour runs on the host; clients show the poses the host sends (see AnimalSpawner).
/// </summary>
public class AnimalAgent : MonoBehaviour
{
    public enum State { Idle, Walk, Run, Eat, Fly }

    public AnimalSpecies Species;
    public float WalkSpeed = 1.2f;
    public float RunSpeed = 6f;
    [Tooltip("Flee from vehicles that would arrive within this many seconds, or are closer than FleeDistance.")]
    public float FleeTime = 2.5f;
    public float FleeDistance = 6f;
    [Tooltip("Flee from people closer than this (0 = not shy of people, like dogs).")]
    public float PeopleShyDistance = 3f;
    [Tooltip("Wanders within this radius (m) of where it was spawned.")]
    public float HomeRadius = 25f;
    public bool CanFly;

    [Header("State (read only)")]
    public State Current = State.Idle;

    public static readonly List<AnimalAgent> All = new List<AnimalAgent>();

    /// <summary>False on clients: the pose comes from the host.</summary>
    public bool Simulate = true;

    Animator _animator;
    Vector3 _home, _target;
    float _stateTimer, _speed, _flyHeight, _groundY;
    System.Random _rng;
    State _shown = (State)(-1);

    public void Init(int seed)
    {
        _rng = new System.Random(seed);
        _home = transform.position;
        _groundY = transform.position.y;
        _animator = GetComponentInChildren<Animator>();
        Current = State.Idle;
        _stateTimer = (float)_rng.NextDouble() * 4f;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    void Update()
    {
        if (Simulate && _rng != null) Think(Time.deltaTime);
        ShowState();
    }

    void Think(float dt)
    {
        _stateTimer -= dt;
        // threats first
        if (Current != State.Fly && Threat(out var away))
        {
            if (CanFly)
            {
                Current = State.Fly;
                _target = transform.position + away * Rand(15f, 30f);
                _stateTimer = Rand(4f, 8f);
            }
            else
            {
                Current = State.Run;
                _target = transform.position + away * Rand(8f, 15f);
                _stateTimer = Rand(1.5f, 3f);
            }
        }

        switch (Current)
        {
            case State.Idle:
            case State.Eat:
                _speed = 0f;
                if (_stateTimer <= 0f) PickNext();
                break;
            case State.Walk:
            case State.Run:
                _speed = Mathf.MoveTowards(_speed, Current == State.Run ? RunSpeed : WalkSpeed, dt * 6f);
                MoveTowardsTarget(dt);
                if ((Flat(_target - transform.position)).magnitude < 0.5f || _stateTimer <= -20f) { Current = State.Idle; _stateTimer = Rand(2f, 6f); }
                else if (Current == State.Run && _stateTimer <= 0f) { Current = State.Walk; _stateTimer = 10f; }
                break;
            case State.Fly:
                _speed = 7f;
                _flyHeight = Mathf.MoveTowards(_flyHeight, _stateTimer > 1.5f ? 6f : 0f, dt * 3f);
                MoveTowardsTarget(dt);
                if (_stateTimer <= 0f && _flyHeight <= 0.01f) { Current = State.Idle; _stateTimer = Rand(3f, 8f); }
                break;
        }
    }

    void PickNext()
    {
        float r = (float)_rng.NextDouble();
        if (r < 0.25f) { Current = State.Eat; _stateTimer = Rand(3f, 8f); return; }
        if (r < 0.4f) { Current = State.Idle; _stateTimer = Rand(2f, 6f); return; }
        // wander: a point around home (sometimes across the road, wherever that is)
        for (int i = 0; i < 6; i++)
        {
            var d = Random2(HomeRadius);
            var p = _home + new Vector3(d.x, 0, d.y);
            if (Ground(p, out var y) && !Blocked(transform.position, p)) { _target = new Vector3(p.x, y, p.z); Current = State.Walk; _stateTimer = 0f; return; }
        }
        Current = State.Idle;
        _stateTimer = Rand(2f, 4f);
    }

    Vector2 Random2(float radius)
    {
        float a = Rand(0f, Mathf.PI * 2f), r = radius * Mathf.Sqrt((float)_rng.NextDouble());
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    void MoveTowardsTarget(float dt)
    {
        var to = Flat(_target - transform.position);
        if (to.sqrMagnitude < 1e-4f) return;
        var dir = to.normalized;
        var look = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-dt * 8f));
        var next = transform.position + transform.forward * _speed * dt;
        if (Current != State.Fly && Blocked(transform.position, next + transform.forward * 0.5f))
        {
            Current = State.Idle;
            _stateTimer = 1f;
            return;
        }
        if (Ground(next, out var y)) _groundY = Mathf.Lerp(_groundY, y, 1f - Mathf.Exp(-dt * 15f));
        next.y = _groundY + (Current == State.Fly ? _flyHeight : 0f);
        transform.position = next;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    bool Ground(Vector3 p, out float y)
    {
        y = 0;
        if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hit.collider.transform.IsChildOf(transform)) return false;
        // not on top of buildings or cars
        if (hit.point.y > _home.y + 1.5f) return false;
        y = hit.point.y;
        return true;
    }

    bool Blocked(Vector3 from, Vector3 to)
    {
        var a = from + Vector3.up * 0.4f;
        var d = to - from; d.y = 0;
        if (d.sqrMagnitude < 1e-4f) return false;
        return Physics.Raycast(a, d.normalized, out var hit, d.magnitude, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform) && hit.rigidbody == null;
    }

    // a vehicle about to arrive, or a person too close: returns the direction to run
    bool Threat(out Vector3 away)
    {
        away = Vector3.zero;
        var me = transform.position;
        foreach (var v in VehicleTracker.Instance.Vehicles)
        {
            if (!v.Active) continue;
            var rel = me - v.Position; rel.y = 0;
            float dist = rel.magnitude;
            if (dist > 40f) continue;
            float closing = Vector3.Dot(v.Velocity, rel.normalized);
            bool close = dist < FleeDistance && v.Velocity.magnitude > 0.5f;
            if ((closing > 0.5f && dist / closing < FleeTime) || close)
            {
                // run sideways, away from the vehicle's path
                var side = Vector3.Cross(Vector3.up, v.Velocity.sqrMagnitude > 0.01f ? v.Velocity.normalized : v.Forward);
                away = (Vector3.Dot(side, rel) >= 0 ? side : -side).normalized;
                return true;
            }
        }
        if (PeopleShyDistance > 0f)
        {
            foreach (var a in PlayerAvatars())
            {
                var rel = me - a.position; rel.y = 0;
                if (rel.magnitude < PeopleShyDistance) { away = rel.normalized; return true; }
            }
        }
        return false;
    }

    static readonly List<Transform> _people = new List<Transform>();
    static int _peopleFrame = -1;

    static List<Transform> PlayerAvatars()
    {
        if (_peopleFrame == Time.frameCount) return _people;
        _peopleFrame = Time.frameCount;
        _people.Clear();
        foreach (var a in FindObjectsByType<PlayerAvatar>())
            if (a.Type == AvatarType.Pedestrian && a.gameObject.activeInHierarchy) _people.Add(a.transform);
        return _people;
    }

    /// <summary>Animator state shown (host: from the behaviour; clients: from the network).</summary>
    public void SetShownState(State s) => Current = s;

    void ShowState()
    {
        if (_animator == null || _shown == Current) return;
        _shown = Current;
        string name = Current == State.Fly ? "Fly" : Current.ToString();
        if (_animator.HasState(0, Animator.StringToHash(name))) _animator.CrossFadeInFixedTime(name, 0.2f);
    }
}
