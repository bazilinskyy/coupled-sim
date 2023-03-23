using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif

public class Recorder : MonoBehaviour
{
    public string directory = "../../videos";
    public Vector2Int resolution = new Vector2Int(3840, 2160);
    public int framerate = 60;
#if UNITY_EDITOR
    RecorderController recorderController; // control interface for recording video
    RecorderControllerSettings controllerSettings;
    MovieRecorderSettings videoRecorder;

    public void Init()
    {
        // Make setup for recording video
        controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        recorderController = new RecorderController(controllerSettings);
        videoRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        videoRecorder.name = "Video recorder";
        videoRecorder.Enabled = true;
        controllerSettings.AddRecorderSettings(videoRecorder);
        // controllerSettings.SetRecordModeToManual(); // will stop when closing
        RecorderOptions.VerboseMode = false;
        videoRecorder.ImageInputSettings = new GameViewInputSettings()
        {
            OutputWidth = resolution.x,
            OutputHeight = resolution.y
        };
        videoRecorder.AudioInputSettings.PreserveAudio = true;
        controllerSettings.AddRecorderSettings(videoRecorder);
        controllerSettings.FrameRate = framerate;

    }

    public void StartRecording(string videoName)
    {
        videoRecorder.OutputFile = Application.dataPath + "/" + directory + "/" + videoName;
        Debug.Log(videoRecorder.OutputFile);
        recorderController.PrepareRecording();
        recorderController.StartRecording();
    }

    public void StopRecording()
    {
        recorderController.StopRecording();
    }
#endif
}
