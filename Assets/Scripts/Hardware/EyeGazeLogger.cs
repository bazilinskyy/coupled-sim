using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Logs the participant's eye gaze for headsets with eye tracking (OpenXR eye gaze: Varjo, Meta
/// Quest Pro, HTC Vive Pro Eye, Pico 4 Pro, HP Reverb G2 Omnicept) to
/// ExperimentLogs/EyeGaze-{date}.csv: time, gaze origin and direction in world coordinates, and the
/// object looked at (raycast) with its distance. Started by XRSession; does nothing without eye tracking.
/// </summary>
public class EyeGazeLogger : MonoBehaviour
{
    [Tooltip("Samples per second (the eye tracker may be faster; frames are the limit).")]
    public float Rate = 90f;
    public float MaxDistance = 200f;

    readonly List<InputDevice> _devices = new List<InputDevice>();
    StreamWriter _writer;
    float _next;

    void Update()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 1f / Rate;
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, _devices);
        if (_devices.Count == 0) return;
        var eyes = _devices[0];
        if (!eyes.TryGetFeatureValue(CommonUsages.devicePosition, out var localPos) ||
            !eyes.TryGetFeatureValue(CommonUsages.deviceRotation, out var localRot)) return;
        var cam = Camera.main;
        if (cam == null) return;
        // the gaze pose is in tracking space: the space of the camera's parent (the XR origin)
        var origin = cam.transform.parent;
        var pos = origin != null ? origin.TransformPoint(localPos) : localPos;
        var dir = (origin != null ? origin.rotation * localRot : localRot) * Vector3.forward;
        string hitName = "";
        float hitDistance = -1f;
        if (Physics.Raycast(pos, dir, out var hit, MaxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            hitName = hit.collider.transform.root.name;
            hitDistance = hit.distance;
        }
        if (_writer == null)
        {
            Directory.CreateDirectory("ExperimentLogs");
            _writer = new StreamWriter("ExperimentLogs/EyeGaze-" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv") { AutoFlush = false };
            _writer.WriteLine("time,origin_x,origin_y,origin_z,dir_x,dir_y,dir_z,object,distance");
        }
        var ci = CultureInfo.InvariantCulture;
        _writer.WriteLine(string.Join(",", Time.time.ToString("F4", ci),
            pos.x.ToString("F3", ci), pos.y.ToString("F3", ci), pos.z.ToString("F3", ci),
            dir.x.ToString("F4", ci), dir.y.ToString("F4", ci), dir.z.ToString("F4", ci),
            "\"" + hitName.Replace("\"", "'") + "\"", hitDistance.ToString("F2", ci)));
    }

    void OnDestroy() => _writer?.Dispose();
    void OnApplicationQuit() => _writer?.Dispose();
}
