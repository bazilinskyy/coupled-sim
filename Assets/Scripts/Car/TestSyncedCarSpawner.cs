using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSyncedCarSpawner : CarSpawnerBase
{
    protected override IEnumerator SpawnCoroutine()
    {
        yield return new WaitForSeconds(0);
        Spawn(spawnParams, false);
    }
}
