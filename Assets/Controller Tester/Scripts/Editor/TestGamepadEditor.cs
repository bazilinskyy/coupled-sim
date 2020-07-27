using UnityEngine;
using UnityEditor;
using System.Collections;

[InitializeOnLoad]
public static class TestGamepadEditor
{

    static TestGamepadEditor()
    {
        EditorApplication.hierarchyWindowChanged += OnHierarchyChange;
    }
    static void OnHierarchyChange()
    {

        if (EditorApplication.currentScene.Contains("controllerTest")) 
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            
            //add our example axes (if not already present when we read the file below)
            SerializedObject serializedObject = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
            SerializedProperty axesProperty = serializedObject.FindProperty("m_Axes");
            SerializedProperty childElement;

            bool isAGGExampleAxesPresent = false;

            int count = axesProperty.arraySize;

            for (int i = 0; i < count; i++)
            {
                childElement = axesProperty.GetArrayElementAtIndex(i);

                if (GetChildProperty(childElement, "m_Name").stringValue.Equals("Axis 1"))
                {
                    isAGGExampleAxesPresent = true;
                    break;

                }
            }

            //the examples were not present add them and save the file (there are 20 example axes)
            //unfortunately have to add manually
            if (!isAGGExampleAxesPresent)
            {
                axesProperty.arraySize += 28;
                serializedObject.ApplyModifiedProperties();


                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 1";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 0;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 2";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 1;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 3";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 2;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 4";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 3;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 5";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 4;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 6";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 5;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 7";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 6;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 8";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 7;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 9";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 8;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 10";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 9;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 11";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 10;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 12";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 11;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 13";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 12;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 14";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 13;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 15";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 14;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 16";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 15;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 17";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 16;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 18";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 17;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 19";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 18;
                GetChildProperty(childElement, "joyNum").intValue = 0;
                count++;

                childElement = axesProperty.GetArrayElementAtIndex(count);

                GetChildProperty(childElement, "m_Name").stringValue = "Axis 20";
                GetChildProperty(childElement, "descriptiveName").stringValue = "";
                GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
                GetChildProperty(childElement, "negativeButton").stringValue = "";
                GetChildProperty(childElement, "positiveButton").stringValue = "";
                GetChildProperty(childElement, "altNegativeButton").stringValue = "";
                GetChildProperty(childElement, "altPositiveButton").stringValue = "";
                GetChildProperty(childElement, "gravity").floatValue = 0f;
                GetChildProperty(childElement, "dead").floatValue = .5f;
                GetChildProperty(childElement, "sensitivity").floatValue = 1f;
                GetChildProperty(childElement, "snap").boolValue = false;
                GetChildProperty(childElement, "invert").boolValue = false;
                GetChildProperty(childElement, "type").intValue = 2;
                GetChildProperty(childElement, "axis").intValue = 19;
                GetChildProperty(childElement, "joyNum").intValue = 0;
				count++;

				childElement = axesProperty.GetArrayElementAtIndex(count);

				GetChildProperty(childElement, "m_Name").stringValue = "Axis 21";
				GetChildProperty(childElement, "descriptiveName").stringValue = "";
				GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
				GetChildProperty(childElement, "negativeButton").stringValue = "";
				GetChildProperty(childElement, "positiveButton").stringValue = "";
				GetChildProperty(childElement, "altNegativeButton").stringValue = "";
				GetChildProperty(childElement, "altPositiveButton").stringValue = "";
				GetChildProperty(childElement, "gravity").floatValue = 0f;
				GetChildProperty(childElement, "dead").floatValue = .5f;
				GetChildProperty(childElement, "sensitivity").floatValue = 1f;
				GetChildProperty(childElement, "snap").boolValue = false;
				GetChildProperty(childElement, "invert").boolValue = false;
				GetChildProperty(childElement, "type").intValue = 2;
				GetChildProperty(childElement, "axis").intValue = 20;
				GetChildProperty(childElement, "joyNum").intValue = 0;
				count++;
				
				childElement = axesProperty.GetArrayElementAtIndex(count);

				GetChildProperty(childElement, "m_Name").stringValue = "Axis 22";
				GetChildProperty(childElement, "descriptiveName").stringValue = "";
				GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
				GetChildProperty(childElement, "negativeButton").stringValue = "";
				GetChildProperty(childElement, "positiveButton").stringValue = "";
				GetChildProperty(childElement, "altNegativeButton").stringValue = "";
				GetChildProperty(childElement, "altPositiveButton").stringValue = "";
				GetChildProperty(childElement, "gravity").floatValue = 0f;
				GetChildProperty(childElement, "dead").floatValue = .5f;
				GetChildProperty(childElement, "sensitivity").floatValue = 1f;
				GetChildProperty(childElement, "snap").boolValue = false;
				GetChildProperty(childElement, "invert").boolValue = false;
				GetChildProperty(childElement, "type").intValue = 2;
				GetChildProperty(childElement, "axis").intValue = 21;
				GetChildProperty(childElement, "joyNum").intValue = 0;
				count++;
				
				childElement = axesProperty.GetArrayElementAtIndex(count);

				GetChildProperty(childElement, "m_Name").stringValue = "Axis 23";
				GetChildProperty(childElement, "descriptiveName").stringValue = "";
				GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
				GetChildProperty(childElement, "negativeButton").stringValue = "";
				GetChildProperty(childElement, "positiveButton").stringValue = "";
				GetChildProperty(childElement, "altNegativeButton").stringValue = "";
				GetChildProperty(childElement, "altPositiveButton").stringValue = "";
				GetChildProperty(childElement, "gravity").floatValue = 0f;
				GetChildProperty(childElement, "dead").floatValue = .5f;
				GetChildProperty(childElement, "sensitivity").floatValue = 1f;
				GetChildProperty(childElement, "snap").boolValue = false;
				GetChildProperty(childElement, "invert").boolValue = false;
				GetChildProperty(childElement, "type").intValue = 2;
				GetChildProperty(childElement, "axis").intValue = 22;
				GetChildProperty(childElement, "joyNum").intValue = 0;
				count++;
				
				childElement = axesProperty.GetArrayElementAtIndex(count);

				GetChildProperty(childElement, "m_Name").stringValue = "Axis 24";
				GetChildProperty(childElement, "descriptiveName").stringValue = "";
				GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
				GetChildProperty(childElement, "negativeButton").stringValue = "";
				GetChildProperty(childElement, "positiveButton").stringValue = "";
				GetChildProperty(childElement, "altNegativeButton").stringValue = "";
				GetChildProperty(childElement, "altPositiveButton").stringValue = "";
				GetChildProperty(childElement, "gravity").floatValue = 0f;
				GetChildProperty(childElement, "dead").floatValue = .5f;
				GetChildProperty(childElement, "sensitivity").floatValue = 1f;
				GetChildProperty(childElement, "snap").boolValue = false;
				GetChildProperty(childElement, "invert").boolValue = false;
				GetChildProperty(childElement, "type").intValue = 2;
				GetChildProperty(childElement, "axis").intValue = 23;
				GetChildProperty(childElement, "joyNum").intValue = 0;
				count++;
				
				childElement = axesProperty.GetArrayElementAtIndex(count);

				GetChildProperty(childElement, "m_Name").stringValue = "Axis 25";
				GetChildProperty(childElement, "descriptiveName").stringValue = "";
				GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
				GetChildProperty(childElement, "negativeButton").stringValue = "";
				GetChildProperty(childElement, "positiveButton").stringValue = "";
				GetChildProperty(childElement, "altNegativeButton").stringValue = "";
				GetChildProperty(childElement, "altPositiveButton").stringValue = "";
				GetChildProperty(childElement, "gravity").floatValue = 0f;
				GetChildProperty(childElement, "dead").floatValue = .5f;
				GetChildProperty(childElement, "sensitivity").floatValue = 1f;
				GetChildProperty(childElement, "snap").boolValue = false;
				GetChildProperty(childElement, "invert").boolValue = false;
				GetChildProperty(childElement, "type").intValue = 2;
				GetChildProperty(childElement, "axis").intValue = 24;
				GetChildProperty(childElement, "joyNum").intValue = 0;
				count++;
				
				childElement = axesProperty.GetArrayElementAtIndex(count);

				GetChildProperty(childElement, "m_Name").stringValue = "Axis 26";
				GetChildProperty(childElement, "descriptiveName").stringValue = "";
				GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
				GetChildProperty(childElement, "negativeButton").stringValue = "";
				GetChildProperty(childElement, "positiveButton").stringValue = "";
				GetChildProperty(childElement, "altNegativeButton").stringValue = "";
				GetChildProperty(childElement, "altPositiveButton").stringValue = "";
				GetChildProperty(childElement, "gravity").floatValue = 0f;
				GetChildProperty(childElement, "dead").floatValue = .5f;
				GetChildProperty(childElement, "sensitivity").floatValue = 1f;
				GetChildProperty(childElement, "snap").boolValue = false;
				GetChildProperty(childElement, "invert").boolValue = false;
				GetChildProperty(childElement, "type").intValue = 2;
				GetChildProperty(childElement, "axis").intValue = 25;
				GetChildProperty(childElement, "joyNum").intValue = 0;
				count++;
				
				childElement = axesProperty.GetArrayElementAtIndex(count);

				GetChildProperty(childElement, "m_Name").stringValue = "Axis 27";
				GetChildProperty(childElement, "descriptiveName").stringValue = "";
				GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
				GetChildProperty(childElement, "negativeButton").stringValue = "";
				GetChildProperty(childElement, "positiveButton").stringValue = "";
				GetChildProperty(childElement, "altNegativeButton").stringValue = "";
				GetChildProperty(childElement, "altPositiveButton").stringValue = "";
				GetChildProperty(childElement, "gravity").floatValue = 0f;
				GetChildProperty(childElement, "dead").floatValue = .5f;
				GetChildProperty(childElement, "sensitivity").floatValue = 1f;
				GetChildProperty(childElement, "snap").boolValue = false;
				GetChildProperty(childElement, "invert").boolValue = false;
				GetChildProperty(childElement, "type").intValue = 2;
				GetChildProperty(childElement, "axis").intValue = 26;
				GetChildProperty(childElement, "joyNum").intValue = 0;
				count++;
				
				childElement = axesProperty.GetArrayElementAtIndex(count);

				GetChildProperty(childElement, "m_Name").stringValue = "Axis 28";
				GetChildProperty(childElement, "descriptiveName").stringValue = "";
				GetChildProperty(childElement, "descriptiveNegativeName").stringValue = "";
				GetChildProperty(childElement, "negativeButton").stringValue = "";
				GetChildProperty(childElement, "positiveButton").stringValue = "";
				GetChildProperty(childElement, "altNegativeButton").stringValue = "";
				GetChildProperty(childElement, "altPositiveButton").stringValue = "";
				GetChildProperty(childElement, "gravity").floatValue = 0f;
				GetChildProperty(childElement, "dead").floatValue = .5f;
				GetChildProperty(childElement, "sensitivity").floatValue = 1f;
				GetChildProperty(childElement, "snap").boolValue = false;
				GetChildProperty(childElement, "invert").boolValue = false;
				GetChildProperty(childElement, "type").intValue = 2;
				GetChildProperty(childElement, "axis").intValue = 27;
				GetChildProperty(childElement, "joyNum").intValue = 0;

                serializedObject.ApplyModifiedProperties();
            }


            //we only need to execute once on our scenes            
            EditorApplication.hierarchyWindowChanged -= OnHierarchyChange;
        }

    }



    public static SerializedProperty GetChildProperty(SerializedProperty parent, string name)
    {

        //copy so we don't iterate original
        SerializedProperty copiedProperty = parent.Copy();

        bool moreChildren = true;

        //step one level into child
        copiedProperty.Next(true);

        //iterate on all properties one level deep
        while (moreChildren)
        {
            //found the child we were looking for
            if (copiedProperty.name.Equals(name))
                return copiedProperty;

            //move to the next property
            moreChildren = copiedProperty.Next(false);
        }

        //if we get here we didn't find it
        return null;
    }

}