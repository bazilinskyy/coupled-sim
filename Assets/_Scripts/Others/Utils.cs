using System;
using UnityEngine;

public static class Utils
{
	public static GameObject GetPlayer()
    {
        return GameObject.FindGameObjectWithTag("Player");
    }

    public static ExperimentInput GetExperimentInput()
    {
        return GameObject.FindGameObjectWithTag("Player").GetComponent<ExperimentInput>();
    }
}
