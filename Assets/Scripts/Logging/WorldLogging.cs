using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

enum LogFrameType
{
    PositionsUpdate,
    AICarSpawn,
}

// Logger class
public class WorldLogger
{
    TrafficLightsSystem _lights;
    PlayerSystem _playerSystem;
    AICarSyncSystem _aiCarSystem;
    int _lastFrameAICarCount;
    BinaryWriter _fileWriter;
    float _startTime;
    LiveLogger _liveLogger;

    // Experiment data:
    int _expDefNr;
    int _trialNr;
    int _participantNr;

    // Data from Varjo HMD:
    Vector3 HMD_pos; float HMD_pos_x; float HMD_pos_y; float HMD_pos_z; // Position data
    Vector3 HMD_rot; float HMD_rot_x; float HMD_rot_y; float HMD_rot_z; // Rotation data

    double LeftEyePupilSize;
    double RightEyePupilSize;
    double FocusDistance;
    double FocusStability;

    // Data from Vive handheld controller
    float SafetyButton;

    public WorldLogger(PlayerSystem playerSys, AICarSyncSystem aiCarSystem)
    {
        _playerSystem = playerSys;
        _aiCarSystem = aiCarSystem;
    }

    List<PlayerAvatar> _driverBuffer = new List<PlayerAvatar>();

    // Writes metadata header in binary log file
    public void BeginLog(string fileName, ExperimentDefinition experiment, TrafficLightsSystem lights, float time, bool sendLiveLog)
    {
        _lights = lights;

        // Create logging directory if it didn't already exist
        if (!Directory.Exists("ExperimentLogs"))
        {
            Directory.CreateDirectory("ExperimentLogs");
        }

        // Logfile name configuration
        _fileWriter = new BinaryWriter(File.Create("ExperimentLogs/" + fileName + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".binLog"));
        /*_fileWriter = new BinaryWriter(File.Create("ExperimentLogs/"
            + "ParticipantNr_" + PersistentManager.Instance.ParticipantNr + "_"
            + "TrialNr_" + PersistentManager.Instance.TrialNr + "_"
            + "ExpDefNr_" + PersistentManager.Instance.ExpDefNr + "_"
            + fileName + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".binLog"));*/

        _startTime = time;
        _fileWriter.Write(DateTime.Now.ToBinary());

        /*// Experiment Definition number:
        _expDefNr = PersistentManager.Instance.ExpDefNr;
        _fileWriter.Write(_expDefNr);

        // Trial number:
        _trialNr = PersistentManager.Instance.TrialNr;
        _fileWriter.Write(_trialNr);

        // Participant number:
        _participantNr = PersistentManager.Instance.ParticipantNr;
        _fileWriter.Write(_participantNr);*/

        // Add drivers and passengers avatars to the playeravatar list
        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Drivers);
        _driverBuffer.AddRange(_playerSystem.Passengers);

        // Add player index of local player, number of avatars, number of pedestrians, number of POI.
        _fileWriter.Write(_driverBuffer.IndexOf(_playerSystem.LocalPlayer));    //1. int32 local driver
        _fileWriter.Write(_driverBuffer.Count);                                 //2. int32 numPersistentDrivers
        _fileWriter.Write(_playerSystem.Pedestrians.Count);                     //3. int32 numPedestrians
        _fileWriter.Write(experiment.PointsOfInterest.Length);

        // For each (Point of Interest) POI, log the name, position and rotation
        foreach (var poi in experiment.PointsOfInterest)
        {
            _fileWriter.Write(poi.name);
            _fileWriter.Write(poi.position);
            _fileWriter.Write(poi.rotation);
        }

        if (sendLiveLog)
        {
            _liveLogger = new LiveLogger();
            _liveLogger.Init();
        }
    }

    string GetHierarchyString(Transform trans)
    {
        List<string> names = new List<string>();
        while (trans != null)
        {
            names.Add(trans.name);
            trans = trans.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    // Main logging logic
    // Adds a single entry to the logfile
    public void LogFrame(float ping, float time)
    {
        // Log the AI cars, position, time and ping
        _liveLogger?.Log(_aiCarSystem, _playerSystem);
        var aiCars = _aiCarSystem.Cars;
        while (aiCars.Count > _lastFrameAICarCount)
        {
            _fileWriter.Write((int)LogFrameType.AICarSpawn);  //6. int32 eventType
            _lastFrameAICarCount++;
        }
        _fileWriter.Write((int)LogFrameType.PositionsUpdate);
        _fileWriter.Write(time - _startTime);
        _fileWriter.Write(ping);

        // Add drivers and passengers avatars, and AI cars to the playeravatar list
        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Drivers);
        _driverBuffer.AddRange(_playerSystem.Passengers);
        _driverBuffer.AddRange(_aiCarSystem.Cars);

        // Log the drivers position, rotation. Log the drivers/passengers Varjo HMD data
        foreach (var driver in _driverBuffer)
        {
            _fileWriter.Write(driver.transform.position);
            _fileWriter.Write(driver.transform.rotation);
            if (_aiCarSystem.Cars.Contains(driver))
            {
                var ai = driver.GetComponent<AICar>();
                Assert.IsNotNull(ai);
                _fileWriter.Write(ai.speed);
                _fileWriter.Write(ai.state == AICar.CarState.BRAKING);
                _fileWriter.Write(ai.state == AICar.CarState.STOPPED);
                _fileWriter.Write(ai.state == AICar.CarState.TAKEOFF);
            }
        }
        foreach (var pedestrian in _playerSystem.Pedestrians)
        {
            pedestrian.GetPose().SerializeTo(_fileWriter);
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

// Convert binary log into human readable csv
public class LogConverter
{
    public struct SerializedPOI
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;

        // ToString return structure 
        public override string ToString()
        {
            var rot = Rotation.eulerAngles;
            return $"{Name};{Position.x};{Position.y};{Position.z};{rot.x};{rot.y};{rot.z}";
        }
    }

    // Assigning the name, pos, and rot to the "POIs" list [unused for world coordinates]
    static SerializedPOI[] ParsePOI(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var pois = new SerializedPOI[count];
        for (int i = 0; i < count; i++)
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

    public const string UNITY_WORLD_ROOT = "World Root";

    const int NumFramesInVelocityRunningAverage = 10;

    struct Log
    {
        public DateTime StartTime;
        public int ExperimentDefinitionNr;
        public int TrialNr;
        public int ParticipantNr;
        public int LocalDriver;
        public List<SerializedPOI> POIs;
        public List<SerializedFrame> Frames;
        public static Log New() => new Log
        {
            POIs = new List<SerializedPOI>(),
            Frames = new List<SerializedFrame>(),
        };
    }

    class SerializedFrame
    {
        public float Timestamp;
        public float RoundtripTime;
        public List<Vector3> DriverPositions = new List<Vector3>();
        public List<Quaternion> DriverRotations = new List<Quaternion>();
        public List<List<Vector3>> PedestrianPositions = new List<List<Vector3>>();
        public List<List<Quaternion>> PedestrianRotations = new List<List<Quaternion>>();
        public Dictionary<int, float> AICarSpeeds = new Dictionary<int, float>();
        public Dictionary<int, bool> braking = new Dictionary<int, bool>();
        public Dictionary<int, bool> stopped = new Dictionary<int, bool>();
        public Dictionary<int, bool> takeoff = new Dictionary<int, bool>();
        
        // Data from Varjo HMD:
        public float HMD_pos_x; public float HMD_pos_y; public float HMD_pos_z; // Position data
        public float HMD_rot_x; public float HMD_rot_y; public float HMD_rot_z; // Rotation data

        public double LeftEyePupilSize;
        public double RightEyePupilSize;
        public double FocusDistance;
        public double FocusStability;

        // Data from Vive handheld controller
        public float SafetyButton;
    }

    List<int> aiCarIndexes = new List<int>();
    bool IsAICar(int i) => aiCarIndexes.Contains(i);

    List<Vector3> _driverPositions;
    List<RunningAverage> _driverVels;

    // Translation logic
    // referenceName, referencePos, referenceRot - parameters specifining new origin point, allowing transforming data into new coordinate system
    public void TranslateBinaryLogToCsv(string sourceFile, string dstFile, string[] pedestrianSkeletonNames, string referenceName, Vector3 referencePos, Quaternion referenceRot)
    {
        _driverPositions = null;
        _driverVels = null;

        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        const string separator = ";";

        // Column headers
        const int columnsPerDriver = 3 /*pos x,y,z*/ + 3 /*rot x,y,z */ + 3 /* local velocity */ + 3 /* local smooth velocity */ + 3 /* world velocity */ + 3 /* world velocity smooth */;
        const int columnsForLocalDriver = columnsPerDriver;
        const int columnsForAICar = columnsPerDriver + 1 /* aicar.speed */ + 3 /* braking, stopped, takeoff */;

        int columnsPerPedestrian = 6;
        var toRefRot = Quaternion.Inverse(referenceRot);

        // Load binary file
        var srcFile = File.OpenRead(sourceFile);
        Log log = Log.New();
        using (var reader = new BinaryReader(srcFile))
        {
            log.StartTime = DateTime.FromBinary(reader.ReadInt64());
            /*log.ExperimentDefinitionNr = reader.ReadInt32();
            log.TrialNr = reader.ReadInt32();
            log.ParticipantNr = reader.ReadInt32();*/
            log.LocalDriver = reader.ReadInt32();
            int numPersistentDrivers = reader.ReadInt32();
            int numPedestrians = reader.ReadInt32();
            log.POIs.AddRange(ParsePOI(reader));
            log.POIs.AddRange(customPois);
            log.POIs.Add(new SerializedPOI()
            {
                Name = UNITY_WORLD_ROOT,
                Position = Vector3.zero,
                Rotation = Quaternion.identity
            });

            int numAICars = 0;
            while (srcFile.Position < srcFile.Length)
            {
                var eventType = (LogFrameType)reader.ReadInt32();
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

                int numDriversThisFrame = numAICars + numPersistentDrivers;
                for (int i = 0; i < numDriversThisFrame; i++)
                {
                    frame.DriverPositions.Add(reader.ReadVector3());
                    frame.DriverRotations.Add(reader.ReadQuaternion());
                    if (IsAICar(i))
                    {
                        frame.AICarSpeeds.Add(i, reader.ReadSingle());
                        frame.braking.Add(i, reader.ReadBoolean());
                        frame.stopped.Add(i, reader.ReadBoolean());
                        frame.takeoff.Add(i, reader.ReadBoolean());
                    }
                }

                for (int i = 0; i < numPedestrians; i++)
                {
                    frame.PedestrianPositions.Add(reader.ReadListVector3());
                    frame.PedestrianRotations.Add(reader.ReadListQuaternion());
                    _ = reader.ReadInt32(); // Blinkers, unused

                    /*// Log data from Varjo HMD:
                    frame.HMD_pos_x = reader.ReadSingle(); frame.HMD_pos_y = reader.ReadSingle(); frame.HMD_pos_z = reader.ReadSingle(); // Position data
                    frame.HMD_rot_x = reader.ReadSingle(); frame.HMD_rot_y = reader.ReadSingle(); frame.HMD_rot_z = reader.ReadSingle(); // Rotation data

                    frame.LeftEyePupilSize = reader.ReadDouble();
                    frame.RightEyePupilSize = reader.ReadDouble();
                    frame.FocusDistance = reader.ReadDouble();
                    frame.FocusStability = reader.ReadDouble();

                    // Data from Vive handheld controller
                    frame.SafetyButton = reader.ReadSingle();*/
                }
            }
        }
        using (var writer = new StreamWriter(File.Create(dstFile)))
        {
            writer.WriteLine($"Root;{referenceName}");

            var startTime = log.StartTime;
            var startString = startTime.ToString("HH:mm:ss") + ":" + startTime.Millisecond.ToString();

            writer.WriteLine($"Start Time;{startString}");

            /*writer.WriteLine($"Exp Def Nr; {log.ExperimentDefinitionNr}");
            writer.WriteLine($"Trial nr; {log.TrialNr}");
            writer.WriteLine($"Participant nr; {log.ParticipantNr}");*/

            var localDriver = log.LocalDriver;
            var lastFrame = log.Frames[log.Frames.Count - 1];
            var numDrivers = lastFrame.DriverPositions.Count;
            var numPedestrians = lastFrame.PedestrianPositions.Count;

            // POI
            var pois = log.POIs;
            Vector3 PosToRefPoint(Vector3 p) => toRefRot * (p - referencePos);
            Quaternion RotToRefPoint(Quaternion q) => toRefRot * q;
            for (int i = 0; i < pois.Count; i++)
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
            for (int i = 0; i < numDrivers; i++)
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

            // Pedestrian headers
            if (numPedestrians > 0)
            {
                writer.Write(separator);
            }
            for (int i = 0; i < numPedestrians; i++)
            {
                var pedestrianName = $"Pedestrian{i}";
                writer.Write(string.Join(separator, Enumerable.Repeat(pedestrianName, columnsPerPedestrian)));
            }
            writer.Write("\n"); // New line after having written the headers.

            writer.Write("t;"); // for the Timestamp column
            writer.Write(separator); // for the Ping column (Round trip time)

            const string driverTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;vel_local_x;vel_local_y;vel_local_z;vel_local_smooth_x;vel_local_smooth_y;vel_local_smooth_z;vel_x;vel_y;vel_z;vel_smooth_x;vel_smooth_y;vel_smooth_z";
            const string localDriverTransformHeader = driverTransformHeader;
            const string aiCarTransformHeader = driverTransformHeader + ";aicar_speed;braking;stopped;takeoff";

            List<string> headers = new List<string>();
            for (int i = 0; i < numDrivers; i++)
            {
                if (i == localDriver)
                {
                    headers.Add(localDriverTransformHeader);
                } else if (IsAICar(i))
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
            const string pedestrianTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z";
            writer.Write(pedestrianTransformHeader);

            writer.Write("\n"); // New line, actual data writing starts now.
            /*writer.Write("\n");* Extra new line, for error that is somewhere down below which causes the first line to be empty for the driver.
                                 * Has to do with the if (prevFrame == null || prevFrame.DriverPositions.Count <= i) statement.
                                 * The prevFrame == null doesn't trigger, because on the previous line there were the driver headers.
                                 * Therefore, I add an extra line such that the values == null and the first line for the driver is correct. 
                                 
                                    DOESNT WORK EITHER */

            //****************
            // ACTUAL DATA
            //****************

            List<string> line = new List<string>();
            SerializedFrame prevFrame = default;
            float prevTime = 0;
            foreach (var frame in log.Frames)
            {
                line.Clear();
                line.Add(frame.Timestamp.ToString());
                line.Add(frame.RoundtripTime.ToString());
                int numDriversThisFrame = frame.DriverPositions.Count;
                if (_driverVels == null)
                {
                    _driverVels = new List<RunningAverage>(numDriversThisFrame);
                }
                for (int i = _driverVels.Count; i < numDriversThisFrame; i++)
                {
                    _driverVels.Add(new RunningAverage(NumFramesInVelocityRunningAverage));
                }

                var dt = frame.Timestamp - prevTime;
                for (int i = 0; i < numDriversThisFrame; i++)
                {
                    var pos = PosToRefPoint(frame.DriverPositions[i]);
                    var rot = frame.DriverRotations[i];
                    var euler = RotToRefPoint(rot).eulerAngles;
                    var inverseRotation = Quaternion.Inverse(rot);
                    if (prevFrame == null || prevFrame.DriverPositions.Count <= i)
                    {
                        if (i == localDriver)
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};0;0;0;0;0;0;0;0;0;0;0");
                        }
                        else if (IsAICar(i))
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0");
                        }
                        else
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};0;0;0;0;0");
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
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z}");

                        }
                        else if (IsAICar(i))
                        {
                            var aiCarSpeed = frame.AICarSpeeds[i];
                            var braking = frame.braking[i];
                            var stopped = frame.stopped[i];
                            var takeoff = frame.takeoff[i];
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z};{aiCarSpeed};{(braking ? 1 : 0)};{(stopped ? 1 : 0)};{(takeoff ? 1 : 0)}");
                        }
                        else
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z}");
                        }
                    }
                }
                for (int i = numDriversThisFrame; i < numDrivers; i++)
                {
                    if (IsAICar(i))
                    {
                        line.Add(";;;;;;;;;;;;;;;;;;;;;");
                    }
                    else {
                        line.Add(";;;;;;;;;;");
                    }
                }
                for (int i = 0; i < frame.PedestrianPositions.Count; i++)
                {
                    var pos = frame.PedestrianPositions[i];
                    var rot = frame.PedestrianRotations[i];

                    /*// Log data from Varjo HMD:
                    var HMD_pos_x = frame.HMD_pos_x; var HMD_pos_y = frame.HMD_pos_y; var HMD_pos_z = frame.HMD_pos_z;
                    var HMD_rot_x = frame.HMD_rot_x; var HMD_rot_y = frame.HMD_rot_y; var HMD_rot_z = frame.HMD_rot_z;

                    var LeftEyePupilSize = frame.LeftEyePupilSize;
                    var RightEyePupilSize = frame.RightEyePupilSize;
                    var FocusDistance = frame.FocusDistance;
                    var FocusStability = frame.FocusStability;

                    // Data from Vive handheld controller:
                    var SafetyButton = frame.SafetyButton;*/

                    // Only write the position of the pedestrian here, and HMD + Vive data
                    for(int j = 0; j < 1; j++)
                    //for (int j = 0; j < pos.Count; j++)
                    {
                        var p = PosToRefPoint(pos[j]);
                        var r = RotToRefPoint(rot[j]).eulerAngles;
                        line.Add($"{p.x};{p.y};{p.z};{r.x};{r.y};{r.z}");
                        //line.Add($"{p.x};{p.y};{p.z};{r.x};{r.y};{r.z};{HMD_pos_x};{HMD_pos_y};{HMD_pos_z};{HMD_rot_x};{HMD_rot_y};{HMD_rot_z};{LeftEyePupilSize};{RightEyePupilSize};{FocusDistance};{FocusStability};{SafetyButton}");
                    }
                }
                writer.Write(string.Join(separator, line));
                writer.Write("\n"); // next data line
                prevTime = frame.Timestamp;
                prevFrame = frame;
            }
        }
    }

    // Gets the names of the components the pedestrian is made of (I'm not using these)
    public LogConverter(PlayerAvatar pedestrianPrefab)
    {
        var transforms = pedestrianPrefab.SyncTransforms;
        _pedestrianSkeletonNames = new string[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            _pedestrianSkeletonNames[i] = transforms[i].name;
        }
    }

    string[] _pedestrianSkeletonNames;
    bool _open;
    List<string> _fileNames = new List<string>();
    string _selectedFileName = "";
    SerializedPOI[] _pois;

    List<SerializedPOI> customPois = new List<SerializedPOI>() {
        /*new SerializedPOI()
        {
            Name = "ldist",
            Position = new Vector3(0, 0, 7.75f),
            Rotation = Quaternion.Euler(new Vector3())
        },
                new SerializedPOI()
        {
            Name = "rdist",
            Position = new Vector3(0, 0, 2.75f),
            Rotation = Quaternion.Euler(new Vector3())
        },
        new SerializedPOI()
        {
            Name = "spawn point",
            Position = new Vector3(0, 0.2224625f, 0),
            Rotation = Quaternion.Euler(new Vector3())
        },*/

    };

    // Displays GUI and handles interactions for a single POI
    public void OnGUI_CustomPoiButton(int i, string name)
    {
        SerializedPOI serializedPOI = customPois[i];
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        GUILayout.Label("name:");
        serializedPOI.Name = GUILayout.TextField(serializedPOI.Name, GUILayout.Width(100));
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("position:");
        var tmp = serializedPOI.Position;
        GUILayout.BeginHorizontal();
        OnGUI_FloatField(ref tmp.x); OnGUI_FloatField(ref tmp.y); OnGUI_FloatField(ref tmp.z);
        GUILayout.EndHorizontal();
        serializedPOI.Position = tmp;
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("rotation:");
        tmp = serializedPOI.Rotation.eulerAngles;
        GUILayout.BeginHorizontal();
        OnGUI_FloatField(ref tmp.x); OnGUI_FloatField(ref tmp.y); OnGUI_FloatField(ref tmp.z);
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
                    for (int i = 0; i < customPois.Count(); i++)
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
}
