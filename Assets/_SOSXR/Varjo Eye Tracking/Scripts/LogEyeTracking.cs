using System;
using System.IO;
using UnityEngine;


public class LogEyeTracking
{
    public LogEyeTracking(VarjoEyeTracking varjoEyeTracking)
    {
        m_useCustomLogPath = false;
        _varjoEyeTracking = varjoEyeTracking;
    }
    
    private readonly bool m_useCustomLogPath;
    private readonly string m_customLogPath = "";

    private Camera _camera;

    private StreamWriter _streamWriter;

    private VarjoEyeTracking _varjoEyeTracking;

    public bool Logging { get; private set; } = false;

    private const string _ValidString = "VALID";
    private const string _InvalidString = "INVALID";


    private static readonly string[] _columnNames = {"Frame", "CaptureTime", "LogTime", "HMDPosition", "HMDRotation", "GazeStatus", "CombinedGazeForward", "CombinedGazePosition", "InterPupillaryDistanceInMM", "LeftEyeStatus", "LeftEyeForward", "LeftEyePosition", "LeftPupilIrisDiameterRatio", "LeftPupilDiameterInMM", "LeftIrisDiameterInMM", "RightEyeStatus", "RightEyeForward", "RightEyePosition", "RightPupilIrisDiameterRatio", "RightPupilDiameterInMM", "RightIrisDiameterInMM", "FocusDistance", "FocusStability"};


    public void ToggleLogging()
    {
        if (!Logging)
        {
            StartLogging();
        }
        else
        {
            StopLogging();
        }
    }


    public void LogGazeData(Varjo.XR.VarjoEyeTracking.GazeData data, Varjo.XR.VarjoEyeTracking.EyeMeasurements eyeMeasurements)
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
        var invalid = data.status == Varjo.XR.VarjoEyeTracking.GazeStatus.Invalid;
        logData[5] = invalid ? _InvalidString : _ValidString;
        logData[6] = invalid ? "" : data.gaze.forward.ToString("F3");
        logData[7] = invalid ? "" : data.gaze.origin.ToString("F3");

        // IPD
        logData[8] = invalid ? "" : eyeMeasurements.interPupillaryDistanceInMM.ToString("F3");

        // Left eye
        var leftInvalid = data.leftStatus == Varjo.XR.VarjoEyeTracking.GazeEyeStatus.Invalid;
        logData[9] = leftInvalid ? _InvalidString : _ValidString;
        logData[10] = leftInvalid ? "" : data.left.forward.ToString("F3");
        logData[11] = leftInvalid ? "" : data.left.origin.ToString("F3");
        logData[12] = leftInvalid ? "" : eyeMeasurements.leftPupilIrisDiameterRatio.ToString("F3");
        logData[13] = leftInvalid ? "" : eyeMeasurements.leftPupilDiameterInMM.ToString("F3");
        logData[14] = leftInvalid ? "" : eyeMeasurements.leftIrisDiameterInMM.ToString("F3");

        // Right eye
        var rightInvalid = data.rightStatus == Varjo.XR.VarjoEyeTracking.GazeEyeStatus.Invalid;
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


    public void StartLogging()
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


    private void OnApplicationQuit()
    {
        StopLogging();
    }
}