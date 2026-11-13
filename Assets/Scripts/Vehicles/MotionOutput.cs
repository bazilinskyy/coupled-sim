using System;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// Motion cues of the participant's vehicle for a motion platform or seat, sent over UDP at the
/// physics rate (or <see cref="Rate"/>): accelerations in the vehicle's axes (surge forward, sway
/// right, heave up; m/s², gravity removed), angular velocities (roll, pitch, yaw; deg/s), attitude
/// (roll, pitch, yaw; deg) and speed (m/s). Motion software (FlyPT Mover, SimTools, own
/// controllers) reads them with a custom UDP input.
/// Turn on with -motionUdp host:port; -motionFormat json (default), csv
/// ("time,surge,sway,heave,roll_rate,pitch_rate,yaw_rate,roll,pitch,yaw,speed") or flypt
/// (the same values separated by spaces, one packet per line); -motionRate 100.
/// </summary>
public class MotionOutput : MonoBehaviour
{
    public enum Format { Json, Csv, Flypt }
    public string Host = "127.0.0.1";
    public int Port = 4123;
    public Format OutputFormat = Format.Json;
    public float Rate = 100f;
    [Tooltip("Low-pass time constant of the accelerations (s).")]
    public float Smoothing = 0.03f;

    Rigidbody _rb;
    UdpClient _udp;
    Vector3 _lastVel, _acc;
    bool _first = true;
    float _next;

    /// <summary>Adds the output to the participant's vehicle when -motionUdp is given.</summary>
    public static void AddIfAsked(GameObject vehicle)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-motionUdp");
        if (i < 0 || i + 1 >= args.Length) return;
        var hp = args[i + 1].Split(':');
        var m = vehicle.AddComponent<MotionOutput>();
        m.Host = hp[0];
        if (hp.Length > 1 && int.TryParse(hp[1], out var port)) m.Port = port;
        int f = Array.IndexOf(args, "-motionFormat");
        if (f >= 0 && f + 1 < args.Length && Enum.TryParse<Format>(args[f + 1], true, out var fmt)) m.OutputFormat = fmt;
        int r = Array.IndexOf(args, "-motionRate");
        if (r >= 0 && r + 1 < args.Length && float.TryParse(args[r + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)) m.Rate = rate;
    }

    void Start()
    {
        _rb = GetComponentInParent<Rigidbody>();
        _udp = new UdpClient();
        Debug.Log($"MotionOutput: {OutputFormat} to {Host}:{Port} at {Rate} Hz");
    }

    void OnDestroy() => _udp?.Dispose();

    void FixedUpdate()
    {
        var t = _rb != null ? _rb.transform : transform;
        var vel = _rb != null && !_rb.isKinematic ? _rb.linearVelocity : Vector3.zero;
        if (_rb == null || _rb.isKinematic) { vel = (t.position - _lastPos) / Time.fixedDeltaTime; _lastPos = t.position; }
        if (_first) { _lastVel = vel; _first = false; }
        // specific force felt by the occupant: acceleration minus gravity, in the vehicle's axes
        var a = (vel - _lastVel) / Time.fixedDeltaTime - Physics.gravity;
        _lastVel = vel;
        float k = Smoothing > 0f ? 1f - Mathf.Exp(-Time.fixedDeltaTime / Smoothing) : 1f;
        _acc = Vector3.Lerp(_acc, t.InverseTransformDirection(a), k);
        if (Time.time < _next) return;
        _next = Time.time + 1f / Mathf.Max(1f, Rate);
        var w = _rb != null ? t.InverseTransformDirection(_rb.angularVelocity) * Mathf.Rad2Deg : Vector3.zero;
        var e = t.eulerAngles;
        float roll = Mathf.DeltaAngle(0f, e.z), pitch = Mathf.DeltaAngle(0f, e.x), yaw = e.y;
        float speed = Vector3.Dot(vel, t.forward);
        // Unity: x right, y up, z forward; surge = z, sway = x, heave = y (minus 1 g at rest)
        float surge = _acc.z, sway = _acc.x, heave = _acc.y - 9.81f;
        // roll rate about forward (z), pitch rate about right (x), yaw rate about up (y)
        float rr = -w.z, pr = w.x, yr = w.y;
        var ci = CultureInfo.InvariantCulture;
        string F(float v) => v.ToString("F3", ci);
        string msg;
        switch (OutputFormat)
        {
            case Format.Csv: msg = string.Join(",", F(Time.time), F(surge), F(sway), F(heave), F(rr), F(pr), F(yr), F(roll), F(pitch), F(yaw), F(speed)); break;
            case Format.Flypt: msg = string.Join(" ", F(surge), F(sway), F(heave), F(rr), F(pr), F(yr), F(roll), F(pitch), F(yaw), F(speed)) + "\n"; break;
            default:
                msg = $"{{\"time\":{F(Time.time)},\"surge\":{F(surge)},\"sway\":{F(sway)},\"heave\":{F(heave)},\"roll_rate\":{F(rr)},\"pitch_rate\":{F(pr)},\"yaw_rate\":{F(yr)},\"roll\":{F(roll)},\"pitch\":{F(pitch)},\"yaw\":{F(yaw)},\"speed\":{F(speed)}}}";
                break;
        }
        var bytes = Encoding.ASCII.GetBytes(msg);
        try { _udp.Send(bytes, bytes.Length, Host, Port); } catch (Exception ex) { Debug.LogWarning("MotionOutput: " + ex.Message); enabled = false; }
    }

    Vector3 _lastPos;
}
