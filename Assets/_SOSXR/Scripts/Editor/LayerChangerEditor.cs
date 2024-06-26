using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LayerChanger))]
public class LayerChangerEditor : Editor
{
    private SerializedProperty objectToLayerChangeProp;
    private SerializedProperty originalLayerProp;
    private SerializedProperty setToLayerProp;

    private void OnEnable()
    {
        objectToLayerChangeProp = serializedObject.FindProperty("objectToLayerChange");
        originalLayerProp = serializedObject.FindProperty("originalLayer");
        setToLayerProp = serializedObject.FindProperty("setToLayer");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(objectToLayerChangeProp, new GUIContent("Object"));
        EditorGUILayout.LabelField("Original Layer", LayerMask.LayerToName(originalLayerProp.intValue));
        setToLayerProp.intValue = EditorGUILayout.LayerField("Set To Layer", setToLayerProp.intValue);

        if (GUILayout.Button("Get Layer"))
        {
            ((LayerChanger)target).GetLayer();
        }

        if (GUILayout.Button("Set Layer"))
        {
            ((LayerChanger)target).SetLayer();
        }

        serializedObject.ApplyModifiedProperties();
    }
}