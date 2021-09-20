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
        StartCoroutine(SpawnCoroutine());
    }

    protected abstract IEnumerator SpawnCoroutine();
    protected AICar Spawn(CarSpawnParams parameters, bool yielding) 
        => _syncSystem.Spawn(parameters, yielding);
}
