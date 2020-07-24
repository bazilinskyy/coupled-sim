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

    //our car
    public GameObject car;
    public Transform rootWaypoints;
    public string mainDataFolder;
    public string carDataFileName;
    public string targetDetectionDataFileName;
    public string experimentName;
    //list of items
    public VehicleData vehicleData;
    public TargetDetectionData targetDetectionData;

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

    void AddVehicleData()
    {   
        //TODO : Make this position of the Varjo camera
        //Add vehicle inputs

        //TODO fix bug somehow during simualtion starts giving error
        //vehicleData.positionList.Add(car.transform.position);
    }

    void AddEyeTrackingData()
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


    public void SaveVehicleData()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(VehicleData));

        //overwrites mydata.xml
        string filePath = mainDataFolder + "\\" + carDataFileName + ".xml";

        Debug.Log("Saving to " + filePath);

        FileStream stream = new FileStream(filePath, FileMode.Create);
        serializer.Serialize(stream, vehicleData);
        stream.Close();
        
    }

    public void SaveTargetDetectionData()
    {
        XmlSerializer serializer = new XmlSerializer(typeof(TargetDetectionData));
        string filePath = mainDataFolder + "\\" + targetDetectionDataFileName + ".xml";

        Debug.Log("Saving to " + filePath);

        FileStream stream = new FileStream(filePath, FileMode.Create);
        serializer.Serialize(stream, targetDetectionData);
        stream.Close();
    }
    public void FinalizeTargetDetectionData()
    {
        //Get total targets
        foreach(Transform child in rootWaypoints)
        {
            Waypoint waypoint = child.GetComponent<Waypoint>();
            
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
            throw new System.Exception("No hits or misses were recorded......");
            
        }

        //detectionRate
        targetDetectionData.detectionRate = 100 * (float)targetDetectionData.totalHits / (float)targetDetectionData.totalTargets;

        print("Total targets: " + targetDetectionData.totalTargets + ", total misses:" + targetDetectionData.totalMisses + ", total hits: " + targetDetectionData.totalHits + ", hit rate: " + targetDetectionData.hitRate + "% ....");
    }

    private void OnApplicationQuit()
    {
        SaveVehicleData();

        FinalizeTargetDetectionData();

        SaveTargetDetectionData();
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
