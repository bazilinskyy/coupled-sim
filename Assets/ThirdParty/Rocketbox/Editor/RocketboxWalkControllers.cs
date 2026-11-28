using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the map pedestrians' walk controllers (Assets/Resources/Pedestrians/Walk_Male and Walk_Female) from the Rocketbox
/// motion-capture clips in Assets/ThirdParty/Rocketbox/Animations: one blend tree on WalkSpeed (m/s for a person of human
/// scale 1) from an idle through a slow, a normal and a fast walk, each placed at its clip's own speed, so the feet keep
/// pace with the body at every speed between them. AIPedestrian sets WalkSpeed and plays the clips faster or slower outside
/// that range; MapTraffic gives each walker one of the normal walks (an override of "neutral") and its own step phase.
/// Run it again after changing the clips: Tools > Coupled Sim > Build pedestrian walk controllers (or -executeMethod
/// RocketboxWalkControllers.Build).
/// </summary>
public static class RocketboxWalkControllers
{
    public const string OutDir = "Assets/Resources/Pedestrians";

    [MenuItem("Tools/Coupled Sim/Build pedestrian walk controllers")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Resources", "Pedestrians");
        foreach (var (sex, name) in new[] { ("m", "Walk_Male"), ("f", "Walk_Female") })
        {
            AnimationClip Clip(string n)
            {
                var path = RocketboxImporter.Animations + sex + "_" + n + ".fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) throw new System.Exception("RocketboxWalkControllers: no clip in " + path);
                return clip;
            }
            var idle = Clip("idle_breathe_01");
            var walks = new[] { Clip("walk_slow_01"), Clip("walk_neutral_01"), Clip("walk_fast_01") };
            var speeds = walks.Select(c => c.averageSpeed.magnitude).ToArray();
            for (int i = 1; i < speeds.Length; i++) if (speeds[i] <= speeds[i - 1] + 0.05f) speeds[i] = speeds[i - 1] + 0.05f;

            var path0 = $"{OutDir}/{name}.controller";
            AssetDatabase.DeleteAsset(path0);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path0);
            ac.AddParameter("WalkSpeed", AnimatorControllerParameterType.Float);
            // (the old controller's parameter: AIPedestrian still sets it, and a missing one logs a warning every frame)
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            // the range the blend covers, read by AIPedestrian (playing the clips slower below it and faster above)
            ac.AddParameter(new AnimatorControllerParameter { name = "SlowWalk", type = AnimatorControllerParameterType.Float, defaultFloat = speeds[0] });
            ac.AddParameter(new AnimatorControllerParameter { name = "FastWalk", type = AnimatorControllerParameterType.Float, defaultFloat = speeds[speeds.Length - 1] });

            var sm = ac.layers[0].stateMachine;
            var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "WalkSpeed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, ac);
            tree.AddChild(idle, 0f);
            for (int i = 0; i < walks.Length; i++) tree.AddChild(walks[i], speeds[i]);
            var state = sm.AddState("Locomotion");
            state.motion = tree;
            state.writeDefaultValues = true;
            sm.defaultState = state;
            // the other normal walks, which MapTraffic puts in place of walk_neutral_01 for some people (an override
            // controller): kept in a state that is never entered, so they are part of the controller and of a build
            var styles = new BlendTree { name = "Styles", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(styles, ac);
            foreach (var n in new[] { "walk_neutral_02", "walk_neutral_03", "walk_stroll_01" }) styles.AddChild(Clip(n));
            sm.AddState("Styles (not entered)").motion = styles;
            // (IK pass: AIPedestrian turns the head towards traffic and places the feet on kerbs)
            var layers = ac.layers;
            layers[0].iKPass = true;
            ac.layers = layers;
            EditorUtility.SetDirty(ac);
            Debug.Log($"RocketboxWalkControllers: {path0}: idle, " + string.Join(", ", walks.Select((c, i) => $"{c.name} at {speeds[i]:F2} m/s")));
        }
        AssetDatabase.SaveAssets();
    }
}
