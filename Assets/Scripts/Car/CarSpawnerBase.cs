using System.Collections;
using UnityEngine;
using UnityStandardAssets.Utility;

//base class for spawning computer controlled cars
//prefabs spawned at runtime with Spawn methond must be added to AICarSyncSystem first
public abstract class CarSpawnerBase : MonoBehaviour
{
    protected AICarSyncSystem _syncSystem;

    // This should be called only on the host
    public void Init(AICarSyncSystem syncSystem)
    {
        _syncSystem = syncSystem;
        StartCoroutine(SpawnCoroutine());
    }

    protected abstract IEnumerator SpawnCoroutine();
    protected AICar Spawn(AICar prefab, Vector3 pos, Quaternion rot, WaypointCircuit Track, bool yielding) 
        => _syncSystem.Spawn(prefab, pos, rot, Track, yielding);
}
