using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static Varjo.XR.VarjoEyeTracking;


public class CreateLogData : MonoBehaviour
{
    [SerializeField] private NullValueHandling m_nullValueHandling = NullValueHandling.Fill;

    [SerializeField] private bool m_startEnabled = true;
    [SerializeField] private Camera m_camera;
    [SerializeField] private EyeTracking m_eyeTracking;
    [SerializeField] private EmperorsRating m_emperorsRating;
    [SerializeField] private bool m_useCustomLogPath = false;
    [SerializeField] private string m_customLogPath = Application.dataPath + "/_SOSXR/Logs/";
    private static readonly string[] _columnNames = {"UnixTimeSeconds", "Frame", "CaptureTime", "LogTime", "HMDPosition", "HMDRotation", "GazeStatus", "CombinedGazeForward", "CombinedGazePosition", "InterPupillaryDistanceInMM", "LeftEyeStatus", "LeftEyeForward", "LeftEyePosition", "LeftPupilIrisDiameterRatio", "LeftPupilDiameterInMM", "LeftIrisDiameterInMM", "RightEyeStatus", "RightEyeForward", "RightEyePosition", "RightPupilIrisDiameterRatio", "RightPupilDiameterInMM", "RightIrisDiameterInMM", "FocusDistance", "FocusStability", "FocusName", "EmperorsRating - UnixTimeSeconds", "RatingHand", "CurrentRotation", "CurrentHumanReadableRotation"};
    private List<GazeData> _dataSinceLastUpdate;
    private List<EyeMeasurements> _eyeMeasurementsSinceLastUpdate;
    private int _gazeDataCount = 0;
    private float _gazeTimer = 0f;
    private StreamWriter _streamWriter;
    private const string _ValidString = "VALID";
    private const string _InvalidString = "INVALID";

    public bool Logging { get; private set; } = false;


    private void Awake()
    {
        if (m_eyeTracking == null)
        {
            m_eyeTracking = GetComponent<EyeTracking>();
        }

        if (m_camera == null)
        {
            m_camera = transform.root.GetComponentInChildren<Camera>();
        }

        if (m_emperorsRating == null)
        {
            m_emperorsRating = GetComponent<EmperorsRating>();
        }
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

        WriteLog(_columnNames);
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
            Debug.Log("Closing StreamWriter");
            _streamWriter.Flush();
            _streamWriter.Close();
            _streamWriter = null;
        }

        Logging = false;
        Debug.Log("Logging ended");
    }


    private void Update()
    {
        if (Time.timeScale <= 0)
        {
            Debug.LogWarning("Timescale is 0, will not write logs");
            return;
        }
        
        LogFrameData();
    }


    /// <summary>
    ///     Should be run in Update when we want to log any eyetracking data.
    /// </summary>
    public void LogFrameData()
    {
        if (!Logging)
        {
            return;
        }

        _gazeTimer += Time.deltaTime;

        if (_gazeTimer >= 1.0f)
        {
            // Debug.Log("Gaze data rows per second: " + _gazeDataCount);
            _gazeDataCount = 0;
            _gazeTimer = 0f;
        }

        var dataCount = GetGazeList(out _dataSinceLastUpdate, out _eyeMeasurementsSinceLastUpdate);

        _gazeDataCount += dataCount;

        for (var i = 0; i < dataCount; i++)
        {
            // Debug.Log($"Logging data for frame {i}");
            CreateLog(_dataSinceLastUpdate[i], _eyeMeasurementsSinceLastUpdate[i]);
        }
    }


    private void CreateLog(GazeData data, EyeMeasurements eyeMeasurements)
    {
        var logData = new string[29];

        // SOSXR : UnixTimeSeconds
        logData[0] = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();

        // Gaze data frame number
        logData[1] = data.frameNumber.ToString();

        // Gaze data capture time (nanoseconds)
        logData[2] = data.captureTime.ToString();

        // Log time (milliseconds)
        logData[3] = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString();

        // HMD
        logData[4] = m_camera.transform.localPosition.ToString("F3");
        logData[5] = m_camera.transform.localRotation.ToString("F3");

        // Combined gaze
        var invalid = data.status == GazeStatus.Invalid;
        logData[6] = invalid ? _InvalidString : _ValidString;
        logData[7] = invalid ? "" : data.gaze.forward.ToString("F3");
        logData[8] = invalid ? "" : data.gaze.origin.ToString("F3");

        // IPD
        logData[9] = invalid ? "" : eyeMeasurements.interPupillaryDistanceInMM.ToString("F3");

        // Left eye
        var leftInvalid = data.leftStatus == GazeEyeStatus.Invalid;
        logData[10] = leftInvalid ? _InvalidString : _ValidString;
        logData[11] = leftInvalid ? "" : data.left.forward.ToString("F3");
        logData[12] = leftInvalid ? "" : data.left.origin.ToString("F3");
        logData[13] = leftInvalid ? "" : eyeMeasurements.leftPupilIrisDiameterRatio.ToString("F3");
        logData[14] = leftInvalid ? "" : eyeMeasurements.leftPupilDiameterInMM.ToString("F3");
        logData[15] = leftInvalid ? "" : eyeMeasurements.leftIrisDiameterInMM.ToString("F3");

        // Right eye
        var rightInvalid = data.rightStatus == GazeEyeStatus.Invalid;
        logData[16] = rightInvalid ? _InvalidString : _ValidString;
        logData[17] = rightInvalid ? "" : data.right.forward.ToString("F3");
        logData[18] = rightInvalid ? "" : data.right.origin.ToString("F3");
        logData[19] = rightInvalid ? "" : eyeMeasurements.rightPupilIrisDiameterRatio.ToString("F3");
        logData[20] = rightInvalid ? "" : eyeMeasurements.rightPupilDiameterInMM.ToString("F3");
        logData[21] = rightInvalid ? "" : eyeMeasurements.rightIrisDiameterInMM.ToString("F3");

        // Focus
        logData[22] = invalid ? "" : data.focusDistance.ToString();
        logData[23] = invalid ? "" : data.focusStability.ToString();

        // SOSXR 
        logData[24] = m_eyeTracking.FocusName;

        // SOSXR : Emperor's Rating
        logData[25] = m_emperorsRating.CurrentRating.CurrentUnixTimeSeconds.ToString();
        logData[26] = m_emperorsRating.CurrentRating.RatingHand.ToString();
        logData[27] = m_emperorsRating.CurrentRating.CurrentRotation.ToString("F3");
        logData[28] = m_emperorsRating.CurrentRating.CurrentHumanReadableRotation.ToString("F3");

        WriteLog(logData);
    }


    private void WriteLog(string[] values)
    {
        if (!Logging || _streamWriter == null)
        {
            Debug.LogWarning("Logging is not active or StreamWriter is null");

            return;
        }

        for (var i = 0; i < values.Length; ++i)
        {
            if (values[i] == null)
            {
                switch (m_nullValueHandling)
                {
                    case NullValueHandling.SkipValue:
                        Debug.LogWarning("One of the log values is null.");

                        continue;
                    case NullValueHandling.Fill:
                        values[i] = "SOSXR_NULL";
                        Debug.Log("We filled a value with" + values[i]);

                        break;
                }
            }

            values[i] = values[i].Replace("\r", "").Replace("\n", ""); // Remove new lines so they don't break csv
        }

        var line = string.Join(";", values); // Join all values with a semicolon

        // Debug.Log("Writing line to log: " + line);

        _streamWriter.WriteLine(line);
        _streamWriter.Flush(); // Make sure to flush the stream writer to ensure data is written to the file
    }


    private void OnApplicationQuit()
    {
        StopLogging(); // Since this is now MonoBehaviour, this can be here
    }


    private enum NullValueHandling
    {
        Fill,
        SkipValue
    }
}