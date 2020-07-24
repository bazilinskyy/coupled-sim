using System.Collections;
using System.Collections.Generic;
using UnityEditor;

[CustomEditor(typeof(Target))]
public class TargetEditor : Editor
    {

    SerializedProperty waypoint;
    SerializedProperty setDifficulty;
    SerializedProperty ID;
    SerializedProperty detected;
    private Target _target;


    void OnEnable()
    {
        waypoint = serializedObject.FindProperty("waypoint");
        setDifficulty = serializedObject.FindProperty("setDifficulty");
        ID = serializedObject.FindProperty("ID");
        detected = serializedObject.FindProperty("detected");
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(waypoint);
        EditorGUILayout.PropertyField(setDifficulty);
        EditorGUILayout.PropertyField(ID);
        EditorGUILayout.PropertyField(detected);


        _target = (Target)target;

        if(_target.setDifficulty.ToString() != setDifficulty.ToString())
        {
            //Use the setDifficulty method of Target to set to current selected difficulty in the inspector.

            _target.SetDifficulty((TargetDifficulty)setDifficulty.enumValueIndex);
        }
        serializedObject.ApplyModifiedProperties();
    }


}
