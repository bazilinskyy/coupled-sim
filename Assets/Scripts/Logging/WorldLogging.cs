using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using Varjo;
using VarjoExample;

enum LogFrameType
{
    PositionsUpdate,
    AICarSpawn,
}

//logger class
public class WorldLogger
{
    TrafficLightsSystem _lights;
    PlayerSystem _playerSystem;
    AICarSyncSystem _aiCarSystem;
    int _lastFrameAICarCount;
    BinaryWriter _fileWriter;
    float _startTime;

    public WorldLogger(PlayerSystem playerSys, AICarSyncSystem aiCarSystem)
    {
        _playerSystem = playerSys;
        _aiCarSystem = aiCarSystem;
    }

    List<PlayerAvatar> _driverBuffer = new List<PlayerAvatar>();

    // Set to true for logging bodySuit data.
    public static Boolean bodySuit = false;

    public static Boolean returnBodySuit()
    {
        return bodySuit;
    }

    //writes metadata header in binary log file
    public void BeginLog(string fileName, ExperimentDefinition experiment, TrafficLightsSystem lights, float time)
    {
        _lights = lights;

        // Create logging directory
        if (!Directory.Exists("ExperimentLogs"))
        {
            Directory.CreateDirectory("ExperimentLogs");
        }

        // Filename log data
        _fileWriter = new BinaryWriter(File.Create("ExperimentLogs/" + fileName + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".binLog"));

        // Add time to log file
        _startTime = time;
        _fileWriter.Write(DateTime.Now.ToBinary());
        
        // Add drivers and passengers avatars to the playeravatar list
        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Drivers);
        _driverBuffer.AddRange(_playerSystem.Passengers);

        // Add player index of local player, number of avatars, number of pedestrians, number of POI.
        _fileWriter.Write(_driverBuffer.IndexOf(_playerSystem.LocalPlayer));
        _fileWriter.Write(_driverBuffer.Count);
        _fileWriter.Write(_playerSystem.Pedestrians.Count);
        _fileWriter.Write(experiment.PointsOfInterest.Length);

        // For each POI, log the name, position and rotation 
        foreach (var poi in experiment.PointsOfInterest)
        {
            _fileWriter.Write(poi.name);
            _fileWriter.Write(poi.position);
            _fileWriter.Write(poi.rotation);
        }

        // Add light info (not relevant for me, can set _lights = null)
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
    }

    string GetHierarchyString(Transform trans)
    {
        List<string> names = new List<string>();
        while (trans != null)
        {
            names.Add(trans.name);      // Put the transform/input name into the list "names"
            trans = trans.parent;       // Turn the transform into its own parent 
        }
        names.Reverse();                // Why reverse the list?
        return string.Join("/", names);
    }

    
    //main logging logic
    //adds a single entry to the logfile
    public void LogFrame(float ping, float time)
    {
        // Log down the AI cars, position, time and ping
        var aiCars = _aiCarSystem.Cars;
        while (aiCars.Count > _lastFrameAICarCount)
        {
            _fileWriter.Write((int)LogFrameType.AICarSpawn);
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

        // Log the drivers position, rotation, carblinker state. Log the drivers/passengers Varjo HMD data  
        foreach (var driver in _driverBuffer)
        {
            _fileWriter.Write(driver.transform.position);
            _fileWriter.Write(driver.transform.rotation);
            _fileWriter.Write((int)driver._carBlinkers.State);
            //_fileWriter.Write(-1.0f); // test value -- WORKS IF PUT OUTSIDE THE IF STATEMENT
            /*Debug.LogError($"debug: {driver.transform.FindChild("Gaze")}");
            if (VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID)
            {
                Debug.LogError("debug: Entered getgaze");
                Debug.LogError($"debug: distance value = {driver.transform.GetComponentInChildren<VarjoGazeRay_CS>().getGazeRayHit().distance}");
                _fileWriter.Write(driver.transform.GetComponentInChildren<VarjoGazeRay_CS>().getGazeRayHit().distance);  // float
                Debug.LogError("debug: Written distance data to filewriter");
            }
            else
            {
                _fileWriter.Write(-1.0f);
            }*/

            // Only log car velocity if local player
            if (driver == _playerSystem.LocalPlayer)
            {
                var rb = driver.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb);
                Assert.IsFalse(rb.isKinematic);
                _fileWriter.Write(rb.velocity);

                // Varjo data driver/passenger (WIP) // Only works after eye-calibration.
                //_fileWriter.Write(-1.0f); // test value -- ERROR WHEN PUT IN THE IF LOCALPLAYER STATEMENT

                /*if (VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID)
                {
                    _fileWriter.Write(driver.transform.GetComponentInChildren<VarjoGazeRay_CS>().getGazeRayHit().distance);  // float
                    _fileWriter.Write(driver.transform.GetComponentInChildren<VarjoGazeRay_CS>().getGazeRayForward());       // hmd space, Vector 3
                    _fileWriter.Write(driver.transform.GetComponentInChildren<VarjoGazeRay_CS>().getGazeRayDirection());     // world space, Vector 3
                    _fileWriter.Write(driver.transform.GetComponentInChildren<VarjoGazeRay_CS>().getGazePosition());         // hmd space, Vector 3
                    _fileWriter.Write(driver.transform.GetComponentInChildren<VarjoGazeRay_CS>().getGazeRayOrigin());        // world space, Vector 3
                }
                else // still fill in data when the eye-calibration has not been done yet.
                {
                    Vector3 beforeCalibration = new Vector3(0, 0, 0);
                    _fileWriter.Write(0.0f);  // float
                    _fileWriter.Write(beforeCalibration);   // hmd space, Vector 3
                    _fileWriter.Write(beforeCalibration);   // world space, Vector 3
                    _fileWriter.Write(beforeCalibration);   // hmd space, Vector 3
                    _fileWriter.Write(beforeCalibration);   // world space, Vector 3
                }*/
            }
        }

        // Log position and rotation of pedestrian (from the GetPose function)
        foreach (var pedestrian in _playerSystem.Pedestrians)
        {
            pedestrian.GetPose(returnBodySuit()).SerializeTo(_fileWriter); // to do: remove non root pose
        }
        if (_lights != null)
        {
            foreach (var light in _lights.CarLights)
            {
                _fileWriter.Write((byte)light.State);
            }
            foreach (var light in _lights.PedestrianLights)
            {
                _fileWriter.Write((byte)light.State);
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
    }
}

//convert binary log into human readable csv
public class LogConverter
{
    public struct SerializedPOI
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;

        // To string return structure 
        public override string ToString()
        {
            var rot = Rotation.eulerAngles;
            return $"{Name};{Position.x};{Position.y};{Position.z};{rot.x};{rot.y};{rot.z}";
        }
    }
    // Assigning the name, pos, and rot to the "pois" list [unused for world coordinates]
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
        public int LocalDriver;
        public List<SerializedPOI> POIs;
        public List<string> CarLightNames;
        public List<string> PedestrianLightsNames;
        public List<SerializedFrame> Frames;
        public static Log New() => new Log
        {
            POIs = new List<SerializedPOI>(),
            CarLightNames = new List<string>(),
            PedestrianLightsNames = new List<string>(),
            Frames = new List<SerializedFrame>(),
        };
    }

    class SerializedFrame
    {
        public float Timestamp;
        public float RoundtripTime;
        public List<Vector3> DriverPositions = new List<Vector3>();
        public List<Quaternion> DriverRotations = new List<Quaternion>();
        public List<BlinkerState> BlinkerStates = new List<BlinkerState>();
        public List<List<Vector3>> PedestrianPositions = new List<List<Vector3>>();
        public List<List<Quaternion>> PedestrianRotations = new List<List<Quaternion>>();
        public List<LightState> CarLightStates = new List<LightState>();
        public List<LightState> PedestrianLightStates = new List<LightState>();
        public Vector3 LocalDriverRbVelocity;
        public float Distance; // test
    }

    List<Vector3> _driverPositions;
    List<RunningAverage> _driverVels;
    //translation logic
    //referenceName, referencePos, rLoeferenceRot - parameters specifying new origin point, allowing transforming data into new coordinate system
    public void TranslateBinaryLogToCsv(string sourceFile, string dstFile, string[] pedestrianSkeletonNames, string referenceName, Vector3 referencePos, Quaternion referenceRot)
    {
        _driverPositions = null;
        _driverVels = null;

        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        const string separator = ";";

        // Column headers
        const int columnsPerDriver = 3 /*pos x,y,z*/ + 3 /*rot x,y,z */ + 1 /*blinkers*/ + 1 /*distance*/ + 3 /* local velocity */ + 3 /* local smooth velocity */ + 3 /* world velocity */ + 3 /* world velocity smooth */;
        const int columnsForLocalDriver = columnsPerDriver + 3 /* rb velocity x,y,z */ + 3 /* rb velocity local x,y,z */; // + 3 /* Gaze Forward (HMD) x,y,z */ + 1 /* Gaze Position (HMD) x,y,z */ + 1 /* Gaze Direction (World) x,y,z */ + 1 /* Gaze Origin (World) x,y,z */ ;
       
        // Column headers for bodysuit tracking
        const int columnsPerBone = 6;
        int columnsPerPedestrian = 6;
        if (WorldLogger.returnBodySuit()) { 
            columnsPerPedestrian = pedestrianSkeletonNames.Length * columnsPerBone + columnsPerBone; // + columnsPerBone is for the root transform;
        }
        var toRefRot = Quaternion.Inverse(referenceRot);
        
        // Load binary file
        var srcFile = File.OpenRead(sourceFile);
        Log log = Log.New();
        using (var reader = new BinaryReader(srcFile))
        {
            log.StartTime = DateTime.FromBinary(reader.ReadInt64());
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
            Debug.Log($"eve: local driver = {log.LocalDriver}");
            Debug.Log($"eve: numPersistentDrivers = {numPersistentDrivers}");
            Debug.Log($"eve: numPedestrians = {numPedestrians}");

            int numCarLights = reader.ReadInt32();
            Debug.Log($"eve: numCarLights = {numCarLights}");
            for (int i = 0; i < numCarLights; i++)
            {
                log.CarLightNames.Add(reader.ReadString());
            }
            int numPedestrianLights = reader.ReadInt32();
            Debug.Log($"eve: numPedestrianLights = {numPedestrianLights}");
            for (int i = 0; i < numPedestrianLights; i++)
            {
                log.PedestrianLightsNames.Add(reader.ReadString());
            }
            int numAICars = 0;
            int n = 0; // test
            while (srcFile.Position < srcFile.Length)
            {
                n++;
                var eventType = (LogFrameType)reader.ReadInt32();
                Debug.Log($"eve: eventtype before = {eventType}, {n} of {srcFile.Length}" );
                if (eventType == LogFrameType.AICarSpawn)
                {
                    numAICars++;
                    continue;
                }
                Debug.Log($"eve: eventtype after = {eventType}, {n} of {srcFile.Length}");
                Assert.AreEqual(LogFrameType.PositionsUpdate, eventType); // error. assertion not equal
                var frame = new SerializedFrame();
                log.Frames.Add(frame);
                frame.Timestamp = reader.ReadSingle();
                frame.RoundtripTime = reader.ReadSingle();

                int numDriversThisFrame = numAICars + numPersistentDrivers;
                for (int i = 0; i < numDriversThisFrame; i++)
                {
                    frame.DriverPositions.Add(reader.ReadVector3());
                    frame.DriverRotations.Add(reader.ReadQuaternion());
                    frame.BlinkerStates.Add((BlinkerState)reader.ReadInt32());
                    frame.Distance = reader.ReadSingle(); // test varjo data logging
                    if (i == log.LocalDriver)
                    {
                        frame.LocalDriverRbVelocity = reader.ReadVector3() * SpeedConvertion.Mps2Kmph;
                    }
                }


                for (int i = 0; i < numPedestrians; i++)
                {
                    frame.PedestrianPositions.Add(reader.ReadListVector3());
                    frame.PedestrianRotations.Add(reader.ReadListQuaternion());
                    _ = reader.ReadInt32(); // Blinkers, unused
                }
                for (int i = 0; i < numCarLights; i++)
                {
                    frame.CarLightStates.Add((LightState)reader.ReadByte());
                }
                for (int i = 0; i < numPedestrianLights; i++)
                {
                    frame.PedestrianLightStates.Add((LightState)reader.ReadByte());
                }
            }
        }
        using (var writer = new StreamWriter(File.Create(dstFile)))
        {
            writer.WriteLine($"Root;{referenceName}");

            var startTime = log.StartTime;
            var startString = startTime.ToString("HH:mm:ss") + ":" + startTime.Millisecond.ToString();

            writer.WriteLine($"Start Time;{startString}");
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
                    writer.Write(string.Join(separator, Enumerable.Repeat($"Driver{i}", columnsForLocalDriver)));
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
            for (int i = 0; i < numPedestrians; i++)
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
            if (localDriver == -1)
            {
                writer.Write(string.Join(separator, new string(';', numDrivers * columnsPerDriver)));
            }
            else
            {
                writer.Write(string.Join(separator, new string(';', (numDrivers - 1) * columnsPerDriver + columnsForLocalDriver)));
            }
            var sb = new StringBuilder();
            sb.Append(string.Join(separator, Enumerable.Repeat("Root", columnsPerBone)));
            sb.Append(";");
            if (WorldLogger.returnBodySuit())
            {
                for (int i = 0; i < pedestrianSkeletonNames.Length; i++)
                {
                    sb.Append(string.Join(separator, Enumerable.Repeat(pedestrianSkeletonNames[i], columnsPerBone)));
                    if (i < pedestrianSkeletonNames.Length - 1)
                    {
                        sb.Append(separator);
                    }
                }
            }
            for (int i = 0; i < numPedestrians; i++)
            {
                writer.Write(sb.ToString());
            }

            writer.Write("\n");

            writer.Write(separator); // for the Timestamp column
            writer.Write(separator); // for the Ping column

            const string driverTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;blinkers;distance;vel_local_x;vel_local_y;vel_local_z;vel_local_smooth_x;vel_local_smooth_y;vel_local_smooth_z;vel_x;vel_y;vel_z;vel_smooth_x;vel_smooth_y;vel_smooth_z"; // added distance after blinkers
            const string localDriverTransformHeader = driverTransformHeader + ";rb_vel_x;rb_vel_y;rb_vel_z;rb_vel_local_x;rb_vel_local_y;rb_vel_local_z";
            List<string> headers = new List<string>();
            for (int i = 0; i < numDrivers; i++)
            {
                if (i == localDriver)
                {
                    headers.Add(localDriverTransformHeader);
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
            if (WorldLogger.returnBodySuit())
            {
                writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians * (pedestrianSkeletonNames.Length + 1))));
            }
            else
            {
                writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians)));
            }
            writer.Write("\n");

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
                    var blinkers = frame.BlinkerStates[i];
                    var distance = frame.Distance;
                    var inverseRotation = Quaternion.Inverse(rot);
                    if (prevFrame == null || prevFrame.DriverPositions.Count <= i)
                    {
                        if (i == localDriver)
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{distance};0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0");
                        }
                        else
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{distance};0;0;0;0;0;0;0;0;0;0;0;0");
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
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{distance};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z};{rbVel.x};{rbVel.y};{rbVel.z};{rbVelLocal.x};{rbVelLocal.y};{rbVelLocal.z}");

                        }
                        else
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{distance};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z}");
                        }
                    }
                }
                for (int i = numDriversThisFrame; i < numDrivers; i++)
                {
                    line.Add(";;;;;;;;;;;;;;;;;;");
                }
                for (int i = 0; i < frame.PedestrianPositions.Count; i++)
                {
                    var pos = frame.PedestrianPositions[i];
                    var rot = frame.PedestrianRotations[i];
                    for (int j = 0; j < pos.Count; j++)
                    {
                        var p = PosToRefPoint(pos[j]);
                        var r = RotToRefPoint(rot[j]).eulerAngles;
                        line.Add($"{p.x};{p.y};{p.z};{r.x};{r.y};{r.z}");
                    }
                }
                foreach (LightState v in frame.CarLightStates)
                {
                    line.Add(v.ToString());
                }
                foreach (LightState v in frame.PedestrianLightStates)
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

    // Gets the names of the components the pedestrian is made of.
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
        new SerializedPOI()
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
        },

    };

    //displays GUI and handles interactions for a single POI
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
