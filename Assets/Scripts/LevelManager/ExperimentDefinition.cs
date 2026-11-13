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
}

public enum SteeringSide { Auto, Left, Right }

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
