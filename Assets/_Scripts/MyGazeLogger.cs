using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using System.IO;
using System;
public class MyGazeLogger : MonoBehaviour
{
    StreamWriter writer = null;

    List<VarjoPlugin.GazeData> dataSinceLastUpdate;

    Vector3 hmdPosition;
    Vector3 hmdRotation;
    public Transform cam;
    public ExperimentManager experimentManager;
    [Header("Should only the latest data be logged on each update")]
    public bool oneGazeDataPerFrame = false;

    [Header("Start logging after calibration")]
    public bool startAutomatically = false;

    [Header("Press to start or end logging")]
    public KeyCode toggleLoggingKey = KeyCode.Return;

    [Header("Default path is Logs under application data path.")]
    public bool useCustomLogPath = false;
    public string customLogPath = "";

    bool logging = false;

    static readonly string[] ColumnNames = { "Frame", "LogTime", "ExperimentTime", "HMDPosition", "HMDRotation", "HMDLocalRotation","GazeStatus", "CombinedGazeForward", "CombinedGazePosition", "LeftEyeStatus", "LeftEyeForward", "LeftEyePosition", "LeftEyePupilSize", "RightEyeStatus", "RightEyeForward", "RightEyePosition", "RightEyePupilSize", "FocusDistance", "FocusStability" };

    const string ValidString = "VALID";
    const string InvalidString = "INVALID";


    private void Start()
    {
        // InitGaze must be called before using or calibrating gaze tracking.
        if (!VarjoPlugin.InitGaze())
        {
            Debug.LogError("Failed to initialize gaze");
            GetComponent<MyGazeLogger>().enabled = false;
        }
    }

    void Update()
    {
        // Do not run update if the application is not visible
        if (!VarjoManager.Instance.IsLayerVisible() || VarjoManager.Instance.IsInStandBy())
        {
            return;
        }

        if (Input.GetKeyDown(toggleLoggingKey))
        {
            if (!logging)
            {
                StartLogging();
            }
            else
            {
                StopLogging();
            }
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
                foreach (var data in dataSinceLastUpdate)
                {
                    LogGazeData(data);

                }


            }
        }
        else if (startAutomatically)
        {
            if (VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID)
            {
                StartLogging();
            }
        }

    }
    void LogGazeData(VarjoPlugin.GazeData data)
    {
        // Get HMD position and rotation
        hmdPosition = VarjoManager.Instance.HeadTransform.position;
        hmdRotation = VarjoManager.Instance.HeadTransform.rotation.eulerAngles;

        string[] logData = new string[19];

        // Unity frame count
        logData[0] = Time.frameCount.ToString();

        // Gaze data capture time (nanoseconds)
        logData[1] = data.captureTime.ToString();

        // Log time of experiment (seconds) experiment
        logData[2] = experimentManager.activeExperiment.experimentTime.ToString();

        // HMD
        logData[3] = hmdPosition.ToString("F3");
        logData[4] = hmdRotation.ToString("F3");

        Quaternion relative = Quaternion.Inverse(experimentManager.driverView.rotation) * experimentManager.CameraTransform().rotation;
        logData[5] = relative.eulerAngles.ToString("F3");

        // Combined gaze
        bool invalid = data.status == VarjoPlugin.GazeStatus.INVALID;
        logData[6] = invalid ? InvalidString : ValidString;
        logData[7] = invalid ? "" : Double3ToString(data.gaze.forward);
        logData[8] = invalid ? "" : Double3ToString(data.gaze.position);

        // Left eye
        bool leftInvalid = data.leftStatus == VarjoPlugin.GazeEyeStatus.EYE_INVALID;
        logData[9] = leftInvalid ? InvalidString : ValidString;
        logData[10] = leftInvalid ? "" : Double3ToString(data.left.forward);
        logData[11] = leftInvalid ? "" : Double3ToString(data.left.position);
        logData[12] = leftInvalid ? "" : data.leftPupilSize.ToString();

        // Right eye
        bool rightInvalid = data.rightStatus == VarjoPlugin.GazeEyeStatus.EYE_INVALID;
        logData[13] = rightInvalid ? InvalidString : ValidString;
        logData[14] = rightInvalid ? "" : Double3ToString(data.right.forward);
        logData[15] = rightInvalid ? "" : Double3ToString(data.right.position);
        logData[16] = rightInvalid ? "" : data.rightPupilSize.ToString();

        // Focus
        logData[17] = invalid ? "" : data.focusDistance.ToString();
        logData[18] = invalid ? "" : data.focusStability.ToString();

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

        logging = true;

        string logPath = useCustomLogPath ? customLogPath : Application.dataPath + "/Logs/";
        
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;
        string fileName = string.Format("{0}-{1:00}-{2:00}-{3:00}-{4:00}", now.Year, now.Month, now.Day, now.Hour, now.Minute);

        string path = logPath + fileName + ".csv";
        writer = new StreamWriter(path);

        Log(ColumnNames);
        //Debug.Log("Log file started at: " + path);
    }

    public void StopLogging()
    {
        if (!logging)
            return;

        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
        logging = false;
        //Debug.Log("Logging ended");
    }

    void OnApplicationQuit()
    {
        if (experimentManager.saveData && !GetComponent<XMLManager>().savedData) { StopLogging(); }
    }

    public static string Double3ToString(double[] doubles)
    {
        return "(" + doubles[0].ToString() + ", " + doubles[1].ToString() + ", " + doubles[2].ToString() + ")";
    }
}