using System.Collections;
using UnityEngine;
using UnityStandardAssets.Utility;

public abstract class CarSpawnerBase : MonoBehaviour
{
    [SerializeField]
    protected Transform SpawnPoint;
    [SerializeField]
    protected WaypointCircuit Track;
    protected AICarSyncSystem _syncSystem;

    // This should be called only on the host
    public void Init(AICarSyncSystem syncSystem)
    {
        _syncSystem = syncSystem;
        StartCoroutine(SpawnCoroutine());
    }

    protected abstract IEnumerator SpawnCoroutine();
    protected AICar Spawn(AICar prefab, bool yielding) 
        => _syncSystem.Spawn(prefab, SpawnPoint.position, SpawnPoint.rotation, Track, yielding);
}
