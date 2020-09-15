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

    SerializedProperty easy_6;
    SerializedProperty easy_5;
    SerializedProperty medium_4;
    SerializedProperty medium_3;
    SerializedProperty hard_2;
    SerializedProperty hard_1;

    private Target _target;


    void OnEnable()
    {
        waypoint = serializedObject.FindProperty("waypoint");
        setDifficulty = serializedObject.FindProperty("setDifficulty");
        ID = serializedObject.FindProperty("ID");
        detected = serializedObject.FindProperty("detected");

        easy_6 = serializedObject.FindProperty("easy_6");
        easy_5 = serializedObject.FindProperty("easy_5");
        medium_4 = serializedObject.FindProperty("medium_4");
        medium_3 = serializedObject.FindProperty("medium_3");
        hard_2 = serializedObject.FindProperty("hard_2");
        hard_1 = serializedObject.FindProperty("hard_1");
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(waypoint);
        EditorGUILayout.PropertyField(setDifficulty);
        EditorGUILayout.PropertyField(ID);
        EditorGUILayout.PropertyField(detected);
 /*       EditorGUILayout.PropertyField(easy_6);
        EditorGUILayout.PropertyField(easy_5);
        EditorGUILayout.PropertyField(medium_4);
        EditorGUILayout.PropertyField(medium_3);
        EditorGUILayout.PropertyField(hard_2);
        EditorGUILayout.PropertyField(hard_1);*/

        _target = (Target)target;

        if(_target.setDifficulty.ToString() != setDifficulty.ToString())
        {
            //Use the setDifficulty method of Target to set to current selected difficulty in the inspector.

            _target.SetDifficulty((TargetDifficulty)setDifficulty.enumValueIndex);
        }
        serializedObject.ApplyModifiedProperties();
    }


}
