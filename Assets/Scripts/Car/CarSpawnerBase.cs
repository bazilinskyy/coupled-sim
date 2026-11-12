using System.Collections;
using System;
using UnityEngine;

public abstract class CarSpawnerBase : MonoBehaviour
{
    public CarSpawnParams spawnParams;
    protected AICarSyncSystem _syncSystem;

    // This should be called only on the host
    public void Init(AICarSyncSystem syncSystem)
    {
        _syncSystem = syncSystem;
        if (!HasValidSpawnParams)
        {
            Debug.LogWarning($"Car spawner '{name}' has no spawn point, track or car prefab set; it spawns nothing.", this);
            return;
        }
        StartCoroutine(SpawnCoroutine());
    }

    /// <summary>Whether this spawner is set up well enough to spawn; override for spawners that do not use spawnParams' spawn point and track.</summary>
    protected virtual bool HasValidSpawnParams => spawnParams.SpawnPoint != null && spawnParams.Track != null && spawnParams.Car != null;

    protected abstract IEnumerator SpawnCoroutine();
    protected AICar Spawn(CarSpawnParams parameters, bool yielding) 
        => _syncSystem.Spawn(parameters, yielding);
}
