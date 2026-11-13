using System;
using System.Globalization;
using UnityEngine;

public enum AuditoryCueTrigger
{
    /// <summary>The visual eHMI on the car shows STOP.</summary>
    EhmiStop,
    /// <summary>The visual eHMI on the car shows WALK.</summary>
    EhmiWalk,
    /// <summary>The car starts to brake (from above 10 km/h, decelerating at more than 1 m/s²).</summary>
    Yielding,
    /// <summary>The car has come to a stop.</summary>
    Stopped,
    /// <summary>The car drives off after a stop.</summary>
    DrivingOff,
    /// <summary>A pedestrian is closer than Distance in front of the car.</summary>
    PedestrianAhead,
    /// <summary>Seconds after the start of the trial (Time).</summary>
    AtTime,
}

public enum AuditoryCueSound { Chime, Beeps, Ding, Custom }

[Serializable]
public struct AuditoryCue
{
    public AuditoryCueTrigger Trigger;
    public AuditoryCueSound Sound;
    [Tooltip("For Custom.")]
    public AudioClip Clip;
    [Range(0f, 1f)] public float Volume;
    [Tooltip("PedestrianAhead: metres; AtTime: seconds after the start.")]
    public float Value;
    [Tooltip("Repeat every this many seconds while the condition holds (0 = once per event).")]
    public float Repeat;
}

/// <summary>
/// Auditory eHMIs (#21): sounds played by automated vehicles, from the front of the car (3D),
/// on events (the visual eHMI's state, yielding, stopping, driving off, a pedestrian ahead, a
/// time) and, optionally, an AVAS (the low-speed warning sound of electric cars, required in the
/// EU below 20 km/h). Put this on the experiment prefab; every AV in the experiment gets it on
/// every agent (events come from the synced eHMI states and the cars' motion, so all agents hear
/// the same). Trial parameters: auditoryEhmi (0 = off), avas (0/1).
/// </summary>
public class AuditoryEhmiSettings : MonoBehaviour, IExperimentModifier
{
    public bool Enabled = true;
    public AuditoryCue[] Cues =
    {
        new AuditoryCue { Trigger = AuditoryCueTrigger.EhmiWalk, Sound = AuditoryCueSound.Chime, Volume = 0.8f },
        new AuditoryCue { Trigger = AuditoryCueTrigger.EhmiStop, Sound = AuditoryCueSound.Beeps, Volume = 0.8f },
    };
    [Tooltip("Acoustic vehicle alerting system: a hum that rises with speed and fades out at 20 km/h.")]
    public bool Avas = false;
    [Range(0f, 1f)] public float AvasVolume = 0.5f;
    [Tooltip("Also on cars driven by people (MDV), not only AVs.")]
    public bool AllCars = false;

    public static AuditoryEhmiSettings Current { get; private set; }
    void Awake() => Current = this;
    void OnDestroy() { if (Current == this) Current = null; }

    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters)
    {
        foreach (var p in experimentParameters)
        {
            var n = p.name.ToLowerInvariant();
            bool on = p.value == "1" || p.value.Equals("true", StringComparison.OrdinalIgnoreCase) || p.value.Equals("on", StringComparison.OrdinalIgnoreCase);
            if (n == "auditoryehmi") Enabled = on;
            if (n == "avas") Avas = on;
        }
    }
}
