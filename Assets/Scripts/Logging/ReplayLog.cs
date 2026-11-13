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
