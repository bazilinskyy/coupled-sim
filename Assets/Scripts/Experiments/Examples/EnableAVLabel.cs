using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableAVLabel : MonoBehaviour, IExperimentModifier
{
    public CarSpawnerBase [] carSpawners;
    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters)
    {
        foreach (var param in experimentParameters) {
            if (param.name == "target_labeled")
            {
                foreach (var carSpawner in carSpawners) {
                    carSpawner.spawnParams.Labeled = param.value.Equals("true");
                }
            }
        }
    }
}
