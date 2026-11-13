using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using CoupledSim.Pedestrians;

/// <summary>
/// A text description of the scene around a viewer (a participant or a virtual pedestrian): place,
/// time and weather, the cars within 60 m (where, how far, speed, approaching or not, braking or
/// stopped, their eHMI state) and the pedestrians nearby. Used by LLM-driven agents (LlmClient,
/// PedestrianGroup's Llm companions) and, with -sceneDescriptions, logged once a second for the
/// participant to ExperimentLogs/SceneDescriptions-*.jsonl, e.g. to compare people's and language
/// models' judgements of the same moments.
/// </summary>
public static class SceneDescriber
{
    static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    public static string Describe(Transform viewer, float radius = 60f)
    {
        var sb = new StringBuilder();
        var map = OsmMap.Current != null && !string.IsNullOrEmpty(OsmMap.Current.Info.Name) ? OsmMap.Current.Info.City + ", " + OsmMap.Current.Info.Country : "a town";
        var env = EnvironmentController.Instance;
        sb.Append($"Place: {map}. ");
        if (env != null) sb.Append($"Sun elevation {env.SunElevation:F0} degrees ({(env.SunElevation > 0 ? "daylight" : "dark")}). ");
        var fwd = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, fwd);
        var cars = new List<(float d, string s)>();
        void AddCar(Transform t, float speedMs, bool braking, bool stopped)
        {
            var to = t.position - viewer.position; to.y = 0f;
            float d = to.magnitude;
            if (d > radius || d < 0.5f) return;
            float ahead = Vector3.Dot(to, fwd), side = Vector3.Dot(to, right);
            string where = Mathf.Abs(ahead) > Mathf.Abs(side) ? (ahead > 0 ? "ahead" : "behind") : (side > 0 ? "to the right" : "to the left");
            var vel = t.forward * speedMs;
            float closing = -Vector3.Dot(vel, to.normalized);
            string motion = speedMs < 0.3f ? "standing still" : $"moving at {speedMs * 3.6f:F0} km/h, {(closing > 0.5f ? "approaching you" : closing < -0.5f ? "moving away" : "passing")}";
            if (braking && speedMs >= 0.3f) motion += ", braking";
            string ehmi = "";
            foreach (var h in t.GetComponentsInChildren<HMI>()) if (h.CurrentState != HMIState.DISABLED) { ehmi = $", its external display shows {h.CurrentState}"; break; }
            cars.Add((d, $"a car {d:F0} m {where}, {motion}{ehmi}"));
        }
        foreach (var c in UnityEngine.Object.FindObjectsByType<AICar>())
            AddCar(c.transform, c.Speed / 3.6f, c.state == AICar.CarState.BRAKING, c.state == AICar.CarState.STOPPED);
        foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerAvatar>())
            if (p.Type == AvatarType.Driver && p.GetComponent<AICar>() == null)
            {
                var rb = p.GetComponent<Rigidbody>();
                AddCar(p.transform, rb != null ? Vector3.Dot(rb.linearVelocity, p.transform.forward) : 0f, false, false);
            }
        cars.Sort((a, b) => a.d.CompareTo(b.d));
        sb.Append(cars.Count == 0 ? "No cars within " + radius.ToString("F0", Ci) + " m. " : $"Cars: {string.Join("; ", cars.ConvertAll(c => c.s))}. ");
        int peds = 0, crossing = 0;
        foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerAvatar>())
        {
            if (p.Type != AvatarType.Pedestrian || p.transform == viewer || (p.transform.position - viewer.position).sqrMagnitude > 15f * 15f) continue;
            peds++;
            var ai = p.GetComponent<AIPedestrian>();
            if (ai != null && ai.Phase == CrossingPhase.Crossing) crossing++;
        }
        sb.Append(peds == 0 ? "No other pedestrians within 15 m." : $"{peds} other pedestrians within 15 m{(crossing > 0 ? $", {crossing} of them crossing" : "")}.");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ log for the participant

    sealed class Logger : MonoBehaviour
    {
        StreamWriter _w;
        float _next;
        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            var me = NetworkingManager.Instance != null && NetworkingManager.Instance.PlayerSystem != null ? NetworkingManager.Instance.PlayerSystem.LocalPlayer : null;
            if (me == null) return;
            try
            {
                if (_w == null)
                {
                    Directory.CreateDirectory("ExperimentLogs");
                    _w = new StreamWriter($"ExperimentLogs/SceneDescriptions-{NetworkingManager.LogPrefix}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.jsonl") { AutoFlush = true };
                }
                _w.WriteLine($"{{\"time\":{Time.time.ToString("F2", Ci)},\"utc\":\"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff}\",\"description\":\"{LlmClient.Escape(Describe(me.transform))}\"}}");
            }
            catch (Exception e) { Debug.LogWarning("SceneDescriber: " + e.Message); enabled = false; }
        }
        void OnDestroy() => _w?.Dispose();
    }

    public static void EnsureLoggerFromCommandLine()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sceneDescriptions") < 0) return;
        if (UnityEngine.Object.FindAnyObjectByType<Logger>() != null) return;
        new GameObject("Scene descriptions").AddComponent<Logger>();
    }
}
