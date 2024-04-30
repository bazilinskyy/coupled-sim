using UnityEditor;
using UnityEngine;


namespace SOSXR
{
    /// <summary>
    ///     From: https://gist.github.com/LotteMakesStuff/c0a3b404524be57574ffa5f8270268ea
    /// </summary>
    [CustomPropertyDrawer(typeof(DisableEditingAttribute))]
    public class DisableEditingPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label);
            GUI.enabled = true;
        }
    }
}