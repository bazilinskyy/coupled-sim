using System;
using UnityEngine;
using UnityStandardAssets.Utility;

public enum SpawnPointType
{
    PlayerControlledPedestrian,
    PlayerControlingCar,
    PlayerInAIControlledCar,
    // cyclist, e-scooter or moped rider: the prefab is ExperimentRoleDefinition.twoWheelerIdx in PlayerSystem's two-wheeler list
    PlayerRidingTwoWheeler,
}
[Serializable]
public struct SpawnPoint
{
    //defines where player avatar will be spawned
    public Transform Point;
    public Vector3 position => Point.position;
    public Quaternion rotation => Point.rotation;
    //a type of player avatar
    public SpawnPointType Type;
    public int CameraIndex;
    public PlayerSystem.VehicleType VehicleType;
}

[Serializable]
public struct ExperimentRoleDefinition
{
    public string Name;
    [Header("Driver HMI")]

    //for Passenger agent, fields define which HMI prefab to spawn on indicated spots
    public HMI TopHMI;
    public HMI WindshieldHMI;
    public HMI HoodHMI;
    [Tooltip("An eHMI from a design file (StreamingAssets/eHMI/<name>.json, see DesignedEhmi); empty: none.")]
    public string EhmiDesign;
    [Tooltip("Where the designed eHMI is: on the hood, the roof, the windshield, the front bumper, or projected on the road ahead.")]
    public EhmiPlacement EhmiDesignPlacement;

    //indicates car prefab that will be spawned for this role. Selected prefab is the one on the indicated index on PlayerSystem component lists 
    // - for Passenger - PassengerAvatarPrefabPassenger list
    // - for Driver - AvatarPrefabDriver list
    public int carIdx;

    //for two-wheeler riders: index in PlayerSystem's Avatar Prefab Two Wheeler list (bicycle, e-scooter, moped)
    public int twoWheelerIdx;

    public SpawnPoint SpawnPoint;

    //for Passenger agent, references game object defining waypoints for the autonomous car via WaypointCirciut component
    public WaypointCircuit AutonomousPath;
    public bool AutonomousIsYielding;

    //side of the steering wheel of the role's car: Auto = on the right on maps with left-hand traffic
    [Tooltip("Where the steering wheel of this role's car is. Auto: on the right in cities with left-hand traffic (London, Tokyo, Sydney, ...), else on the left.")]
    public SteeringSide Steering;

    [Header("Riding in another role's car")]
    [Tooltip("Name of the role whose car this participant sits in (e.g. three participants in one shared AV); empty: own avatar. Use a pedestrian spawn type for this role.")]
    public string RideInCarOf;
    [Tooltip("Seat in that car (rear seats need a car with them, e.g. the hatchback).")]
    public CarSeat Seat;

    [Header("In-car displays")]
    [Tooltip("Head-up display from StreamingAssets/InCar/<name>.json (see InCarDisplay); empty: none.")]
    public string HudDesign;
    [Tooltip("Centre screen on the dashboard from StreamingAssets/InCar/<name>.json; empty: none.")]
    public string CentreScreenDesign;

    [Header("Participant responses")]
    [Tooltip("A hold-to-respond button and/or a 0-100 slider, logged every frame to ExperimentLogs/Responses-*.csv.")]
    public ResponseSettings Response;
}

public enum SteeringSide { Auto, Left, Right }

public enum EhmiPlacement { Hood, Top, Windshield, Bumper, Road }

/// <summary>
/// Continuous responses of a participant, logged every frame (ResponseInput): a button held while
/// e.g. it feels safe to cross, and a slider (0-100) for e.g. trust or perceived risk.
/// </summary>
[Serializable]
public struct ResponseSettings
{
    [Tooltip("Hold-to-respond button (keyboard key below, gamepad right shoulder or trigger, headset controller trigger), logged with its analog value.")]
    public bool Button;
    [Tooltip("Shown above the button state, e.g. 'Hold B while you feel safe to cross'. Empty: no text.")]
    public string ButtonLabel;
    [Tooltip("Slider from 0 to 100 (keys below, gamepad d-pad up/down, headset thumbstick), e.g. trust in the AV.")]
    public bool Slider;
    [Tooltip("Shown next to the slider, e.g. 'Trust'.")]
    public string SliderLabel;
    [Range(0, 100)] public float SliderStart;
    [Tooltip("Log the responses but show nothing on the screen.")]
    public bool Hidden;
}

public class ExperimentDefinition : MonoBehaviour
{
    public string ShortName;
    //the name of the experiment
    public string Name;
    //Unity scene name to be loaded as an experiment environment
    public string Scene;
    //city map (MapCatalog name) built in the OsmMap scene; empty for hand-built scenes.
    //The trial parameter "map" or the host's choice can override it.
    public string Map;
    //list defining roles that can be taken during an experiment by participants
    public ExperimentRoleDefinition[] Roles;
    //static points that are logged in experiment logs to be used in the log processing and analysis
    public Transform[] PointsOfInterest;
    //references to game objects spawning non-player controlled cars
    public CarSpawnerBase[] CarSpawners;
    public AIPedestrianSyncSystem AIPedestrians;
}
