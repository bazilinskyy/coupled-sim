// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

namespace VarjoExample
{
    /// <summary>
    /// Logs gaze data.
    /// Requires gaze calibration first.
    /// </summary>
    ///
    public class VarjoGazeLog_CS : MonoBehaviour
    {
        StreamWriter writer = null;

        List<VarjoPlugin.GazeData> dataSinceLastUpdate;

        Vector3 hmdPosition;
        Vector3 hmdRotation;

        // Data to log
        RaycastHit gazeRayHit;
        Vector3 gazeRayForward;
        Vector3 gazeRayDirection;
        Vector3 gazePosition;
        Vector3 gazeRayOrigin;
        string role_varjo;
        float distance;
        float time = 0.0f;

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

        //static readonly string[] ColumnNames = { "Frame", "CaptureTime", "LogTime", "HMDPosition", "HMDRotation", "GazeStatus", "CombinedGazeForward", "CombinedGazePosition", "LeftEyeStatus", "LeftEyeForward", "LeftEyePosition", "LeftEyePupilSize", "RightEyeStatus", "RightEyeForward", "RightEyePosition", "RightEyePupilSize", "FocusDistance", "FocusStability", "distance"};
        static readonly string[] ColumnNames = { "Role", "Time", "Distance", "HMDPosition", "HMDRotation", "Gaze Forward (HMD)", "Gaze Position (HMD)", "Gaze Direction (world)", "Gaze Origin (world)" };

        const string ValidString = "VALID";
        const string InvalidString = "INVALID";

        void Update()
        {
            time += Time.deltaTime;
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
                    Debug.Log("Varjo Logging started key press");
                }
                else
                {
                    StopLogging();
                }
                return;
            }

            if (logging) // Enters after calling "StartLogging()"
            {
                if (oneGazeDataPerFrame)
                {
                    // Get and log latest gaze data
                    //LogGazeData(VarjoPlugin.GetGaze());
                    Debug.Log("Varjo entered one gaze data per frame");
                }
                else
                {
                    // Get and log all gaze data since last update
                    dataSinceLastUpdate = VarjoPlugin.GetGazeList(); // error here???
                    Debug.Log($"Data count {dataSinceLastUpdate.Count}");
                    /*foreach (var data in dataSinceLastUpdate)
                    {
                        LogGazeData(data);
                        Debug.Log($"Varjo logged at {time}");
                    }*/
                    LogGazeData();
                }
            }
            else if (startAutomatically)
            {
                if (VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID)
                {
                    StartLogging(); // Creates the log file and set the bool "logging" to true
                    Debug.Log($"Varjo Logging started automatically at {time}");
                }
            }
        }

        void LogGazeData()//(VarjoPlugin.GazeData data)
        {
            Debug.Log("Data logged");

            // Load car-pedestrian distance during eye contact
            gazeRayHit = this.GetComponent<VarjoGazeRay_CS>().getGazeRayHit();
            distance = gazeRayHit.distance;
            gazeRayForward = this.GetComponent<VarjoGazeRay_CS>().getGazeRayForward();      // hmd space
            gazeRayDirection = this.GetComponent<VarjoGazeRay_CS>().getGazeRayDirection();  // world space
            gazePosition = this.GetComponent<VarjoGazeRay_CS>().getGazePosition();          // hmd space
            gazeRayOrigin = this.GetComponent<VarjoGazeRay_CS>().getGazeRayOrigin();        // world space
            //role_varjo = this.GetComponent<VarjoGazeRay_CS>().getRoleVarjo();

            // Get HMD position and rotation
            hmdPosition = VarjoManager.Instance.HeadTransform.position;
            hmdRotation = VarjoManager.Instance.HeadTransform.rotation.eulerAngles;

            string[] logData = new string[9]; //new string[19];

            // Role varjo user
            logData[0] = "Person";// role_varjo;

            // Time
            logData[1] = time.ToString();

            // Distance
            logData[2] = distance.ToString("F3");

            // HMD
            logData[3] = hmdPosition.ToString("F3");
            logData[4] = hmdRotation.ToString("F3");

            // Gaze in HMD space
            logData[5] = gazeRayForward.ToString("F3");
            logData[6] = gazePosition.ToString("F3");

            // Gaze in world space
            logData[7] = gazeRayDirection.ToString("F3");
            logData[8] = gazeRayOrigin.ToString("F3");

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
            Debug.Log("Log file started at: " + path);
        }

        void StopLogging()
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
            Debug.Log("Logging ended");
        }

        void OnApplicationQuit()
        {
            StopLogging();
        }

        public static string Double3ToString(double[] doubles)
        {
            return doubles[0].ToString() + ", " + doubles[1].ToString() + ", " + doubles[2].ToString();
        }
    }
}
