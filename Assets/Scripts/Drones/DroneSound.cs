using UnityEngine;

/// <summary>
/// The sound of a drone's rotors: a tone at the blade-passing frequency (blades × rotor revolutions per second, about
/// 200 Hz for a consumer drone in hover, 110 Hz for an industrial one) with its harmonics, the main audible feature of
/// multirotor noise, plus broadband noise; louder with more thrust, and 3D with logarithmic roll-off (spherical
/// spreading: -6 dB per doubling of distance) from the drone. The loop is made in code (no audio files).
/// </summary>
[RequireComponent(typeof(DroneController))]
public class DroneSound : MonoBehaviour
{
    const float Fundamental = 100f;   // the loop's tone; the pitch scales it to the blade-passing frequency
    static AudioClip _clip;
    AudioSource _src;
    DroneController _drone;
    float _startY;

    static AudioClip Clip()
    {
        if (_clip != null) return _clip;
        int rate = 44100, n = rate;   // one second: whole periods of 100 Hz, so it loops without a click
        var data = new float[n];
        var rng = new System.Random(5);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float tone = 0f;
            for (int h = 1; h <= 6; h++) tone += Mathf.Sin(2f * Mathf.PI * Fundamental * h * t + h * 1.3f) / h;
            float white = (float)rng.NextDouble() * 2f - 1f;
            lp += (white - lp) * 0.15f;   // a little low-passed: the "whoosh"
            data[i] = 0.32f * tone + 0.35f * lp;
        }
        _clip = AudioClip.Create("drone rotors", n, 1, rate, false);
        _clip.SetData(data, 0);
        return _clip;
    }

    void Start()
    {
        _drone = GetComponent<DroneController>();
        _startY = transform.position.y;
        _src = gameObject.AddComponent<AudioSource>();
        _src.clip = Clip();
        _src.loop = true;
        _src.spatialBlend = 1f;
        _src.rolloffMode = AudioRolloffMode.Logarithmic;
        _src.minDistance = 2f * _drone.Parameters.ArmLength / 0.19f;
        _src.maxDistance = 400f;
        _src.dopplerLevel = 0.5f;
        _src.volume = 0f;
        _src.Play();
    }

    void Update()
    {
        if (_src == null || _drone == null) return;
        // a remote participant's drone (its controller off here): flying once it has risen from where it was spawned
        bool simulated = _drone.Model != null && _drone.Simulate && _drone.enabled;
        bool flying = simulated ? !_drone.Landed : (_drone.Model != null ? !_drone.Landed : transform.position.y > _startY + 0.3f);
        float bpf = simulated ? _drone.BladePassingHz : (flying ? _drone.Parameters.Blades * _drone.Parameters.HoverRotorSpeed / (2f * Mathf.PI) : 0f);
        if (!_drone.enabled) _drone.SpinPropellers(flying ? _drone.Parameters.HoverRotorSpeed : 0f);   // another participant's drone
        _src.pitch = Mathf.Clamp(bpf / Fundamental, 0.2f, 3f);
        float thrust = simulated ? _drone.ThrustRatio : (flying ? 1f : 0f);
        _src.volume = bpf < 5f ? 0f : Mathf.Clamp01(0.35f + 0.45f * thrust) * (_drone.Parameters.Mass > 3f ? 1f : 0.7f);
    }
}
