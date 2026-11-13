using System;
using System.Globalization;
using UnityEngine;

/// <summary>Plays the cues of <see cref="AuditoryEhmiSettings"/> for one car (added by PlayerAvatar).</summary>
public class AuditoryEhmi : MonoBehaviour
{
    AuditoryEhmiSettings _settings;
    HMI[] _hmis;
    HMIState _lastState = HMIState.DISABLED;
    AudioSource _cueSource, _avasSource;
    float[] _lastPlayed;
    bool[] _active;
    float _speed, _lastSpeed, _start, _stoppedSince = -1f;
    Vector3 _lastPos;
    bool _wasStopped, _braking;

    public void Init(AuditoryEhmiSettings settings)
    {
        _settings = settings;
        _hmis = GetComponentsInChildren<HMI>(true);
        _lastPlayed = new float[settings.Cues.Length];
        _active = new bool[settings.Cues.Length];
        for (int i = 0; i < _lastPlayed.Length; i++) _lastPlayed[i] = -999f;
        _start = Time.time;
        _lastPos = transform.position;
        var front = new GameObject("Auditory eHMI");
        front.transform.SetParent(transform, false);
        front.transform.localPosition = new Vector3(0f, 0.8f, 1.8f);
        _cueSource = Source(front);
        _avasSource = Source(front);
        _avasSource.clip = AvasClip();
        _avasSource.loop = true;
    }

    static AudioSource Source(GameObject go)
    {
        var s = go.AddComponent<AudioSource>();
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 3f;
        s.maxDistance = 80f;
        s.playOnAwake = false;
        return s;
    }

    void Update()
    {
        if (_settings == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        var pos = transform.position;
        float v = Vector3.Dot(pos - _lastPos, transform.forward) / dt;
        _lastPos = pos;
        _speed = Mathf.Lerp(_speed, v, 1f - Mathf.Exp(-dt * 5f));
        float accel = (_speed - _lastSpeed) / dt;
        _lastSpeed = _speed;

        // AVAS: a hum whose pitch follows the speed, fading out from 15 to 20 km/h
        if (_settings.Enabled && _settings.Avas)
        {
            float kmh = Mathf.Abs(_speed) * 3.6f;
            float gain = kmh < 0.5f ? 0.3f : Mathf.Clamp01((20f - kmh) / 5f);
            _avasSource.volume = _settings.AvasVolume * gain;
            _avasSource.pitch = 0.8f + kmh / 25f;
            if (!_avasSource.isPlaying) _avasSource.Play();
        }
        else if (_avasSource.isPlaying) _avasSource.Stop();
        if (!_settings.Enabled) return;

        var state = HMIState.DISABLED;
        foreach (var h in _hmis) if (h != null && h.CurrentState != HMIState.DISABLED) { state = h.CurrentState; break; }
        bool stopped = Mathf.Abs(_speed) < 0.2f;
        bool startBraking = !_braking && _speed > 2.8f && accel < -1f;
        _braking = _speed > 0.5f && accel < -0.5f;
        bool justStopped = stopped && !_wasStopped;
        if (justStopped) _stoppedSince = Time.time;
        bool drivingOff = !stopped && _wasStopped && Time.time - _stoppedSince > 1f;
        _wasStopped = stopped;

        for (int i = 0; i < _settings.Cues.Length; i++)
        {
            var c = _settings.Cues[i];
            bool condition, edge;
            switch (c.Trigger)
            {
                case AuditoryCueTrigger.EhmiStop: condition = state == HMIState.STOP; edge = condition && _lastState != HMIState.STOP; break;
                case AuditoryCueTrigger.EhmiWalk: condition = state == HMIState.WALK; edge = condition && _lastState != HMIState.WALK; break;
                case AuditoryCueTrigger.Yielding: condition = _braking; edge = startBraking; break;
                case AuditoryCueTrigger.Stopped: condition = stopped; edge = justStopped; break;
                case AuditoryCueTrigger.DrivingOff: condition = edge = drivingOff; break;
                case AuditoryCueTrigger.PedestrianAhead: condition = PedestrianAhead(c.Value > 0f ? c.Value : 15f); edge = condition && !_active[i]; break;
                case AuditoryCueTrigger.AtTime: edge = condition = Time.time - _start >= c.Value && _lastPlayed[i] < 0f; break;
                default: condition = edge = false; break;
            }
            if (edge || (condition && c.Repeat > 0f && Time.time - _lastPlayed[i] >= c.Repeat)) Play(i, c);
            _active[i] = condition;
        }
        _lastState = state;
    }

    bool PedestrianAhead(float distance)
    {
        foreach (var a in TrafficPeople())
        {
            var d = transform.InverseTransformPoint(a.position);
            if (d.z > 0f && d.z < distance && Mathf.Abs(d.x) < 3f) return true;
        }
        return false;
    }

    static readonly System.Collections.Generic.List<Transform> _people = new System.Collections.Generic.List<Transform>();
    static int _peopleFrame = -1;
    static System.Collections.Generic.List<Transform> TrafficPeople()
    {
        if (_peopleFrame == Time.frameCount) return _people;
        _peopleFrame = Time.frameCount;
        _people.Clear();
        foreach (var a in FindObjectsByType<PlayerAvatar>()) if (a.Type == AvatarType.Pedestrian && a.gameObject.activeInHierarchy) _people.Add(a.transform);
        foreach (var p in FindObjectsByType<AIPedestrian>()) if (p.gameObject.activeInHierarchy) _people.Add(p.transform);
        return _people;
    }

    void Play(int i, AuditoryCue c)
    {
        _lastPlayed[i] = Time.time;
        var clip = c.Sound == AuditoryCueSound.Custom ? c.Clip : Clip(c.Sound);
        if (clip == null) return;
        _cueSource.PlayOneShot(clip, c.Volume > 0f ? c.Volume : 0.8f);
        AuditoryEhmiLog.Write(name, c.Trigger.ToString(), c.Sound.ToString(), transform.position);
    }

    // ---- generated sounds

    static AudioClip _chime, _beeps, _ding, _avas;
    const int Rate = 22050;

    static AudioClip Make(string name, float seconds, Func<float, float> f)
    {
        var data = new float[(int)(seconds * Rate)];
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Bell(float t, float f) => Mathf.Exp(-t * 4f) * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 2.76f * f * t) * Mathf.Exp(-t * 6f));

    public static AudioClip Clip(AuditoryCueSound s)
    {
        switch (s)
        {
            // two descending bell tones (E5, C5)
            case AuditoryCueSound.Chime: return _chime ??= Make("chime", 1.6f, t => 0.45f * (Bell(t, 659.3f) + (t > 0.35f ? Bell(t - 0.35f, 523.3f) : 0f)));
            // three short 1.5 kHz beeps
            case AuditoryCueSound.Beeps: return _beeps ??= Make("beeps", 0.9f, t => (t % 0.3f) < 0.15f ? 0.4f * Mathf.Sin(2f * Mathf.PI * 1500f * t) : 0f);
            case AuditoryCueSound.Ding: return _ding ??= Make("ding", 1.2f, t => 0.5f * Bell(t, 880f));
            default: return null;
        }
    }

    // AVAS: a soft two-tone hum with a slow modulation
    static AudioClip AvasClip() => _avas ??= Make("avas", 2f, t =>
        0.25f * Mathf.Sin(2f * Mathf.PI * 220f * t) * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 2f * t))
        + 0.12f * Mathf.Sin(2f * Mathf.PI * 330f * t) + 0.05f * Mathf.Sin(2f * Mathf.PI * 660f * t));
}

/// <summary>ExperimentLogs/AuditoryEhmi-{date}.csv: when which car played which cue.</summary>
public static class AuditoryEhmiLog
{
    static System.IO.StreamWriter _w;
    public static void Write(string car, string trigger, string sound, Vector3 p)
    {
        try
        {
            if (_w == null)
            {
                System.IO.Directory.CreateDirectory("ExperimentLogs");
                _w = new System.IO.StreamWriter($"ExperimentLogs/AuditoryEhmi-{NetworkingManager.LogPrefix}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv") { AutoFlush = true };
                _w.WriteLine("time,car,trigger,sound,x,z");
                Application.quitting += () => { _w?.Dispose(); _w = null; };
            }
            var ci = CultureInfo.InvariantCulture;
            _w.WriteLine($"{Time.time.ToString("F3", ci)},\"{car}\",{trigger},{sound},{p.x.ToString("F2", ci)},{p.z.ToString("F2", ci)}");
        }
        catch (Exception e) { Debug.LogWarning("AuditoryEhmiLog: " + e.Message); }
    }
}
