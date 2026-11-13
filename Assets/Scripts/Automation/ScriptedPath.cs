using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoupledSim.Experiments;
using UnityEngine;
using UnityStandardAssets.Utility;

/// <summary>
/// A path from a file for a car to follow (#30): CSV with columns x and z (and y), optionally
/// speed (km/h) and t (s). With t, the car is where the file says at each moment (replay of a
/// recorded trajectory); with speed, it follows that speed profile along the path, braking in
/// time for slower points; otherwise it drives at DefaultSpeedKmh. Files are looked up as given,
/// then in StreamingAssets/Paths. Used by <see cref="ScriptedPathSpawner"/> (AI cars) and the
/// trial parameter avPath (the participant's AV, <see cref="AutomatedDriving"/>).
/// </summary>
public class ScriptedPath
{
    public readonly List<Vector3> Points = new List<Vector3>();
    public readonly List<float> Speeds = new List<float>();  // m/s, empty = none
    public readonly List<float> Times = new List<float>();   // s, empty = none
    public readonly List<float> Distances = new List<float>();
    /// <summary>Highest speed (m/s) through each point's bend, for MaxLateralAcceleration.</summary>
    public readonly List<float> CornerSpeeds = new List<float>();
    public const float MaxLateralAcceleration = 2.5f;
    public float Length => Distances.Count > 0 ? Distances[Distances.Count - 1] : 0f;
    public bool HasTimes => Times.Count == Points.Count && Points.Count > 1;
    public bool HasSpeeds => Speeds.Count == Points.Count && Points.Count > 1;

    public static string Resolve(string file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        foreach (var p in new[] { file, Path.Combine(Application.streamingAssetsPath, "Paths", file), Path.Combine(Application.streamingAssetsPath, file) })
            if (File.Exists(p)) return p;
        return null;
    }

    public static ScriptedPath Load(string file, float groundY = float.NaN)
    {
        var full = Resolve(file);
        if (full == null) throw new FileNotFoundException("path file not found (also looked in StreamingAssets/Paths)", file);
        return Parse(File.ReadAllText(full), groundY);
    }

    public static ScriptedPath Parse(string csv, float groundY = float.NaN)
    {
        var table = TrialTable.ParseCsv(csv);
        if (!table.Columns.Any(c => c.Equals("x", StringComparison.OrdinalIgnoreCase)) || !table.Columns.Any(c => c.Equals("z", StringComparison.OrdinalIgnoreCase)))
            throw new FormatException("a path file needs the columns x and z");
        bool hasY = table.Columns.Any(c => c.Equals("y", StringComparison.OrdinalIgnoreCase));
        bool hasSpeed = table.Columns.Any(c => c.Equals("speed", StringComparison.OrdinalIgnoreCase));
        bool hasT = table.Columns.Any(c => c.Equals("t", StringComparison.OrdinalIgnoreCase) || c.Equals("time", StringComparison.OrdinalIgnoreCase));
        string tCol = table.Columns.FirstOrDefault(c => c.Equals("t", StringComparison.OrdinalIgnoreCase) || c.Equals("time", StringComparison.OrdinalIgnoreCase));
        var path = new ScriptedPath();
        for (int r = 0; r < table.Rows.Count; r++)
        {
            float x = TrialTable.ParseFloat(table.Get(r, "x"), float.NaN), z = TrialTable.ParseFloat(table.Get(r, "z"), float.NaN);
            if (float.IsNaN(x) || float.IsNaN(z)) continue;
            float y = hasY ? TrialTable.ParseFloat(table.Get(r, "y"), 0f) : float.IsNaN(groundY) ? 0f : groundY;
            var p = new Vector3(x, y, z);
            // drop repeated points (standstill in recordings) unless they carry time
            if (path.Points.Count > 0 && (p - path.Points[path.Points.Count - 1]).sqrMagnitude < 1e-4f && !hasT) continue;
            path.Points.Add(p);
            if (hasSpeed) path.Speeds.Add(TrialTable.ParseFloat(table.Get(r, "speed"), 0f) / 3.6f);
            if (hasT) path.Times.Add(TrialTable.ParseFloat(table.Get(r, tCol), 0f));
        }
        if (path.Points.Count < 2) throw new FormatException("a path needs at least two points");
        float d = 0f;
        for (int i = 0; i < path.Points.Count; i++)
        {
            if (i > 0) d += Vector3.Distance(path.Points[i - 1], path.Points[i]);
            path.Distances.Add(d);
        }
        // corner radius from the turn angle and the shorter neighbouring segment (as the route is smoothed)
        for (int i = 0; i < path.Points.Count; i++)
        {
            float v = float.PositiveInfinity;
            if (i > 0 && i < path.Points.Count - 1)
            {
                var a = path.Points[i] - path.Points[i - 1]; var b = path.Points[i + 1] - path.Points[i];
                a.y = 0; b.y = 0;
                float angle = Vector3.Angle(a, b) * Mathf.Deg2Rad;
                if (angle > 0.02f)
                {
                    float r = 0.5f * Mathf.Min(a.magnitude, b.magnitude) / Mathf.Tan(angle / 2f);
                    v = Mathf.Sqrt(MaxLateralAcceleration * Mathf.Max(1f, r));
                }
            }
            path.CornerSpeeds.Add(v);
        }
        return path;
    }

    static float Interp(List<float> xs, List<float> ys, float x)
    {
        if (x <= xs[0]) return ys[0];
        for (int i = 1; i < xs.Count; i++)
            if (x <= xs[i]) return xs[i] - xs[i - 1] < 1e-6f ? ys[i] : Mathf.Lerp(ys[i - 1], ys[i], (x - xs[i - 1]) / (xs[i] - xs[i - 1]));
        return ys[ys.Count - 1];
    }

    /// <summary>Distance along the path at time t (timestamped paths).</summary>
    public float DistanceAt(float t) => Interp(Times, Distances, t);

    /// <summary>
    /// Speed (m/s) to drive at distance s: the profile's speed, limited so the car can still slow
    /// down to the speeds of the points ahead with <paramref name="decel"/> (m/s²); 0 at the end.
    /// </summary>
    public float TargetSpeed(float s, float defaultSpeed, float decel)
    {
        float v = HasSpeeds ? Interp(Distances, Speeds, s) : defaultSpeed;
        for (int i = 0; i < Points.Count; i++)
        {
            float ahead = Distances[i] - s;
            // still in a bend: keep its speed until a few metres past the corner point
            if (ahead <= 0f) { if (ahead > -6f) v = Mathf.Min(v, CornerSpeeds[i]); continue; }
            float vi = i == Points.Count - 1 ? 0f : Mathf.Min(HasSpeeds ? Speeds[i] : defaultSpeed, CornerSpeeds[i]);
            v = Mathf.Min(v, Mathf.Sqrt(vi * vi + 2f * decel * ahead));
        }
        return Mathf.Max(0f, v);
    }

    /// <summary>Distance along the circuit of <see cref="BuildCircuit"/> where the path starts (after the lead-in).</summary>
    public const float LeadIn = 50f;

    /// <summary>
    /// A waypoint circuit through the points (child waypoints of a new object), for AICar to steer
    /// along. Start trackers at <see cref="LeadIn"/> (WaypointProgressTracker.SetProgress).
    /// </summary>
    public WaypointCircuit BuildCircuit(string name)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        // the circuit is a loop: two extra points straight on past the end keep the car from
        // steering back to the start while it stops at the last point
        // the circuit is a loop: straight lead-in points before the start keep the closing segment
        // from bending the route at the start
        var first = Points[1] - Points[0]; first.y = 0;
        first = first.sqrMagnitude > 1e-6f ? first.normalized : Vector3.forward;
        var pts = new List<Vector3> { Points[0] - first * LeadIn, Points[0] - first * (LeadIn / 2f) };
        pts.AddRange(Points);
        var dir = Points[Points.Count - 1] - Points[Points.Count - 2]; dir.y = 0;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
        pts.Add(Points[Points.Count - 1] + dir * 25f);
        pts.Add(Points[Points.Count - 1] + dir * 50f);
        var items = new Transform[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            var w = new GameObject("Waypoint " + i.ToString("000")).transform;
            w.SetParent(root.transform, false);
            w.position = pts[i];
            var next = i + 1 < pts.Count ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
            if (next.sqrMagnitude > 1e-6f) w.rotation = Quaternion.LookRotation(new Vector3(next.x, 0, next.z));
            items[i] = w;
        }
        var circuit = root.AddComponent<WaypointCircuit>();
        circuit.waypointList.items = items;
        // straight lines between the points: a spline overshoots where long and short segments meet
        // (recorded trajectories are dense, so they stay smooth)
        circuit.smoothRoute = false;
        root.SetActive(true);
        return circuit;
    }
}
