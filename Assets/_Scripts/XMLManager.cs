using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

public class XMLManager : MonoBehaviour
{

    //singleton pattern
    public static XMLManager ins;

    private string carDataFileName = "carData.xml";
    private string targetDetectionDataFileName = "targetData.xml";
    private string targetDetectionDataSummaryFileName = "targetDataSummary.xml";
    private string navigationSettingsFileName = "navigationSettings.xml";
    private string navigationLineFileName = "navigationLine.xml";
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
    private VehicleData vehicleData;
    private TargetDetectionData targetDetectionData;
    private TargetDetectionSummary targetDetectionSummary;

    private void OnApplicationQuit()
    {
        SaveData();
    }
    // Start is called before the first frame update
    private void Awake()
    {
        ins = this;
        vehicleData = new VehicleData();
        targetDetectionData = new TargetDetectionData();

        experimentManager = gameObject.GetComponent<ExperimentManager>();
        gameState = experimentManager.gameState;

        SetAllInputs(car, navigation, "John Doe");


    }
    private void Update()
    {
        if (gameState.isExperiment() && experimentManager.saveData)
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
        subjectName = _subjectName + "-" + dateTime;
    }
    public void SaveData()
    {
               
        SaveThis<VehicleData>(carDataFileName, vehicleData);
        
        SaveThis<TargetDetectionData>(targetDetectionDataFileName, targetDetectionData);

        SummariseTargetDetectionData();
        SaveThis<TargetDetectionSummary>(targetDetectionDataSummaryFileName, targetDetectionSummary);

        SaveNavigationData();
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
        string name = typeof(T).FullName;
        Debug.Log($"Saving {name} data to {filePath}...");

        FileStream stream = new FileStream(filePath, FileMode.Create);
        serializer.Serialize(stream, data);
        stream.Close();
    }
    public void StartNewMeasurement()
    {
        Debug.Log("Starting new measurement...");
        targetDetectionData = null;

        vehicleData = new VehicleData();
        targetDetectionData = new TargetDetectionData();
        targetDetectionSummary = new TargetDetectionSummary();

}
    public void SetNavigation(Transform _navigation)
    {
        navigation = _navigation;
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        navigationLine = navigationHelper.GetNavigationLine();
        indexClosestPoint = 0;
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

        vehicleData.distanceToOptimalPath.Add(GetDistanceToOptimalPath(car.transform.position));
        vehicleData.position.Add(car.transform.position);
        vehicleData.time.Add(experimentManager.activeExperiment.experimentTime);
        vehicleData.throttleInput.Add(vehicleBehaviour.Throttle);
        vehicleData.brakeInput.Add(vehicleBehaviour.Braking);
        vehicleData.steerInput.Add(vehicleBehaviour.Steering);

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
        alarm.targetDifficulty = target.GetTargetDifficulty().ToString();
        targetDetectionData.trueAlarmList.Add(alarm);

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
        targetDetectionSummary.totalMisses = targetDetectionData.falseAlarmList.Count;

        //TotalHits
        targetDetectionSummary.totalHits = targetDetectionData.trueAlarmList.Count;

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

public class VehicleData
{
    public List<float> distanceToOptimalPath = new List<float>();
    public List<Vector3> position = new List<Vector3>();
    public List<float> time = new List<float>();
    public List<float> throttleInput = new List<float>();
    public List<float> brakeInput = new List<float>();
    public List<float> steerInput = new List<float>();
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
    public string targetDifficulty;
}

public class TargetDetectionData
{
    public List<FalseAlarm> falseAlarmList = new List<FalseAlarm>();
    public List<TrueAlarm> trueAlarmList = new List<TrueAlarm>();
    
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
