using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Haptic cues for the participant: a named cue ("takeover", or any name an experiment uses) with an
/// intensity (0-1), a duration and a pattern ("pulse": one, "double", "triple", "continuous") is played on
/// - gamepads (rumble motors),
/// - the controllers of a headset (OpenXR haptic impulse),
/// - Logitech steering wheels (vibration through the Logitech SDK, see SteeringFeedback),
/// - external devices (vibrating seats, belts, helmets, bike handlebars): a UDP message in JSON,
///   {"cue":"takeover","intensity":1.0,"duration":1.0,"pattern":"triple","time":12.34}, to every
///   address given with -hapticsUdp host:port (several separated by commas), e.g. an Arduino or
///   Raspberry Pi bridge.
/// Every cue is logged to ExperimentLogs/Haptics-{date}.csv. Take-over requests play "takeover".
/// </summary>
public static class Haptics
{
    static readonly List<(string host, int port)> _targets = new List<(string, int)>();
    static UdpClient _udp;
    static bool _init;
    static StreamWriter _log;
    static Runner _runner;

    class Runner : MonoBehaviour { }

    static void Init()
    {
        if (_init) return;
        _init = true;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-hapticsUdp")
                foreach (var t in args[i + 1].Split(','))
                {
                    var hp = t.Trim().Split(':');
                    if (hp.Length == 2 && int.TryParse(hp[1], out var port)) _targets.Add((hp[0], port));
                }
        if (_targets.Count > 0)
        {
            _udp = new UdpClient();
            Debug.Log("Haptics: UDP cues to " + string.Join(", ", _targets.ConvertAll(t => t.host + ":" + t.port)));
        }
        Application.quitting += () => { _log?.Dispose(); _udp?.Dispose(); };
    }

    /// <summary>Adds an external device address (the same as -hapticsUdp host:port).</summary>
    public static void AddTarget(string host, int port) { Init(); _targets.Add((host, port)); _udp ??= new UdpClient(); }

    /// <summary>Plays a cue on all haptic outputs.</summary>
    public static void Cue(string name, float intensity = 1f, float duration = 0.5f, string pattern = "pulse")
    {
        Init();
        intensity = Mathf.Clamp01(intensity);
        var ci = CultureInfo.InvariantCulture;
        try
        {
            if (_log == null)
            {
                Directory.CreateDirectory("ExperimentLogs");
                _log = new StreamWriter($"ExperimentLogs/Haptics-{NetworkingManager.LogPrefix}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv") { AutoFlush = true };
                _log.WriteLine("utc;time;cue;intensity;duration;pattern");
            }
            _log.WriteLine(string.Join(";", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", ci), Time.time.ToString("F3", ci), name, intensity.ToString("F2", ci), duration.ToString("F2", ci), pattern));
        }
        catch (Exception e) { Debug.LogWarning("Haptics: log " + e.Message); }

        if (_udp != null)
        {
            var json = $"{{\"cue\":\"{name.Replace("\"", "'")}\",\"intensity\":{intensity.ToString("F2", ci)},\"duration\":{duration.ToString("F2", ci)},\"pattern\":\"{pattern}\",\"time\":{Time.time.ToString("F3", ci)}}}";
            var bytes = Encoding.UTF8.GetBytes(json);
            foreach (var (host, port) in _targets)
                try { _udp.Send(bytes, bytes.Length, host, port); } catch (Exception e) { Debug.LogWarning($"Haptics: UDP to {host}:{port}: {e.Message}"); }
        }
        if (_runner == null)
        {
            var go = new GameObject("Haptics");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }
        _runner.StartCoroutine(Play(intensity, duration, pattern));
    }

    static IEnumerator Play(float intensity, float duration, string pattern)
    {
        int pulses = pattern == "double" ? 2 : pattern == "triple" ? 3 : 1;
        float on = pattern == "continuous" || pulses == 1 ? duration : duration / (pulses * 2 - 1);
        for (int p = 0; p < pulses; p++)
        {
            Set(intensity, on);
            yield return new WaitForSecondsRealtime(on);
            Set(0f, 0f);
            if (p < pulses - 1) yield return new WaitForSecondsRealtime(on);
        }
    }

    static readonly List<UnityEngine.XR.InputDevice> _xr = new List<UnityEngine.XR.InputDevice>();

    static void Set(float intensity, float seconds)
    {
        foreach (var pad in Gamepad.all) pad.SetMotorSpeeds(intensity * 0.8f, intensity);
        if (intensity > 0f)
        {
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.Controller, _xr);
            foreach (var d in _xr)
                if (d.TryGetHapticCapabilities(out var cap) && cap.supportsImpulse) d.SendHapticImpulse(0, intensity, seconds);
        }
        SteeringFeedback.Vibrate(intensity);
    }
}
