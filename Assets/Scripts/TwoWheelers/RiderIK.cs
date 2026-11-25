using CoupledSim.TwoWheelers;
using UnityEngine;

/// <summary>
/// Puts a humanoid rider on a two-wheeler by IK (needs IK Pass on the rider's animator layer).
/// On an e-scooter or a moped: hands on the grips, feet on the footboard, the head turned with the bend ahead.
/// On a bicycle (<see cref="Cycling"/>) a whole cycling pose, from the bicycle's motion: the hips on the saddle (or out
/// of it, standing on the pedals when setting off), the torso leaning forward as the bicycle needs (upright on a city
/// bike, low and flat on a road bike, lower still on the drops at speed) with arms slightly bent, hands round the grips
/// (or on the hoods or drops); the balls of the feet on the pedals as the cranks turn (<see cref="TwoWheelerController.Legs"/>),
/// the ankles moving through the stroke, the knees forward; the upper body swaying a little with the effort; the head
/// looking ahead and into bends, with an occasional look over the shoulder (and before a turn across the traffic);
/// stopped, a foot on the ground beside the bicycle, the hips forward off the saddle.
/// Without a working <see cref="TwoWheelerController"/> (another participant's rider, a replay, a parked line-up) the
/// motion is read from how the bicycle and its cranks move.
/// </summary>
[RequireComponent(typeof(Animator))]
public class RiderIK : MonoBehaviour
{
    public Transform LeftHand, RightHand, LeftFoot, RightFoot;
    [Tooltip("Hints the knees point to (in front of the rider; scooters and mopeds).")]
    public Transform LeftKnee, RightKnee;
    [Tooltip("Where the rider looks (scooters and mopeds; a cyclist looks along the road by itself).")]
    public Transform LookTarget;
    [Range(0f, 1f)] public float Weight = 1f;

    [Header("Cycling")]
    [Tooltip("The full cycling pose (bicycles): pedalling, torso, foot down. Off: hands and feet only.")]
    public bool Cycling;
    [Tooltip("The bicycle's leaning frame (its parts lean with it).")]
    public Transform Frame;
    [Tooltip("The vehicle's root, on the ground.")]
    public Transform Root;
    [Tooltip("The crank (its rotation about x is the crank angle; 0 = right crank up).")]
    public Transform Crank;
    [Tooltip("Where the hips go when seated (just above the saddle).")]
    public Transform HipTarget;
    [Tooltip("Drop handlebar: the hands' place low on the drops (LeftHand/RightHand are on the hoods).")]
    public Transform LeftDrop, RightDrop;
    [Tooltip("Lean of the torso forward from upright (deg) the rider prefers; more when the grips are too far to reach with bent arms.")]
    public float TorsoPitch = 15f;
    [Tooltip("The same on the drops.")]
    public float DropsTorsoPitch = 58f;
    [Tooltip("Above this speed (km/h) the hands go to the drops.")]
    public float DropsAboveKmh = 38f;
    [Tooltip("Hands on the hoods of a road bike (fingers forward over the levers), not round transverse grips.")]
    public bool Hoods;

    Animator _animator;
    TwoWheelerController _tw;

    // ---- the skeleton (measured from the bind pose and the first animated frames)
    Transform _hips, _head;
    float _thigh, _shin, _arm, _torso, _eyeAbove, _toeHeight = 0.03f, _hipDown = 0.07f, _hipSide = 0.09f;
    Vector3 _toeL, _toeR;                               // foot bone to toe bone, in the foot's bind frame
    Quaternion _footBasisL, _footBasisR;                // the foot's toe and up directions in its bind frame
    Quaternion _handBasisL, _handBasisR;                // the hand's finger and palm directions in its bind frame
    readonly HumanBodyBones[] _fingers = new HumanBodyBones[24];
    readonly Quaternion[] _fingerBind = new Quaternion[24];
    readonly Vector3[] _fingerAxis = new Vector3[24];
    int _fingerCount;
    // IK goal to bone offsets (rotation and position, in the goal's frame) and the hips relative to the body centre
    readonly Quaternion[] _goalRot = new Quaternion[4];
    readonly Vector3[] _goalPos = new Vector3[4];
    Vector3 _hipsFromBody;
    int _ikFrames;

    // ---- the motion read from the bicycle, smoothed
    Vector3 _lastPos; float _lastYaw, _lastCrank, _speed, _yawRate, _effort, _footDown, _standing, _drops, _stillFor;
    bool _haveLast, _footRight;
    /// <summary>How long a look over the shoulder takes (s): turned in a third of it, held, turned back.</summary>
    const float CheckSeconds = 1.3f;
    float _look, _checkUntil = -1f, _checkSide, _nextCheck, _lastCheck = -100f, _prevYawRate;
    System.Random _rng;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _tw = GetComponentInParent<TwoWheelerController>();
        _rng = new System.Random(GetEntityId().GetHashCode());
        _nextCheck = 8f + (float)_rng.NextDouble() * 25f;
        if (Cycling) MeasureBindPose();
    }

    Transform B(HumanBodyBones b) => _animator.isHuman ? _animator.GetBoneTransform(b) : null;

    void MeasureBindPose()
    {
        if (!_animator.isHuman) { Cycling = false; return; }
        _hips = B(HumanBodyBones.Hips); _head = B(HumanBodyBones.Head);
        var ul = B(HumanBodyBones.LeftUpperLeg); var ll = B(HumanBodyBones.LeftLowerLeg); var fl = B(HumanBodyBones.LeftFoot);
        var ua = B(HumanBodyBones.LeftUpperArm); var la = B(HumanBodyBones.LeftLowerArm); var hl = B(HumanBodyBones.LeftHand);
        if (_hips == null || ul == null || ll == null || fl == null || ua == null || la == null || hl == null || _head == null) { Cycling = false; return; }
        _thigh = Vector3.Distance(ul.position, ll.position);
        _shin = Vector3.Distance(ll.position, fl.position);
        _arm = Vector3.Distance(ua.position, la.position) + Vector3.Distance(la.position, hl.position);
        _torso = Vector3.Distance(_hips.position, ua.position);
        _eyeAbove = _head.position.y - _hips.position.y + 0.08f;
        _hipDown = Mathf.Clamp(_hips.position.y - ul.position.y, 0f, 0.15f);
        _hipSide = Mathf.Clamp(Mathf.Abs(transform.InverseTransformPoint(ul.position).x - transform.InverseTransformPoint(_hips.position).x), 0.05f, 0.14f);
        var up = transform.up; var fwd = transform.forward; var right = transform.right;
        foreach (var left in new[] { true, false })
        {
            var foot = B(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            var toe = B(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
            var hand = B(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var middle = B(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var inv = Quaternion.Inverse(foot.rotation);
            var toeVec = toe != null ? toe.position - foot.position : fwd * 0.13f - up * 0.06f;
            var toeDir = Vector3.ProjectOnPlane(toeVec, up).normalized;
            var basis = Quaternion.LookRotation(inv * toeDir, inv * up);
            // the hand: fingers along the arm in the bind pose (T-pose), palm down
            var hinv = Quaternion.Inverse(hand.rotation);
            var fingerDir = middle != null ? (middle.position - hand.position).normalized : (left ? -right : right);
            var hbasis = Quaternion.LookRotation(hinv * fingerDir, hinv * -up);
            if (toe != null) _toeHeight = Mathf.Clamp(toe.position.y - transform.position.y, 0.015f, 0.08f);
            if (left) { _toeL = inv * toeVec; _footBasisL = basis; _handBasisL = hbasis; }
            else { _toeR = inv * toeVec; _footBasisR = basis; _handBasisR = hbasis; }
        }
        // fingers curl round the grip: about the palm's side axis, towards the palm
        _fingerCount = 0;
        for (int side = 0; side < 2; side++)
        {
            var hand = B(side == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var index = B(side == 0 ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = B(side == 0 ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            if (hand == null || index == null || little == null) continue;
            var across = (little.position - index.position).normalized;
            int first = side == 0 ? (int)HumanBodyBones.LeftIndexProximal : (int)HumanBodyBones.RightIndexProximal;
            for (int k = 0; k < 12; k++)
            {
                var hb = (HumanBodyBones)(first + k);
                var t = B(hb); if (t == null || t.childCount == 0) continue;
                var along = (t.GetChild(0).position - t.position).normalized;
                // the axis that turns the finger towards the palm (down in the bind pose)
                var axis = across;
                if (Vector3.Dot(Vector3.Cross(axis, along), -up) < 0f) axis = -axis;
                _fingers[_fingerCount] = hb; _fingerBind[_fingerCount] = t.localRotation; _fingerAxis[_fingerCount] = Quaternion.Inverse(t.rotation) * axis;
                _fingerCount++;
            }
        }
    }

    /// <summary>
    /// The forward lean of the torso (deg from upright) at which the shoulders are 88% of the arm's length from the grips
    /// (arms slightly bent: 93%), at least <paramref name="preferred"/>: hips and grips in the bicycle's frame (y up, z forward).
    /// </summary>
    public static float TorsoPitchFor(Vector3 hips, Vector3 grip, float torso, float arm, float preferred)
    {
        float want = 0.93f * arm, lo = 0f, hi = 85f;
        // (the wrist some 7 cm behind the grip)
        float Reach(float p) { var s = hips + new Vector3(0f, Mathf.Cos(p * Mathf.Deg2Rad), Mathf.Sin(p * Mathf.Deg2Rad)) * torso; s.x = 0f; var g = grip - new Vector3(0f, 0f, 0.07f); g.x = Mathf.Max(0f, Mathf.Abs(g.x) - 0.17f); return Vector3.Distance(s, g); }
        if (Reach(preferred) <= want) return preferred;
        for (int i = 0; i < 20; i++) { float m = (lo + hi) / 2f; if (Reach(m) > want) lo = m; else hi = m; }
        return Mathf.Max(preferred, hi);
    }

    /// <summary>The rider's eyes in the bicycle's frame for a torso lean (for the participant's camera).</summary>
    public static Vector3 EyeFor(Vector3 hips, float pitchDeg, float eyeAboveHips)
        => hips + new Vector3(0f, Mathf.Cos(pitchDeg * Mathf.Deg2Rad), Mathf.Sin(pitchDeg * Mathf.Deg2Rad)) * (eyeAboveHips * 0.93f) + new Vector3(0f, 0f, 0.1f);

    void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null) return;
        if (!Cycling || Frame == null || HipTarget == null) { SimplePose(); return; }
        // (the first two frames: measure how the IK goals sit on the bones, with no IK yet)
        if (_ikFrames < 2)
        {
            if (++_ikFrames == 2) MeasureGoals();
            return;
        }
        ReadMotion(Time.deltaTime);
        CyclingPose();
    }

    void MeasureGoals()
    {
        var goals = new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot, AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand };
        var bones = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
        for (int i = 0; i < 4; i++)
        {
            var t = B(bones[i]);
            var gr = _animator.GetIKRotation(goals[i]);
            _goalRot[i] = Quaternion.Inverse(gr) * t.rotation;
            _goalPos[i] = Quaternion.Inverse(gr) * (t.position - _animator.GetIKPosition(goals[i]));
        }
        _hipsFromBody = Quaternion.Inverse(_animator.bodyRotation) * (_hips.position - _animator.bodyPosition);
    }

    /// <summary>Puts an IK goal so that its bone (ankle, wrist) is at <paramref name="pos"/> with rotation <paramref name="rot"/>.</summary>
    void Goal(int i, AvatarIKGoal goal, Vector3 pos, Quaternion rot, float w)
    {
        var gr = rot * Quaternion.Inverse(_goalRot[i]);
        _animator.SetIKPositionWeight(goal, w);
        _animator.SetIKRotationWeight(goal, w);
        _animator.SetIKPosition(goal, pos - gr * _goalPos[i]);
        _animator.SetIKRotation(goal, gr);
    }

    // ------------------------------------------------------------------ the bicycle's motion

    bool Live => _tw != null && _tw.LastShown >= Time.time - 0.25f;

    void ReadMotion(float dt)
    {
        dt = Mathf.Max(dt, 1e-4f);
        var root = Root != null ? Root : Frame.parent;
        var pos = root.position; float yaw = root.eulerAngles.y;
        float crank = CrankAngle();
        if (!_haveLast) { _lastPos = pos; _lastYaw = yaw; _lastCrank = crank; _haveLast = true; }
        float k = 1f - Mathf.Exp(-dt / 0.25f);
        if (Live)
        {
            _speed = _tw.State.Speed;
            _yawRate = _tw.State.YawRate;
            _effort = _tw.Legs.Effort;
            _footDown = _tw.Legs.FootDown;
            _standing = _tw.Legs.Standing;
            _footRight = _tw.FootDownRight;
        }
        else
        {
            // another participant's rider, a replay, a line-up: from how it moves
            var d = pos - _lastPos; d.y = 0f;
            _speed = Mathf.Lerp(_speed, d.magnitude / dt, k);
            _yawRate = Mathf.Lerp(_yawRate, Mathf.DeltaAngle(_lastYaw, yaw) * Mathf.Deg2Rad / dt, k);
            bool pedalling = Mathf.Abs(Mathf.DeltaAngle(_lastCrank, crank)) > 0.5f;
            _effort = Mathf.Lerp(_effort, pedalling ? 0.4f : 0f, k);
            _stillFor = _speed < 0.2f && !pedalling ? _stillFor + dt : 0f;
            _footDown = Mathf.MoveTowards(_footDown, _stillFor > 0.3f ? 1f : 0f, dt / 0.5f);
            _standing = 0f;
            // the foot on the side the bicycle leans to (left unless it leans right)
            float lean = Mathf.DeltaAngle(0f, Frame.localEulerAngles.z);
            if (_footDown > 0.5f && Mathf.Abs(lean) > 2f) _footRight = lean < 0f;
        }
        _lastPos = pos; _lastYaw = yaw; _lastCrank = crank;
        // the drops at speed (with a margin, so it does not flicker)
        if (LeftDrop != null && RightDrop != null)
        {
            float kmh = _speed * 3.6f;
            float target = kmh > DropsAboveKmh ? 1f : kmh < DropsAboveKmh - 4f ? 0f : (_drops > 0.5f ? 1f : 0f);
            _drops = Mathf.MoveTowards(_drops, target, dt / 0.6f);
        }
        // looking into the bend ahead (where the bicycle will be in about a second)
        // (calmly: at most 25°, eased over 0.6 s; the head swung 40° into every bend and looked restless)
        _look = Mathf.Lerp(_look, Mathf.Clamp(_yawRate * 0.6f * Mathf.Rad2Deg, -25f, 25f), 1f - Mathf.Exp(-dt / 0.6f));
        // a look over the shoulder: now and then while riding, and when a turn begins (to the side it turns to)
        float now = Time.time;
        if (_checkUntil < now && _speed > 2f && _footDown < 0.1f)
        {
            bool turnStarts = Mathf.Abs(_yawRate) > 0.12f && Mathf.Abs(_prevYawRate) <= 0.12f && now - _lastCheck > 6f;
            if (turnStarts || now > _nextCheck)
            {
                _checkSide = turnStarts ? Mathf.Sign(_yawRate) : (_footRight ? 1f : -1f);
                _checkUntil = now + CheckSeconds; _lastCheck = now;
                // (riding straight on, now and then: every 40-80 s; every 15-45 s with a quick 72° turn of the head
                // looked nervous)
                _nextCheck = now + 40f + (float)_rng.NextDouble() * 40f;
            }
        }
        _prevYawRate = _yawRate;
    }

    float CrankAngle()
    {
        if (Live) return _tw.Legs.CrankAngle;
        if (Crank == null) return 0f;
        var v = Crank.localRotation * Vector3.up;
        float a = Mathf.Atan2(v.z, v.y) * Mathf.Rad2Deg;
        return a < 0f ? a + 360f : a;
    }

    // ------------------------------------------------------------------ the pose

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    void CyclingPose()
    {
        float w = Weight;
        var frameRot = Frame.rotation;
        Vector3 up = frameRot * Vector3.up, fwd = frameRot * Vector3.forward, right = frameRot * Vector3.right;
        float crank = CrankAngle();
        float down = Smooth(_footDown), stand = Smooth(_standing), drops = Smooth(_drops);
        float sinC = Mathf.Sin(crank * Mathf.Deg2Rad);
        float time = Time.time;

        // grips (hoods or drops)
        Vector3 gripL = LeftHand.position, gripR = RightHand.position;
        if (LeftDrop != null && drops > 0f) { gripL = Vector3.Lerp(gripL, LeftDrop.position, drops); gripR = Vector3.Lerp(gripR, RightDrop.position, drops); }
        var gripMid = Frame.InverseTransformPoint((gripL + gripR) / 2f);

        // torso: the preferred lean, more if the grips need it; more when standing, or low on the drops
        var hipLocal = Frame.InverseTransformPoint(HipTarget.position);
        float pitch = TorsoPitchFor(hipLocal, gripMid, _torso, _arm, Mathf.Lerp(TorsoPitch, DropsTorsoPitch, drops));
        pitch += 8f * stand - 10f * down;
        // (sitting up a little for a look over the shoulder, more from low on the drops)
        if (_checkUntil > time) { float t = 1f - (_checkUntil - time) / CheckSeconds; pitch -= Mathf.Lerp(4f, 10f, drops) * Smooth(Mathf.Min(t / 0.3f, (1f - t) / 0.3f)); }
        // sway with the effort: the shoulders rock a little with each stroke (more standing, when the bicycle rocks
        // under the rider: the rider stays over it)
        float effort = Mathf.Clamp01(_effort);
        float roll = (1f + 2.5f * effort) * sinC * (1f - down);
        float rock = _tw != null && Live ? _tw.RockDeg : 0f;
        roll += rock * 0.8f;
        float twist = (1.5f + 2f * effort) * sinC * (1f - down) + (Live ? _tw.SteerDeg * 0.25f : 0f);

        // hips: on the saddle; standing: up and forward; foot down: forward off the saddle and lower
        var hips = HipTarget.position + (up * 0.13f + fwd * 0.1f) * stand + fwd * (0.13f * down) - up * (0.06f * down);
        // the body leans forward about the hips (the spine keeps its natural curve from the animation)
        float bodyPitch = pitch;
        var bodyRot = frameRot * Quaternion.Euler(bodyPitch, twist, -roll) * (Quaternion.Inverse(frameRot) * _animator.bodyRotation);

        // feet
        bool groundRight = _footRight;
        Vector3 ankleL = PedalAnkle(true, crank, frameRot, out var rotL);
        Vector3 ankleR = PedalAnkle(false, crank, frameRot, out var rotR);
        if (down > 0f)
        {
            // the foot on the ground beside the bicycle, below the hips and a little ahead, toes slightly out
            var root = Root != null ? Root : Frame.parent;
            var rootFwd = root.forward; var rootRight = root.right;
            float side = groundRight ? 1f : -1f;
            var groundRot = Quaternion.LookRotation(Quaternion.AngleAxis(side * 12f, Vector3.up) * rootFwd, Vector3.up);
            var ball = root.position + rootRight * (side * 0.33f) + rootFwd * (Frame.InverseTransformPoint(HipTarget.position).z + 0.32f) + Vector3.up * _toeHeight;
            var groundAnkle = ball - FootRot(groundRight ? _footBasisR : _footBasisL, groundRot) * (groundRight ? _toeR : _toeL);
            // (lifted off the pedal and put down in an arc)
            float lift = Mathf.Sin(down * Mathf.PI) * 0.12f;
            if (groundRight) { ankleR = Vector3.Lerp(ankleR, groundAnkle, down) + Vector3.up * lift; rotR = Quaternion.Slerp(rotR, FootRot(_footBasisR, groundRot), down); }
            else { ankleL = Vector3.Lerp(ankleL, groundAnkle, down) + Vector3.up * lift; rotL = Quaternion.Slerp(rotL, FootRot(_footBasisL, groundRot), down); }
        }
        // legs not overstretched: the hips come down so that each ankle is within reach
        float reach = 0.985f * (_thigh + _shin);
        float Drop(Vector3 ankle, float s)
        {
            var v = hips + right * (s * _hipSide) - up * _hipDown - ankle;
            float horiz2 = v.x * v.x + v.z * v.z;
            return v.sqrMagnitude > reach * reach && reach * reach > horiz2 ? v.y - Mathf.Sqrt(reach * reach - horiz2) : 0f;
        }
        hips -= Vector3.up * Mathf.Max(Drop(ankleL, -1f), Drop(ankleR, 1f));

        _animator.bodyRotation = bodyRot;
        _animator.bodyPosition = hips - bodyRot * _hipsFromBody;
        Goal(0, AvatarIKGoal.LeftFoot, ankleL, rotL, w);
        Goal(1, AvatarIKGoal.RightFoot, ankleR, rotR, w);

        // knees: forward over the feet and a little out
        void Knee(AvatarIKHint hint, Vector3 ankle, float s)
        {
            _animator.SetIKHintPositionWeight(hint, w);
            _animator.SetIKHintPosition(hint, (hips + right * (s * _hipSide) - up * _hipDown + ankle) / 2f + fwd * 0.45f + right * (s * 0.06f) + up * 0.1f);
        }
        Knee(AvatarIKHint.LeftKnee, ankleL, -1f);
        Knee(AvatarIKHint.RightKnee, ankleR, 1f);

        var bodyRight = bodyRot * Vector3.right;

        // hands: wrists just behind the grips, fingers round them (hoods: over the levers), elbows bent out and down
        HandGoal(2, AvatarIKGoal.LeftHand, gripL, true, frameRot, drops, w);
        HandGoal(3, AvatarIKGoal.RightHand, gripR, false, frameRot, drops, w);
        var shoulderMid = hips + bodyRot * (Vector3.up * _torso);
        void Elbow(AvatarIKHint hint, Vector3 grip, float s)
        {
            _animator.SetIKHintPositionWeight(hint, w);
            _animator.SetIKHintPosition(hint, (shoulderMid + bodyRight * (s * 0.17f) + grip) / 2f + bodyRight * (s * 0.1f) - up * 0.25f - fwd * 0.1f);
        }
        Elbow(AvatarIKHint.LeftElbow, gripL, -1f);
        Elbow(AvatarIKHint.RightElbow, gripR, 1f);
        for (int i = 0; i < _fingerCount; i++)
        {
            int k = ((int)_fingers[i] - (int)HumanBodyBones.LeftIndexProximal) % 12 % 3;   // proximal, intermediate, distal
            float curl = (Hoods && drops < 0.5f ? 35f : 55f) + (k == 0 ? 0f : 10f);
            _animator.SetBoneLocalRotation(_fingers[i], _fingerBind[i] * Quaternion.AngleAxis(curl * w, _fingerAxis[i]));
        }

        // head: ahead along the road, 15-20 m ahead (a rider low on a road bike lifts the head to it rather than looking
        // at the front wheel), and into the bend; now and then over the shoulder: as riders do it, the head turned some
        // 50° (45° low on the drops) with the shoulders taking part of it, over 1.3 s, and level (aimed 150° round with the
        // neck alone, the head twisted onto the shoulder)
        var eye = hips + bodyRot * (Vector3.up * _eyeAbove);
        var flatFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
        float yawLook = _look;
        float check = 0f;
        if (_checkUntil > time) { float t = 1f - (_checkUntil - time) / CheckSeconds; check = Smooth(Mathf.Min(t / 0.35f, (1f - t) / 0.35f)); yawLook = Mathf.Lerp(yawLook, _checkSide * Mathf.Lerp(50f, 45f, drops), check); }
        var lookDir = Quaternion.AngleAxis(yawLook, Vector3.up) * flatFwd;
        // (body: the shoulders turn with a look over the shoulder; head: most of the turn; clamp: no more than a neck
        // turns, the rest left out)
        _animator.SetLookAtWeight(w, 0.08f + 0.4f * check, 0.9f, 1f, 0.55f);
        float eyeHeight = Mathf.Max(1.0f, eye.y - transform.position.y);
        _animator.SetLookAtPosition(eye + lookDir * 18f - Vector3.up * eyeHeight * 0.8f * (1f - check));
    }

    /// <summary>The ankle and foot rotation for a foot on its pedal: the ball of the foot over the spindle, the ankle
    /// moving through the stroke (heel a little down at the top and over the front, toes down at the bottom and back:
    /// "ankling").</summary>
    Vector3 PedalAnkle(bool left, float crank, Quaternion frameRot, out Quaternion rot)
    {
        var pedal = left ? LeftFoot : RightFoot;
        float a = left ? crank + 180f : crank;
        float toesDown = 8f - 11f * Mathf.Cos((a - 45f) * Mathf.Deg2Rad);
        var r = frameRot * Quaternion.Euler(toesDown, left ? -6f : 6f, 0f);
        rot = FootRot(left ? _footBasisL : _footBasisR, r);
        var ball = pedal.position + (frameRot * Vector3.up) * (_toeHeight + 0.012f);
        return ball - rot * (left ? _toeL : _toeR);
    }

    /// <summary>The foot bone's rotation for which its toes point along <paramref name="aim"/>'s forward and its sole along its up.</summary>
    static Quaternion FootRot(Quaternion basis, Quaternion aim) => aim * Quaternion.Inverse(basis);

    void HandGoal(int i, AvatarIKGoal goal, Vector3 grip, bool left, Quaternion frameRot, float drops, float w)
    {
        var steer = (left ? LeftHand : RightHand).parent != null ? (left ? LeftHand : RightHand).parent.rotation : frameRot;
        Vector3 fwd = steer * Vector3.forward, up = steer * Vector3.up, outward = steer * (left ? Vector3.left : Vector3.right);
        Vector3 fingers, palm, wrist;
        if (Hoods)
        {
            // on the hoods: fingers forward over the levers, palm inwards and down; on the drops: hooked round the bar
            fingers = Vector3.Lerp((fwd + up * -0.25f).normalized, (fwd * 0.5f - up).normalized, drops);
            palm = Vector3.Lerp((-outward * 0.8f - up * 0.6f).normalized, -outward, drops);
            wrist = grip - fwd * Mathf.Lerp(0.085f, 0.06f, drops) + up * Mathf.Lerp(0.045f, 0.03f, drops);
        }
        else
        {
            // round a transverse grip: knuckles forward and a little out, palm down and inwards
            fingers = (fwd + outward * 0.35f - up * 0.25f).normalized;
            palm = (-up - outward * 0.25f).normalized;
            wrist = grip - fwd * 0.065f + up * 0.035f - outward * 0.02f;
        }
        var rot = Quaternion.LookRotation(fingers, palm) * Quaternion.Inverse(left ? _handBasisL : _handBasisR);
        Goal(i, goal, wrist, rot, w);
    }

    // ------------------------------------------------------------------ scooters and mopeds

    void SimplePose()
    {
        Set(AvatarIKGoal.LeftHand, LeftHand);
        Set(AvatarIKGoal.RightHand, RightHand);
        Set(AvatarIKGoal.LeftFoot, LeftFoot);
        Set(AvatarIKGoal.RightFoot, RightFoot);
        Hint(AvatarIKHint.LeftKnee, LeftKnee);
        Hint(AvatarIKHint.RightKnee, RightKnee);
        if (LookTarget != null)
        {
            // into the bend ahead
            float yawRate = _tw != null ? _tw.State.YawRate : 0f;
            _look = Mathf.Lerp(_look, Mathf.Clamp(yawRate * 0.9f * Mathf.Rad2Deg, -40f, 40f), 1f - Mathf.Exp(-Time.deltaTime / 0.4f));
            var head = transform.position + Vector3.up * 1.5f;
            _animator.SetLookAtWeight(0.8f * Weight, 0.1f, 0.7f, 1f, 0.5f);
            _animator.SetLookAtPosition(head + Quaternion.AngleAxis(_look, Vector3.up) * (LookTarget.position - head));
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
