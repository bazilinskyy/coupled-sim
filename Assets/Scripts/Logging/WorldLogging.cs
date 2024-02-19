﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;


internal enum LogFrameType
{
    PositionsUpdate,
    AICarSpawn
}


//logger class
public class WorldLogger
{
    private TrafficLightsSystem _lights;
    private readonly PlayerSystem _playerSystem;
    private readonly AICarSyncSystem _aiCarSystem;
    private int _lastFrameAICarCount;
    private BinaryWriter _fileWriter;
    private float _startTime;

    private LiveLogger _liveLogger;

    private bool active;

    private readonly List<PlayerAvatar> _driverBuffer = new();

    public float RealtimeLogInterval = 0;
    private float lastRealtimeLog = 0;


    public WorldLogger(PlayerSystem playerSys, AICarSyncSystem aiCarSystem)
    {
        _playerSystem = playerSys;
        _aiCarSystem = aiCarSystem;
    }


    //writes metadata header in binary log file
    public void BeginLog(string fileName, ExperimentDefinition experiment, TrafficLightsSystem lights, float time, bool sendLiveLog)
    {
        active = true;
        _lights = lights;

        if (!Directory.Exists("ExperimentLogs"))
        {
            Directory.CreateDirectory("ExperimentLogs");
        }

        _fileWriter = new BinaryWriter(File.Create("ExperimentLogs/" + fileName + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".binLog"));
        _startTime = time;
        _fileWriter.Write(DateTime.Now.ToBinary());
        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Cars);
        _driverBuffer.AddRange(_playerSystem.Passengers);
        _fileWriter.Write(_driverBuffer.IndexOf(_playerSystem.LocalPlayer));
        _fileWriter.Write(_driverBuffer.Count);
        _fileWriter.Write(_playerSystem.Pedestrians.Count);
        _fileWriter.Write(experiment.PointsOfInterest.Length);

        foreach (var poi in experiment.PointsOfInterest)
        {
            _fileWriter.Write(poi.name);
            _fileWriter.Write(poi.position);
            _fileWriter.Write(poi.rotation);
        }

        if (_lights != null)
        {
            _fileWriter.Write(_lights.CarLights.Length);

            foreach (var light in _lights.CarLights)
            {
                _fileWriter.Write(GetHierarchyString(light.transform));
            }

            _fileWriter.Write(_lights.PedestrianLights.Length);

            foreach (var light in _lights.PedestrianLights)
            {
                _fileWriter.Write(GetHierarchyString(light.transform));
            }
        }
        else
        {
            _fileWriter.Write(0);
            _fileWriter.Write(0);
        }

        if (sendLiveLog)
        {
            _liveLogger = new LiveLogger();
            _liveLogger.Init();

            _liveLogger.BeginLog(
                _driverBuffer.IndexOf(_playerSystem.LocalPlayer),
                _playerSystem.Cars.Count + _playerSystem.Passengers.Count,
                _playerSystem.Pedestrians.Count,
                _lights != null ? _lights.CarLights.Length : 0,
                _lights != null ? _lights.PedestrianLights.Length : 0
            );

            _liveLogger.Flush();
        }
    }


    private string GetHierarchyString(Transform trans)
    {
        var names = new List<string>();

        while (trans != null)
        {
            names.Add(trans.name);
            trans = trans.parent;
        }

        names.Reverse();

        return string.Join("/", names);
    }


    public void LogFrame(float ping, float time)
    {
        var aiCars = _aiCarSystem.Cars;
        var newAiCarsCount = aiCars.Count - _lastFrameAICarCount;

        LogFrame(ping, time, newAiCarsCount, _fileWriter);

        if (_liveLogger != null)
        {
            if (newAiCarsCount > 0 || Time.realtimeSinceStartup - lastRealtimeLog > RealtimeLogInterval)
            {
                _liveLogger._writer.Write((int) LiveLogger.LogPacketType.Frame);
                LogFrame(ping, time, newAiCarsCount, _liveLogger._writer);
                _liveLogger.Flush();
                lastRealtimeLog = Time.realtimeSinceStartup;
            }
        }

        _lastFrameAICarCount = aiCars.Count;
    }


    //main logging logic
    //adds a single entry to the logfile
    private void LogFrame(float ping, float time, int newAiCarsCount, BinaryWriter writer)
    {
        if (!active)
        {
            return;
        }

        for (var i = 0; i < newAiCarsCount; i++)
        {
            writer.Write((int) LogFrameType.AICarSpawn);
        }

        writer.Write((int) LogFrameType.PositionsUpdate);
        writer.Write(time - _startTime);
        writer.Write(ping);

        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Cars);
        _driverBuffer.AddRange(_playerSystem.Passengers);
        _driverBuffer.AddRange(_aiCarSystem.Cars);

        foreach (var driver in _driverBuffer)
        {
            writer.Write(driver.transform.position);
            writer.Write(driver.transform.rotation);

            if (driver.CarBlinkers != null)
            {
                writer.Write((int) driver.CarBlinkers.State);
            }
            else
            {
                Debug.LogWarning("SOSXR: We don't have any CarBlinkers in our _driverBuffer");
            }

            if (driver.frontLights != null)
            {
                writer.Write(driver.frontLights.gameObject.activeSelf);
            }
            else
            {
                Debug.LogWarning("SOSXR: We dont have any FrontLights in our _driverBuffer");
            }

            if (driver.stopLights != null)
            {
                writer.Write(driver.stopLights.gameObject.activeSelf);
            }
            else
            {
                Debug.LogWarning("SOSXR: We don't have any StopLights in our _driverBuffer");
            }

            if (driver == _playerSystem.LocalPlayer)
            {
                var rb = driver.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb);
                Assert.IsFalse(rb.isKinematic);
                writer.Write(rb.velocity);
            }
            else if (_aiCarSystem.Cars.Contains(driver))
            {
                var rb = driver.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb);
                //Assert.IsFalse(rb.isKinematic);
                writer.Write(rb.velocity);
                var ai = driver.GetComponent<AICar>();
                Assert.IsNotNull(ai);
                writer.Write(ai.speed);
                writer.Write(ai.state == AICar.CarState.BRAKING);
                writer.Write(ai.state == AICar.CarState.STOPPED);
                writer.Write(ai.state == AICar.CarState.TAKEOFF);
                var plap = driver.GetComponentInChildren<EyeContact>();
                //Assert.IsNotNull(plap);
                writer.Write(plap != null && plap.TargetPed != null);
            }
        }

        foreach (var pedestrian in _playerSystem.Pedestrians)
        {
            pedestrian.GetPose().SerializeTo(writer);
        }

        if (_lights != null)
        {
            foreach (var light in _lights.CarLights)
            {
                writer.Write((byte) light.State);
            }

            foreach (var light in _lights.PedestrianLights)
            {
                writer.Write((byte) light.State);
            }
        }
    }


    //cleans up after logging has ended
    public void EndLog()
    {
        // TODO(jacek): for now we just call EndLog in NetworkingManager.OnDestroy, so we close the log when the game finishes
        // Remove that call and this null check when we have proper "End experiment" handling
        if (_fileWriter != null)
        {
            _fileWriter.Dispose();
        }

        if (_liveLogger != null)
        {
            _liveLogger.Dispose();
        }
    }
}


//convert binary log into human readable csv
public class LogConverter
{
    private readonly List<int> aiCarIndexes = new();

    #pragma warning disable 0414
    private List<Vector3> _driverPositions;
    #pragma warning restore 0414
    private List<RunningAverage> _driverVels;

    private readonly string[] _pedestrianSkeletonNames;
    private bool _open;
    private readonly List<string> _fileNames = new();
    private string _selectedFileName = "";
    private SerializedPOI[] _pois;

    private readonly List<SerializedPOI> customPois = new()
    {
        new SerializedPOI
        {
            Name = "ldist",
            Position = new Vector3(0, 0, 7.75f),
            Rotation = Quaternion.Euler(new Vector3())
        },
        new SerializedPOI
        {
            Name = "rdist",
            Position = new Vector3(0, 0, 2.75f),
            Rotation = Quaternion.Euler(new Vector3())
        },
        new SerializedPOI
        {
            Name = "spawn point",
            Position = new Vector3(0, 0.2224625f, 0),
            Rotation = Quaternion.Euler(new Vector3())
        }
    };


    public LogConverter(PlayerAvatar pedestrianPrefab)
    {
        var transforms = pedestrianPrefab.SyncTransforms;
        _pedestrianSkeletonNames = new string[transforms.Length];

        for (var i = 0; i < transforms.Length; i++)
        {
            _pedestrianSkeletonNames[i] = transforms[i].name;
        }
    }


    public const string UNITY_WORLD_ROOT = "World Root";

    private const int NumFramesInVelocityRunningAverage = 10;


    private static SerializedPOI[] ParsePOI(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var pois = new SerializedPOI[count];

        for (var i = 0; i < count; i++)
        {
            pois[i].Name = reader.ReadString();
            pois[i].Position = reader.ReadVector3();
            pois[i].Rotation = reader.ReadQuaternion();
        }

        return pois;
    }


    public static SerializedPOI[] GetPOIs(string sourceFile)
    {
        using (var reader = new BinaryReader(File.OpenRead(sourceFile)))
        {
            var unusedTimestamp = reader.ReadInt64();
            var unusedLocalDriver = reader.ReadInt32();
            var unusedDriverCount = reader.ReadInt32();
            var unusedPedestrianCount = reader.ReadInt32();

            return ParsePOI(reader);
        }
    }


    private bool IsAICar(int i)
    {
        return aiCarIndexes.Contains(i);
    }


    //translation logic
    //referenceName, referencePos, referenceRot - parameters specifining new origin point, allowing transforming data into new coordinate system
    public void TranslateBinaryLogToCsv(string sourceFile, string dstFile, string[] pedestrianSkeletonNames, string referenceName, Vector3 referencePos, Quaternion referenceRot)
    {
        _driverPositions = null;
        _driverVels = null;

        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        const string separator = ";";
        const string driverTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;blinkers;front_lights;stop_lights;vel_local_x;vel_local_y;vel_local_z;vel_local_smooth_x;vel_local_smooth_y;vel_local_smooth_z;vel_x;vel_y;vel_z;vel_smooth_x;vel_smooth_y;vel_smooth_z";
        const string localDriverTransformHeader = driverTransformHeader + ";rb_vel_x;rb_vel_y;rb_vel_z;rb_vel_local_x;rb_vel_local_y;rb_vel_local_z";
        const string aiCarTransformHeader = localDriverTransformHeader + ";aicar_speed;braking;stopped;takeoff;eyecontact";

        var columnsPerDriver = driverTransformHeader.Split(separator).Length;
        var columnsForLocalDriver = localDriverTransformHeader.Split(separator).Length;
        var columnsForAICar = aiCarTransformHeader.Split(separator).Length;

        const int columnsPerBone = 6;
        var columnsPerPedestrian = pedestrianSkeletonNames.Length * columnsPerBone + columnsPerBone; // + columnsPerBone is for the root transform;
        var toRefRot = Quaternion.Inverse(referenceRot);

        var srcFile = File.OpenRead(sourceFile);
        var log = Log.New();

        using (var reader = new BinaryReader(srcFile))
        {
            log.StartTime = DateTime.FromBinary(reader.ReadInt64());
            log.LocalDriver = reader.ReadInt32();
            var numPersistentDrivers = reader.ReadInt32();
            var numPedestrians = reader.ReadInt32();
            log.POIs.AddRange(ParsePOI(reader));
            log.POIs.AddRange(customPois);

            log.POIs.Add(new SerializedPOI
            {
                Name = UNITY_WORLD_ROOT,
                Position = Vector3.zero,
                Rotation = Quaternion.identity
            });

            var numCarLights = reader.ReadInt32();

            for (var i = 0; i < numCarLights; i++)
            {
                log.CarLightNames.Add(reader.ReadString());
            }

            var numPedestrianLights = reader.ReadInt32();

            for (var i = 0; i < numPedestrianLights; i++)
            {
                log.PedestrianLightsNames.Add(reader.ReadString());
            }

            var numAICars = 0;

            while (srcFile.Position < srcFile.Length)
            {
                var eventType = (LogFrameType) reader.ReadInt32();

                if (eventType == LogFrameType.AICarSpawn)
                {
                    aiCarIndexes.Add(numAICars + numPersistentDrivers);
                    numAICars++;

                    continue;
                }

                Assert.AreEqual(LogFrameType.PositionsUpdate, eventType);
                var frame = new SerializedFrame();
                log.Frames.Add(frame);
                frame.Timestamp = reader.ReadSingle();
                frame.RoundtripTime = reader.ReadSingle();

                var numDriversThisFrame = numAICars + numPersistentDrivers;

                for (var i = 0; i < numDriversThisFrame; i++)
                {
                    frame.DriverPositions.Add(reader.ReadVector3());
                    frame.DriverRotations.Add(reader.ReadQuaternion());
                    frame.BlinkerStates.Add((BlinkerState) reader.ReadInt32());
                    frame.Front.Add(reader.ReadBoolean());
                    frame.Stop.Add(reader.ReadBoolean());

                    if (i == log.LocalDriver)
                    {
                        frame.LocalDriverRbVelocity = reader.ReadVector3() * SpeedConvertion.Mps2Kmph;
                    }
                    else if (IsAICar(i))
                    {
                        frame.AICarRbVelocities.Add(i, reader.ReadVector3() * SpeedConvertion.Mps2Kmph);
                        frame.AICarSpeeds.Add(i, reader.ReadSingle());
                        frame.braking.Add(i, reader.ReadBoolean());
                        frame.stopped.Add(i, reader.ReadBoolean());
                        frame.takeoff.Add(i, reader.ReadBoolean());
                        frame.eyecontact.Add(i, reader.ReadBoolean());
                    }
                }

                for (var i = 0; i < numPedestrians; i++)
                {
                    frame.PedestrianPositions.Add(reader.ReadListVector3());
                    frame.PedestrianRotations.Add(reader.ReadListQuaternion());
                    _ = reader.ReadInt32(); // Blinkers, unused
                    _ = reader.ReadBoolean(); //high-beam, unused
                    _ = reader.ReadBoolean(); //stop lights, unused
                }

                for (var i = 0; i < numCarLights; i++)
                {
                    frame.CarLightStates.Add((LightState) reader.ReadByte());
                }

                for (var i = 0; i < numPedestrianLights; i++)
                {
                    frame.PedestrianLightStates.Add((LightState) reader.ReadByte());
                }
            }
        }

        using (var writer = new StreamWriter(File.Create(dstFile)))
        {
            writer.WriteLine($"Root;{referenceName}");

            var startTime = log.StartTime;
            var startString = startTime.ToString("HH:mm:ss") + ":" + startTime.Millisecond;

            writer.WriteLine($"Start Time;{startString}");
            var localDriver = log.LocalDriver;
            var lastFrame = log.Frames[log.Frames.Count - 1];
            var numDrivers = lastFrame.DriverPositions.Count;
            var numPedestrians = lastFrame.PedestrianPositions.Count;

            // POI
            var pois = log.POIs;


            Vector3 PosToRefPoint(Vector3 p)
            {
                return toRefRot * (p - referencePos);
            }


            Quaternion RotToRefPoint(Quaternion q)
            {
                return toRefRot * q;
            }


            for (var i = 0; i < pois.Count; i++)
            {
                var poi = pois[i];
                poi.Position = PosToRefPoint(poi.Position);
                poi.Rotation = RotToRefPoint(poi.Rotation);
                writer.WriteLine(poi);
            }


            //****************
            // HEADER ROWS
            //****************

            writer.Write("Timestamp;Roundtrip Time to Host;");

            // Drivers header
            for (var i = 0; i < numDrivers; i++)
            {
                if (i == localDriver)
                {
                    writer.Write(string.Join(separator, Enumerable.Repeat($"Local driver{i}", columnsForLocalDriver)));
                }
                else if (IsAICar(i))
                {
                    writer.Write(string.Join(separator, Enumerable.Repeat($"AI driver{i}", columnsForAICar)));
                }
                else
                {
                    writer.Write(string.Join(separator, Enumerable.Repeat($"Driver{i}", columnsPerDriver)));
                }

                if (i < numDrivers - 1)
                {
                    writer.Write(separator);
                }
            }

            if (numPedestrians > 0)
            {
                writer.Write(separator);
            }

            for (var i = 0; i < numPedestrians; i++)
            {
                var pedestrianName = $"Pedestrian{i}";
                writer.Write(string.Join(separator, Enumerable.Repeat(pedestrianName, columnsPerPedestrian)));
            }

            if (log.CarLightNames.Count > 0)
            {
                writer.Write(separator);
                writer.Write(string.Join(separator, log.CarLightNames));
            }

            if (log.PedestrianLightsNames.Count > 0)
            {
                writer.Write(separator);
                writer.Write(string.Join(separator, log.PedestrianLightsNames));
            }

            writer.Write("\n");

            // No bone names for drivers
            writer.Write(separator); // for the Timestamp column
            writer.Write(separator); // for the Ping column
            var localDriversCount = localDriver == -1 ? 0 : 1;
            var aiCarsCount = aiCarIndexes.Count;
            var otherDriversCount = numDrivers - aiCarsCount - localDriversCount;
            writer.Write(string.Join(separator, new string(';', otherDriversCount * columnsPerDriver + localDriversCount * columnsForLocalDriver + aiCarsCount * columnsForAICar)));
            var sb = new StringBuilder();
            sb.Append(string.Join(separator, Enumerable.Repeat("Root", columnsPerBone)));
            sb.Append(";");

            for (var i = 0; i < pedestrianSkeletonNames.Length; i++)
            {
                sb.Append(string.Join(separator, Enumerable.Repeat(pedestrianSkeletonNames[i], columnsPerBone)));

                if (i < pedestrianSkeletonNames.Length - 1)
                {
                    sb.Append(separator);
                }
            }

            for (var i = 0; i < numPedestrians; i++)
            {
                writer.Write(sb.ToString());
            }

            writer.Write("\n");

            writer.Write(separator); // for the Timestamp column
            writer.Write(separator); // for the Ping column

            var headers = new List<string>();

            for (var i = 0; i < numDrivers; i++)
            {
                if (i == localDriver)
                {
                    headers.Add(localDriverTransformHeader);
                }
                else if (IsAICar(i))
                {
                    headers.Add(aiCarTransformHeader);
                }
                else
                {
                    headers.Add(driverTransformHeader);
                }
            }

            writer.Write(string.Join(separator, headers));

            if (numPedestrians > 0)
            {
                writer.Write(separator);
            }

            const string boneTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z";
            writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians * (pedestrianSkeletonNames.Length + 1))));

            writer.Write("\n");

            //****************
            // ACTUAL DATA
            //****************

            var line = new List<string>();
            SerializedFrame prevFrame = default;
            float prevTime = 0;

            foreach (var frame in log.Frames)
            {
                line.Clear();
                line.Add(frame.Timestamp.ToString());
                line.Add(frame.RoundtripTime.ToString());
                var numDriversThisFrame = frame.DriverPositions.Count;

                if (_driverVels == null)
                {
                    _driverVels = new List<RunningAverage>(numDriversThisFrame);
                }

                for (var i = _driverVels.Count; i < numDriversThisFrame; i++)
                {
                    _driverVels.Add(new RunningAverage(NumFramesInVelocityRunningAverage));
                }

                var dt = frame.Timestamp - prevTime;

                for (var i = 0; i < numDriversThisFrame; i++)
                {
                    var pos = PosToRefPoint(frame.DriverPositions[i]);
                    var rot = frame.DriverRotations[i];
                    var euler = RotToRefPoint(rot).eulerAngles;
                    var blinkers = frame.BlinkerStates[i];
                    var front = frame.Front[i];
                    var stop = frame.Stop[i];
                    var inverseRotation = Quaternion.Inverse(rot);

                    if (prevFrame == null || prevFrame.DriverPositions.Count <= i)
                    {
                        if (i == localDriver)
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{blinkers};{front};{stop};0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0");
                        }
                        else if (IsAICar(i))
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{blinkers};{front};{stop};0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0");
                        }
                        else
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{blinkers};{front};{stop};0;0;0;0;0;0;0;0;0;0;0;0");
                        }
                    }
                    else
                    {
                        var vel = (pos - PosToRefPoint(prevFrame.DriverPositions[i])) / dt * SpeedConvertion.Mps2Kmph;
                        var speed = inverseRotation * vel;
                        var runningAvg = _driverVels[i];
                        runningAvg.Add(vel);
                        _driverVels[i] = runningAvg;
                        var velSmooth = runningAvg.Get();
                        var localSmooth = rot * velSmooth;

                        if (i == localDriver)
                        {
                            var rbVel = frame.LocalDriverRbVelocity;
                            var rbVelLocal = inverseRotation * rbVel;
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{blinkers};{front};{stop};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z};{rbVel.x};{rbVel.y};{rbVel.z};{rbVelLocal.x};{rbVelLocal.y};{rbVelLocal.z}");
                        }
                        else if (IsAICar(i))
                        {
                            var rbVel = frame.AICarRbVelocities[i];
                            var aiCarSpeed = frame.AICarSpeeds[i];
                            var braking = frame.braking[i];
                            var stopped = frame.stopped[i];
                            var takeoff = frame.takeoff[i];
                            var eyecontact = frame.eyecontact[i];
                            var rbVelLocal = inverseRotation * rbVel;
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{blinkers};{front};{stop};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z};{rbVel.x};{rbVel.y};{rbVel.z};{rbVelLocal.x};{rbVelLocal.y};{rbVelLocal.z};{aiCarSpeed};{(braking ? 1 : 0)};{(stopped ? 1 : 0)};{(takeoff ? 1 : 0)};{(eyecontact ? 1 : 0)}");
                        }
                        else
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{blinkers};{front};{stop};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z}");
                        }
                    }
                }

                for (var i = numDriversThisFrame; i < numDrivers; i++)
                {
                    if (IsAICar(i))
                    {
                        line.Add(";;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;");
                    }
                    else
                    {
                        line.Add(";;;;;;;;;;;;;;;;;;;;");
                    }
                }

                for (var i = 0; i < frame.PedestrianPositions.Count; i++)
                {
                    var pos = frame.PedestrianPositions[i];
                    var rot = frame.PedestrianRotations[i];

                    for (var j = 0; j < pos.Count; j++)
                    {
                        var p = PosToRefPoint(pos[j]);
                        var r = RotToRefPoint(rot[j]).eulerAngles;
                        line.Add($"{p.x};{p.y};{p.z};{r.x};{r.y};{r.z}");
                    }
                }

                foreach (var v in frame.CarLightStates)
                {
                    line.Add(v.ToString());
                }

                foreach (var v in frame.PedestrianLightStates)
                {
                    line.Add(v.ToString());
                }

                writer.Write(string.Join(separator, line));
                writer.Write("\n");
                prevTime = frame.Timestamp;
                prevFrame = frame;
            }
        }
    }


    //displays GUI and handles interactions for a single POI
    public void OnGUI_CustomPoiButton(int i, string name)
    {
        var serializedPOI = customPois[i];
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        GUILayout.Label("name:");
        serializedPOI.Name = GUILayout.TextField(serializedPOI.Name, GUILayout.Width(100));
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("position:");
        var tmp = serializedPOI.Position;
        GUILayout.BeginHorizontal();
        OnGUI_FloatField(ref tmp.x);
        OnGUI_FloatField(ref tmp.y);
        OnGUI_FloatField(ref tmp.z);
        GUILayout.EndHorizontal();
        serializedPOI.Position = tmp;
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("rotation:");
        tmp = serializedPOI.Rotation.eulerAngles;
        GUILayout.BeginHorizontal();
        OnGUI_FloatField(ref tmp.x);
        OnGUI_FloatField(ref tmp.y);
        OnGUI_FloatField(ref tmp.z);
        GUILayout.EndHorizontal();
        serializedPOI.Rotation = Quaternion.Euler(tmp);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (GUILayout.Button("Transform with " + serializedPOI.Name))
        {
            var fullName = "ExperimentLogs/" + name;
            var csvName = "ExperimentLogs/" + serializedPOI.Name + "-" + name.Replace("binLog", "csv");
            TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, serializedPOI.Name, serializedPOI.Position, serializedPOI.Rotation);
        }
    }


    //helper function displaying text field accepting float numbers
    private void OnGUI_FloatField(ref float x)
    {
        string tmp;
        float ftmp;
        tmp = GUILayout.TextField(x.ToString(CultureInfo.InvariantCulture.NumberFormat), GUILayout.Width(100));

        if (float.TryParse(tmp, NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out ftmp))
        {
            x = ftmp;
        }
    }


    //displays GUI and handles interactions of a log transforming logic
    public void OnGUI()
    {
        if (!_open && GUILayout.Button("Convert log to csv"))
        {
            var files = Directory.GetFiles("ExperimentLogs/");
            _fileNames.Clear();

            foreach (var file in files)
            {
                if (file.EndsWith("binLog"))
                {
                    _fileNames.Add(file.Split('/')[1]);
                }
            }

            _open = true;
        }

        if (_open)
        {
            if (GUILayout.Button("Close"))
            {
                _open = false;
                _selectedFileName = null;
            }

            foreach (var name in _fileNames)
            {
                if (string.IsNullOrEmpty(_selectedFileName))
                {
                    if (GUILayout.Button(name))
                    {
                        _selectedFileName = name;
                        _pois = GetPOIs("ExperimentLogs/" + name);
                    }
                }
                else if (_selectedFileName == name)
                {
                    GUILayout.BeginVertical();
                    GUILayout.Label(_selectedFileName);

                    GUILayout.Label("Custom reference points:");

                    for (var i = 0; i < customPois.Count(); i++)
                    {
                        OnGUI_CustomPoiButton(i, name);
                    }

                    GUILayout.Label("Stored reference Point:");

                    if (GUILayout.Button("Transform with " + UNITY_WORLD_ROOT))
                    {
                        var fullName = "ExperimentLogs/" + name;
                        var csvName = "ExperimentLogs/" + UNITY_WORLD_ROOT + "-" + name.Replace("binLog", "csv");
                        TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, UNITY_WORLD_ROOT, default, Quaternion.identity);
                    }

                    foreach (var poi in _pois)
                    {
                        if (GUILayout.Button("Transform with " + poi.Name))
                        {
                            var fullName = "ExperimentLogs/" + name;
                            var csvName = "ExperimentLogs/" + poi.Name + "-" + name.Replace("binLog", "csv");
                            TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, poi.Name, poi.Position, poi.Rotation);
                        }
                    }

                    GUILayout.EndVertical();
                }
            }
        }
    }


    public struct SerializedPOI
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;


        public override string ToString()
        {
            var rot = Rotation.eulerAngles;

            return $"{Name};{Position.x};{Position.y};{Position.z};{rot.x};{rot.y};{rot.z}";
        }
    }


    private struct Log
    {
        public DateTime StartTime;
        public int LocalDriver;
        public List<SerializedPOI> POIs;
        public List<string> CarLightNames;
        public List<string> PedestrianLightsNames;
        public List<SerializedFrame> Frames;


        public static Log New()
        {
            return new Log
            {
                POIs = new List<SerializedPOI>(),
                CarLightNames = new List<string>(),
                PedestrianLightsNames = new List<string>(),
                Frames = new List<SerializedFrame>()
            };
        }
    }


    private class SerializedFrame
    {
        public float Timestamp;
        public float RoundtripTime;
        public readonly List<Vector3> DriverPositions = new();
        public readonly List<Quaternion> DriverRotations = new();
        public readonly List<BlinkerState> BlinkerStates = new();
        public readonly List<bool> Front = new();
        public readonly List<bool> Stop = new();
        public readonly List<List<Vector3>> PedestrianPositions = new();
        public readonly List<List<Quaternion>> PedestrianRotations = new();
        public readonly List<LightState> CarLightStates = new();
        public readonly List<LightState> PedestrianLightStates = new();
        public Vector3 LocalDriverRbVelocity;
        public readonly Dictionary<int, Vector3> AICarRbVelocities = new();
        public readonly Dictionary<int, float> AICarSpeeds = new();
        public readonly Dictionary<int, bool> braking = new();
        public readonly Dictionary<int, bool> stopped = new();
        public readonly Dictionary<int, bool> takeoff = new();
        public readonly Dictionary<int, bool> eyecontact = new();
    }
}