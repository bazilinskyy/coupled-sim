using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

//example implementation of computer controlled car spawning logic 
public class CarSpawnerSynced : CarSpawnerBase
{
    [SerializeField]
    protected Transform SpawnPoint;
    [SerializeField]
    protected WaypointCircuit Track;
    //public AICar AutonomousVehicle;
    public AICar[] cars;
    public float numCars = 20f;
    private int iCar;
    private int iType;
    protected override IEnumerator SpawnCoroutine()
    {
        for (iCar = 0; iCar < numCars; iCar++)
        {
            for (iType = 0; iType < 2; iType++)
            {
                yield return new WaitForSeconds(3);
                Spawn(cars[iType], SpawnPoint.position, SpawnPoint.rotation, Track, false);
            }
        }       
    }
}


// for (iCar = 0; iCar<numCars; iCar++)
//        {
//            yield return new WaitForSeconds(3);
//            Spawn(AutonomousVehicle, SpawnPoint.position, SpawnPoint.rotation, Track, false);
//        } 