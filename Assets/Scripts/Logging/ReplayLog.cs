using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// What a recording needs for its replay besides the .binLog itself, saved next to it as
/// &lt;log&gt;.replay.json when the log starts: the experiment, its scene and map, the time of day
/// and weather, and the avatar of every vehicle and pedestrian in the log's order.
/// </summary>
[Serializable]
public class ReplayInfo
{
    public string Experiment;
    public string Scene;
    public string Map;
    public float TimeOfDay = -1f;
    public string Weather;
    public int LocalDriver = -1;
    public string[] Drivers = new string[0];        // participants' vehicles (cars, AV passengers' cars, two-wheelers)
    public string[] Pedestrians = new string[0];    // participants on foot
    public List<string> AICars = new List<string>(); // AI cars, in the order they appeared
    public List<string> Drones = new List<string>(); // "participant|Consumer|name" or "ai|Industrial|name", in the order they appeared

    public static string PathFor(string binLog) => Path.ChangeExtension(binLog, ".replay.json");

    public static ReplayInfo Load(string binLog)
    {
        var p = PathFor(binLog);
        return File.Exists(p) ? JsonUtility.FromJson<ReplayInfo>(File.ReadAllText(p)) : null;
    }

    public void Save(string binLog)
    {
        try { File.WriteAllText(PathFor(binLog), JsonUtility.ToJson(this, true)); }
        catch (Exception e) { Debug.LogWarning("Replay info not saved: " + e.Message); }
    }

    /// <summary>The prefab's name of a spawned object ("DrivableSmartCommon(Clone) (right-hand drive)" → "DrivableSmartCommon").</summary>
    public static string PrefabName(string objectName)
    {
        int i = objectName.IndexOf("(Clone)", StringComparison.Ordinal);
        return (i >= 0 ? objectName.Substring(0, i) : objectName).Trim();
    }
}

/// <summary>A recording (.binLog, written by WorldLogger) read for a replay.</summary>
public class ReplayLog
{
    public class Frame
    {
        public float Time;
        public int DriverCount;
        public Vector3[] Positions;
        public Quaternion[] Rotations;
        public BlinkerState[] Blinkers;
        public bool[] FrontLights, StopLights;
        public AvatarPose[] Pedestrians;
        public byte[] CarLights, PedestrianLights;
        public Vector3[] DronePositions;
        public Quaternion[] DroneRotations;
        public float[] DroneBladeHz;
        public bool[] DroneLanded;
    }

    public DateTime Start;
    public int LocalDriver, PersistentDrivers, PedestrianCount;
    public readonly List<Frame> Frames = new List<Frame>();
    public float Duration => Frames.Count > 0 ? Frames[Frames.Count - 1].Time : 0f;

    public static ReplayLog Read(string path)
    {
        var log = new ReplayLog();
        using (var stream = File.OpenRead(path))
        using (var r = new BinaryReader(stream))
        {
            log.Start = DateTime.FromBinary(r.ReadInt64());
            log.LocalDriver = r.ReadInt32();
            log.PersistentDrivers = r.ReadInt32();
            log.PedestrianCount = r.ReadInt32();
            int pois = r.ReadInt32();
            for (int i = 0; i < pois; i++) { r.ReadString(); r.ReadVector3(); r.ReadQuaternion(); }
            int carLights = r.ReadInt32();
            for (int i = 0; i < carLights; i++) r.ReadString();
            int pedLights = r.ReadInt32();
            for (int i = 0; i < pedLights; i++) r.ReadString();
            int aiCars = 0;
            try
            {
                while (stream.Position < stream.Length)
                {
                    var type = (LogFrameType)r.ReadInt32();
                    if (type == LogFrameType.AICarSpawn) { aiCars++; continue; }
                    if (type == LogFrameType.Drones)
                    {
                        int nd = r.ReadInt32();
                        var last = log.Frames.Count > 0 ? log.Frames[log.Frames.Count - 1] : null;
                        var dp = new Vector3[nd]; var dr = new Quaternion[nd]; var dh = new float[nd]; var dl = new bool[nd];
                        for (int i = 0; i < nd; i++) { dp[i] = r.ReadVector3(); dr[i] = r.ReadQuaternion(); dh[i] = r.ReadSingle(); dl[i] = r.ReadBoolean(); }
                        if (last != null) { last.DronePositions = dp; last.DroneRotations = dr; last.DroneBladeHz = dh; last.DroneLanded = dl; }
                        continue;
                    }
                    int n = log.PersistentDrivers + aiCars;
                    var f = new Frame
                    {
                        Time = r.ReadSingle(),
                        DriverCount = n,
                        Positions = new Vector3[n], Rotations = new Quaternion[n], Blinkers = new BlinkerState[n],
                        FrontLights = new bool[n], StopLights = new bool[n],
                        Pedestrians = new AvatarPose[log.PedestrianCount],
                        CarLights = new byte[carLights], PedestrianLights = new byte[pedLights],
                    };
                    r.ReadSingle(); // ping
                    for (int i = 0; i < n; i++)
                    {
                        f.Positions[i] = r.ReadVector3();
                        f.Rotations[i] = r.ReadQuaternion();
                        f.Blinkers[i] = (BlinkerState)r.ReadInt32();
                        f.FrontLights[i] = r.ReadBoolean();
                        f.StopLights[i] = r.ReadBoolean();
                        if (i == log.LocalDriver) r.ReadVector3();
                        else if (i >= log.PersistentDrivers) { r.ReadVector3(); r.ReadSingle(); r.ReadBytes(4); }
                    }
                    for (int i = 0; i < log.PedestrianCount; i++) f.Pedestrians[i].DeserializeFrom(r);
                    f.CarLights = r.ReadBytes(carLights);
                    f.PedestrianLights = r.ReadBytes(pedLights);
                    log.Frames.Add(f);
                }
            }
            catch (EndOfStreamException) { } // the last frame of a log that was not closed (crash): dropped
        }
        return log;
    }

    /// <summary>The frame at or before <paramref name="t"/> and the fraction of the way to the next one.</summary>
    public int FrameAt(float t, out float fraction)
    {
        fraction = 0f;
        if (Frames.Count == 0) return -1;
        int lo = 0, hi = Frames.Count - 1;
        if (t <= Frames[0].Time) return 0;
        if (t >= Frames[hi].Time) return hi;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (Frames[mid].Time <= t) lo = mid; else hi = mid;
        }
        float span = Frames[hi].Time - Frames[lo].Time;
        fraction = span > 1e-6f ? (t - Frames[lo].Time) / span : 0f;
        return lo;
    }
}

/// <summary>
/// One participant's samples in a recording, as they changed: a host logs the others' vehicles and bodies at every
/// physics step but gets new ones only with each network update (and only once per frame of its own), so the same
/// sample repeats and then jumps. The replay moves between these key samples instead of from step to step, so the
/// motion is smooth at the rate the samples came in; stops stay stops, and jumps faster than <see cref="MaxSpeed"/>
/// (a respawn, a teleport, a host that ran far behind) are jumps.
/// </summary>
public class ReplayTrack
{
    public readonly ReplayLog Log;
    public readonly int Index;          // driver index, or pedestrian index when Pedestrian
    public readonly bool Pedestrian;
    public readonly int[] Keys;         // frame indices where the sample changed (the first frame it is in, too)
    public readonly float Interval;     // typical time between new samples while moving (s)
    public readonly float MaxSpeed;     // faster than this between two samples: a jump (m/s)
    public readonly float Offset;       // this log's time = the replay's time + Offset (another participant's own log)

    /// <summary>This track's log time at the replay's time <paramref name="t"/>.</summary>
    public float LocalTime(float t) => t + Offset;

    public ReplayTrack(ReplayLog log, int index, bool pedestrian, float offset = 0f)
    {
        Log = log; Index = index; Pedestrian = pedestrian; Offset = offset;
        MaxSpeed = pedestrian ? 12f : 70f;
        var keys = new List<int>();
        Vector3 last = Vector3.zero; Quaternion lastRot = Quaternion.identity; bool any = false;
        for (int k = 0; k < log.Frames.Count; k++)
        {
            if (!Has(log.Frames[k])) continue;
            var p = Position(log.Frames[k]); var r = Rotation(log.Frames[k]);
            if (!any || p != last || r != lastRot) { keys.Add(k); last = p; lastRot = r; any = true; }
        }
        Keys = keys.ToArray();
        // the typical gap between new samples while it moves (gaps over 1.5 s are stops, not the update rate)
        var gaps = new List<float>();
        for (int j = 1; j < Keys.Length; j++) { float g = log.Frames[Keys[j]].Time - log.Frames[Keys[j - 1]].Time; if (g < 1.5f) gaps.Add(g); }
        gaps.Sort();
        float step = log.Frames.Count > 1 ? log.Frames[1].Time - log.Frames[0].Time : 0.02f;
        Interval = gaps.Count > 0 ? Mathf.Max(gaps[gaps.Count / 2], step) : step;
    }

    bool Has(ReplayLog.Frame f) => Pedestrian
        ? Index < f.Pedestrians.Length && f.Pedestrians[Index].LocalPositions != null && f.Pedestrians[Index].LocalPositions.Count > 0
        : Index < f.DriverCount;
    Vector3 Position(ReplayLog.Frame f) => Pedestrian ? f.Pedestrians[Index].LocalPositions[0] : f.Positions[Index];
    Quaternion Rotation(ReplayLog.Frame f) => Pedestrian ? f.Pedestrians[Index].LocalRotations[0] : f.Rotations[Index];

    /// <summary>How many times the sample changed: the participant's own one changes at nearly every step.</summary>
    public int Changes => Keys.Length;

    /// <summary>The two key samples around time <paramref name="t"/> and how far to go from the first to the second
    /// (0: at the first). False before the participant is in the log.</summary>
    public bool At(float t, out ReplayLog.Frame a, out ReplayLog.Frame b, out float u)
    {
        a = b = null; u = 0f;
        if (Keys.Length == 0) return false;
        int k = Log.FrameAt(t, out _);
        if (k < Keys[0]) return false;
        int lo = 0, hi = Keys.Length - 1;
        while (lo < hi) { int mid = (lo + hi + 1) / 2; if (Keys[mid] <= k) lo = mid; else hi = mid - 1; }
        a = Log.Frames[Keys[lo]];
        if (lo + 1 >= Keys.Length) { b = a; return true; }
        b = Log.Frames[Keys[lo + 1]];
        // the move to the next sample takes the update interval and ends when that sample came in (a stop before it
        // stays a stop); faster than MaxSpeed: a jump when it came in
        float gap = b.Time - a.Time, dur = Mathf.Min(gap, Interval);
        float dist = Vector3.Distance(Position(a), Position(b));
        if (dur <= 1e-4f || dist / dur > MaxSpeed) { u = 0f; return true; }
        // (the frame at k is the last one before b came in: until then only the time decides)
        u = Mathf.Clamp01((t - (b.Time - dur)) / dur);
        return true;
    }

    /// <summary>Position and rotation at time <paramref name="t"/>.</summary>
    public bool Pose(float t, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        if (!At(t, out var a, out var b, out var u)) return false;
        pos = Vector3.Lerp(Position(a), Position(b), u);
        rot = Quaternion.Slerp(Rotation(a), Rotation(b), u);
        return true;
    }

    /// <summary>
    /// The curvature of the path around time <paramref name="t"/> (1/m, positive = turning right): the change in the
    /// direction of travel over about <paramref name="metres"/> before and after (0.5 to 2 s), per metre driven. From
    /// the path, not the body's heading, and over several metres, so a body that sways or yaws about its path (e.g. a
    /// car whose controller hunted from side to side) does not swing the wheel to and fro. Null while (nearly)
    /// standing still, reversing or across a jump.
    /// </summary>
    public float? Curvature(float t, float metres = 6f)
    {
        if (!Pose(t - 0.25f, out var q0, out _) || !Pose(t + 0.25f, out var q1, out _)) return null;
        float speed = Vector3.ProjectOnPlane(q1 - q0, Vector3.up).magnitude / 0.5f;
        float window = Mathf.Clamp(metres / Mathf.Max(speed, 0.1f), 0.5f, 2f);
        if (!Pose(t - window, out var p0, out _) || !Pose(t, out var p1, out var r1) || !Pose(t + window, out var p2, out _)) return null;
        var a = Vector3.ProjectOnPlane(p1 - p0, Vector3.up); var b = Vector3.ProjectOnPlane(p2 - p1, Vector3.up);
        float ds = 0.5f * (a.magnitude + b.magnitude);
        if (a.magnitude < 0.3f || b.magnitude < 0.3f || ds > MaxSpeed * window) return null;
        if (Vector3.Dot(a + b, r1 * Vector3.forward) < 0f) return null;   // reversing
        return Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad / ds;
    }

    /// <summary>The first sample at least <paramref name="distance"/> from where the participant started (not the
    /// settling of a body or vehicle at its spawn point), or -1.</summary>
    public float FirstMove(out Vector3 at, float distance = 0.5f)
    {
        at = Vector3.zero;
        if (Keys.Length < 2) return -1f;
        var start = Position(Log.Frames[Keys[0]]);
        foreach (var k in Keys)
        {
            var p = Position(Log.Frames[k]);
            if (Vector3.Distance(p, start) >= distance) { at = p; return Log.Frames[k].Time; }
        }
        return -1f;
    }

    /// <summary>The last sample at least <paramref name="distance"/> from the one before (the last time it moved on, not
    /// a body settling where it stopped), or -1.</summary>
    public float LastMove(out Vector3 at, float distance = 0.5f)
    {
        at = Vector3.zero;
        for (int j = Keys.Length - 1; j > 0; j--)
        {
            var p = Position(Log.Frames[Keys[j]]);
            if (Vector3.Distance(p, Position(Log.Frames[Keys[j - 1]])) >= distance) { at = p; return Log.Frames[Keys[j]].Time; }
        }
        return -1f;
    }

    /// <summary>The first time the participant was within <paramref name="tolerance"/> of a point (else the nearest), after <paramref name="from"/>.</summary>
    public float TimeAt(Vector3 point, float from, float tolerance, out float error)
    {
        float best = float.MaxValue, bestT = -1f;
        foreach (var k in Keys)
        {
            var f = Log.Frames[k];
            if (f.Time < from) continue;
            float d = Vector3.Distance(Position(f), point);
            if (d < best) { best = d; bestT = f.Time; }
            if (d <= tolerance) break;
        }
        error = best;
        return bestT;
    }
}
