using UnityEngine;

public static class StaticSceneManager
{
    
    public static MyCameraType camType = MyCameraType.Normal;

    public static string subjectDataFolder;
    public static bool automateSpeed = true;
    public static bool saveData = true;
    public static float animationTime = 2.5f;

    public static bool calibratedUsingHands = false;
    public static float driverViewHorizontalDistance = 0f;
    public static float driverViewVerticalDistance = 0f;

    [Header("Inputs")]
    public static KeyCode myPermission = KeyCode.F1;
    public static KeyCode resetHeadPosition = KeyCode.F2;
    public static KeyCode spawnSteeringWheel = KeyCode.F3;
    public static KeyCode calibrateGaze = KeyCode.F4;
    public static KeyCode resetExperiment = KeyCode.Escape;

    public static KeyCode keyToggleDriving = KeyCode.Space;

    public static KeyCode keyToggleSymbology = KeyCode.Tab;

    public static KeyCode setToLastWaypoint = KeyCode.R;
    public static KeyCode inputNameKey = KeyCode.Y;

    public static KeyCode saveTheData = KeyCode.F7;


    public static string waitingRoomScene = "WaitingScene";
    public static string drivingPractiseScene = "DrivingPractiseScene";
    public static string targetScene = "Targets";
    public static string calibrationScene = "CalibrationScene";
    public static string experimentScene1 = "ExperimentScene1";
    public static string experimentScene2 = "ExperimentScene2";

    public static string[] sceneArray = { calibrationScene, drivingPractiseScene, experimentScene1, experimentScene2 };

    //public static string[] sceneArray = { "Test1", "Test2", "Test3" };

    private static string participantName = "me";
    public static string ParticipantName { get => participantName; set => participantName = value; }

    public static int sceneIndex = 0;
    public static string GetNextScene()
    {
        string nextScene = sceneArray[sceneIndex];
        sceneIndex++;

        return nextScene;
    }

    public static bool IsNextScene()
    {
        if(sceneIndex < sceneArray.Length) { return true; }
        else { return false; }
    }

    public static int GetExperimentNumber()
    {
        return sceneIndex - 1;
    }
}
