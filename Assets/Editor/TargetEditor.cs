using System.Collections;
using System.Collections.Generic;
using UnityEditor;

[CustomEditor(typeof(Target))]
public class TargetEditor : Editor
    {

    SerializedProperty waypoint;
    SerializedProperty difficulty;
    SerializedProperty ID;
    SerializedProperty detected;

    SerializedProperty easy;
    SerializedProperty medium;
    SerializedProperty hard;
    
    SerializedProperty afterTurn;
    SerializedProperty side;
    SerializedProperty difficultPosition;

    SerializedProperty hasBeenVisible;


    private Target _target;


    void OnEnable()
    {
        waypoint = serializedObject.FindProperty("waypoint");
        difficulty = serializedObject.FindProperty("difficulty");
        ID = serializedObject.FindProperty("ID");
        detected = serializedObject.FindProperty("detected");

        easy = serializedObject.FindProperty("easy");
        medium = serializedObject.FindProperty("medium");
        hard = serializedObject.FindProperty("hard");

        afterTurn = serializedObject.FindProperty("afterTurn");
        side = serializedObject.FindProperty("side");
        difficultPosition = serializedObject.FindProperty("difficultPosition");

        hasBeenVisible = serializedObject.FindProperty("hasBeenVisible");
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(waypoint);
        EditorGUILayout.PropertyField(difficulty);
        EditorGUILayout.PropertyField(ID);
        EditorGUILayout.PropertyField(detected);

        EditorGUILayout.PropertyField(afterTurn);
        EditorGUILayout.PropertyField(side);
        EditorGUILayout.PropertyField(difficultPosition);
        
        EditorGUILayout.PropertyField(hasBeenVisible);

        //Uncomment if you want to set these to different materials
        /* EditorGUILayout.PropertyField(easy);
         EditorGUILayout.PropertyField(medium);
         EditorGUILayout.PropertyField(hard);*/


        _target = (Target)target;

        if(_target.difficulty.ToString() != difficulty.ToString())
        {
            //Use the setDifficulty method of Target to set to current selected difficulty in the inspector.

            _target.SetDifficulty((TargetDifficulty)difficulty.enumValueIndex);
        }
        serializedObject.ApplyModifiedProperties();
    }


}
