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
[RequireComponent(typeof(MyGazeLogger))]
public class DataLogger : MonoBehaviour
{
    private const string vehicleDataFileName = "vehicleData.csv";
    private const string navigationLineFileName = "navigationLine.csv";
    private const string targetDetectionDataFileName = "targetDetectionData.csv";
    private const string generalTargetInfo = "generalTargetInfo.csv";
    private const string generalExperimentInfo = "GeneralExperimentInfo.csv";
    private const string fixationFileName = "fixationData.csv";
    //our car
    public GameObject car;
    private newNavigator carNavigator;
    //Current Navigation
    private NavigationHelper navigationHelper;
    public Vector3[] currentNavigationLine = new Vector3[0];
    public Vector3[] totalNavigationLine;
    private int indexClosestPoint = 0;//Used for calculating the distance to optimal navigation path (i.e., centre of raod)

    //Experiment manager;
    private newExperimentManager experimentManager;

    private MyGazeLogger myGazeLogger;
   
    //list of items
    private List<VehicleDataPoint> vehicleData;
    private List<TargetAlarm> targetDetectionData;
    public List<TargetInfo> targetInfoData;

    private MainManager mainManager;

    private string saveFolder;
    private string steerInputAxis;
    private string gasInputAxis;
    private string brakeInputAxis;
    [HideInInspector]
    public bool savedData = false;
    public bool logging;

    private CultureInfo culture = CultureInfo.CreateSpecificCulture("eu-ES");
    // Use standard numeric format specifiers.
    private string specifier = "G";
    
    private void OnApplicationQuit() { if (mainManager.saveData && !savedData) { SaveData(); } }

    public void StartUp()
    {
        
        Debug.Log("Started data logger...");
        mainManager = MyUtils.GetMainManager();

        experimentManager = GetComponent<newExperimentManager>();

        List<string> inputs = experimentManager.GetCarControlInput();
        steerInputAxis = inputs[0];
        gasInputAxis = inputs[1];
        brakeInputAxis = inputs[2];

        carNavigator = car.GetComponent<newNavigator>();

        StartNewMeasurement();

    }
    
    public void LogTargets(List<Target> targets)
    {
        foreach(Target target in targets)
        {
            TargetInfo targetInfo = new TargetInfo(target);
            targetInfoData.Add(targetInfo);
        }
    }
    public void TookWrongTurn() { Debug.Log("Logging wrong turn..."); }
    private void Update()
    {
        if (logging) { AddVehicleData(); }
    }
    public void SaveData()
    {
        if (!logging) { return; }
        Debug.Log($"Saving all data to {saveFolder}...");
        if (mainManager.camType != MyCameraType.Normal) { SaveFixationData(); myGazeLogger.StopLogging();  }

        SaveVehicleData();
        SaveTargetDetectionData();
        SaveTargetInfoData();
        SaveNavigationData();
        //SaveGeneralExperimentInfo();

        logging = false;
        savedData = true;
    }

    public void AddCurrentNavigationLine(Vector3[] points)
    {
        Debug.Log($"Got new navigation points: {points.Length}...");
        indexClosestPoint = 0;
        currentNavigationLine = points;
        totalNavigationLine = AppendArrays(totalNavigationLine, points);
    }
    private void SaveFixationData()
    {
        Debug.Log("Saving fixation data...");
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

            logData[0] = myGazeLogger.fixationData.world.ToString(specifier,culture);
            logData[1] = myGazeLogger.fixationData.target.ToString(specifier,culture);
            logData[2] = myGazeLogger.fixationData.hudSymbology.ToString(specifier,culture);
            logData[3] = myGazeLogger.fixationData.hudText.ToString(specifier,culture);
            logData[4] = myGazeLogger.fixationData.conformalSymbology.ToString(specifier,culture);
            logData[5] = myGazeLogger.fixationData.insideCar.ToString(specifier,culture);
            logData[6] = myGazeLogger.fixationData.leftMirror.ToString(specifier,culture);
            logData[7] = myGazeLogger.fixationData.rightMirror.ToString(specifier,culture);
            logData[8] = myGazeLogger.fixationData.rearMirror.ToString(specifier,culture);
            logData[9] = myGazeLogger.fixationData.unknown.ToString(specifier,culture);

            Log(logData, file);
            file.Flush();
            file.Close();
        }
    }
    private void SaveVehicleData()
    {
        Debug.Log("Saving vehicle data...");
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
        Debug.Log("Saving alarms...");
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
        Debug.Log("Saving navigation info...");
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
    private void SaveTargetInfoData()
    {
        Debug.Log("Saving target info...");
        string[] columns = { "ID", "Detected", "ReactionTime", "TotalFixationTime", "FirstFixationTime", "DetectionTime", "Difficulty","UpcomingTurn","Position" };
        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, generalTargetInfo);
        

        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);
            
            foreach(TargetInfo targetInfo in targetInfoData)
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
                
                Log(logData, file);
            }
            file.Flush();
            file.Close();
        }
    }
    void SaveGeneralExperimentInfo()
    {
        Debug.Log("Saving general experiment info...");
        string[] columns = { "Total Targets", "LeftTarget", "RightTargets", TargetDifficulty.easy.ToString(), TargetDifficulty.medium.ToString(), TargetDifficulty.hard.ToString(), "RelativePositionDriverView", "RelativeRotationDriverView",
                              "Navigation", "Condition", "Transparency", "TotalExperimentTime", "LeftTurns","RightTurns"};
        string[] logData = new string[columns.Length];
        string filePath = string.Join("/", saveFolder, generalExperimentInfo);
        TargetCountInfo targetCountInfo = navigationHelper.targetCountInfo;
        List<DifficultyCount> targetDifficulty = navigationHelper.targetDifficultyList;
        
        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);

            //General target info
            logData[0] = targetCountInfo.totalTargets.ToString();
            logData[1] = targetCountInfo.LeftPosition.ToString();
            logData[2] = targetCountInfo.rightPosition.ToString();

            int index = 3;
            foreach (DifficultyCount difficltyCount in targetDifficulty)
            { 
                logData[index] = difficltyCount.count.ToString();
                index++;
            }

            //Info on the driver view
            Vector3 relativePosition = experimentManager.driverView.position - car.transform.position;
            Quaternion relativeRotation = Quaternion.Inverse(car.transform.rotation) * experimentManager.driverView.rotation;
            logData[6] = relativePosition.ToString("F3");
            logData[7] = relativeRotation.eulerAngles.ToString("F3");

            //Experiment inputs
            logData[8] = experimentManager.activeExperiment.navigation.name;
            logData[9] = experimentManager.activeExperiment.navigationType.ToString();
            logData[10] = experimentManager.activeExperiment.transparency.ToString();
            logData[11] = experimentManager.activeExperiment.experimentTime.ToString(specifier,culture);

            logData[12] = experimentManager.activeExperiment.navigationHelper.leftTurns.ToString();
            logData[13] = experimentManager.activeExperiment.navigationHelper.rightTurns.ToString();
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
    public void StartNewMeasurement()
    {
        Debug.Log($"Starting new measurement...");

        logging = true;
        savedData = false;
        saveFolder = SaveFolder();

        vehicleData = new List<VehicleDataPoint>();
        targetDetectionData = new List<TargetAlarm>();
        targetInfoData = new List<TargetInfo>();

        if (mainManager.camType != MyCameraType.Normal)
        {
            myGazeLogger.customLogPath = saveFolder + "/";
            myGazeLogger.fixationData = new Fixation();
            if (myGazeLogger.IsLogging()) { myGazeLogger.RestartLogging(); }
            else { myGazeLogger.StartLogging(); }
        }
    }
    private string SaveFolder()
    {
        //Save folder will be .../unityproject/Data/subjectName-date/subjectName/navigationName

        /*string[] assetsFolderArray = Application.dataPath.Split('/'); //Gives .../unityproject/assest

        //emmit unityfolder/assets and keep root folder

        string[] baseFolderArray = new string[assetsFolderArray.Length - 2];
        for (int i = 0; i < (assetsFolderArray.Length - 2); i++) { baseFolderArray[i] = assetsFolderArray[i]; }

        string baseFolder = string.Join("/", baseFolderArray);*/
        Debug.Log(mainManager.subjectDataFolder);
        Debug.Log(mainManager.GetExperimentNumber());
        Debug.Log(mainManager.gameObject.name);

        string saveFolder = string.Join("/", mainManager.subjectDataFolder, "Experiment-" + mainManager.GetExperimentNumber().ToString() + DateTime.Now.ToString("_HH-mm-ss"));
        Debug.Log($"Creaiting savefolder: {saveFolder}...");
        Directory.CreateDirectory(saveFolder);
        

        return saveFolder;
    }
    private void AddVehicleData()
    {
        if (vehicleData == null)
        {
            Debug.LogError("ERROR: Vehicle data was corrupted... Re-started measurement...");
            return;
            //StartNewMeasurement();
        }
        VehicleDataPoint dataPoint = new VehicleDataPoint();
        dataPoint.time = experimentManager.activeExperiment.experimentTime;
        dataPoint.frame = Time.frameCount;

        dataPoint.distanceToOptimalPath = GetDistanceToOptimalPath(car.transform.position);
        dataPoint.position = car.transform.position.ToString("F3");
        dataPoint.rotation = car.transform.rotation.eulerAngles.ToString("F3");
        dataPoint.steerInput = Input.GetAxis(steerInputAxis);
        dataPoint.speed = car.GetComponent<Rigidbody>().velocity.magnitude;
        dataPoint.upcomingOperation = carNavigator.target.turn.ToString();

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

        string direction;

        current_distance = forward_distance = backward_distance = 10000f;


        if (indexClosestPoint + 1 < currentNavigationLine.Length) { current_distance = DistanceClosestPointOnLineSegment(currentNavigationLine[indexClosestPoint], currentNavigationLine[indexClosestPoint + 1], car_position); }
        if (indexClosestPoint + 2 < currentNavigationLine.Length) { forward_distance = DistanceClosestPointOnLineSegment(currentNavigationLine[indexClosestPoint + 1], currentNavigationLine[indexClosestPoint + 2], car_position); }
        if (indexClosestPoint - 1 >= 0) { backward_distance = DistanceClosestPointOnLineSegment(currentNavigationLine[indexClosestPoint - 1], currentNavigationLine[indexClosestPoint], car_position); }


        int indexLowestValue = GetIndexOfLowestValue(new float[3] { backward_distance, current_distance, forward_distance });
        
        int step;
        if (indexLowestValue == 0) { direction = "backward"; step = -1; current_distance = backward_distance; }
        else if (indexLowestValue == 2) { direction = "forward"; step = 1; current_distance = forward_distance; }
        else { return current_distance; }

        Debug.Log($"Checking in {direction} direction...");

        float next_distance; int count=0;
        while (true)
        {
            count++; if (count > 50) { Debug.LogError("Could not find minimum distance, max count reached..."); return 1000f; }
            indexClosestPoint += step;

            if(indexClosestPoint < 0 || indexClosestPoint >= currentNavigationLine.Length) { Debug.LogError("Could not find minimum distance..."); return 1000f; }

            next_distance = DistanceClosestPointOnLineSegment(currentNavigationLine[indexClosestPoint], currentNavigationLine[indexClosestPoint + step], car_position);

            if(current_distance <= next_distance) { return current_distance; }
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
        alarm.time = experimentManager.activeExperiment.experimentTime;
        alarm.frame = Time.frameCount;
        alarm.alarmType = false;

        targetDetectionData.Add(alarm);
        Debug.Log("Added false alarm...");
    }
    public void AddTrueAlarm(Target target)
    {
        if (!logging) { return; }
        TargetAlarm alarm = new TargetAlarm();

        alarm.time = experimentManager.activeExperiment.experimentTime;
        alarm.frame = Time.frameCount;
        alarm.alarmType = true;
        alarm.targetID = target.GetID();
        alarm.reactionTime = alarm.time - target.startTimeVisible;

        //The difficulty names always end with a number ranging from 1-6
        alarm.targetDifficulty = target.GetTargetDifficulty().ToString();
        targetDetectionData.Add(alarm);

        Debug.Log($"Added true alarm for {target.GetID()}, reaction time: {Math.Round(alarm.reactionTime, 2)}s ...");
    }
    private Vector3[] AppendArrays(Vector3[] arr1, Vector3[] arr2)
    {
        Vector3[] arrOut = new Vector3[arr1.Length + arr2.Length];
        System.Array.Copy(arr1, arrOut, arr1.Length);
        System.Array.Copy(arr2, 0, arrOut, arr1.Length, arr2.Length);
        return arrOut;
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
public class TargetInfo
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

    public TargetInfo(Target target)
    {
        ID = target.GetID();
        isDetected = target.IsDetected();
        reactionTime = target.reactionTime;
        totalFixationTime = target.totalFixationTime;
        firstFixationTime = target.firstFixationTime;
        detectionTime = target.detectionTime;
        difficulty = target.difficulty;
        upcomingTurn = target.waypoint.turn;
        position = target.transform.position;
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
