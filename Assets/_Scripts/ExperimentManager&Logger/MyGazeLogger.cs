using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using System.IO;
using System;
using System.Globalization;
using System.Linq;

public class MyGazeLogger : MonoBehaviour
{
    StreamWriter writer = null;

    List<VarjoPlugin.GazeData> dataSinceLastUpdate;

    public bool highlightGaze = true;

    public GameObject gazeHighlight;
    private GameObject lightObj;
    Vector3 hmdPosition;
    Vector3 hmdRotation;
    private newExperimentManager experimentManager;
    [Header("Should only the latest data be logged on each update")]
    public bool oneGazeDataPerFrame = false;

    [Header("Start logging after calibration")]
    public bool startAutomatically = false;

    [Header("Press to start or end logging")]
    public KeyCode toggleLoggingKey = KeyCode.Return;

    [Header("Default path is Logs under application data path.")]
    [Header("Default path is Logs under application data path.")]
    public bool useCustomLogPath = false;
    public string customLogPath = "";

    bool logging = false;

    static readonly string[] ColumnNames = { "Frame", "LogTime", "ExperimentTime", "HMDPosition", "HMDRotation", "HMDWorldPosition",
                                            "HMDWorldRotation","GazeStatus", "CombinedGazeForwardLocal", "CombinedGazePositionLocal", "LeftEyeStatus",
                                            "LeftEyeForward", "LeftEyePosition", "LeftEyePupilSize", "RightEyeStatus", "RightEyeForward",
                                            "RightEyePosition", "RightEyePupilSize", "FocusDistance", "FocusStability", "LookingAt", "CombinedGazeForwardWorld","CombinedGazePositionWorld"  };
    
    const string ValidString = "VALID";
    const string InvalidString = "INVALID";
    private int logCount = 0; 
    private float startTime = 0f;
    private CultureInfo culture = CultureInfo.CreateSpecificCulture("eu-ES");
    //Looking at what exactly?
    public Fixation fixationData;
    private LoggedTags fixatingOn = LoggedTags.Environment;
    private long baseTimeNanoSeconds = 0;
    
    public void StartUp()
    {
        experimentManager = MyUtils.GetExperimentManager();
        if (experimentManager == null) { GetComponent<MyGazeLogger>().enabled = false; return; }
        // InitGaze must be called before using or calibrating gaze tracking.
        if (!VarjoPlugin.InitGaze())
        {
            Debug.LogError("Failed to initialize gaze");
            GetComponent<MyGazeLogger>().enabled = false;
        }
        
        if (highlightGaze) { StartHighlight(); }

        StartLogging();

        
    }

   
    void Update()
    {
        if (!logging) { return; }
        // Do not run update if the application is not visible
        if (!VarjoManager.Instance.IsLayerVisible() || VarjoManager.Instance.IsInStandBy()) { return; }
        
        if (Input.GetKeyDown(toggleLoggingKey))
        {
            if (!logging) { StartLogging(); }
            else { StopLogging(); }
            return;
        }

        if (logging)
        {
            if (oneGazeDataPerFrame)
            {
                // Get and log latest gaze data
                LogGazeData(VarjoPlugin.GetGaze());
            }
            else
            {
                // Get and log all gaze data since last update
                dataSinceLastUpdate = VarjoPlugin.GetGazeList();
                foreach (var data in dataSinceLastUpdate) { LogGazeData(data); }

                if (highlightGaze) { RenderGaze(); }

                CheckLogCount();

            }
        }
        else if (startAutomatically) { if (VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID) { StartLogging(); } }

    }
    public void StartHighlight()
    {
        if( lightObj == null)
        {
            lightObj = Instantiate(gazeHighlight);
            lightObj.transform.parent = MyUtils.GetPlayer().transform.Find("VarjoCameraRig").transform.Find("VarjoCamera").transform;
            
        }
        
    }
    private void DisableHighlight()
    {
        if(lightObj != null) { lightObj.SetActive(false);  }
    }
    public VarjoPlugin.GazeData GetLastGaze()
    {
        if (logging) { return dataSinceLastUpdate.Last(); }
        else { throw new Exception(); }
    }

    void CheckLogCount()
    {
        float checkTime = 1f; float minimumLogsPerSecond = 2;
        if(startTime + checkTime < Time.time) 
        {
            startTime = Time.time;  
            if(logCount < checkTime * minimumLogsPerSecond) { Debug.Log($"Slow gaze logging! Received {logCount} gaze points in the pas {checkTime} seconds..."); }
            logCount = 0;
        }
    }
    void LogGazeData(VarjoPlugin.GazeData data)
    {
        logCount++;
        if (baseTimeNanoSeconds == 0) { baseTimeNanoSeconds = data.captureTime; }
        // Get HMD position and rotation
        hmdPosition = VarjoManager.Instance.HeadTransform.position;
        hmdRotation = VarjoManager.Instance.HeadTransform.rotation.eulerAngles;

        string[] logData = new string[ColumnNames.Length];

        // Unity frame count
        logData[0] = Time.frameCount.ToString();

        // Gaze data capture time (nanoseconds)
        logData[1] = (data.captureTime - baseTimeNanoSeconds).ToString();

        // Log time of experiment (seconds) experiment
        logData[2] = experimentManager.experimentSettings.experimentTime.ToString("G", culture );

        // HMD
        logData[3] = hmdPosition.ToString("F3");
        logData[4] = hmdRotation.ToString("F3");

        Quaternion relative = Quaternion.Inverse(experimentManager.driverView.rotation) * experimentManager.CameraTransform().rotation;
        
        logData[5] = experimentManager.CameraTransform().position.ToString("F3");
        logData[6] = relative.eulerAngles.ToString("F3");// experimentManager.CameraTransform().rotation.eulerAngles.ToString("F3");

        // Combined gaze
        bool invalid = data.status == VarjoPlugin.GazeStatus.INVALID;
        logData[7] = invalid ? InvalidString : ValidString;

        //This is relative to the hmd position and rotation
        logData[8] = invalid ? "" : Double3ToString(data.gaze.forward);
        logData[9] = invalid ? "" : Double3ToString(data.gaze.position);

        // Left eye
        bool leftInvalid = data.leftStatus == VarjoPlugin.GazeEyeStatus.EYE_INVALID;
        logData[10] = leftInvalid ? InvalidString : ValidString;
        logData[11] = leftInvalid ? "" : Double3ToString(data.left.forward);
        logData[12] = leftInvalid ? "" : Double3ToString(data.left.position);
        logData[13] = leftInvalid ? "" : data.leftPupilSize.ToString("G", culture);

        // Right eye
        bool rightInvalid = data.rightStatus == VarjoPlugin.GazeEyeStatus.EYE_INVALID;
        logData[14] = rightInvalid ? InvalidString : ValidString;
        logData[15] = rightInvalid ? "" : Double3ToString(data.right.forward);
        logData[16] = rightInvalid ? "" : Double3ToString(data.right.position);
        logData[17] = rightInvalid ? "" : data.rightPupilSize.ToString("G", culture);

        // Focus
        logData[18] = invalid ? "" : data.focusDistance.ToString("G", culture);
        logData[19] = invalid ? "" : data.focusStability.ToString("G", culture);

        logData[20] = invalid ? "" : fixatingOn.ToString();
        logData[21] = "";
        
        Transform HMD = VarjoManager.Instance.HeadTransform;
        // Transform gaze direction and origin from HMD space to world space
        Vector3 gazeRayDirection = HMD.TransformVector(Double3ToVector3(data.gaze.forward));
        Vector3 gazeRayOrigin = HMD.TransformPoint(Double3ToVector3(data.gaze.position));

        logData[21] = invalid ? "" : gazeRayDirection.ToString("F3");
        logData[22] = invalid ? "" : gazeRayOrigin.ToString("F3");

        Log(logData);
    }
    // Write given values in the log file
    void Log(string[] values)
    {
        if (!logging || writer == null)
            return;

        string line = "";
        for (int i = 0; i < values.Length; ++i)
        {
            values[i] = values[i].Replace("\r", "").Replace("\n", ""); // Remove new lines so they don't break csv
            line += values[i] + (i == (values.Length - 1) ? "" : ";"); // Do not add semicolon to last data string
        }
        writer.WriteLine(line);
    }
    public void StartLogging()
    {
        if (logging)
        {
            Debug.LogWarning("Logging was on when StartLogging was called. No new log was started.");
            return;
        }

        fixationData = new Fixation();
        logging = true;

        string logPath = useCustomLogPath ? customLogPath : Application.dataPath + "/Logs/";
        
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;
        string fileName = string.Format("{0}-{1:00}-{2:00}-{3:00}-{4:00}", now.Year, now.Month, now.Day, now.Hour, now.Minute);

        string path = logPath + fileName + ".csv";
        writer = new StreamWriter(path);

        Log(ColumnNames);
        //Debug.Log("GazeLog file started at: " + path);
    }
    public void StopLogging()
    {
        if (!logging) { return; }

        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
        logging = false;
        //Debug.Log("Logging ended");
    }
    void RenderGaze()
    {
        if(dataSinceLastUpdate.Count() ==0) { return; }
        if(dataSinceLastUpdate.Last().status == VarjoPlugin.GazeStatus.INVALID) { return; }
        if(lightObj == null) { return; }
        
        Transform HMD = VarjoManager.Instance.HeadTransform;
        // Transform gaze direction and origin from HMD space to world space
        Vector3 gazeRayDirection = HMD.TransformVector(Double3ToVector3(dataSinceLastUpdate.Last().gaze.forward));
        Vector3 gazeRayOrigin = HMD.TransformPoint(Double3ToVector3(dataSinceLastUpdate.Last().gaze.position));

        lightObj.transform.position = gazeRayOrigin;
        lightObj.transform.rotation = Quaternion.LookRotation(gazeRayDirection);
    }

    public void RestartLogging()
    {
        if (writer != null) { writer.Dispose(); logging = false; }
        fixationData = new Fixation();
        StartLogging();
    }
    public bool IsLogging() { return logging; }
    public static string Double3ToString(double[] doubles)
    {
        return "(" + doubles[0].ToString() + ", " + doubles[1].ToString() + ", " + doubles[2].ToString() + ")";
    }

    public static Vector3 Double3ToVector3(double[] doubles)
    {
        return new Vector3((float)doubles[0], (float)doubles[1], (float)doubles[2]);
    }
    public void FixatingOn(LoggedTags tag)
    {
        //if(tag != fixatingOn) { Debug.Log($"Fixating on {tag}..."); }
        fixationData.FixatingOn(tag);
        fixatingOn = tag;
    }
}
public class Fixation
{
    public float conformalSymbology;
    public float hudSymbology;
    public float hudText;
    public float target;
    public float insideCar;
    public float leftMirror;
    public float rightMirror;
    public float rearMirror;

    public float environment;
    public float unknown;

    public Fixation()
    {
        conformalSymbology = 0f;
        hudSymbology = 0f;
        hudText = 0f;
        target = 0f;
        insideCar = 0f;
        leftMirror = 0f;
        rightMirror = 0f;
        rearMirror = 0f;
        
        environment = 0f;
        unknown= 0f;
    }
    public void FixatingOn(LoggedTags tag)
    {
        if (tag == LoggedTags.Environment) { environment += Time.deltaTime; }
        else if (tag == LoggedTags.ConformalSymbology) { conformalSymbology += Time.deltaTime; }
        else if (tag == LoggedTags.HUDSymbology) { hudSymbology += Time.deltaTime; }
        else if (tag == LoggedTags.HUDText) { hudText += Time.deltaTime; }
        else if(tag == LoggedTags.Target) { target += Time.deltaTime;}
        else if (tag == LoggedTags.InsideCar) { insideCar += Time.deltaTime; }
        else if (tag == LoggedTags.LeftMirror) { leftMirror += Time.deltaTime; }
        else if (tag == LoggedTags.RightMirror) { rightMirror += Time.deltaTime; }
        else if (tag == LoggedTags.RearMirror) { rearMirror += Time.deltaTime; }
        else if (tag == LoggedTags.Unknown) { unknown += Time.deltaTime; }
    }
}

