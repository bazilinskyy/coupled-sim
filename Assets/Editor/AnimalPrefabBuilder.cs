using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the animal prefabs from the CC0 Quaternius models in Assets/ThirdParty/Quaternius/Animals:
/// real body size, an animator with idle/walk/run/eat (and flight for pigeons), AnimalAgent with
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
    }

    static readonly Spec[] Specs =
    {
        new Spec { Species = AnimalSpecies.Dog, File = "ShibaInu", Length = 0.75f, Walk = 1.3f, Run = 6f, Shy = 0f, Home = 30f },
        new Spec { Species = AnimalSpecies.Husky, File = "Husky", Length = 1.0f, Walk = 1.4f, Run = 7f, Shy = 0f, Home = 30f },
        new Spec { Species = AnimalSpecies.Cat, File = "Cat", Length = 0.5f, Walk = 0.9f, Run = 5f, Shy = 2.5f, Home = 20f },
        new Spec { Species = AnimalSpecies.Pigeon, File = "Pigeon", Length = 0.32f, Walk = 0.6f, Run = 1.5f, Shy = 2.5f, Fly = true, Home = 15f },
        new Spec { Species = AnimalSpecies.Fox, File = "Fox", Length = 1.0f, Walk = 1.2f, Run = 8f, Shy = 8f, Home = 40f },
        new Spec { Species = AnimalSpecies.Deer, File = "Deer", Length = 1.6f, Walk = 1.3f, Run = 10f, Shy = 15f, Home = 60f },
        new Spec { Species = AnimalSpecies.Horse, File = "Horse", Length = 2.4f, Walk = 1.6f, Run = 9f, Shy = 4f, Home = 40f },
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
        var path = $"{Src}/{s.File}.glb";
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
        var states = new (string name, AnimationClip clip)[]
        {
            ("Idle", Find("Idle")),
            ("Walk", Find("Walk")),
            ("Run", Find("Gallop", "Run")),
            ("Eat", Find("Eating", "Idle_Eating", "Yes", "Idle_2_HeadLow", "Idle_Headlow")),
            ("Fly", s.Fly ? Find("Jump") : null),
        };
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
            inst.transform.SetParent(root.transform, false);
            // real size: scale the model so its body length matches
            var b = new Bounds(inst.transform.position, Vector3.zero);
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            float length = Mathf.Max(b.size.z, b.size.x);
            float scale = length > 1e-3f ? s.Length / length : 1f;
            inst.transform.localScale = Vector3.one * scale;
            b = new Bounds(inst.transform.position, Vector3.zero);
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            inst.transform.localPosition = new Vector3(0, -b.min.y, 0);
            if (s.Species == AnimalSpecies.Pigeon) Desaturate(inst, s.Species);
            if (!inst.TryGetComponent<Animator>(out var animator)) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

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
            Debug.Log($"AnimalPrefabBuilder: {dst} (scale {scale:F3}, clips {string.Join(", ", states.Where(x => x.clip != null).Select(x => x.name + "=" + Clean(x.clip.name)))})");
            return AssetDatabase.LoadAssetAtPath<GameObject>(dst);
        }
        finally
        {
            Object.DestroyImmediate(root);
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
