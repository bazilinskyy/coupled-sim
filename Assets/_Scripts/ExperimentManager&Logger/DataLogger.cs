using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using UnityEngine;
using Varjo;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Globalization;
[RequireComponent(typeof(MyGazeLogger), typeof(MyVarjoGazeRay))]
public class DataLogger : MonoBehaviour
{
    private const string vehicleDataFileName = "vehicleData.csv";
    private const string navigationLineFileName = "navigationLine.csv";
    private const string targetDetectionDataFileName = "targetDetectionData.csv";
    private const string generalTargetInfo = "generalTargetInfo.csv";
    private const string generalExperimentInfo = "GeneralExperimentInfo.csv";
    private const string fixationFileName = "fixationData.csv";
    private const string WrongTurnFileName = "carIrregularities.csv";
    //our car
    private newNavigator car;
    //Current Navigation
    public Vector3[] currentNavigationLine = new Vector3[0];
    public Vector3[] totalNavigationLine;
    private int indexClosestPoint = 0;//Used for calculating the distance to optimal navigation path (i.e., centre of raod)

    private GameObject lightObj;
    //Experiment manager;
    private newExperimentManager experimentManager;
   
    //list of items
    private List<VehicleDataPoint> vehicleData;
    private List<TargetAlarm> targetDetectionData;
    public List<TargetData> targetData;
    public List<DrivingIrregularities> carIrregularityData;

    private MainManager mainManager;

    private string saveFolder;
    private string steerInputAxis;
    private string gasInputAxis;
    [HideInInspector]
    public bool savedData = false;
    public bool logging;

    private MyGazeLogger gazeLogger;

    // Use standard numeric format specifiers.
    private CultureInfo culture = CultureInfo.CreateSpecificCulture("eu-ES");
    private string specifier = "G";
    
    private void OnApplicationQuit() { if (mainManager.SaveData && !savedData) { SaveData(); } }

    public void StartUp()
    {
        mainManager = MyUtils.GetMainManager();
        experimentManager = MyUtils.GetExperimentManager();
        car = MyUtils.GetCar().GetComponent<newNavigator>();

        List<string> inputs = experimentManager.GetCarControlInput();
        steerInputAxis = inputs[0];
        gasInputAxis = inputs[1];

        logging = true; savedData = false; saveFolder = SaveFolder();

        //initialising lists
        vehicleData = new List<VehicleDataPoint>();
        targetDetectionData = new List<TargetAlarm>();
        targetData = new List<TargetData>();
        carIrregularityData = new List<DrivingIrregularities>();

        //Eye fixation stuff
        if (mainManager.camType == MyCameraType.Normal)
        {
            GetComponent<MyVarjoGazeRay>().enabled = false;
            GetComponent<MyGazeLogger>().enabled = false;
            return;
        }

        else
        {
            //Get the gaze logging components
            MyVarjoGazeRay gazeRay = GetComponent<MyVarjoGazeRay>();
            gazeLogger = GetComponent<MyGazeLogger>();

            gazeLogger.enabled = true; gazeRay.enabled = true;

            //Set car to be ignored by raycast of gaze logger
            gazeRay.layerMask = ~experimentManager.layerToIgnoreForTargetDetection;
            
            //Set logging path and some variables
            gazeLogger.startAutomatically = false;
            gazeLogger.useCustomLogPath = true;
            gazeLogger.oneGazeDataPerFrame = false;
            gazeLogger.customLogPath = saveFolder + "/";
            
            //Start both components
            gazeRay.StartUp();
            gazeLogger.StartUp();
        }
    }
    
    public void LogTargets(List<Target> targets)
    {
        //Debug.Log($"Logging {targets.Count()}... ");
        foreach(Target target in targets)
        {
            TargetData targetInfo = new TargetData(target);
            targetData.Add(targetInfo);
        }
    }
    public void LogIrregularity(Irregularity irregularity) 
    { 
        carIrregularityData.Add(new DrivingIrregularities(car.waypoint,  irregularity, experimentManager.experimentSettings.experimentTime, car.transform.position));
    }
    private void Update()
    {
        if (logging) { AddVehicleData(); }
    }
    public void SaveData()
    {
        if (!logging) { return; }
        Debug.Log($"Saving {experimentManager.experimentSettings.name} data to {saveFolder}...");
        if (mainManager.camType != MyCameraType.Normal) { SaveFixationData(); gazeLogger.StopLogging();  }

        SaveVehicleData();
        SaveTargetDetectionData();
        SaveTargetData();
        SaveCarIrregularityData();
        SaveNavigationData();
        SaveExperimentInfo();

        logging = false;
        savedData = true;
    }
    private void SaveCarIrregularityData()
    {
        string[] columns = { "ExperimentTime", "Irregularity", "WaypointID", "TurnType", "CarPosition" };
        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, WrongTurnFileName);


        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);

            foreach (DrivingIrregularities irregularity in carIrregularityData)
            {
                logData[0] = irregularity.time.ToString(specifier,culture);
                logData[1] = irregularity.irregularity.ToString();
                logData[2] = irregularity.waypoint.waypointID.ToString();
                logData[3] = irregularity.waypoint.turn.ToString();
                logData[4] = irregularity.carPosition.ToString("F3");
                

                Log(logData, file);
            }
            file.Flush();
            file.Close();
        }
    }
    public void AddCurrentNavigationLine(Vector3[] points)
    {
        //Debug.Log($"Got new navigation points: {points.Length}...");
        indexClosestPoint = 0;
        currentNavigationLine = points;
        totalNavigationLine = AppendArrays(totalNavigationLine, points);
    }
    private void SaveFixationData()
    {
        //Order of LoggedTag enum
        /*World,
        Target,
        HUDSymbology,
        HUDText,
        ConformalSymbology,
        InsideCar,
        LeftMirror,
        RightMirror,
        RearMirror,*/

        IEnumerable<LoggedTags> loggedTags = EnumUtil.GetValues<LoggedTags>();
        string[] columns = new string[loggedTags.Count()]; int index = 0;
        foreach (LoggedTags tag in loggedTags) { columns[index] = tag.ToString(); index++; }

        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, fixationFileName);

        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);

            logData[0] = gazeLogger.fixationData.environment.ToString(specifier,culture);
            logData[1] = gazeLogger.fixationData.target.ToString(specifier,culture);
            logData[2] = gazeLogger.fixationData.hudSymbology.ToString(specifier,culture);
            logData[3] = gazeLogger.fixationData.hudText.ToString(specifier,culture);
            logData[4] = gazeLogger.fixationData.conformalSymbology.ToString(specifier,culture);
            logData[5] = gazeLogger.fixationData.insideCar.ToString(specifier,culture);
            logData[6] = gazeLogger.fixationData.leftMirror.ToString(specifier,culture);
            logData[7] = gazeLogger.fixationData.rightMirror.ToString(specifier,culture);
            logData[8] = gazeLogger.fixationData.rearMirror.ToString(specifier,culture);
            logData[9] = gazeLogger.fixationData.unknown.ToString(specifier,culture);

            Log(logData, file);
            file.Flush();
            file.Close();
        }
    }
    private void SaveVehicleData()
    {
        string[] columns = { "Time", "Frame", "Speed", "DistanceToOptimalPath", "Position", "Rotation", "SteeringInput", "UpcomingOperation", "ThrottleInput", "BrakeInput","TrackProgress" };
        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, vehicleDataFileName);

        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);

            foreach (VehicleDataPoint dataPoint in vehicleData)
            {
                logData[0] = dataPoint.time.ToString(specifier,culture);
                logData[1] = dataPoint.frame.ToString();
                logData[2] = dataPoint.speed.ToString(specifier,culture);
                logData[3] = dataPoint.distanceToOptimalPath.ToString(specifier,culture);
                logData[4] = dataPoint.position;
                logData[5] = dataPoint.rotation;
                logData[6] = dataPoint.steerInput.ToString(specifier,culture);
                logData[7] = dataPoint.upcomingOperation;

                logData[8] = dataPoint.throttleInput.ToString(specifier,culture);
                logData[9] = dataPoint.brakeInput.ToString(specifier,culture);
                logData[10] = dataPoint.trackProgression;

                Log(logData, file);
            }
            file.Flush();
            file.Close();
        }
    }
    private void SaveTargetDetectionData()
    {
        string[] columns = { "Time", "Frame", "AlarmType", "ReactionTime", "TargetID", "TargetDifficulty", "totalfixationTime", "FirstFixationTime", "DetectionTime"};
        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, targetDetectionDataFileName);
        
        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);
            
            foreach (TargetAlarm alarm in targetDetectionData)
            {
                logData[0] = alarm.time.ToString(specifier,culture);
                logData[1] = alarm.frame.ToString();
                logData[2] = alarm.alarmType.ToString();
                logData[3] = alarm.reactionTime.ToString(specifier,culture);
                logData[4] = alarm.targetID;
                
                logData[5] = alarm.targetDifficulty.ToString();
                logData[6] = alarm.totalFixationTime.ToString(specifier,culture);
                logData[7] = alarm.firstFixationTime.ToString(specifier, culture);
                logData[8] = alarm.detectionTime.ToString(specifier, culture);

                Log(logData, file);

            }
            file.Flush();
            file.Close();
        }
    }
    private void SaveNavigationData()
    {
        string[] columns = { "NavigationLine" };
        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, navigationLineFileName);

        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);

            foreach (Vector3 point in totalNavigationLine)
            {
                logData[0] = point.ToString("F3");
                Log(logData, file);
            }
            file.Flush();
            file.Close();
        }
    }
    private void SaveTargetData()
    {
        string[] columns = { "ID", "Detected", "ReactionTime", "TotalFixationTime", "FirstFixationTime",
                            "DetectionTime", "Difficulty","UpcomingTurn","Position", "RoadSide", "WaypointID",
                            "Transparency", "DetectionDistance", "PositionNumber", "Size" };
        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, generalTargetInfo);
        
        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);
            
            foreach(TargetData targetInfo in targetData)
            {
                logData[0] = targetInfo.ID;
                logData[1] = targetInfo.isDetected.ToString();
                logData[2] = targetInfo.reactionTime.ToString(specifier,culture);
                logData[3] = targetInfo.totalFixationTime.ToString(specifier,culture);
                logData[4] = targetInfo.firstFixationTime.ToString(specifier,culture);
                logData[5] = targetInfo.detectionTime.ToString(specifier, culture);
                logData[6] = targetInfo.difficulty.ToString();
                logData[7] = targetInfo.upcomingTurn.ToString();
                logData[8] = targetInfo.position.ToString("F3");
                logData[9] = targetInfo.side.ToString();
                logData[10] = targetInfo.waypointID.ToString();
                logData[11] = targetInfo.transparency.ToString(specifier,culture);
                logData[12] = targetInfo.detectionDistance.ToString(specifier, culture);
                logData[13] = targetInfo.positionNumber.ToString(specifier, culture);
                logData[14] = targetInfo.size.ToString(specifier, culture);
                Log(logData, file);
            }
            file.Flush();
            file.Close();
        }
    }
    void SaveExperimentInfo()
    {
        string[] columns = { "SubjectID", "SubjectAge", "SubjectSex","TotalTargets", "LeftTarget", "RightTargets","TargetDifficulty", "DriverViewToSteeringWHeel", "RelativePositionDriverView", "RelativeRotationDriverView", "ExperimentName",
                              "NavigationType", "Transparency", "TotalExperimentTime", "LeftTurns", "RightTurns"};
        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, generalExperimentInfo);
        
        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);

            //General subject info
            logData[0] = mainManager.SubjectID;
            logData[1] = mainManager.Age;
            logData[2] = mainManager.Sex;

            //General target info
            logData[3] = TotalTargets().ToString();
            logData[4] = LeftTargets().ToString();
            logData[5] = RightTargets().ToString();
            logData[6] = experimentManager.experimentSettings.targetDifficulty.ToString();

            Vector3 driverViewToSteeringWheel = new Vector3(mainManager.DriverViewXToSteeringWheel, mainManager.DriverViewYToSteeringWheel, mainManager.DriverViewZToSteeringWheel);

            logData[7] = driverViewToSteeringWheel.ToString("F3");

            //Info on the driver view
            Vector3 relativePosition = experimentManager.driverView.position - car.transform.position;
            Quaternion relativeRotation = Quaternion.Inverse(car.transform.rotation) * experimentManager.driverView.rotation;
            logData[8] = relativePosition.ToString("F3");
            logData[9] = relativeRotation.eulerAngles.ToString("F3");

            //Experiment inputs
            logData[10] = experimentManager.experimentSettings.name;
            logData[11] = experimentManager.experimentSettings.navigationType.ToString();
            logData[12] = experimentManager.experimentSettings.transparency.ToString();
            logData[13] = experimentManager.experimentSettings.experimentTime.ToString(specifier,culture);

            logData[14] = experimentManager.experimentSettings.LeftTurns().ToString();
            logData[15] = experimentManager.experimentSettings.RightTurns().ToString();
            //Log data and close file
            Log(logData, file);
            file.Flush();
            file.Close();
        }
    }
    void Log(string[] values, StreamWriter file)
    {
        string line = "";
        if(values == null) { Debug.LogError("Got null values in Log()..."); return; }
        for (int i = 0; i < values.Length; ++i)
        {
            values[i] = values[i].Replace("\r", "").Replace("\n", ""); // Remove new lines so they don't break csv
            line += values[i] + (i == (values.Length - 1) ? "" : ";"); // Do not add semicolon to last data string
        }
        file.WriteLine(line);
    }
    private string SaveFolder()
    {
        string saveFolder = string.Join("/", mainManager.SubjectDataFolder, experimentManager.experimentSettings.name + DateTime.Now.ToString("_HH-mm-ss"));
        
        Directory.CreateDirectory(saveFolder);
        Debug.Log($"Created data folder for {experimentManager.experimentSettings.name}: {saveFolder}...");

        return saveFolder;
    }
    private void AddVehicleData()
    {
        if (vehicleData == null)
        {
            GetComponent<DebugCaller>().DebugThis("DataLogger error", "ERROR: Vehicle data was corrupted...");
            return;
            //StartNewMeasurement();
        }
        VehicleDataPoint dataPoint = new VehicleDataPoint();
        dataPoint.time = experimentManager.experimentSettings.experimentTime;
        dataPoint.frame = Time.frameCount;

        dataPoint.distanceToOptimalPath = GetDistanceToOptimalPath(car.transform.position);
        
        //GetComponent<DebugCaller>().DebugThis("optimal distance", dataPoint.distanceToOptimalPath);

        dataPoint.position = car.transform.position.ToString("F3");
        dataPoint.rotation = car.transform.rotation.eulerAngles.ToString("F3");
        dataPoint.steerInput = Input.GetAxis(steerInputAxis);
        dataPoint.speed = car.GetComponent<Rigidbody>().velocity.magnitude;
        dataPoint.upcomingOperation = car.waypoint.turn.ToString();

        ///extra ///
        dataPoint.throttleInput = Input.GetAxis(gasInputAxis);
        dataPoint.brakeInput = car.GetComponent<VehiclePhysics.VPVehicleController>().brakes.maxBrakeTorque;
        dataPoint.trackProgression = car.GetComponent<SpeedController>().GetTrackProgression().ToString();

        //Debug.Log($"Steerinput: {dataPoint.steerInput}, throttle: {dataPoint.throttleInput}, brake: {dataPoint.brakeInput}...");
        vehicleData.Add(dataPoint);
    }
    private float GetDistanceToOptimalPath(Vector3 car_position)
    {
        
        //(1) Checks if current line segment is still the closest one 
        //(2)Then calculates the distance to the line they span
        
        if(currentNavigationLine.Length < 4) { Debug.Log($"NavigationLine consisting of only {currentNavigationLine.Length} points..."); return 1000f; }
        if(indexClosestPoint >= currentNavigationLine.Length || indexClosestPoint < 0) { Debug.Log($"Got invalid index {indexClosestPoint}, size currentNavigationLine:{currentNavigationLine.Length}.... "); return 1000f; }

        float current_distance, forward_distance, backward_distance;

        current_distance = forward_distance = backward_distance = 10000f;


        if (indexClosestPoint + 1 < currentNavigationLine.Length) { current_distance = DistanceClosestPointOnLineSegment(currentNavigationLine[indexClosestPoint], currentNavigationLine[indexClosestPoint + 1], car_position); }
        if (indexClosestPoint + 2 < currentNavigationLine.Length) { forward_distance = DistanceClosestPointOnLineSegment(currentNavigationLine[indexClosestPoint + 1], currentNavigationLine[indexClosestPoint + 2], car_position); }
        if (indexClosestPoint - 1 >= 0) { backward_distance = DistanceClosestPointOnLineSegment(currentNavigationLine[indexClosestPoint - 1], currentNavigationLine[indexClosestPoint], car_position); }


        int indexLowestValue = GetIndexOfLowestValue(new float[3] { backward_distance, current_distance, forward_distance });
        
        int step;
        if (indexLowestValue == 0) { step = -1; current_distance = backward_distance; }
        else if (indexLowestValue == 2) { step = 1; current_distance = forward_distance; }
        else { return current_distance; }


        float next_distance; int count=0;
        while (true)
        {
            count++; if (count > 50) { Debug.LogError("Could not find minimum distance, max count reached..."); return 1000f; }

            int nextIndex = indexClosestPoint + step;

            if (nextIndex < 0 || nextIndex >= currentNavigationLine.Length) { Debug.LogError("Could not find minimum distance..."); return 1000f; }

            next_distance = DistanceClosestPointOnLineSegment(currentNavigationLine[indexClosestPoint], currentNavigationLine[nextIndex], car_position);

            indexClosestPoint+=step;

            if (current_distance <= next_distance) { return current_distance; }
        }

    }
    public int GetIndexOfLowestValue(float[] arr)
    {
        float value = float.PositiveInfinity;
        int index = -1;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] < value)
            {
                index = i;
                value = arr[i];
            }
        }
        return index;
    }
    float DistanceClosestPointOnLineSegment(Vector3 segmentStart, Vector3 segmentEnd, Vector3 point)
    {
        // Shift the problem to the origin to simplify the math.    
        var wander = point - segmentStart;
        var span = segmentEnd - segmentStart;

        // Compute how far along the line is the closest approach to our point.
        float t = Vector3.Dot(wander, span) / span.sqrMagnitude;

        // Restrict this point to within the line segment from start to end.
        t = Mathf.Clamp01(t);

        // Return this point.
        return Vector3.Distance(segmentStart + t * span, point);
    }
    public void AddFalseAlarm()
    {
        if (!logging) { return; }
        TargetAlarm alarm = new TargetAlarm();
        alarm.time = experimentManager.experimentSettings.experimentTime;
        alarm.frame = Time.frameCount;
        alarm.alarmType = false;

        targetDetectionData.Add(alarm);
        Debug.Log("Added false alarm...");
    }
    public void AddTrueAlarm(Target target)
    {
        if (!logging) { return; }
        TargetAlarm alarm = new TargetAlarm();

        alarm.time = experimentManager.experimentSettings.experimentTime;
        alarm.frame = Time.frameCount;
        alarm.alarmType = true;
        alarm.targetID = target.GetID();
        alarm.reactionTime = alarm.time - target.startTimeVisible;

        //The difficulty names always end with a number ranging from 1-6
        alarm.targetDifficulty = target.GetTargetDifficulty().ToString();
        targetDetectionData.Add(alarm);

        //Debug.Log($"Added true alarm for {target.GetID()}, reaction time: {Math.Round(alarm.reactionTime, 2)}s ...");
    }
    private Vector3[] AppendArrays(Vector3[] arr1, Vector3[] arr2)
    {
        Vector3[] arrOut = new Vector3[arr1.Length + arr2.Length];
        System.Array.Copy(arr1, arrOut, arr1.Length);
        System.Array.Copy(arr2, 0, arrOut, arr1.Length, arr2.Length);
        return arrOut;
    }
    private int TotalTargets()
    {
        return targetData.Count();
    }
    private int RightTargets()
    {
        return targetData.Where(s => s.side == Side.Right).Count();
    }
    private int LeftTargets()
    {
        return targetData.Where(s => s.side == Side.Left).Count();
    }
}
public class VehicleDataPoint
{
    public float time;
    public int frame;

    public float speed;
    public float distanceToOptimalPath;
    public string position;
    public string rotation;
    public float steerInput;

    public string upcomingOperation;

    public float throttleInput;
    public float brakeInput;
    public string trackProgression;

}

public enum Irregularity
{
    WrongTurn,
    OutOfBounce,
}
public class DrivingIrregularities
{
    public WaypointStruct waypoint;
    public Irregularity irregularity;
    public float time;
    public Vector3 carPosition;

    public DrivingIrregularities(WaypointStruct _waypoint, Irregularity _irregularity, float _time, Vector3 _carPos)
    {
        waypoint = _waypoint;
        time = _time;
        irregularity = _irregularity;
        carPosition = _carPos;
    }
}
public class TargetData
{
    public string ID;
    public bool isDetected;
    public float reactionTime;
    public float totalFixationTime;
    public float firstFixationTime;
    public float detectionTime;
    public TargetDifficulty difficulty;
    public TurnType upcomingTurn;
    public Vector3 position;
    public Side side;
    public int waypointID;
    public float transparency;
    public float detectionDistance;
    public int positionNumber;
    public float size;
    public TargetData(Target target)
    {
        ID = target.GetID();
        isDetected = target.IsDetected();
        reactionTime = target.reactionTime;
        totalFixationTime = target.totalFixationTime;
        firstFixationTime = target.firstFixationTime;
        detectionTime = target.detectionTime;
        detectionDistance = target.detectionDistance;
        difficulty = target.difficulty;
        upcomingTurn = target.waypoint.turn;
        position = target.transform.position;
        side = target.side;
        waypointID = target.waypoint.waypointID;
        transparency = target.transparency; 
        positionNumber = target.positionNumber;
        size = target.transform.localScale.x;
    }

}
public class TargetAlarm
{
    public float time;
    public int frame;

    public bool alarmType;
    public float reactionTime;
    public string targetID;
    public string targetDifficulty;
    public float detectionTime;
    public float firstFixationTime;
    public float totalFixationTime;

    public TargetAlarm()
    {
        targetID = "-";
        reactionTime = 0f;
        targetDifficulty = "-";
        detectionTime = 0f;
        totalFixationTime = 0f;
        firstFixationTime = 0f;
    }
}


public class NavigationLine
{
    public Vector3[] navigationLine;
}
