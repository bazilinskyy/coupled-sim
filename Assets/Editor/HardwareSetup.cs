using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

/// <summary>
/// Hardware set-up: XR Plug-in Management with OpenXR for PC headsets (not started at launch:
/// XRSession starts it for HMD roles), OpenXR controller and eye-gaze profiles, raw joystick axes for
/// the steering wheel profiles, and the wheel calibration screen in StartScene.
/// Menu: Coupled Sim ➡️ Hardware ➡️ Set up.
/// </summary>
public static class HardwareSetup
{
    [MenuItem("Coupled Sim/Hardware/Set up")]
    public static void SetUp()
    {
        SetUpXR();
        SetUpInputAxes();
        AddCalibrationToStartScene();
        AssetDatabase.SaveAssets();
        Debug.Log("HardwareSetup: done");
    }

    static void SetUpXR()
    {
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
        {
            System.IO.Directory.CreateDirectory("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
        }
        var settings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
            settings.name = "Standalone Settings";
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            manager.name = "Standalone Providers";
            settings.Manager = manager;
            AssetDatabase.AddObjectToAsset(settings, perTarget);
            AssetDatabase.AddObjectToAsset(manager, perTarget);
            perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Standalone, settings);
        }
        // only start VR when a participant uses a headset (see XRSession)
        settings.InitManagerOnStart = false;
        XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Standalone);
        EditorUtility.SetDirty(perTarget);
        EditorUtility.SetDirty(settings);

        // create the feature assets of all installed OpenXR features first
        UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
        var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        if (openxr != null)
        {
            foreach (var feature in openxr.GetFeatures())
            {
                var n = feature.GetType().Name;
                // controller profiles of common PC headsets, and eye gaze for eye-tracking headsets
                if (n.EndsWith("ControllerProfile") || n == "EyeGazeInteraction" || n == "KHRSimpleControllerProfile")
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                }
            }
            EditorUtility.SetDirty(openxr);
        }
    }

    static void SetUpInputAxes()
    {
        var inputManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0];
        var so = new SerializedObject(inputManager);
        var axes = so.FindProperty("m_Axes");
        var mapped = new[] { "Throttle", "Break", "Horizontal", "forward", "reverse", "blinker_left", "blinker_right", "blinker_clear" };
        for (int i = axes.arraySize - 1; i >= 0; i--)
        {
            var a = axes.GetArrayElementAtIndex(i);
            var name = a.FindPropertyRelative("m_Name").stringValue;
            int type = a.FindPropertyRelative("type").intValue;
            var pos = a.FindPropertyRelative("positiveButton").stringValue;
            // the wheel goes through WheelProfile now; these joystick bindings only fit the G27
            if (mapped.Contains(name) && (type == 2 || pos.StartsWith("joystick button"))) axes.DeleteArrayElementAtIndex(i);
            else if (name.StartsWith("Raw Axis ")) axes.DeleteArrayElementAtIndex(i);
        }
        for (int i = 0; i < WheelProfile.RawAxisCount; i++)
        {
            axes.InsertArrayElementAtIndex(axes.arraySize);
            var a = axes.GetArrayElementAtIndex(axes.arraySize - 1);
            a.FindPropertyRelative("m_Name").stringValue = "Raw Axis " + i;
            a.FindPropertyRelative("descriptiveName").stringValue = "Joystick axis " + i + " (steering wheel profiles)";
            a.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
            a.FindPropertyRelative("negativeButton").stringValue = "";
            a.FindPropertyRelative("positiveButton").stringValue = "";
            a.FindPropertyRelative("altNegativeButton").stringValue = "";
            a.FindPropertyRelative("altPositiveButton").stringValue = "";
            a.FindPropertyRelative("gravity").floatValue = 0f;
            a.FindPropertyRelative("dead").floatValue = 0.001f;
            a.FindPropertyRelative("sensitivity").floatValue = 1f;
            a.FindPropertyRelative("snap").boolValue = false;
            a.FindPropertyRelative("invert").boolValue = false;
            a.FindPropertyRelative("type").intValue = 2;
            a.FindPropertyRelative("axis").intValue = i;
            a.FindPropertyRelative("joyNum").intValue = 0;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    static void AddCalibrationToStartScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Scenes/StartScene.unity");
        var nm = Object.FindAnyObjectByType<NetworkingManager>();
        if (nm.GetComponent<WheelCalibration>() == null) nm.gameObject.AddComponent<WheelCalibration>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
