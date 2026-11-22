using CoupledSim.Sound;
using UnityEngine;

/// <summary>
/// The sound of a drone's rotors (<see cref="SoundClips.DroneRotors"/>): the four rotors' tones at the blade-passing
/// frequency (blades × rotor revolutions per second, about 200 Hz for a consumer drone in hover, 110 Hz for an
/// industrial one) with their harmonics, the main audible feature of multirotor noise, beating as the rotors turn at
/// slightly different speeds, plus broadband noise. Its level is in dB SPL like the <see cref="Soundscape"/>'s: the
/// sound power of the drone's class (<see cref="SoundPowerDbA"/>), louder with more thrust, falling by 6 dB per doubling
/// of distance (3D, logarithmic roll-off), with air absorption, the Doppler shift and, heard from inside the
/// participant's car, its body's 20 dB.
/// </summary>
[RequireComponent(typeof(DroneController))]
public class DroneSound : MonoBehaviour
{
    static SoundClips.Recording _rec;
    AudioSource _src;
    AudioLowPassFilter _lp;
    DroneController _drone;
    float _startY;

    /// <summary>
    /// A-weighted sound power (dB re 1 pW) of a multirotor of <paramref name="kg"/> in hover: the limits of Regulation
    /// (EU) 2019/945 (Annex, Part 15, four years after entry into force) for classes C1 and C2, 81 dB(A) below 900 g
    /// and 18.5 dB more per tenfold mass above it (a consumer drone of 0.9 kg: 81 dB(A); an industrial delivery drone
    /// of 6.3 kg, beyond the 4 kg of class C2, 97 dB(A)). These are the limits for drones sold in the EU, taken as
    /// their level.
    /// </summary>
    public static float SoundPowerDbA(float kg) => 81f + 18.5f * Mathf.Log10(Mathf.Max(kg, 0.9f) / 0.9f);

    /// <summary>A-weighted level (dB(A)) at <paramref name="metres"/> in free field (spherical spreading) of a drone of
    /// <paramref name="kg"/> at <paramref name="thrustRatio"/> times its hover thrust: rotor noise grows with about the
    /// fifth power of the rotor speed, and the thrust with its square (25 dB per tenfold thrust, within -10..+6 dB).</summary>
    public static float LevelDbA(float kg, float thrustRatio, float metres) =>
        SoundPowerDbA(kg) + Mathf.Clamp(25f * Mathf.Log10(Mathf.Max(thrustRatio, 1e-3f)), -10f, 6f) - 10f * Mathf.Log10(4f * Mathf.PI * metres * metres);

    void Start()
    {
        _drone = GetComponent<DroneController>();
        _startY = transform.position.y;
        _rec = SoundClips.DroneRotors();
        _src = gameObject.AddComponent<AudioSource>();
        _src.clip = _rec.Clip;
        _src.loop = true;
        _src.spatialBlend = 1f;
        _src.rolloffMode = AudioRolloffMode.Logarithmic;
        // closer than this it stays as loud (1 m; a heavier drone's rotors spread wider and would clip the volume:
        // up to where it is 80 dB(A) at full thrust)
        _src.minDistance = Mathf.Max(1f, Mathf.Pow(10f, (LevelDbA(_drone.Parameters.Mass, 4f, 1f) - 80f) / 20f));
        _src.maxDistance = 1000f;
        _src.dopplerLevel = 1f;
        _src.volume = 0f;
        _src.time = Random.value * _rec.Clip.length * 0.9f;
        _lp = gameObject.AddComponent<AudioLowPassFilter>();
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
        _src.pitch = Mathf.Clamp(bpf / SoundClips.DroneBaseHz, 0.2f, 3f);
        float thrust = simulated ? _drone.ThrustRatio : (flying ? 1f : 0f);
        var sc = Soundscape.Current;
        bool inside = sc != null && sc.InsideCar;
        float cutoff = 22000f;
        if (sc != null && sc.Listener != null) cutoff = AirAbsorption.Cutoff(Vector3.Distance(sc.Listener.position, transform.position));
        if (inside) cutoff = Mathf.Min(cutoff, 1000f);
        _lp.cutoffFrequency = cutoff;
        // its A-weighted level (at the pitch it plays at) is the model's
        float db = LevelDbA(_drone.Parameters.Mass, thrust, _src.minDistance) - SoundClips.DroneAWeightDb(_src.pitch) + (inside ? -20f : 0f);
        float reference = sc != null ? sc.ReferenceDb : 94f;
        _src.volume = bpf < 5f ? 0f : Mathf.Clamp01(Mathf.Pow(10f, (db - reference) / 20f) * 0.7071f / SoundClips.Rms * _rec.Factor);
    }
}
