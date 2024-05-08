using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static Varjo.XR.VarjoEyeTracking;


[RequireComponent(typeof(EyeTracking))]
public class LogEyeTracking : MonoBehaviour
{
    [SerializeField] private bool m_startEnabled = true;

    private readonly bool m_useCustomLogPath = false;
    private readonly string m_customLogPath = "";
    private static readonly string[] _columnNames = {"Frame", "CaptureTime", "LogTime", "HMDPosition", "HMDRotation", "GazeStatus", "CombinedGazeForward", "CombinedGazePosition", "InterPupillaryDistanceInMM", "LeftEyeStatus", "LeftEyeForward", "LeftEyePosition", "LeftPupilIrisDiameterRatio", "LeftPupilDiameterInMM", "LeftIrisDiameterInMM", "RightEyeStatus", "RightEyeForward", "RightEyePosition", "RightPupilIrisDiameterRatio", "RightPupilDiameterInMM", "RightIrisDiameterInMM", "FocusDistance", "FocusStability"};
    private Camera _camera;
    private StreamWriter _streamWriter;
    private EyeTracking _eyeTracking;
    private int _gazeDataCount = 0;
    private float _gazeTimer = 0f;
    private List<GazeData> _dataSinceLastUpdate;
    private List<EyeMeasurements> _eyeMeasurementsSinceLastUpdate;
    private const string _ValidString = "VALID";
    private const string _InvalidString = "INVALID";


    public bool Logging { get; private set; } = false;


    private void Awake()
    {
        _eyeTracking = GetComponent<EyeTracking>();
        _camera = transform.root.GetComponentInChildren<Camera>();
    }


    private void Start()
    {
        if (m_startEnabled)
        {
            StartLogging();
        }
    }


    [ContextMenu(nameof(ToggleLogging))]
    public void ToggleLogging()
    {
        if (Logging)
        {
            StopLogging();
        }
        else
        {
            StartLogging();
        }
    }


    [ContextMenu(nameof(StartLogging))]
    private void StartLogging()
    {
        if (Logging)
        {
            Debug.LogWarning("Logging was on when StartLogging was called. No new log was started.");

            return;
        }

        Logging = true;

        var logPath = m_useCustomLogPath ? m_customLogPath : Application.dataPath + "/Logs/";
        Directory.CreateDirectory(logPath);

        var now = DateTime.Now;
        var fileName = $"{now.Year}-{now.Month:00}-{now.Day:00}-{now.Hour:00}-{now.Minute:00}";

        var path = logPath + fileName + ".csv";
        _streamWriter = new StreamWriter(path);

        Log(_columnNames);
        Debug.Log("Log file started at: " + path);
    }


    [ContextMenu(nameof(StopLogging))]
    public void StopLogging()
    {
        if (!Logging)
        {
            return;
        }

        if (_streamWriter != null)
        {
            _streamWriter.Flush();
            _streamWriter.Close();
            _streamWriter = null;
        }

        Logging = false;
        Debug.Log("Logging ended");
    }


    /// <summary>
    ///     Should be run in Update when we want to log any eyetracking data.
    /// </summary>
    public void LogFrameEyeTrackingData()
    {
        if (!Logging)
        {
            return;
        }

        _gazeTimer += Time.deltaTime;

        if (_gazeTimer >= 1.0f)
        {
            Debug.Log("Gaze data rows per second: " + _gazeDataCount);
            _gazeDataCount = 0;
            _gazeTimer = 0f;
        }

        var dataCount = GetGazeList(out _dataSinceLastUpdate, out _eyeMeasurementsSinceLastUpdate);

        _gazeDataCount += dataCount;

        for (var i = 0; i < dataCount; i++)
        {
            LogGazeData(_dataSinceLastUpdate[i], _eyeMeasurementsSinceLastUpdate[i]);
        }
    }


    private void LogGazeData(GazeData data, EyeMeasurements eyeMeasurements)
    {
        var logData = new string[23];

        // Gaze data frame number
        logData[0] = data.frameNumber.ToString();

        // Gaze data capture time (nanoseconds)
        logData[1] = data.captureTime.ToString();

        // Log time (milliseconds)
        logData[2] = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString();

        // HMD
        logData[3] = _camera.transform.localPosition.ToString("F3");
        logData[4] = _camera.transform.localRotation.ToString("F3");

        // Combined gaze
        var invalid = data.status == GazeStatus.Invalid;
        logData[5] = invalid ? _InvalidString : _ValidString;
        logData[6] = invalid ? "" : data.gaze.forward.ToString("F3");
        logData[7] = invalid ? "" : data.gaze.origin.ToString("F3");

        // IPD
        logData[8] = invalid ? "" : eyeMeasurements.interPupillaryDistanceInMM.ToString("F3");

        // Left eye
        var leftInvalid = data.leftStatus == GazeEyeStatus.Invalid;
        logData[9] = leftInvalid ? _InvalidString : _ValidString;
        logData[10] = leftInvalid ? "" : data.left.forward.ToString("F3");
        logData[11] = leftInvalid ? "" : data.left.origin.ToString("F3");
        logData[12] = leftInvalid ? "" : eyeMeasurements.leftPupilIrisDiameterRatio.ToString("F3");
        logData[13] = leftInvalid ? "" : eyeMeasurements.leftPupilDiameterInMM.ToString("F3");
        logData[14] = leftInvalid ? "" : eyeMeasurements.leftIrisDiameterInMM.ToString("F3");

        // Right eye
        var rightInvalid = data.rightStatus == GazeEyeStatus.Invalid;
        logData[15] = rightInvalid ? _InvalidString : _ValidString;
        logData[16] = rightInvalid ? "" : data.right.forward.ToString("F3");
        logData[17] = rightInvalid ? "" : data.right.origin.ToString("F3");
        logData[18] = rightInvalid ? "" : eyeMeasurements.rightPupilIrisDiameterRatio.ToString("F3");
        logData[19] = rightInvalid ? "" : eyeMeasurements.rightPupilDiameterInMM.ToString("F3");
        logData[20] = rightInvalid ? "" : eyeMeasurements.rightIrisDiameterInMM.ToString("F3");

        // Focus
        logData[21] = invalid ? "" : data.focusDistance.ToString();
        logData[22] = invalid ? "" : data.focusStability.ToString();

        Log(logData);
    }


    private void Log(string[] values)
    {
        if (!Logging || _streamWriter == null)
        {
            return;
        }

        var line = "";

        for (var i = 0; i < values.Length; ++i)
        {
            values[i] = values[i].Replace("\r", "").Replace("\n", ""); // Remove new lines so they don't break csv
            line += values[i] + (i == values.Length - 1 ? "" : ";"); // Do not add semicolon to last data string
        }

        _streamWriter.WriteLine(line);
    }


    private void OnApplicationQuit()
    {
        StopLogging();
    }
}