using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the animal prefabs from realistic animated models (CC BY 4.0, Sketchfab; credits in
/// Assets/ThirdParty/Animals/CREDITS.md), and the CC0 Quaternius models in Assets/ThirdParty/Quaternius/Animals
/// where no realistic one is free (husky) or a realistic one is missing: real body size, an animator with idle/walk/run/eat (and flight for pigeons), AnimalAgent with
/// species behaviour, collider; registers them in Resources/AnimalAssets and adds an AnimalSpawner
/// to Base and the example experiments. Menu: Coupled Sim ➡️ Animals ➡️ Build.
/// </summary>
public static class AnimalPrefabBuilder
{
    const string Src = "Assets/ThirdParty/Quaternius/Animals";
    const string Dst = "Assets/Prefabs/Animals";

    struct Spec
    {
        public AnimalSpecies Species; public string File; public float Length; public float Walk, Run, Shy; public bool Fly; public float Home;
        public string Model;                       // realistic model (else the Quaternius one, File)
        public string[] Idle, WalkClip, RunClip, Eat, FlyClip;   // the realistic model's clip names per state
        public string[] Remove;                    // the realistic model's nodes to leave out (display stands)
        public Color? Tint;                        // multiplies the realistic model's colours
        public float Yaw;                          // turns the realistic model so it faces +z
    }

    const string Realistic = "Assets/ThirdParty/Animals";

    static readonly Spec[] Specs =
    {
        new Spec { Species = AnimalSpecies.Dog, File = "ShibaInu", Length = 0.95f, Walk = 1.3f, Run = 6f, Shy = 0f, Home = 30f,
                   Model = Realistic + "/PitBull.glb", Idle = new[] { "IDdle" }, WalkClip = new[] { "Walkl" }, RunClip = new[] { "Run" }, Eat = new[] { "Eat" } },
        new Spec { Species = AnimalSpecies.Husky, File = "Husky", Length = 1.0f, Walk = 1.4f, Run = 7f, Shy = 0f, Home = 30f },
        new Spec { Species = AnimalSpecies.Cat, File = "Cat", Length = 0.6f, Walk = 0.9f, Run = 5f, Shy = 2.5f, Home = 20f,
                   Model = Realistic + "/SomaliCat.glb", Yaw = 180f, Remove = new[] { "Object_54" }, Idle = new[] { "Idle" }, WalkClip = new[] { "WalkClean" }, RunClip = new[] { "WalkClean" }, Eat = new[] { "SittingIdle" } },
        new Spec { Species = AnimalSpecies.Pigeon, File = "Pigeon", Length = 0.33f, Tint = new Color(0.56f, 0.6f, 0.68f), Walk = 0.6f, Run = 1.5f, Shy = 2.5f, Fly = true, Home = 15f,
                   Model = Realistic + "/Pigeon.glb", Idle = new[] { "Standing Idle" }, WalkClip = new[] { "Standing Idle" }, RunClip = new[] { "Standing Idle" }, Eat = new[] { "Standing Idle" }, FlyClip = new[] { "Flapping" } },
        new Spec { Species = AnimalSpecies.Fox, File = "Fox", Length = 1.0f, Walk = 1.2f, Run = 8f, Shy = 8f, Home = 40f },   // the free animated foxes are cartoons
        new Spec { Species = AnimalSpecies.Deer, File = "Deer", Length = 1.6f, Walk = 1.3f, Run = 10f, Shy = 15f, Home = 60f,
                   Model = Realistic + "/Doe.glb", Idle = new[] { "Stand_Idle_02", "Stand_Breathing_01" }, WalkClip = new[] { "WalkSlow" }, RunClip = new[] { "Run", "Trot" }, Eat = new[] { "Stand_Eating_01" } },
        new Spec { Species = AnimalSpecies.Horse, File = "Horse", Length = 2.4f, Walk = 1.6f, Run = 9f, Shy = 4f, Home = 40f,
                   Model = Realistic + "/Horse.glb", Idle = new[] { "1 Ilde" }, WalkClip = new[] { "Walk" }, RunClip = new[] { "Gallop" }, Eat = new[] { "6 Eat" } },
    };

    [MenuItem("Coupled Sim/Animals/Build prefabs and add to experiments")]
    public static void Build()
    {
        Directory.CreateDirectory(Dst + "/Animation");
        Directory.CreateDirectory(Dst + "/Materials");
        var assets = AssetDatabase.LoadAssetAtPath<AnimalAssets>("Assets/Resources/AnimalAssets.asset");
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<AnimalAssets>();
            AssetDatabase.CreateAsset(assets, "Assets/Resources/AnimalAssets.asset");
        }
        assets.Animals = Specs.Select(s => new AnimalAssets.Entry { Species = s.Species, Prefab = BuildOne(s) }).Where(e => e.Prefab != null).ToArray();
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        AddToExperiments();
    }

    static string Clean(string clipName) => clipName.Split('|').Last();

    static GameObject BuildOne(Spec s)
    {
        bool realistic = s.Model != null && AssetDatabase.LoadAssetAtPath<GameObject>(s.Model) != null;
        var path = realistic ? s.Model : $"{Src}/{s.File}.glb";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null) { Debug.LogWarning("AnimalPrefabBuilder: missing " + path); return null; }
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
        AnimationClip Find(params string[] names)
        {
            foreach (var n in names)
            {
                var exact = clips.FirstOrDefault(c => Clean(c.name) == n);
                if (exact != null) return exact;
            }
            foreach (var n in names)
            {
                var part = clips.FirstOrDefault(c => Clean(c.name).StartsWith(n));
                if (part != null) return part;
            }
            return null;
        }
        // glTFast imports the clips for Mecanim, looping
        var states = realistic
            ? new (string name, AnimationClip clip)[]
            {
                ("Idle", Find(s.Idle)),
                ("Walk", Find(s.WalkClip)),
                ("Run", Find(s.RunClip)),
                ("Eat", Find(s.Eat)),
                ("Fly", s.Fly ? Find(s.FlyClip) : null),
            }
            : new (string name, AnimationClip clip)[]
            {
                ("Idle", Find("Idle")),
                ("Walk", Find("Walk")),
                ("Run", Find("Gallop", "Run")),
                ("Eat", Find("Eating", "Idle_Eating", "Yes", "Idle_2_HeadLow", "Idle_Headlow")),
                ("Fly", s.Fly ? Find("Jump") : null),
            };
        if (realistic)
            foreach (var (name, clip) in states)
                if (clip == null && (name != "Fly" || s.Fly)) Debug.LogWarning($"AnimalPrefabBuilder: {s.Species}: no clip for {name} in {path} (has {string.Join(", ", clips.Select(c => Clean(c.name)))})");
        var controllerPath = $"{Dst}/Animation/{s.Species}.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;
        foreach (var (name, clip) in states)
        {
            if (clip == null) continue;
            var st = sm.AddState(name);
            st.motion = clip;
            if (name == "Idle") sm.defaultState = st;
        }

        var root = new GameObject("Animal_" + s.Species);
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // size, height and turn go on a parent: the clips may move the model's own root node (a doe's walk
            // replaced its offset and sank it 1 m into the ground)
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            inst.transform.SetParent(holder, false);
            if (realistic && s.Remove != null)
                foreach (var n in s.Remove)
                    foreach (var t in inst.GetComponentsInChildren<Transform>(true).Where(t => t.name == n).ToArray()) Object.DestroyImmediate(t.gameObject);
            if (realistic && s.Tint.HasValue) Tint(inst, s.Species, s.Tint.Value);
            if (realistic && s.Yaw != 0f) holder.localRotation = Quaternion.Euler(0f, s.Yaw, 0f);
            if (s.Species == AnimalSpecies.Pigeon && !realistic) Desaturate(inst, s.Species);
            if (!inst.TryGetComponent<Animator>(out var animator)) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            // real size, measured in the walking pose (a rest pose may be sitting or have the wings spread): the body's
            // length scaled to the species', its lowest point over the step on the ground
            var pose = states.FirstOrDefault(x => x.name == "Walk").clip ?? states.FirstOrDefault(x => x.name == "Idle").clip;
            var b = Posed(root.transform, inst, pose);
            float length = Mathf.Max(b.size.z, b.size.x);
            float scale = length > 1e-3f ? s.Length / length : 1f;
            holder.localScale = Vector3.one * scale;
            b = Posed(root.transform, inst, pose);
            holder.localPosition = new Vector3(0f, -b.min.y, 0f);
            b = Posed(root.transform, inst, pose);
            // walks and runs that carry the body forward: the hips held over the ground (the agent does the moving)
            var walk = states.FirstOrDefault(x => x.name == "Walk").clip;
            var run = states.FirstOrDefault(x => x.name == "Run").clip;
            var (bone, drift) = Drift(inst, holder, walk);
            var (bone2, drift2) = Drift(inst, holder, run);
            if (bone == null) { bone = bone2; }
            if (bone != null && Mathf.Max(drift, drift2) * scale > 0.1f * s.Length)
            {
                var idle = states.FirstOrDefault(x => x.name == "Idle").clip;
                AnimationMode.StartAnimationMode();
                Vector3 rest;
                try
                {
                    if (idle != null) { AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(inst, idle, 0f); AnimationMode.EndSampling(); }
                    rest = holder.InverseTransformPoint(bone.position);
                }
                finally { AnimationMode.StopAnimationMode(); }
                var lk = inst.AddComponent<RootMotionLock>();
                lk.Bone = bone; lk.Space = holder; lk.Rest = new Vector2(rest.x, rest.z);
                Debug.Log($"AnimalPrefabBuilder: {s.Species}: walk/run move the body {Mathf.Max(drift, drift2) * scale:F2} m per loop: hips ({bone.name}) held in place");
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = Mathf.Max(0.08f, b.size.x / 2f);
            col.height = Mathf.Max(col.radius * 2f, b.size.z);
            col.center = new Vector3(0, b.size.y / 2f, 0);

            var agent = root.AddComponent<AnimalAgent>();
            agent.Species = s.Species;
            agent.WalkSpeed = s.Walk;
            agent.RunSpeed = s.Run;
            agent.PeopleShyDistance = s.Shy;
            agent.CanFly = s.Fly;
            agent.HomeRadius = s.Home;
            var dst = $"{Dst}/{s.Species}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, dst);
            Debug.Log($"AnimalPrefabBuilder: {dst} from {path} (scale {scale:F3}, walking {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m, clips {string.Join(", ", states.Where(x => x.clip != null).Select(x => x.name + "=" + Clean(x.clip.name)))})");
            return AssetDatabase.LoadAssetAtPath<GameObject>(dst);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>Bounds (in the root's space) of the drawn body in a clip's pose, over a few moments of the clip
    /// (sampled as the Animator plays it, then put back).</summary>
    static Bounds Posed(Transform root, GameObject model, AnimationClip clip)
    {
        // the animated body only, when there is one (a model's static parts may stand where the rest pose had them)
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Any(r => r is SkinnedMeshRenderer)) renderers = renderers.Where(r => r is SkinnedMeshRenderer).ToArray();
        Bounds Now()
        {
            bool first = true; var b = new Bounds();
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer smr)
                {
                    var m = new Mesh(); smr.BakeMesh(m, true);
                    foreach (var v in m.vertices) { var p = root.InverseTransformPoint(smr.transform.TransformPoint(v)); if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
                    Object.DestroyImmediate(m);
                }
                else
                {
                    var wb = r.bounds;
                    foreach (var c in new[] { wb.min, wb.max }) { var p = root.InverseTransformPoint(c); if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
                }
            }
            return b;
        }
        if (clip == null) return Now();
        // each moment's body: the largest size and the lowest point over the clip (the centre of the first moment)
        Bounds result = new Bounds(); Vector3 size = Vector3.zero; float low = float.MaxValue;
        AnimationMode.StartAnimationMode();
        try
        {
            for (int k = 0; k < 6; k++)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(model, clip, clip.length * k / 6f);
                AnimationMode.EndSampling();
                var b = Now();
                if (k == 0) result = b;
                size = Vector3.Max(size, b.size); low = Mathf.Min(low, b.min.y);
            }
        }
        finally { AnimationMode.StopAnimationMode(); }
        var c0 = result.center; c0.y = low + size.y / 2f;
        return new Bounds(c0, size);
    }

    /// <summary>The topmost bone whose position a clip animates, and how far it moves from the first frame to the last
    /// (in <paramref name="space"/>: forward drift of a walk or run that carries the body along).</summary>
    static (Transform bone, float drift) Drift(GameObject model, Transform space, AnimationClip clip)
    {
        if (clip == null) return (null, 0f);
        var bindings = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform) && b.propertyName.Contains("LocalPosition")).ToArray();
        if (bindings.Length == 0) return (null, 0f);
        int Depth(EditorCurveBinding b) => b.path.Length == 0 ? 0 : b.path.Count(c => c == '/') + 1;
        var top = bindings.OrderBy(Depth).First();
        var bone = model.transform.Find(top.path);
        if (bone == null || bone == model.transform) return (null, 0f);
        Vector3 At(float t)
        {
            AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(model, clip, t); AnimationMode.EndSampling();
            return space.InverseTransformPoint(bone.position);
        }
        AnimationMode.StartAnimationMode();
        try
        {
            var d = At(clip.length * 0.999f) - At(0f);
            d.y = 0f;
            return (bone, d.magnitude);
        }
        finally { AnimationMode.StopAnimationMode(); }
    }

    // a realistic model's colours multiplied (a white-textured pigeon to a city pigeon's blue-grey)
    static void Tint(GameObject inst, AnimalSpecies species, Color tint)
    {
        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                var path = $"{Dst}/Materials/{species}_{mats[i].name}_tint.mat";
                AssetDatabase.DeleteAsset(path);
                var m = new Material(mats[i]);
                foreach (var prop in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                    if (m.HasProperty(prop)) m.SetColor(prop, m.GetColor(prop) * tint);
                AssetDatabase.CreateAsset(m, path);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    // the stylised model's purple feathers to a city pigeon's blue-grey
    static void Desaturate(GameObject inst, AnimalSpecies species)
    {
        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                var path = $"{Dst}/Materials/{species}_{mats[i].name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(mats[i]);
                    foreach (var prop in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                    {
                        if (!m.HasProperty(prop)) continue;
                        var c = m.GetColor(prop);
                        Color.RGBToHSV(c, out var h, out var sat, out var v);
                        // keep light/dark and warm (beak, feet) parts, take the purple out
                        bool purple = h > 0.6f && h < 0.9f;
                        var grey = Color.HSVToRGB(0.6f, purple ? 0.12f : sat * 0.6f, Mathf.Lerp(v, 0.45f, purple ? 0.3f : 0f));
                        m.SetColor(prop, purple ? grey : c);
                    }
                    AssetDatabase.CreateAsset(m, path);
                }
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    static void AddToExperiments()
    {
        foreach (var name in new[] { "Base", "PedestrianCrossings", "Traffic", "SumoTraffic", "CityMap" })
        {
            var path = $"Assets/ExperimentDefinitions/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponentInChildren<AnimalSpawner>(true) != null) continue;
                var go = new GameObject("Animals");
                go.transform.SetParent(root.transform, false);
                var spawner = go.AddComponent<AnimalSpawner>();
                if (name == "CityMap")
                {
                    spawner.Radius = 120f;
                    spawner.Animals = new[]
                    {
                        new AnimalSpawner.Group { Species = AnimalSpecies.Pigeon, Count = 20 },
                        new AnimalSpawner.Group { Species = AnimalSpecies.Dog, Count = 3 },
                        new AnimalSpawner.Group { Species = AnimalSpecies.Husky, Count = 1 },
                        new AnimalSpawner.Group { Species = AnimalSpecies.Cat, Count = 3 },
                        new AnimalSpawner.Group { Species = AnimalSpecies.Fox, Count = 1 },
                    };
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("AnimalPrefabBuilder: animals added to " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
