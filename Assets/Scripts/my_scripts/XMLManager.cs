using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

public class XMLManager : MonoBehaviour
{

    //singleton pattern
    public static XMLManager ins;

    private string carDataFileName = "carData.xml";
    private string targetDetectionDataFileName = "targetData.xml";

    //our car
    private GameObject car;

    //Current Navigation
    private Transform navigation;
    private NavigationHelper navigationHelper;

    private string subjectName;
    //list of items
    private VehicleData vehicleData;
    private TargetDetectionData targetDetectionData;

    // Start is called before the first frame update
    private void Awake()
    {
        ins = this;
        vehicleData = new VehicleData();
        targetDetectionData = new TargetDetectionData();
    }

    private void Update()
    {
        AddVehicleData();
        AddEyeTrackingData();
    }
    public void SetAllInputs(GameObject _car, Transform _navigation, string _subjectName)
    {
        car = _car;
        navigation = _navigation;

        navigationHelper = navigation.GetComponent<NavigationHelper>();

        string dateTime = System.DateTime.Now.ToString("MM-dd-HH-mm");
        if (_subjectName == null || _subjectName == "") { _subjectName = "JohnDoe"; }
        subjectName = _subjectName + "-"+ dateTime;
    }

    public void SaveData()
    {
        SaveVehicleData();

        FinalizeTargetDetectionData();

        SaveTargetDetectionData();
    }

    public void StartNewMeasurement()
    {
        vehicleData = new VehicleData();
        targetDetectionData = new TargetDetectionData();
    }
    public void SetNavigation(Transform _navigation)
    {
        navigation = _navigation;
    }
    private string saveFolder()
    {
        //Save folder will be .../unityproject/Data/subjectName-date/subjectName/navigationName

        string assetsFolder = Application.dataPath;
        //Removing the assets folder remove last 6 chars
        string unityFolder = assetsFolder.Remove(assetsFolder.Length - 6);

        string saveFolder = string.Join(Path.DirectorySeparatorChar.ToString(), unityFolder, "Data", subjectName, navigation.name);
        Directory.CreateDirectory(saveFolder);

        return saveFolder;
    }
    private void AddVehicleData()
    {   
        //TODO : Make this position of the Varjo camera
        //Add vehicle inputs

        //TODO fix bug somehow during simualtion starts giving error
        //vehicleData.positionList.Add(car.transform.position);
    }

    private void AddEyeTrackingData()
    {
        //TODO: Add the eye tracking data
    }
    public void AddFalseAlarm()
    {
        print("Adding false alarm");
        FalseAlarm alarm = new FalseAlarm();
        targetDetectionData.falseAlarmList.Add(alarm);
    }
    public void AddTrueAlarm(GameObject target)
    {
        print("Adding true alarm for " + target.name);
        TrueAlarm alarm = new TrueAlarm();
        alarm.waypointID = target.GetComponent<Target>().waypoint.orderId;
        alarm.targetID = target.GetComponent<Target>().ID;
        targetDetectionData.trueAlarmList.Add(alarm);
    }
    private void SaveVehicleData()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(VehicleData));

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
        //Get total targets
        foreach(Waypoint waypoint in navigationHelper.GetOrderedWaypointList())
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

    private void OnApplicationQuit()
    {

        SaveData();
    }
}


public class VehicleDataPoint
{
    public Vector3 position { get; set; }
}

public class VehicleData
{
    public List<Vector3> positionList = new List<Vector3>();
}

public class FalseAlarm
{
    public float time = Time.time;
}

public class TrueAlarm
{
    public float time = Time.time;
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
