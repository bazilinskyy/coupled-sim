using System;
using UnityEngine;
using UnityStandardAssets.Utility;

public enum SpawnPointType
{
    Pedestrian,
    Driver,
    Passenger
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

    public SpawnPoint SpawnPoint;

    //for Passenger agent, references game object defining waypoints for the autonomous car via WaypointCirciut component
    public WaypointCircuit AutonomousPath;
    public bool AutonomousIsYielding;
}

public class ExperimentDefinition : MonoBehaviour
{
    //the name of the experiment
    public string Name;
    //Unity scene name to be loaded as an experiment environment
    public string Scene;
    //list defining roles that can be taken during an experiment by participants
    public ExperimentRoleDefinition[] Roles;
    //static points that are logged in experiment logs to be used in the log processing and analysis
    public Transform[] PointsOfInterest;
    //references to game objects spawning non-player controlled cars
    public CarSpawnerBase[] CarSpawners;
    public AIPedestrianSyncSystem AIPedestrians;
}
