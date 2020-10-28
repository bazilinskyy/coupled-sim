using JetBrains.Annotations;
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
    int _expDefnr;
    int _trialNr;
    int _participantNr;

    // Data from varjo HMD
    float distance_pa;
    long Frame_pa;
    long CaptureTime_pa;

    Vector3 Hmdposition_pa; float Hmdposition_pa_x; float Hmdposition_pa_y; float Hmdposition_pa_z;
    Vector3 Hmdrotation_pa; float Hmdrotation_pa_x; float Hmdrotation_pa_y; float Hmdrotation_pa_z;

    double LeftEyePupilSize_pa;
    double RightEyePupilSize_pa;
    double FocusDistance_pa;
    double FocusStability_pa;

    Vector3 gazeRayForward_pa;      float gazeRayForward_pa_x;      float gazeRayForward_pa_y;      float gazeRayForward_pa_z;
    Vector3 gazeRayDirection_pa;    float gazeRayDirection_pa_x;    float gazeRayDirection_pa_y;    float gazeRayDirection_pa_z;
    Vector3 gazePosition_pa;        float gazePosition_pa_x;        float gazePosition_pa_y;        float gazePosition_pa_z;
    Vector3 gazeRayOrigin_pa;       float gazeRayOrigin_pa_x;       float gazeRayOrigin_pa_y;       float gazeRayOrigin_pa_z;

    float distance_pe;
    float Frame_pe;
    float CaptureTime_pe;
    
    Vector3 Hmdposition_pe; float Hmdposition_pe_x; float Hmdposition_pe_y; float Hmdposition_pe_z;
    Vector3 Hmdrotation_pe; float Hmdrotation_pe_x; float Hmdrotation_pe_y; float Hmdrotation_pe_z;
    
    double LeftEyePupilSize_pe;
    double RightEyePupilSize_pe;
    double FocusDistance_pe;
    double FocusStability_pe;

    Vector3 gazeRayForward_pe;      float gazeRayForward_pe_x;      float gazeRayForward_pe_y;      float gazeRayForward_pe_z;
    Vector3 gazeRayDirection_pe;    float gazeRayDirection_pe_x;    float gazeRayDirection_pe_y;    float gazeRayDirection_pe_z;
    Vector3 gazePosition_pe;        float gazePosition_pe_x;        float gazePosition_pe_y;        float gazePosition_pe_z;
    Vector3 gazeRayOrigin_pe;       float gazeRayOrigin_pe_x;       float gazeRayOrigin_pe_y;       float gazeRayOrigin_pe_z;

    // Data from Vive controller
    float gapAcceptance;

    public WorldLogger(PlayerSystem playerSys, AICarSyncSystem aiCarSystem)
    {
        _playerSystem = playerSys;
        _aiCarSystem = aiCarSystem;
    }

    List<PlayerAvatar> _driverBuffer = new List<PlayerAvatar>();

    // Set to true for logging bodySuit data.
    public static Boolean bodySuit = true;

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

        // Experiment Definition nr:
        _expDefnr = PersistentManager.Instance.experimentnr;
        _fileWriter.Write(_expDefnr);

        // Experiment trial nr:
        _trialNr = PersistentManager.Instance.listNr;
        _fileWriter.Write(_trialNr);

        // Participant nr:
        _participantNr = PersistentManager.Instance.ParticipantNr;
        _fileWriter.Write(_participantNr);
        
        // Add drivers and passengers avatars to the playeravatar list
        _driverBuffer.Clear();
        _driverBuffer.AddRange(_playerSystem.Drivers);
        _driverBuffer.AddRange(_playerSystem.Passengers);

        // Add player index of local player, number of avatars, number of pedestrians, number of POI.
        _fileWriter.Write(_driverBuffer.IndexOf(_playerSystem.LocalPlayer));    //1. int32 local driver
        _fileWriter.Write(_driverBuffer.Count);                                 //2. int32 numPersistentDrivers
        _fileWriter.Write(_playerSystem.Pedestrians.Count);                     //3. int32 numPedestrians
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
            _fileWriter.Write(_lights.CarLights.Length);                        //4. int32 numCarLights
            foreach (var light in _lights.CarLights)
            {
                _fileWriter.Write(GetHierarchyString(light.transform));
            }
            _fileWriter.Write(_lights.PedestrianLights.Length);                 //5. int32 numPedestrianLights
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
            _fileWriter.Write((int)LogFrameType.AICarSpawn);    //6. int32 eventType
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

            var passengerGaze = driver.transform.GetComponentInChildren<VarjoGazeRay_CS>();
            // Code enters the invalid statement and afterwards the valid statement (eye-calibration)
            if (VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID && driver.transform.Find("Gaze"))
            {
                    distance_pa = passengerGaze.getGazeRayHit().distance;

                    Frame_pa = passengerGaze.Frame;
                    CaptureTime_pa = passengerGaze.CaptureTime;

                    Hmdposition_pa = passengerGaze.hmdposition;
                    Hmdposition_pa_x = Hmdposition_pa.x; Hmdposition_pa_y = Hmdposition_pa.y; Hmdposition_pa_z = Hmdposition_pa.z;
                    
                    Hmdrotation_pa = passengerGaze.hmdrotation;
                    Hmdrotation_pa_x = Hmdrotation_pa.x; Hmdrotation_pa_y = Hmdrotation_pa.y; Hmdrotation_pa_z = Hmdrotation_pa.z;

                    LeftEyePupilSize_pa = passengerGaze.LeftPupilSize;
                    RightEyePupilSize_pa = passengerGaze.RightPupilSize;
                    FocusDistance_pa = passengerGaze.FocusDistance;
                    FocusStability_pa = passengerGaze.FocusStability;

                    gazeRayForward_pa = passengerGaze.gazeRayForward;       // hmd space
                    gazeRayForward_pa_x = gazeRayForward_pa.x; gazeRayForward_pa_y = gazeRayForward_pa.y; gazeRayForward_pa_z = gazeRayForward_pa.z;

                    gazeRayDirection_pa = passengerGaze.gazeRayDirection;   // world space
                    gazeRayDirection_pa_x = gazeRayDirection_pa.x; gazeRayDirection_pa_y = gazeRayDirection_pa.y; gazeRayDirection_pa_z = gazeRayDirection_pa.z;

                    gazePosition_pa = passengerGaze.gazePosition;           // hmd space
                    gazePosition_pa_x = gazePosition_pa.x; gazePosition_pa_y = gazePosition_pa.y; gazePosition_pa_z = gazePosition_pa.z;

                    gazeRayOrigin_pa = passengerGaze.gazeRayOrigin;         // world space
                    gazeRayOrigin_pa_x = gazeRayOrigin_pa.x; gazeRayOrigin_pa_y = gazeRayOrigin_pa.y; gazeRayOrigin_pa_z = gazeRayOrigin_pa.z;
            }
            else if(VarjoPlugin.GetGaze().status != VarjoPlugin.GazeStatus.VALID)
            {
                distance_pa = -1.0f;
                Frame_pa = (long)-1.0f;
                CaptureTime_pa = (long)-1.0f;

                Hmdposition_pa_x = -1.0f; Hmdposition_pa_y = -1.0f; Hmdposition_pa_z = -1.0f;
                Hmdrotation_pa_x = -1.0f; Hmdrotation_pa_y = -1.0f; Hmdrotation_pa_z = -1.0f;

                LeftEyePupilSize_pa = -1.0;
                RightEyePupilSize_pa = -1.0;
                FocusDistance_pa = -1.0;
                FocusStability_pa = -1.0;

                gazeRayForward_pa_x = -1.0f;    gazeRayForward_pa_y = -1.0f;    gazeRayForward_pa_z = -1.0f;
                gazeRayDirection_pa_x = -1.0f;  gazeRayDirection_pa_y = -1.0f;  gazeRayDirection_pa_z = -1.0f;
                gazePosition_pa_x = -1.0f;      gazePosition_pa_y = -1.0f;      gazePosition_pa_z = -1.0f;
                gazeRayOrigin_pa_x = -1.0f;     gazeRayOrigin_pa_y = -1.0f;     gazeRayOrigin_pa_z = -1.0f;
            }
            _fileWriter.Write(distance_pa);
            _fileWriter.Write(Frame_pa);
            _fileWriter.Write(CaptureTime_pa);

            _fileWriter.Write(Hmdposition_pa_x); _fileWriter.Write(Hmdposition_pa_y); _fileWriter.Write(Hmdposition_pa_z);
            _fileWriter.Write(Hmdrotation_pa_x); _fileWriter.Write(Hmdrotation_pa_y); _fileWriter.Write(Hmdrotation_pa_z);

            _fileWriter.Write(LeftEyePupilSize_pa);
            _fileWriter.Write(RightEyePupilSize_pa);
            _fileWriter.Write(FocusDistance_pa);
            _fileWriter.Write(FocusStability_pa);

            _fileWriter.Write(gazeRayForward_pa_x);     _fileWriter.Write(gazeRayForward_pa_y);     _fileWriter.Write(gazeRayForward_pa_z);
            _fileWriter.Write(gazeRayDirection_pa_x);   _fileWriter.Write(gazeRayDirection_pa_y);   _fileWriter.Write(gazeRayDirection_pa_z);
            _fileWriter.Write(gazePosition_pa_x);       _fileWriter.Write(gazePosition_pa_y);       _fileWriter.Write(gazePosition_pa_z);
            _fileWriter.Write(gazeRayOrigin_pa_x);      _fileWriter.Write(gazeRayOrigin_pa_y);      _fileWriter.Write(gazeRayOrigin_pa_z);

            // Only log car velocity if local player
            if (driver == _playerSystem.LocalPlayer)
            {
                var rb = driver.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb);
                Assert.IsFalse(rb.isKinematic);
                _fileWriter.Write(rb.velocity);
            }
        }

        // Log position and rotation of pedestrian (from the GetPose function)
        foreach (var pedestrian in _playerSystem.Pedestrians)
        {
            pedestrian.GetPose(returnBodySuit()).SerializeTo(_fileWriter); // to do: remove non root pose

            var pedestrianGaze = pedestrian.transform.GetComponentInChildren<VarjoGazeRay_CS_1>(); // to be deleted

            // Get NetworkObject data
            var N1 = pedestrian.transform.Find("NetworkObject_1");
            var N2 = pedestrian.transform.Find("NetworkObject_2");
            var N3 = pedestrian.transform.Find("NetworkObject_3");
            var N4 = pedestrian.transform.Find("NetworkObject_4");
            var N5 = pedestrian.transform.Find("NetworkObject_5");

            float gaze_status_pe = Mathf.Round(N1.position.x);
            if (gaze_status_pe == (float)VarjoPlugin.GazeStatus.VALID && pedestrian.transform.Find("Gaze"))
            {
                    distance_pe     = N1.localScale.x; 
                    Frame_pe        = N1.localScale.y;   // change to float
                    CaptureTime_pe  = N1.localScale.z;   // change to float

                    Hmdposition_pe_x = N2.position.x;   Hmdposition_pe_y = N2.position.y;   Hmdposition_pe_z = N2.position.z;
                    Hmdrotation_pe_x = N2.localScale.x; Hmdrotation_pe_y = N2.localScale.y; Hmdrotation_pe_z = N2.localScale.z;

                    LeftEyePupilSize_pe     = N3.position.x;
                    RightEyePupilSize_pe    = N3.position.y;
                    FocusDistance_pe        = N3.localScale.x;
                    FocusStability_pe       = N3.localScale.y;

                    gazeRayForward_pe_x = N4.position.x;        gazeRayForward_pe_y = N4.position.y;        gazeRayForward_pe_z = N4.position.z;// hmd space
                    gazeRayDirection_pe_x = N4.localScale.x;    gazeRayDirection_pe_y = N4.localScale.y;    gazeRayDirection_pe_z = N4.localScale.z;// world space

                    gazePosition_pe_x = N5.position.x;      gazePosition_pe_y = N5.position.y;      gazePosition_pe_z = N5.position.z;// hmd space
                    gazeRayOrigin_pe_x = N5.localScale.x;   gazeRayOrigin_pe_y = N5.localScale.y;   gazeRayOrigin_pe_z = N5.localScale.z;// world space
            }
            else if (gaze_status_pe != (float)VarjoPlugin.GazeStatus.VALID)
            {
                distance_pe = -8.0f;
                Debug.LogError($"Varjo NOT tracking - pedestrian gaze distance in logging = {distance_pe}");
                Frame_pe = -1.0f;
                CaptureTime_pe = -1.0f;

                Hmdposition_pe_x = -1.0f; Hmdposition_pe_y = -1.0f; Hmdposition_pe_z = -1.0f;
                Hmdrotation_pe_x = -1.0f; Hmdrotation_pe_y = -1.0f; Hmdrotation_pe_z = -1.0f;

                LeftEyePupilSize_pe = -1.0;
                RightEyePupilSize_pe = -1.0;
                FocusDistance_pe = -1.0;
                FocusStability_pe = -1.0;

                gazeRayForward_pe_x = -1.0f;    gazeRayForward_pe_y = -1.0f;    gazeRayForward_pe_z = -1.0f;
                gazeRayDirection_pe_x = -1.0f;  gazeRayDirection_pe_y = -1.0f;  gazeRayDirection_pe_z = -1.0f;
                gazePosition_pe_x = -1.0f;      gazePosition_pe_y = -1.0f;      gazePosition_pe_z = -1.0f;
                gazeRayOrigin_pe_x = -1.0f;     gazeRayOrigin_pe_y = -1.0f;     gazeRayOrigin_pe_z = -1.0f;
            }
            _fileWriter.Write(distance_pe);
            _fileWriter.Write(Frame_pe);
            _fileWriter.Write(CaptureTime_pe);

            _fileWriter.Write(Hmdposition_pe_x); _fileWriter.Write(Hmdposition_pe_y); _fileWriter.Write(Hmdposition_pe_z);
            _fileWriter.Write(Hmdrotation_pe_x); _fileWriter.Write(Hmdrotation_pe_y); _fileWriter.Write(Hmdrotation_pe_z);

            _fileWriter.Write(LeftEyePupilSize_pe);
            _fileWriter.Write(RightEyePupilSize_pe);
            _fileWriter.Write(FocusDistance_pe);
            _fileWriter.Write(FocusStability_pe);

            _fileWriter.Write(gazeRayForward_pe_x);     _fileWriter.Write(gazeRayForward_pe_y);     _fileWriter.Write(gazeRayForward_pe_z);
            _fileWriter.Write(gazeRayDirection_pe_x);   _fileWriter.Write(gazeRayDirection_pe_y);   _fileWriter.Write(gazeRayDirection_pe_z);
            _fileWriter.Write(gazePosition_pe_x);       _fileWriter.Write(gazePosition_pe_y);       _fileWriter.Write(gazePosition_pe_z);
            _fileWriter.Write(gazeRayOrigin_pe_x);      _fileWriter.Write(gazeRayOrigin_pe_y);      _fileWriter.Write(gazeRayOrigin_pe_z);

            // Vive controller
            if (pedestrian.transform.GetComponentInChildren<ViveInput>() != null)
            {
                gapAcceptance = pedestrian.transform.GetComponentInChildren<ViveInput>().getGapAcceptance();
            }
            else if (pedestrian.transform.GetComponentInChildren<ViveInput>() != null)
            {
                Debug.LogError("Vive controller action script called [ViveInput] is missing");
                gapAcceptance = -9.0f;
            }
            _fileWriter.Write(gapAcceptance);

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
        public int ExperimentDefinitionNr;
        public int TrialNr;
        public int ParticipantNr;
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
        // Varjo data of the passenger
        public float distance_pa;
        public long Frame_pa;
        public long CaptureTime_pa;

        public float Hmdposition_pa_x; public float Hmdposition_pa_y; public float Hmdposition_pa_z;
        public float Hmdrotation_pa_x; public float Hmdrotation_pa_y; public float Hmdrotation_pa_z;

        public double LeftEyePupilSize_pa;
        public double RightEyePupilSize_pa;
        public double FocusDistance_pa;
        public double FocusStability_pa;

        public float gazeRayForward_pa_x;   public float gazeRayForward_pa_y;   public float gazeRayForward_pa_z;
        public float gazeRayDirection_pa_x; public float gazeRayDirection_pa_y; public float gazeRayDirection_pa_z;
        public float gazePosition_pa_x;     public float gazePosition_pa_y;     public float gazePosition_pa_z;
        public float gazeRayOrigin_pa_x;    public float gazeRayOrigin_pa_y;    public float gazeRayOrigin_pa_z;

        // Varjo data of the pedestrian
        public float distance_pe;
        public float Frame_pe;
        public float CaptureTime_pe;

        public float Hmdposition_pe_x; public float Hmdposition_pe_y; public float Hmdposition_pe_z;
        public float Hmdrotation_pe_x; public float Hmdrotation_pe_y; public float Hmdrotation_pe_z;

        public double LeftEyePupilSize_pe;
        public double RightEyePupilSize_pe;
        public double FocusDistance_pe;
        public double FocusStability_pe;

        public float gazeRayForward_pe_x;   public float gazeRayForward_pe_y;   public float gazeRayForward_pe_z;
        public float gazeRayDirection_pe_x; public float gazeRayDirection_pe_y; public float gazeRayDirection_pe_z;
        public float gazePosition_pe_x;     public float gazePosition_pe_y;     public float gazePosition_pe_z;
        public float gazeRayOrigin_pe_x;    public float gazeRayOrigin_pe_y;    public float gazeRayOrigin_pe_z;

        // Vive controller data of the pedestrian
        public float gapAcceptance;
    }

    List<Vector3> _driverPositions;
    List<RunningAverage> _driverVels;
    //translation logic
    //referenceName, referencePos, referenceRot - parameters specifying new origin point, allowing transforming data into new coordinate system
    public void TranslateBinaryLogToCsv(string sourceFile, string dstFile, string[] pedestrianSkeletonNames, string referenceName, Vector3 referencePos, Quaternion referenceRot)
    {
        _driverPositions = null;
        _driverVels = null;

        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        const string separator = ";";

        // Column headers
        const int columnsPerDriver = 3 /*pos x,y,z*/ + 3 /*rot x,y,z */ + 1 /*blinkers*/ + 1 /*distance_pa*/+ 1 /*Frame*/ + 1 /*CaptureTime*/ + 3 /*hmdposition_xyz*/ + 3 /*hmdrotation_xyz*/ + 2 /*eyePupilSize*/ + 1 /*focusDistance*/ + 1 /*focusStability*/ + 3 /*gazeForward_xyz*/  + 3 /* gazeDirection_xyz */+ 3 /*gazePosition_xyz*/ + 3 /*gazeOrigin_xyz*/ + 3 /* local velocity */ + 3 /* local smooth velocity */ + 3 /* world velocity */ + 3 /* world velocity smooth */;
        const int columnsForLocalDriver = columnsPerDriver + 3 /* rb velocity x,y,z */ + 3 /* rb velocity local x,y,z */; 

        // Column headers for bodysuit tracking
        const int columnsPerBone = 6;
        int columnsPerPedestrian = 3 /*pos x,y,z*/+ 3/*rot x,y,z */ + 1 /*distance_pe*/ + 1 /*Frame*/ + 1 /*CaptureTime*/ + 3 /*hmdposition_xyz*/ + 3 /*hmdrotation_xyz*/ + 2 /*eyePupilSize*/ + 1 /*focusDistance*/ + 1 /*focusStability*/+ 3 /*gazeForward_xyz*/  + 3 /* gazeDirection_xyz */+ 3 /*gazePosition_xyz*/ + 3 /*gazeOrigin_xyz*/ + 1 /*gapAcceptance*/;
        /*if (WorldLogger.returnBodySuit()) { 
            columnsPerPedestrian = pedestrianSkeletonNames.Length * columnsPerBone + columnsPerBone + 14; // + columnsPerBone is for the root transform;
        }*/
        var toRefRot = Quaternion.Inverse(referenceRot);
        
        // Load binary file
        var srcFile = File.OpenRead(sourceFile);
        Log log = Log.New();
        using (var reader = new BinaryReader(srcFile))
        {
            /*Debug.LogError($"1 Local driver = {reader.ReadInt32()}");
            Debug.LogError($"2 numPersistentDrivers = {reader.ReadInt32()}");
            Debug.LogError($"3 numPedestrians = {reader.ReadInt32()}");
            Debug.LogError($"4 numCarlights = {reader.ReadInt32()}");
            Debug.LogError($"5 numPedestrianLights = {reader.ReadInt32()}");
            Debug.LogError($"6 eventType = {(LogFrameType)reader.ReadInt32()}");
            Debug.LogError($"------------------End List------------------");*/

            log.StartTime = DateTime.FromBinary(reader.ReadInt64());
            log.ExperimentDefinitionNr = reader.ReadInt32();
            log.TrialNr = reader.ReadInt32();               //Debug.LogError($"1 trial nr = {log.TrialNr}");
            log.ParticipantNr = reader.ReadInt32();         //Debug.LogError($"2 participant nr = {log.ParticipantNr}");
            log.LocalDriver = reader.ReadInt32();           //Debug.LogError($"3 Local driver = {log.LocalDriver}");
            int numPersistentDrivers = reader.ReadInt32();  //Debug.LogError($"4 numPersistentDrivers = {numPersistentDrivers}");
            int numPedestrians = reader.ReadInt32();        //Debug.LogError($"5 numPedestrians = {numPedestrians}");
            log.POIs.AddRange(ParsePOI(reader));
            log.POIs.AddRange(customPois);
            log.POIs.Add(new SerializedPOI()
            {
                Name = UNITY_WORLD_ROOT,
                Position = Vector3.zero,
                Rotation = Quaternion.identity
            });

            int numCarLights = reader.ReadInt32();          //Debug.LogError($"6 numCarLights = {numCarLights}");
            for (int i = 0; i < numCarLights; i++)
            {
                log.CarLightNames.Add(reader.ReadString());
            }
            int numPedestrianLights = reader.ReadInt32();   //Debug.LogError($"7 numPedestrianLights = {numPedestrianLights}");
            for (int i = 0; i < numPedestrianLights; i++)
            {
                log.PedestrianLightsNames.Add(reader.ReadString());
            }
            int numAICars = 0;
             while (srcFile.Position < srcFile.Length)
            {
                var eventType = (LogFrameType)reader.ReadInt32(); //Debug.LogError($"8 eventType = {eventType}");
                if (eventType == LogFrameType.AICarSpawn)
                {
                    Debug.LogError("eventtype aicarspawn");
                    numAICars++;
                    continue;
                }
                /*Debug.LogError($"logframe type = {LogFrameType.PositionsUpdate}");
                Debug.LogError($"event type = {eventType}");
                if(eventType != LogFrameType.PositionsUpdate)
                {
                    Debug.LogError("event type is not equal to the logframe type");
                    return;
                }*/
                //Debug.LogError($"Eventtype = {eventType}");
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
                    var blinkerstate = (BlinkerState)reader.ReadInt32(); //Debug.LogError($"9 blinkerstate = {blinkerstate}");
                    frame.BlinkerStates.Add(blinkerstate); //frame.BlinkerStates.Add((BlinkerState)reader.ReadInt32());
                    frame.distance_pa = reader.ReadSingle();
                    frame.Frame_pa = reader.ReadInt64();
                    frame.CaptureTime_pa = reader.ReadInt64();

                    frame.Hmdposition_pa_x = reader.ReadSingle();   frame.Hmdposition_pa_y = reader.ReadSingle();   frame.Hmdposition_pa_z = reader.ReadSingle();
                    frame.Hmdrotation_pa_x = reader.ReadSingle();   frame.Hmdrotation_pa_y = reader.ReadSingle();   frame.Hmdrotation_pa_z = reader.ReadSingle();

                    frame.LeftEyePupilSize_pa = reader.ReadDouble(); 
                    frame.RightEyePupilSize_pa = reader.ReadDouble();
                    frame.FocusDistance_pa = reader.ReadDouble();
                    frame.FocusStability_pa = reader.ReadDouble();

                    frame.gazeRayForward_pa_x = reader.ReadSingle();    frame.gazeRayForward_pa_y = reader.ReadSingle();    frame.gazeRayForward_pa_z = reader.ReadSingle();
                    frame.gazeRayDirection_pa_x = reader.ReadSingle();  frame.gazeRayDirection_pa_y = reader.ReadSingle();  frame.gazeRayDirection_pa_z = reader.ReadSingle();
                    frame.gazePosition_pa_x = reader.ReadSingle();      frame.gazePosition_pa_y = reader.ReadSingle();      frame.gazePosition_pa_z = reader.ReadSingle();
                    frame.gazeRayOrigin_pa_x = reader.ReadSingle();     frame.gazeRayOrigin_pa_y = reader.ReadSingle();     frame.gazeRayOrigin_pa_z = reader.ReadSingle();

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

                    frame.distance_pe = reader.ReadSingle();
                    frame.Frame_pe = reader.ReadSingle();
                    frame.CaptureTime_pe = reader.ReadSingle();

                    frame.Hmdposition_pe_x = reader.ReadSingle(); frame.Hmdposition_pe_y = reader.ReadSingle(); frame.Hmdposition_pe_z = reader.ReadSingle();
                    frame.Hmdrotation_pe_x = reader.ReadSingle(); frame.Hmdrotation_pe_y = reader.ReadSingle(); frame.Hmdrotation_pe_z = reader.ReadSingle();

                    frame.LeftEyePupilSize_pe = reader.ReadDouble();
                    frame.RightEyePupilSize_pe = reader.ReadDouble();
                    frame.FocusDistance_pe = reader.ReadDouble();
                    frame.FocusStability_pe = reader.ReadDouble();

                    frame.gazeRayForward_pe_x = reader.ReadSingle(); frame.gazeRayForward_pe_y = reader.ReadSingle(); frame.gazeRayForward_pe_z = reader.ReadSingle();
                    frame.gazeRayDirection_pe_x = reader.ReadSingle(); frame.gazeRayDirection_pe_y = reader.ReadSingle(); frame.gazeRayDirection_pe_z = reader.ReadSingle();
                    frame.gazePosition_pe_x = reader.ReadSingle(); frame.gazePosition_pe_y = reader.ReadSingle(); frame.gazePosition_pe_z = reader.ReadSingle();
                    frame.gazeRayOrigin_pe_x = reader.ReadSingle(); frame.gazeRayOrigin_pe_y = reader.ReadSingle(); frame.gazeRayOrigin_pe_z = reader.ReadSingle();

                    frame.gapAcceptance = reader.ReadSingle();
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

            writer.WriteLine($"Exp Def Nr; {log.ExperimentDefinitionNr}");
            writer.WriteLine($"Trial nr; {log.TrialNr}");
            writer.WriteLine($"Participant nr; {log.ParticipantNr}");

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
            if (log.CarLightNames.Count > 0) Debug.LogError($"num carlightnames = {log.CarLightNames.Count}");
            {
                writer.Write(separator);
                writer.Write(string.Join(separator, log.CarLightNames));
            }
            if (log.PedestrianLightsNames.Count > 0) Debug.LogError($"num pedestrianlight names = {log.PedestrianLightsNames.Count}");
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
                    Debug.LogError($"PedestrianSkeletonNames are {i} = {pedestrianSkeletonNames[i]}");// test
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

            const string driverTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;blinkers;distance_pa;frame;captureTime;hmdpos_x;hmdpos_y;hmdpos_z;hmdrot_x;hmdrot_y;hmdrot_z;leftEyePupilSize;rightEyePupilSize;focusDistance;focusStability;gazeRayForward_x;gazeRayForward_y;gazeRayForward_z;gazeRayDirection_x;gazeRayDirection_y;gazeRayDirection_z;gazePosition_x;gazePosition_y;gazePosition_z;gazeOrigin_x;gazeOrigin_y;gazeOrigin_z;vel_local_x;vel_local_y;vel_local_z;vel_local_smooth_x;vel_local_smooth_y;vel_local_smooth_z;vel_x;vel_y;vel_z;vel_smooth_x;vel_smooth_y;vel_smooth_z"; // added distance after blinkers
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
            // test
            Debug.LogError($"numPedstrians = {numPedestrians}");
            const string boneTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;distance_pe;frame;captureTime;hmdpos_x;hmdpos_y;hmdpos_z;hmdrot_x;hmdrot_y;hmdrot_z;leftEyePupilSize;rightEyePupilSize;focusDistance;focusStability;gazeRayForward_x;gazeRayForward_y;gazeRayForward_z;gazeRayDirection_x;gazeRayDirection_y;gazeRayDirection_z;gazePosition_x;gazePosition_y;gazePosition_z;gazeOrigin_x;gazeOrigin_y;gazeOrigin_z;gapAcceptance;";
            /*if (WorldLogger.returnBodySuit())
            {
                writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians * (pedestrianSkeletonNames.Length + 1)))); //writes the bonetransformheader for every "part" of the pedestrian
            }
            else
            {*/
                writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians)));
            //}
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
                    var distance_pa = frame.distance_pa;
                    var Frame_pa = frame.Frame_pa;
                    var CaptureTime_pa = frame.CaptureTime_pa;
                    var HmdPos_pa_x = frame.Hmdposition_pa_x; var HmdPos_pa_y = frame.Hmdposition_pa_y; var HmdPos_pa_z = frame.Hmdposition_pa_z;
                    var HmdRot_pa_x = frame.Hmdrotation_pa_x; var HmdRot_pa_y = frame.Hmdrotation_pa_y; var HmdRot_pa_z = frame.Hmdrotation_pa_z;
                    var LeftEyePupilSize_pa = frame.LeftEyePupilSize_pa;
                    var RightEyePupilSize_pa = frame.RightEyePupilSize_pa;
                    var FocusDistance_pa = frame.FocusDistance_pa;
                    var FocusStability_pa = frame.FocusStability_pa;

                    var gazeRayForward_pa_x = frame.gazeRayForward_pa_x;        var gazeRayForward_pa_y = frame.gazeRayForward_pa_y; var gazeRayForward_pa_z = frame.gazeRayForward_pa_z;
                    var gazeRayDirection_pa_x = frame.gazeRayDirection_pa_x;    var gazeRayDirection_pa_y = frame.gazeRayDirection_pa_y; var gazeRayDirection_pa_z = frame.gazeRayDirection_pa_z;
                    var gazePosition_pa_x = frame.gazePosition_pa_x;            var gazePosition_pa_y = frame.gazePosition_pa_y; var gazePosition_pa_z = frame.gazePosition_pa_z;
                    var gazeRayOrigin_pa_x = frame.gazeRayOrigin_pa_x;          var gazeRayOrigin_pa_y = frame.gazeRayOrigin_pa_y; var gazeRayOrigin_pa_z = frame.gazeRayOrigin_pa_z;

                    var inverseRotation = Quaternion.Inverse(rot);
                    if (prevFrame == null || prevFrame.DriverPositions.Count <= i)
                    {
                        if (i == localDriver)
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{distance_pa};{Frame_pa};{CaptureTime_pa};{HmdPos_pa_x};{HmdPos_pa_y};{HmdPos_pa_z};{HmdRot_pa_x};{HmdRot_pa_y};{HmdRot_pa_z};{LeftEyePupilSize_pa};{RightEyePupilSize_pa};{FocusDistance_pa};{FocusStability_pa};{gazeRayForward_pa_x};{gazeRayForward_pa_y};{gazeRayForward_pa_z};{gazeRayDirection_pa_x};{gazeRayDirection_pa_y};{gazeRayDirection_pa_z};{gazePosition_pa_x};{gazePosition_pa_y};{gazePosition_pa_z};{gazeRayOrigin_pa_x};{gazeRayOrigin_pa_y};{gazeRayOrigin_pa_z};0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0");
                        }
                        else
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{distance_pa};{Frame_pa};{CaptureTime_pa};{HmdPos_pa_x};{HmdPos_pa_y};{HmdPos_pa_z};{HmdRot_pa_x};{HmdRot_pa_y};{HmdRot_pa_z};{LeftEyePupilSize_pa};{RightEyePupilSize_pa};{FocusDistance_pa};{FocusStability_pa};{gazeRayForward_pa_x};{gazeRayForward_pa_y};{gazeRayForward_pa_z};{gazeRayDirection_pa_x};{gazeRayDirection_pa_y};{gazeRayDirection_pa_z};{gazePosition_pa_x};{gazePosition_pa_y};{gazePosition_pa_z};{gazeRayOrigin_pa_x};{gazeRayOrigin_pa_y};{gazeRayOrigin_pa_z};0;0;0;0;0;0;0;0;0;0;0;0");
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
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{distance_pa};{Frame_pa};{CaptureTime_pa};{HmdPos_pa_x};{HmdPos_pa_y};{HmdPos_pa_z};{HmdRot_pa_x};{HmdRot_pa_y};{HmdRot_pa_z};{LeftEyePupilSize_pa};{RightEyePupilSize_pa};{FocusDistance_pa};{FocusStability_pa};{gazeRayForward_pa_x};{gazeRayForward_pa_y};{gazeRayForward_pa_z};{gazeRayDirection_pa_x};{gazeRayDirection_pa_y};{gazeRayDirection_pa_z};{gazePosition_pa_x};{gazePosition_pa_y};{gazePosition_pa_z};{gazeRayOrigin_pa_x};{gazeRayOrigin_pa_y};{gazeRayOrigin_pa_z};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z};{rbVel.x};{rbVel.y};{rbVel.z};{rbVelLocal.x};{rbVelLocal.y};{rbVelLocal.z}");
                        }
                        else
                        {
                            line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{distance_pa};{Frame_pa};{CaptureTime_pa};{HmdPos_pa_x};{HmdPos_pa_y};{HmdPos_pa_z};{HmdRot_pa_x};{HmdRot_pa_y};{HmdRot_pa_z};{LeftEyePupilSize_pa};{RightEyePupilSize_pa};{FocusDistance_pa};{FocusStability_pa};{gazeRayForward_pa_x};{gazeRayForward_pa_y};{gazeRayForward_pa_z};{gazeRayDirection_pa_x};{gazeRayDirection_pa_y};{gazeRayDirection_pa_z};{gazePosition_pa_x};{gazePosition_pa_y};{gazePosition_pa_z};{gazeRayOrigin_pa_x};{gazeRayOrigin_pa_y};{gazeRayOrigin_pa_z};{speed.x};{speed.y};{speed.z};{localSmooth.x};{localSmooth.y};{localSmooth.z};{vel.x};{vel.y};{vel.z};{velSmooth.x};{velSmooth.y};{velSmooth.z}");
                        }
                    }
                }
                for (int i = numDriversThisFrame; i < numDrivers; i++)
                {
                    line.Add(";;;;;;;;;;;;;;;;;;");
                }
                Debug.LogError($"actual data fram pedestrian positions count = {frame.PedestrianPositions.Count}");
                for (int i = 0; i < frame.PedestrianPositions.Count; i++) 
                {
                    Debug.LogError($"i = {i}, with frame pedestrian position = {frame.PedestrianPositions[i]}");
                    var pos = frame.PedestrianPositions[i];
                    var rot = frame.PedestrianRotations[i];
                    var distance_pe = frame.distance_pe;

                    var Frame_pe = frame.Frame_pe;
                    var CaptureTime_pe = frame.CaptureTime_pe;
                    var HmdPos_pe_x = frame.Hmdposition_pe_x; var HmdPos_pe_y = frame.Hmdposition_pe_y; var HmdPos_pe_z = frame.Hmdposition_pe_z;
                    var HmdRot_pe_x = frame.Hmdrotation_pe_x; var HmdRot_pe_y = frame.Hmdrotation_pe_y; var HmdRot_pe_z = frame.Hmdrotation_pe_z;
                    var LeftEyePupilSize_pe = frame.LeftEyePupilSize_pe;
                    var RightEyePupilSize_pe = frame.RightEyePupilSize_pe;
                    var FocusDistance_pe = frame.FocusDistance_pe;
                    var FocusStability_pe = frame.FocusStability_pe;

                    var gazeRayForward_pe_x = frame.gazeRayForward_pe_x;        var gazeRayForward_pe_y = frame.gazeRayForward_pe_y;        var gazeRayForward_pe_z = frame.gazeRayForward_pe_z;
                    var gazeRayDirection_pe_x = frame.gazeRayDirection_pe_x;    var gazeRayDirection_pe_y = frame.gazeRayDirection_pe_y;    var gazeRayDirection_pe_z = frame.gazeRayDirection_pe_z;
                    var gazePosition_pe_x = frame.gazePosition_pe_x;            var gazePosition_pe_y = frame.gazePosition_pe_y;            var gazePosition_pe_z = frame.gazePosition_pe_z;
                    var gazeRayOrigin_pe_x = frame.gazeRayOrigin_pe_x;          var gazeRayOrigin_pe_y = frame.gazeRayOrigin_pe_y;          var gazeRayOrigin_pe_z = frame.gazeRayOrigin_pe_z;

                    var gapAcceptance = frame.gapAcceptance;

                    Debug.LogError($"pos count inside pedestrian actual data = {pos.Count}");
                    for (int j = 0; j < pos.Count; j++)
                    {
                        var p = PosToRefPoint(pos[j]);
                        var r = RotToRefPoint(rot[j]).eulerAngles;
                        line.Add($"{p.x};{p.y};{p.z};{r.x};{r.y};{r.z};{distance_pe};{Frame_pe};{CaptureTime_pe};{HmdPos_pe_x};{HmdPos_pe_y};{HmdPos_pe_z};{HmdRot_pe_x};{HmdRot_pe_y};{HmdRot_pe_z};{LeftEyePupilSize_pe};{RightEyePupilSize_pe};{FocusDistance_pe};{FocusStability_pe};{gazeRayForward_pe_x};{gazeRayForward_pe_y};{gazeRayForward_pe_z};{gazeRayDirection_pe_x};{gazeRayDirection_pe_y};{gazeRayDirection_pe_z};{gazePosition_pe_x};{gazePosition_pe_y};{gazePosition_pe_z};{gazeRayOrigin_pe_x};{gazeRayOrigin_pe_y};{gazeRayOrigin_pe_z};{gapAcceptance};");
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

    private bool convertAll = false;

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
        convertAll = GUILayout.Toggle(convertAll, "Convert all log files");
        
        if (!_open && GUILayout.Button("Convert log to csv")) // first button
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
                if(convertAll == true)
                {
                    var fullName = "ExperimentLogs/" + name;
                    var csvName = "ExperimentLogs/" + UNITY_WORLD_ROOT + "-" + name.Replace("binLog", "csv");
                    TranslateBinaryLogToCsv(fullName, csvName, _pedestrianSkeletonNames, UNITY_WORLD_ROOT, default, Quaternion.identity);
                }
                else {
                    if (string.IsNullOrEmpty(_selectedFileName))
                    {
                        if (GUILayout.Button(name)) // second button: logfile selection
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

                        Debug.Log($"poy = {_pois[0]}");
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
}
