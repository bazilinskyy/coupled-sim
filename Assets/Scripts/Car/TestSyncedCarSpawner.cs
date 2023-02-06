using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSyncedCarSpawner : CarSpawnerBase
{
    protected override IEnumerator SpawnCoroutine()
    {
        yield return new WaitForSeconds(0);
        AICar aICar = Spawn(spawnParams, false);
        foreach(var waypoint in spawnParams.Track.Waypoints)
        {
            var speedSettings = waypoint.GetComponent<SpeedSettings>();
            if (speedSettings != null)
            {
                speedSettings.targetAICar = aICar;
            }
        }
    }
}
