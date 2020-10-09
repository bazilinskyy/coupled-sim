using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using UnityEngine;
using Varjo;
using System.Linq;

public class XMLManager : MonoBehaviour
{
    //singleton pattern
    public static XMLManager ins;

    private string vehicleDataFileName = "vehicleData.csv";
    private string navigationLineFileName = "navigationLine.csv";
    private string targetDetectionDataFileName = "targetDetectionData.csv";
    private string generalTargetInfo = "generalTargetInfo.csv";
    private string generalExperimentInfo = "GeneralExperimentInfo.csv";
    private string dataFolder = "Data";
    //our car
    private GameObject car;
    
    //Current Navigation
    private Transform navigation;
    private NavigationHelper navigationHelper;
    private Vector3[] navigationLine;
    private int indexClosestPoint;//Used for calculating the distance to optimal navigation path (i.e., centre of raod)

    //Experiment manager;
    private ExperimentManager experimentManager;

    private GameState gameState;

    private MyGazeLogger myGazeLogger;
    private string subjectName;
   
    //list of items
    private List<VehicleDataPoint> vehicleData;
    private List<TargetAlarm> targetDetectionData;
    
    private string saveFolder;
    private string steerInputAxis;
    [HideInInspector]
    public bool savedData;

    private void OnApplicationQuit()
    {

      if (experimentManager.saveData && !savedData) { SaveData(); }

    }
    private void Awake()
    {
        ins = this;

        experimentManager = GetComponent<ExperimentManager>();
        gameState = experimentManager.gameState;
        myGazeLogger = GetComponent<MyGazeLogger>();
        myGazeLogger.experimentManager = experimentManager;
        myGazeLogger.startAutomatically = false;
        myGazeLogger.useCustomLogPath = true;
    }
    private void Update()
    {
        if (gameState.isExperiment() && experimentManager.saveData)
        {
            AddVehicleData();
        }
    }
    public void SetAllInputs(GameObject _car, Transform _navigation, string _subjectName)
    {
        car = _car;
        
        //set car inputs
        List<string> inputs = experimentManager.GetCarControlInput();
        steerInputAxis = inputs[0];

        SetNavigation(_navigation);

        string dateTime = System.DateTime.Now.ToString("MM-dd_HH-mm");
        if (_subjectName == null || _subjectName == "") { _subjectName = "JohnDoe"; }
        subjectName = dateTime + "-" + _subjectName;

        //Set inputs of GazeLogger
        myGazeLogger.cam = experimentManager.CameraTransform();
    }
    public void SaveData()
    {
        Debug.Log($"Saving all data to {saveFolder}...");
        if (experimentManager.camType != MyCameraType.Normal) { myGazeLogger.StopLogging(); }

        SaveVehicleData();
        SaveTargetDetectionData();
        SaveTargetInfoData();
        SaveNavigationData();
        SaveGeneralExperimentInfo();

        savedData = true;
    }
    public void SetNavigation(Transform _navigation)
    {
        navigation = _navigation;
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        navigationLine = navigationHelper.GetNavigationLine();
        indexClosestPoint = 0;
    }
    private void SaveVehicleData()
    {
        Debug.Log("Saving vehicle data...");
        string[] columns = { "Time", "Frame", "Speed", "DistanceToOptimalPath", "Position", "Rotation", "SteeringInput" };
        string[] logData = new string[7];
        string filePath = string.Join("/", saveFolder, vehicleDataFileName);

        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);

            foreach (VehicleDataPoint dataPoint in vehicleData)
            {
                logData[0] = dataPoint.time.ToString();
                logData[1] = dataPoint.frame.ToString();
                logData[2] = dataPoint.speed.ToString();
                logData[3] = dataPoint.distanceToOptimalPath.ToString();
                logData[4] = dataPoint.position;
                logData[5] = dataPoint.rotation;
                logData[6] = dataPoint.steerInput.ToString();

                Log(logData, file);
            }
            file.Close();
        }
    }
    private void SaveTargetDetectionData()
    {
        Debug.Log("Saving alarms...");
        string[] columns = { "Time", "Frame", "AlarmType", "ReactionTime", "TargetID", "TargetDifficulty"};
        string[] logData = new string[6];
        string filePath = string.Join("/", saveFolder, targetDetectionDataFileName);
        
        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);
            
            foreach (TargetAlarm alarm in targetDetectionData)
            {
                logData[0] = alarm.time.ToString();
                logData[1] = alarm.frame.ToString();
                logData[2] = alarm.AlarmType.ToString();
                logData[3] = alarm.reactionTime.ToString();
                logData[4] = alarm.targetID;
                logData[5] = alarm.targetDifficulty.ToString();

                Log(logData, file);
            }
            file.Close();
        }
    }
    private void SaveNavigationData()
    {
        Debug.Log("Saving navigation info...");
        string[] columns = { "NavigationLine" };
        string[] logData = new string[1];
        Vector3[] navigationLine = navigationHelper.GetNavigationLine();
        string filePath = string.Join("/", saveFolder, navigationLineFileName);

        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);
            
            foreach (Vector3 point in navigationLine)
            {
                logData[0] = point.ToString("F3");
                Log(logData, file);
            }
            file.Close();
        }
    }
    private void SaveTargetInfoData()
    {
        Debug.Log("Saving target info...");
        string[] columns = { "ID", "Detected", "ReactionTime", "FixationTime", "Difficulty","Side","Position" };
        string filePath = string.Join("/", saveFolder, generalTargetInfo);
        
        List<Target> targets = navigationHelper.GetAllTargets();

        using (StreamWriter file = new StreamWriter(filePath))
        {
            Log(columns, file);
            
            foreach(Target target in targets)
            {
                string[] logData = new string[7];
                logData[0] = target.GetID();
                logData[1] = target.detected.ToString();
                logData[2] = target.reactionTime.ToString();
                logData[3] = target.fixationTime.ToString();
                logData[4] = target.difficulty.ToString().Last().ToString();
                logData[5] = target.GetRoadSide().ToString();
                logData[6] = target.transform.position.ToString("F3");
                Log(logData, file);
            }
            file.Close();
        }
    }
    void SaveGeneralExperimentInfo()
    {
        Debug.Log("Saving general experiment info...");
        string[] columns = { "Total Targets", "LeftTarget", "RightTargets", TargetDifficulty.easy_6.ToString(), TargetDifficulty.easy_5.ToString(), TargetDifficulty.medium_4.ToString(),
                            TargetDifficulty.medium_3.ToString(), TargetDifficulty.hard_2.ToString(), TargetDifficulty.hard_1.ToString(), "RelativePositionDriverView", "RelativeRotationDriverView",
                              "Navigation", "Condition", "Transparency", "TotalExperimentTime"};
        string[] logData = new string[15];
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
            logData[9] = relativePosition.ToString("F3");
            logData[10] = relativeRotation.eulerAngles.ToString("F3");

            //Experiment inputs
            logData[11] = experimentManager.activeExperiment.navigation.name;
            logData[12] = experimentManager.activeExperiment.navigationType.ToString();
            logData[13] = experimentManager.activeExperiment.transparency.ToString();
            logData[14] = experimentManager.activeExperiment.experimentTime.ToString();

            //Log data and close file
            Log(logData, file);
            file.Close();
        }
    }
    void Log(string[] values, StreamWriter file)
    {
        string line = "";
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
        savedData = false;
        saveFolder = SaveFolder();

        vehicleData = new List<VehicleDataPoint>();
        targetDetectionData = new List<TargetAlarm>();

        if (experimentManager.camType != MyCameraType.Normal)
        {
            myGazeLogger.customLogPath = saveFolder + "/";
            myGazeLogger.StartLogging();
        }
    }
    private string SaveFolder()
    {
        //Save folder will be .../unityproject/Data/subjectName-date/subjectName/navigationName

        string[] assetsFolderArray = Application.dataPath.Split('/'); //Gives .../unityproject/assest

        //emmit unityfolder/assets and keep root folder

        string[] baseFolderArray = new string[assetsFolderArray.Length - 2];
        for (int i = 0; i < (assetsFolderArray.Length - 2); i++) { baseFolderArray[i] = assetsFolderArray[i]; }

        string baseFolder = string.Join("/", baseFolderArray);
        string saveFolder = string.Join("/", baseFolder, dataFolder, subjectName, navigation.name + DateTime.Now.ToString("_HH-mm-ss"));
        Directory.CreateDirectory(saveFolder);
        

        return saveFolder;
    }
    private void AddVehicleData()
    {
        if (vehicleData == null)
        {
            Debug.Log("ERROR: Vehicle data was corrupted... Re-started measurement...");
            StartNewMeasurement();
        }
        VehicleDataPoint dataPoint = new VehicleDataPoint();
        dataPoint.time = experimentManager.activeExperiment.experimentTime;
        dataPoint.frame = Time.frameCount;

        dataPoint.distanceToOptimalPath = GetDistanceToOptimalPath(car.transform.position);
        dataPoint.position = car.gameObject.transform.position.ToString("F3");
        dataPoint.rotation = car.gameObject.transform.rotation.eulerAngles.ToString("F3");
        dataPoint.steerInput = Input.GetAxis(steerInputAxis);
        dataPoint.speed = car.GetComponent<Rigidbody>().velocity.magnitude;

        vehicleData.Add(dataPoint);
    }
    private float GetDistanceToOptimalPath(Vector3 car_position)
    {
        
        //(1) Checks if current line segment is still the closest one 
        //(2)Then calculates the distance to the line they span
        
        float current_distance,next_distance, former_distance;

        current_distance = DistanceClosestPointOnLineSegment(navigationLine[indexClosestPoint], navigationLine[indexClosestPoint + 1], car_position);

        while (true)
        {
            //check forward
            if (indexClosestPoint + 2 >= navigationLine.Length) { break; }

            next_distance = DistanceClosestPointOnLineSegment(navigationLine[indexClosestPoint + 1], navigationLine[indexClosestPoint + 2], car_position);
            
            if (next_distance < current_distance)
            {
                //If closer we  update distance and index and redo the loop
                current_distance = next_distance;
                indexClosestPoint++;
                continue;
            }

            //check backward
            if (indexClosestPoint - 2 <= 0) { break; }

            
            former_distance = DistanceClosestPointOnLineSegment(navigationLine[indexClosestPoint - 2], navigationLine[indexClosestPoint  - 1], car_position);
           
            if (former_distance < current_distance) { 
                current_distance = former_distance;
                indexClosestPoint--;
            }

            if (former_distance > current_distance && next_distance > current_distance) { break; }
        }

        return current_distance;
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
        TargetAlarm alarm = new TargetAlarm();
        alarm.time = experimentManager.activeExperiment.experimentTime;
        alarm.frame = Time.frameCount;
        alarm.AlarmType = false;
        targetDetectionData.Add(alarm);
        Debug.Log("Added false alarm...");
    }
    public void AddTrueAlarm(Target target)
    {

        TargetAlarm alarm = new TargetAlarm();

        alarm.time = experimentManager.activeExperiment.experimentTime;
        alarm.frame = Time.frameCount;
        alarm.AlarmType = true;
        alarm.targetID = target.GetID();
        alarm.reactionTime = alarm.time - target.startTimeVisible;

        //The difficulty names always end with a number ranging from 1-6
        alarm.targetDifficulty = int.Parse(target.GetTargetDifficulty().ToString().Last().ToString());
        targetDetectionData.Add(alarm);

        Debug.Log($"Added true alarm for {target.name}, reaction time: {Math.Round(alarm.reactionTime, 2)}s ...");
    }
/*
    void SaveThis<T>(string fileName, object data)
    {
        //check if data makes sense
        Type dataType = data.GetType();
        if (!dataType.Equals(typeof(T))) { throw new Exception($"ERROR: data type and given type done match {dataType.FullName} != {typeof(T).FullName}..."); }

        XmlSerializer serializer = new XmlSerializer(typeof(T));
        //overwrites mydata.xml
        string filePath = string.Join("/", saveFolder, fileName);

        FileStream stream = new FileStream(filePath, FileMode.Create);
        serializer.Serialize(stream, data);
        stream.Close();
    }*/
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
}

public class TargetAlarm
{
    public float time;
    public int frame;

    public bool AlarmType;
    public float reactionTime;
    public string targetID;
    public int targetDifficulty;
}
/*
public class TargetInfo
{
    public bool detected;
    public float reactionTime;
    public float fixationTime;

    public TargetDifficulty difficulty;
    public Side side;
    public string targetName;
    public string position;
}
public class TargetDetectionSummary
{
    public int totalTargets;
    public int totalMisses;
    public int totalHits;
    public float missRate;
    public float hitRate;
    public float detectionRate;
}*/
public class NavigationLine
{
    public Vector3[] navigationLine;
}
