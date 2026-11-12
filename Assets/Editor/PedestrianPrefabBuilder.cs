using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds AI pedestrian prefabs from humanoid character models (for example the Rocketbox avatars
/// in Assets/ThirdParty/Rocketbox), using an existing AI pedestrian prefab as the template: same
/// components and settings, animator controller, and the same synchronised/logged bones (mapped
/// by humanoid bone), with the collider scaled to the character's height.
/// Menu: Coupled Sim ➡️ Pedestrians ➡️ Build pedestrian prefabs from Rocketbox avatars.
/// </summary>
public static class PedestrianPrefabBuilder
{
    const string TemplatePath = "Assets/Prefabs/AIPedestrians/Pedestrian_Claudia.prefab";
    const string ControllerPath = "Assets/Prefabs/AIPedestrians/Pedestrian.controller";
    const string OutputDir = "Assets/Prefabs/AIPedestrians/Rocketbox";

    [MenuItem("Coupled Sim/Pedestrians/Build pedestrian prefabs from Rocketbox avatars")]
    public static void BuildRocketbox()
    {
        var models = AssetDatabase.FindAssets("t:Model", new[] { RocketboxImporter.Root.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx") && !p.Contains("_facial"))
            .ToList();
        EnableIkPass(ControllerPath);
        foreach (var m in models) Build(m, OutputDir);
        AssetDatabase.SaveAssets();
        Debug.Log($"PedestrianPrefabBuilder: built {models.Count} prefabs in {OutputDir}");
    }

    /// <summary>Turns on the IK pass of the base layer (needed for look-at and foot IK in AIPedestrian).</summary>
    public static void EnableIkPass(string controllerPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) return;
        var layers = controller.layers;
        if (layers.Length == 0 || layers[0].iKPass) return;
        layers[0].iKPass = true;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
    }

    public static string Build(string modelPath, string outputDir)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (template == null || model == null)
        {
            Debug.LogError($"PedestrianPrefabBuilder: missing template or model ({modelPath})");
            return null;
        }
        Directory.CreateDirectory(outputDir);

        var templateInstance = (GameObject)PrefabUtility.InstantiatePrefab(template);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        try
        {
            go.name = "Pedestrian_" + model.name;
            go.tag = template.tag;
            go.layer = template.layer;

            var tAnimator = templateInstance.GetComponent<Animator>();
            var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            animator.runtimeAnimatorController = tAnimator.runtimeAnimatorController;
            animator.applyRootMotion = tAnimator.applyRootMotion;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; // off-screen pedestrians cost little
            animator.enabled = tAnimator.enabled;

            foreach (var src in templateInstance.GetComponents<Component>())
            {
                if (src is Transform || src is Animator) continue;
                var dst = go.AddComponent(src.GetType());
                EditorUtility.CopySerialized(src, dst);
            }

            // height-dependent collider (children are smaller)
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float height = Mathf.Max(0.8f, bounds.size.y);
            if (go.TryGetComponent<CharacterController>(out var cc))
            {
                cc.height = height;
                cc.center = new Vector3(0, height / 2f, 0);
                cc.radius = Mathf.Clamp(height * 0.15f, 0.2f, 0.4f);
            }

            var ai = go.GetComponent<AIPedestrian>();
            if (ai != null)
            {
                ai.animator = animator;
                ai.AgeGroup = model.name.Contains("Child") ? CoupledSim.Pedestrians.AgeGroup.Child : CoupledSim.Pedestrians.AgeGroup.Adult;
                ai.Behaviour = AIPedestrian.BehaviourMode.BehaviourModels;
            }

            // same logged/synced bones as the template, matched by humanoid bone
            var avatar = go.GetComponent<PlayerAvatar>();
            if (avatar != null)
            {
                tAnimator.enabled = true;
                animator.enabled = true;
                var map = new Dictionary<Transform, HumanBodyBones>();
                for (var b = HumanBodyBones.Hips; b < HumanBodyBones.LastBone; b++)
                {
                    var t = tAnimator.GetBoneTransform(b);
                    if (t != null && !map.ContainsKey(t)) map[t] = b;
                }
                var sync = new List<Transform>();
                var missing = new List<string>();
                foreach (var t in templateInstance.GetComponent<PlayerAvatar>().SyncTransforms)
                {
                    Transform mapped = null;
                    if (t != null && map.TryGetValue(t, out var bone)) mapped = animator.GetBoneTransform(bone);
                    if (mapped == null)
                    {
                        // e.g. the skeleton root: use the parent of the hips
                        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                        mapped = hips != null && hips.parent != null ? hips.parent : go.transform;
                        missing.Add(t != null ? t.name : "null");
                    }
                    sync.Add(mapped);
                }
                avatar.SyncTransforms = sync.ToArray();
                animator.enabled = tAnimator.enabled = false;
                if (missing.Count > 0)
                    Debug.Log($"PedestrianPrefabBuilder: {go.name}: non-humanoid template bones mapped to the skeleton root: {string.Join(", ", missing)}");
            }

            var path = $"{outputDir}/{go.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            return path;
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(templateInstance);
        }
    }
}
