using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>Recenters the head-mounted display on all running XR input subsystems.</summary>
public static class XRRecenter
{
    static readonly List<XRInputSubsystem> _subsystems = new List<XRInputSubsystem>();

    public static void Recenter()
    {
        SubsystemManager.GetSubsystems(_subsystems);
        foreach (var s in _subsystems)
        {
            if (s.running) s.TryRecenter();
        }
    }
}
