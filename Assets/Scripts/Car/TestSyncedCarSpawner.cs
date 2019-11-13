using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

//example implementation of computer controlled car spawning logic 
public class TestSyncedCarSpawner : CarSpawnerBase
{
    [SerializeField]
    protected Transform SpawnPoint;
    [SerializeField]
    protected WaypointCircuit Track;
    public AICar TestCar;
    protected override IEnumerator SpawnCoroutine()
    {
        yield return new WaitForSeconds(5);
        Spawn(TestCar, SpawnPoint.position, SpawnPoint.rotation, Track, false);
        yield return new WaitForSeconds(5);
        Spawn(TestCar, SpawnPoint.position, SpawnPoint.rotation, Track, false);
        yield return new WaitForSeconds(5);
        Spawn(TestCar, SpawnPoint.position, SpawnPoint.rotation, Track, true);
    }
}
