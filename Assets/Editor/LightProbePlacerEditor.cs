using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LightProbePlacer))]
public class LightProbePlacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LightProbePlacer myTarget = (LightProbePlacer)target;
        DrawDefaultInspector();

        if (GUILayout.Button("Place Light Probes"))
        {
            myTarget.PlaceLightProbes();
        }
    }
}
