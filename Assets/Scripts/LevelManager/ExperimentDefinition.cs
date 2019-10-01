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
    public Transform Point;
    public Vector3 position => Point.position;
    public Quaternion rotation => Point.rotation;
    public SpawnPointType Type;
}

[Serializable]
public struct ExperimentRoleDefinition
{
    public string Name;
    [Header("Driver HMI")]
    public HMI TopHMI;
    public HMI WindshieldHMI;
    public HMI HoodHMI;
    public int carIdx;
    public SpawnPoint SpawnPoint;
    public WaypointCircuit AutonomousPath;
    public bool AutonomousIsYielding;
}

public class ExperimentDefinition : MonoBehaviour
{
    public string Name;
    public string Scene;
    public ExperimentRoleDefinition[] Roles;
    public Transform[] PointsOfInterest;
    public CarSpawnerBase[] CarSpawners;
}
