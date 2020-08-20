using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System;

public class XMLManager : MonoBehaviour
{

    //singleton pattern
    public static XMLManager ins;

    private string carDataFileName = "carData.xml";
    private string targetDetectionDataFileName = "targetData.xml";
    private string dataFolder = "Data";
    //our car
    private GameObject car;
    private VehicleBehaviour.WheelVehicle vehicleBehaviour; 

    //Current Navigation
    private Transform navigation;
    private NavigationHelper navigationHelper;

    //Experiment manager;
    private ExperimentManager experimentManager;

    private GameState gameState;

    private string subjectName;
    //list of items
    private List<VehicleDataPoint> vehicleData;
    private TargetDetectionData targetDetectionData;

    // Start is called before the first frame update
    private void Awake()
    {
        ins = this;
        vehicleData = new List<VehicleDataPoint>();
        targetDetectionData = new TargetDetectionData();

        experimentManager = gameObject.GetComponent<ExperimentManager>();
        gameState = experimentManager.gameState;
        
    }

    private void Update()
    {
        if (gameState.isExperiment())
        {
            AddVehicleData();
            AddEyeTrackingData();
        }
    }
    public void SetAllInputs(GameObject _car, Transform _navigation, string _subjectName)
    {
        car = _car;
        vehicleBehaviour = car.GetComponent<VehicleBehaviour.WheelVehicle>();


        SetNavigation(_navigation);

        string dateTime = System.DateTime.Now.ToString("MM-dd_HH-mm");
        if (_subjectName == null || _subjectName == "") { _subjectName = "JohnDoe"; }
        subjectName = _subjectName + "-"+ dateTime;
    }

    public void SaveData()
    {
        FinalizeTargetDetectionData();
        SaveVehicleData();
        SaveTargetDetectionData();
    }

    public void StartNewMeasurement()
    {
        Debug.Log("Starting new measurement...");
        vehicleData.Clear(); targetDetectionData = null;

        vehicleData = new List<VehicleDataPoint>();
        targetDetectionData = new TargetDetectionData();
    }
    public void SetNavigation(Transform _navigation)
    {
        navigation = _navigation;
        navigationHelper = navigation.GetComponent<NavigationHelper>();
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
        if(vehicleData == null)
        {
            Debug.Log("ERROR: Vehicle data was corrupted... Re-started measurement...");
            StartNewMeasurement();
        }
        
        VehicleDataPoint dataPoint = new VehicleDataPoint();
        dataPoint.position = car.transform.position;
        dataPoint.time = experimentManager.activeExperiment.experimentTime;
        dataPoint.throttleInput = vehicleBehaviour.Throttle;
        dataPoint.brakeInput = vehicleBehaviour.Braking;
        dataPoint.steerInput = vehicleBehaviour.Steering;

        vehicleData.Add(dataPoint);

        //TODO fix bug somehow during simualtion starts giving error
        //vehicleData.positionList.Add(car.transform.position);
    }

    private void AddEyeTrackingData()
    {
        //TODO: Add the eye tracking data
    }
    public void AddFalseAlarm()
    {
        FalseAlarm alarm = new FalseAlarm();
        alarm.time = experimentManager.activeExperiment.experimentTime;
        targetDetectionData.falseAlarmList.Add(alarm);
        Debug.Log("Added false alarm...");
    }
    public void AddTrueAlarm(Target target)
    {

        TrueAlarm alarm = new TrueAlarm();
        alarm.waypointID = target.waypoint.orderId;
        alarm.targetID = target.ID;
        alarm.time = experimentManager.activeExperiment.experimentTime;
        alarm.reactionTime = alarm.time - target.startTimeVisible;
        targetDetectionData.trueAlarmList.Add(alarm);

        Debug.Log($"Added true alarm for {target.name}, reaction time: {Math.Round(alarm.reactionTime,2)}s ...");
    }
    private void SaveVehicleData()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(List<VehicleDataPoint>));

        //overwrites mydata.xml
        string filePath = string.Join("/", saveFolder(), carDataFileName);

        Debug.Log($"Saving to vehicle data to {filePath}...");

        FileStream stream = new FileStream(filePath, FileMode.Create);
        serializer.Serialize(stream, vehicleData);
        stream.Close();
        
    }

    private void SaveTargetDetectionData()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(TargetDetectionData));
        
        string filePath = string.Join("/", saveFolder(), targetDetectionDataFileName);
        
        Debug.Log($"Saving to target detection data to {filePath}...");

        FileStream stream = new FileStream(filePath, FileMode.Create);
        serializer.Serialize(stream, targetDetectionData);
        stream.Close();
    }
    private void FinalizeTargetDetectionData()
    {
        if (targetDetectionData == null)
        {
            Debug.Log("ERROR: Target detection datata was corrupted... Re-started measurement...");
            StartNewMeasurement();
        }
        //Get total targets
        foreach (Waypoint waypoint in navigationHelper.GetOrderedWaypointList())
        {            
            targetDetectionData.totalTargets += waypoint.GetTargets().Count;
        }

        //TotalMisses
        targetDetectionData.totalMisses = targetDetectionData.falseAlarmList.Count;
        
        //TotalHits
        targetDetectionData.totalHits = targetDetectionData.trueAlarmList.Count;

        //missrate & hitRate
        if ((targetDetectionData.totalHits + targetDetectionData.totalMisses) > 0)
        {
            targetDetectionData.missRate = 100 * (float)targetDetectionData.totalMisses / (float)(targetDetectionData.totalHits + targetDetectionData.totalMisses);
            targetDetectionData.hitRate = 100 * (float)targetDetectionData.totalHits / (float)(targetDetectionData.totalHits + targetDetectionData.totalMisses);
        }
        else
        {
            targetDetectionData.missRate = targetDetectionData.hitRate = 0f;            
        }

        //detectionRate
        targetDetectionData.detectionRate = 100 * (float)targetDetectionData.totalHits / (float)targetDetectionData.totalTargets;

        print("Total targets: " + targetDetectionData.totalTargets + ", total misses:" + targetDetectionData.totalMisses + ", total hits: " + targetDetectionData.totalHits + ", hit rate: " + targetDetectionData.hitRate + "% ....");
    }

}


public class VehicleDataPoint
{
    public Vector3 position;
    public float time;
    public float throttleInput;
    public float brakeInput;
    public float steerInput;
}

public class FalseAlarm
{
    public float time;
}

public class TrueAlarm
{
    public float time;
    public float reactionTime;
    public int waypointID;
    public int targetID;
    
}

public class TargetDetectionData
{
    public List<FalseAlarm> falseAlarmList = new List<FalseAlarm>();
    public List<TrueAlarm> trueAlarmList = new List<TrueAlarm>();
    public int totalTargets;
    public int totalMisses;
    public int totalHits;
    public float missRate;
    public float hitRate;
    public float detectionRate;
}
