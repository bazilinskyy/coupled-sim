using System;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

[Serializable]
public struct CarSpawnParams
{
    public Transform SpawnPoint;
    public WaypointCircuit Track;
    public bool SpawnDriver;
    public bool SpawnPassenger;
    public AICar Car;
}