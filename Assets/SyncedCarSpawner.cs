using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

//example implementation of computer controlled car spawning logic 
public class SyncedCarSpawner : CarSpawnerBase
{
    [SerializeField]
    protected Transform SpawnPoint;
    [SerializeField]
    protected WaypointCircuit Track;
    public AICar AutonomousVehicle;
    protected override IEnumerator SpawnCoroutine()
    {
        Spawn(AutonomousVehicle, SpawnPoint.position, SpawnPoint.rotation, Track, false);
        yield return new WaitForSeconds(5);
        //yield return new WaitForSeconds(5);
        //Spawn(AutonomousVehicle, SpawnPoint.position, SpawnPoint.rotation, Track, false);
        //yield return new WaitForSeconds(5);
        //Spawn(AutonomousVehicle, SpawnPoint.position, SpawnPoint.rotation, Track, true);
    }
}
