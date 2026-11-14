using UnityEngine;

/// <summary>
/// Turn signals for the participant's own car: Q (left), E (right) or the wheel's blinker buttons
/// (WheelProfile.BlinkerLeft/Right/Clear); the same key again switches them off. Like a real car
/// they cancel themselves when the steering wheel returns after a turn. A soft tick sounds with
/// each flash. The state is sent to the other agents with the car's pose and logged with it.
/// <c>-blinker left|right</c> starts with one on (previews, screenshots).
/// </summary>
public class ParticipantBlinkers : MonoBehaviour
{
    public KeyCode LeftKey = KeyCode.Q, RightKey = KeyCode.E;
    [Tooltip("Steering (-1..1) beyond which a turn counts, and below which (after a turn) the signal cancels.")]
    public float TurnSteer = 0.3f, CentreSteer = 0.08f;

    CarBlinkers _blinkers;
    AudioSource _tick;
    bool _turned, _lastLamp;
    static AudioClip _click;

    void Start()
    {
        _blinkers = GetComponentInChildren<CarBlinkers>(true);
        if (_click == null)
        {
            // a short relay click: 15 ms of decaying noise
            int n = 660; var d = new float[n]; var r = new System.Random(3);
            for (int i = 0; i < n; i++) d[i] = ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-i / 90f) * 0.6f;
            _click = AudioClip.Create("blinker tick", n, 1, 44100, false);
            _click.SetData(d, 0);
        }
        _tick = gameObject.AddComponent<AudioSource>();
        _tick.spatialBlend = 0f; _tick.volume = 0.35f; _tick.playOnAwake = false;
        var args = System.Environment.GetCommandLineArgs();
        int bi = System.Array.IndexOf(args, "-blinker");
        if (_blinkers != null && bi >= 0 && bi + 1 < args.Length && System.Enum.TryParse<BlinkerState>(args[bi + 1], true, out var st)) _blinkers.SwitchToState(st);
    }

    void Update()
    {
        if (_blinkers == null) return;
        bool left = Input.GetKeyDown(LeftKey) || VehicleInput.GetButtonDown("blinker_left");
        bool right = Input.GetKeyDown(RightKey) || VehicleInput.GetButtonDown("blinker_right");
        if (VehicleInput.GetButtonDown("blinker_clear")) Set(BlinkerState.None);
        if (left) Set(_blinkers.State == BlinkerState.Left ? BlinkerState.None : BlinkerState.Left);
        if (right) Set(_blinkers.State == BlinkerState.Right ? BlinkerState.None : BlinkerState.Right);
        // self-cancelling: after steering into the turn, cancel when the wheel is back near the centre
        if (_blinkers.State != BlinkerState.None)
        {
            float steer = VehicleInput.GetAxis("Horizontal");
            bool intoTurn = _blinkers.State == BlinkerState.Left ? steer < -TurnSteer : steer > TurnSteer;
            if (intoTurn) _turned = true;
            else if (_turned && Mathf.Abs(steer) < CentreSteer) Set(BlinkerState.None);
        }
        if (_blinkers.LampOn != _lastLamp) { _lastLamp = _blinkers.LampOn; if (_tick != null && _click != null) _tick.PlayOneShot(_click, _lastLamp ? 1f : 0.6f); }
    }

    void Set(BlinkerState s)
    {
        _turned = false;
        _blinkers.SwitchToState(s);
    }
}
