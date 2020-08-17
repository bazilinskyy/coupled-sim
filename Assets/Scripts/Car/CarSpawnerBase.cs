using System.Collections;
using UnityEngine;
using UnityStandardAssets.Utility;

public abstract class CarSpawnerBase : MonoBehaviour
{
    [SerializeField]
    protected Transform SpawnPointDistraction;
    [SerializeField]
    protected WaypointCircuit Track;
    protected AICarSyncSystem _syncSystem;

    // This should be called only on the host
    public void Init(AICarSyncSystem syncSystem)
    {
        _syncSystem = syncSystem;
        if (this.isActiveAndEnabled)
        { 
            StartCoroutine(SpawnCoroutine());
        }
    }

    protected abstract IEnumerator SpawnCoroutine();
    /*protected AICar Spawn(AICar prefab, bool yielding) 
        => _syncSystem.Spawn(prefab, SpawnPoint.position, SpawnPoint.rotation, Track, yielding);*/
    protected AICar SpawnDistraction(AICar prefab, bool yielding)
    => _syncSystem.SpawnDistraction(prefab, SpawnPointDistraction.position, SpawnPointDistraction.rotation, Track, yielding);
}
