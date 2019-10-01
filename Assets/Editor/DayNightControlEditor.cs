using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(DayNightControl))]
public class DayNightControlEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DayNightControl myTarget = (DayNightControl)target;

        DrawDefaultInspector();

        if (GUILayout.Button("Night"))
        {
            myTarget.InitNight();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        if (GUILayout.Button("Day"))
        {
            myTarget.InitDay();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
