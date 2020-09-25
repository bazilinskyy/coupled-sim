using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;
using Varjo;
using System.Linq;

public class XMLManager : MonoBehaviour
{

    //singleton pattern
    public static XMLManager ins;

    private string carDataFileName = "carData.xml";
    private string targetDetectionDataFileName = "targetData.xml";
    private string targetDetectionDataSummaryFileName = "targetDataSummary.xml";
    private string navigationSettingsFileName = "navigationSettings.xml";
    private string navigationLineFileName = "navigationLine.xml";
    private string gazeDataFileName = "gazeData.xml";
    private string dataFolder = "Data";
    //our car
    private GameObject car;
    private VehicleBehaviour.WheelVehicle vehicleBehaviour;

    //Current Navigation
    private Transform navigation;
    private NavigationHelper navigationHelper;
    private Vector3[] navigationLine;
    private int indexClosestPoint;//Used for calculating the distance to optimal navigation path (i.e., centre of raod)

    //Experiment manager;
    private ExperimentManager experimentManager;

    private GameState gameState;

    private string subjectName;
   
    //list of items
    private VehicleDataContainer vehicleData;
    private AlarmContainer targetDetectionData;
    private TargetDetectionSummary targetDetectionSummary;
    private GazeContainer gazeData;
    /*
        private void OnApplicationQuit()
        {
            SaveData();
        }*/
    private void Awake()
    {
        ins = this;

        experimentManager = gameObject.GetComponent<ExperimentManager>();
        gameState = experimentManager.gameState;

    }

    private void Update()
    {
        if (gameState.isExperiment() && experimentManager.saveData)
        {
            AddVehicleData();

            if (experimentManager.camType == MyCameraType.Varjo) { AddEyeTrackingData(); }
        }
    }
    public void SetAllInputs(GameObject _car, Transform _navigation, string _subjectName)
    {
        car = _car;
        //vehicleBehaviour = car.GetComponent<VehicleBehaviour.WheelVehicle>();


        SetNavigation(_navigation);

        string dateTime = System.DateTime.Now.ToString("MM-dd_HH-mm");
        if (_subjectName == null || _subjectName == "") { _subjectName = "JohnDoe"; }
        subjectName = dateTime + "-" + _subjectName;
    }
    public void SaveData()
    {
               
        SaveThis<VehicleDataContainer>(carDataFileName, vehicleData);
        
        SaveThis<AlarmContainer>(targetDetectionDataFileName, targetDetectionData);

        SummariseTargetDetectionData();
        SaveThis<TargetDetectionSummary>(targetDetectionDataSummaryFileName, targetDetectionSummary);

        SaveNavigationData();

        SaveThis<GazeContainer>(gazeDataFileName, gazeData);
    }
    public void SetNavigation(Transform _navigation)
    {
        navigation = _navigation;
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        navigationLine = navigationHelper.GetNavigationLine();
        indexClosestPoint = 0;
    }
    private void SaveNavigationData()
    {

        NavigationSettings navigationSettings = new NavigationSettings();
        NavigationLine navigationLine = new NavigationLine();
        
        (navigationLine.navigationLine, navigationSettings.renderVirtualCable, navigationSettings.renderHighlightedRoad, navigationSettings.renderHUD, navigationSettings.transparency) =  navigationHelper.GetNavigationInformation();
        navigationSettings.NavigationTime = experimentManager.activeExperiment.experimentTime;

        SaveThis<NavigationSettings>(navigationSettingsFileName, navigationSettings);

        SaveThis<NavigationLine>(navigationLineFileName, navigationLine);

    }
    void SaveThis<T>(string fileName, object data)
    {
        //check if data makes sense
        Type dataType = data.GetType();
        if (!dataType.Equals(typeof(T))) { throw new Exception($"ERROR: data type and given type done match {dataType.FullName} != {typeof(T).FullName}..."); }

        XmlSerializer serializer = new XmlSerializer(typeof(T));
        //overwrites mydata.xml
        string filePath = string.Join("/", saveFolder(), fileName);
//        string name = typeof(T).FullName;
        Debug.Log($"Saving {fileName} data to {filePath}...");

        FileStream stream = new FileStream(filePath, FileMode.Create);
        serializer.Serialize(stream, data);
        stream.Close();
    }
    public void StartNewMeasurement()
    {
        Debug.Log("Starting new measurement...");
        
        vehicleData = new VehicleDataContainer();
        targetDetectionData = new AlarmContainer();
        targetDetectionSummary = new TargetDetectionSummary();
        gazeData = new GazeContainer();

}
    private string saveFolder()
    {
        //Save folder will be .../unityproject/Data/subjectName-date/subjectName/navigationName

        string[] assetsFolderArray = Application.dataPath.Split('/'); //Gives .../unityproject/assest

        //emmit unityfolder/assets and keep root folder

        string[] baseFolderArray = new string[assetsFolderArray.Length - 2];
        for (int i = 0; i < (assetsFolderArray.Length - 2); i++) { baseFolderArray[i] = assetsFolderArray[i]; }

        string baseFolder = string.Join("/", baseFolderArray);
        string saveFolder = string.Join("/", baseFolder, dataFolder, subjectName, navigation.name);
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
        dataPoint.distanceToOptimalPath = GetDistanceToOptimalPath(car.transform.position);
        dataPoint.position = car.transform.position;
        /*dataPoint.throttleInput = vehicleBehaviour.Throttle;
        dataPoint.brakeInput = vehicleBehaviour.Braking;
        dataPoint.steerInput = vehicleBehaviour.Steering;*/

        vehicleData.dataList.Add(dataPoint);

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
    private void AddEyeTrackingData()
    {

        VarjoPlugin.GazeData varjo_data = VarjoPlugin.GetGaze();

        if (varjo_data.status == Varjo.VarjoPlugin.GazeStatus.VALID)
        {
            //Valid gaze data
            MyGazeData data = new MyGazeData();
            data.time = experimentManager.activeExperiment.experimentTime;
            
            data.focusDistance = varjo_data.focusDistance;
            data.focusStability = varjo_data.focusStability;

            data.rightPupilSize = varjo_data.rightPupilSize;
            data.forward_right = ConvertToVector3(varjo_data.right.forward);
            data.position_right = ConvertToVector3(varjo_data.right.position);

            data.leftPupilSize = varjo_data.leftPupilSize;
            data.forward_left = ConvertToVector3(varjo_data.left.forward);
            data.position_left = ConvertToVector3(varjo_data.left.position);
            data.forward_combined = ConvertToVector3(varjo_data.gaze.forward);
            data.position_combined = ConvertToVector3(varjo_data.gaze.position);

            data.status = varjo_data.status;
            data.leftCalibrationQuality = VarjoPlugin.GetGazeCalibrationQuality().left;
            data.leftStatus = varjo_data.leftStatus;

            data.rightCalibrationQuality = VarjoPlugin.GetGazeCalibrationQuality().right;
            data.rightStatus = varjo_data.rightStatus;

            gazeData.dataList.Add(data);
        }
        else
        {
            //Invalid gaze data
            MyGazeData data = new MyGazeData();
            
            data.time = experimentManager.activeExperiment.experimentTime;
            data.status = varjo_data.status;
            data.leftCalibrationQuality = VarjoPlugin.GetGazeCalibrationQuality().left;
            data.leftStatus = varjo_data.leftStatus;

            data.rightCalibrationQuality = VarjoPlugin.GetGazeCalibrationQuality().right;
            data.rightStatus = varjo_data.rightStatus;

            gazeData.dataList.Add(data);

        }
    }
    private Vector3 ConvertToVector3(double[] varjo_data)
        {
            return new Vector3((float)varjo_data[0], (float)varjo_data[1], (float)varjo_data[2]);
        }
    public void AddFalseAlarm()
    {
        TargetAlarm alarm = new TargetAlarm();
        alarm.time = experimentManager.activeExperiment.experimentTime;
        alarm.AlarmType = false;
        targetDetectionData.dataList.Add(alarm);
        Debug.Log("Added false alarm...");
    }
    public void AddTrueAlarm(Target target)
    {

        TargetAlarm alarm = new TargetAlarm();

        alarm.time = experimentManager.activeExperiment.experimentTime;
        alarm.waypointID = target.waypoint.orderId;
        alarm.AlarmType = true;
        alarm.targetID = target.ID;
        alarm.reactionTime = alarm.time - target.startTimeVisible;

        //The difficulty names always end with a number ranging from 1-6
        int difficulty = int.Parse(target.GetTargetDifficulty().ToString().Last().ToString());

        alarm.targetDifficulty = difficulty;
        targetDetectionData.dataList.Add(alarm);

        Debug.Log($"Added true alarm for {target.name}, reaction time: {Math.Round(alarm.reactionTime, 2)}s ...");
    }
    private void SummariseTargetDetectionData()
    {
        targetDetectionSummary = new TargetDetectionSummary();
        if (targetDetectionData == null)
        {
            Debug.Log("ERROR: Target detection datata was corrupted... Re-started measurement...");
            StartNewMeasurement();
        }
        //Get total targets
        foreach (Waypoint waypoint in navigationHelper.GetOrderedWaypointList())
        {
            targetDetectionSummary.totalTargets += waypoint.GetTargets().Count;
        }

        //TotalMisses
        targetDetectionSummary.totalMisses = targetDetectionData.dataList.Count(x => x.AlarmType == false);

        //TotalHits
        targetDetectionSummary.totalHits = targetDetectionData.dataList.Count(x => x.AlarmType == true);

        //missrate & hitRate
        if ((targetDetectionSummary.totalHits + targetDetectionSummary.totalMisses) > 0)
        {
            targetDetectionSummary.missRate = 100 * (float)targetDetectionSummary.totalMisses / (float)(targetDetectionSummary.totalHits + targetDetectionSummary.totalMisses);
            targetDetectionSummary.hitRate = 100 * (float)targetDetectionSummary.totalHits / (float)(targetDetectionSummary.totalHits + targetDetectionSummary.totalMisses);
        }
        else { targetDetectionSummary.missRate = targetDetectionSummary.hitRate = 0f; }

        //detectionRate
        targetDetectionSummary.detectionRate = 100 * (float)targetDetectionSummary.totalHits / (float)targetDetectionSummary.totalTargets;

        print("Total targets: " + targetDetectionSummary.totalTargets + ", total misses:" + targetDetectionSummary.totalMisses + ", total hits: " + targetDetectionSummary.totalHits + ", hit rate: " + targetDetectionSummary.hitRate + "% ....");
    }
}
[XmlRoot("VehicleDataCollection")]
public class VehicleDataContainer
{
    [XmlArray("VehicleData"), XmlArrayItem("DataPoints")]
    public List<VehicleDataPoint> dataList = new List<VehicleDataPoint>();
}
public class VehicleDataPoint
{
    public float time;
    public float distanceToOptimalPath;
    public Vector3 position = new Vector3();
    public float throttleInput;
    public float brakeInput;
    public float steerInput;
}
[XmlRoot("GazeDataCollection")]
public class GazeContainer
{

    [XmlArray("GazeData"), XmlArrayItem("GazePoints")]
    public List<MyGazeData> dataList = new List<MyGazeData>();
    
}
public class MyGazeData
{
    public float time;

    public double focusDistance;
    public double focusStability;

    public double rightPupilSize;
    public Vector3 forward_right;
    public Vector3 position_right;

    public double leftPupilSize;
    public Vector3 forward_left;
    public Vector3 position_left;
    public Vector3 forward_combined;
    public Vector3 position_combined;

    public VarjoPlugin.GazeStatus status;
    public VarjoPlugin.GazeEyeCalibrationQuality leftCalibrationQuality;
    public VarjoPlugin.GazeEyeStatus leftStatus;
    public VarjoPlugin.GazeEyeCalibrationQuality rightCalibrationQuality;
    public VarjoPlugin.GazeEyeStatus rightStatus;
}

[XmlRoot("AlarmCollection")]
public class AlarmContainer
{
    [XmlArray("AlarmData"), XmlArrayItem("Alarms")]
    public List<TargetAlarm> dataList = new List<TargetAlarm>();
}
public class TargetAlarm
{
    public float time;
    public bool AlarmType;
    public float reactionTime;
    public int waypointID;
    public int targetID;
    public int targetDifficulty;
}
public class TargetDetectionSummary
{
    public int totalTargets;
    public int totalMisses;
    public int totalHits;
    public float missRate;
    public float hitRate;
    public float detectionRate;
}
public class NavigationLine
{
    public Vector3[] navigationLine;
   
}
public class NavigationSettings
{
    public bool renderVirtualCable;
    public bool renderHighlightedRoad;
    public bool renderHUD;
    public float transparency;
    public float NavigationTime;
}
