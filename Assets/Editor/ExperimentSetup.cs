using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoupledSim.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Experiment setup in the editor (Coupled Sim ➡️ Experiments):
/// - Setup window: a new experiment from a template (map, roles, traffic, animals, time and
///   weather), registered in StartScene and the build settings; trial files with a preview of
///   each participant's order;
/// - validator: missing references, prefabs missing from the PlayerSystem and AI car lists,
///   scenes missing from the build settings, duplicate roles, wrong trials; runs before Play
///   (scenes are added to the build settings automatically).
/// </summary>
public static class ExperimentSetup
{
    public const string StartScenePath = "Assets/_Scenes/StartScene.unity";
    public const string OsmScenePath = "Assets/_Scenes/OsmMap.unity";
    public const string ExperimentFolder = "Assets/ExperimentDefinitions";
    const string ValidateBeforePlayKey = "CoupledSim.ValidateBeforePlay";

    public class Issue
    {
        public bool Error;
        public string Message;
        public Object Context;
        public override string ToString() => (Error ? "Error: " : "Warning: ") + Message;
    }

    // ---- StartScene access

    /// <summary>Runs <paramref name="action"/> with StartScene's NetworkingManager (opening the scene additively if needed).</summary>
    public static void WithStartScene(Action<NetworkingManager> action, bool save)
    {
        var scene = SceneManager.GetSceneByPath(StartScenePath);
        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Additive);
            opened = true;
        }
        try
        {
            var nm = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<NetworkingManager>(true)).FirstOrDefault(x => x != null);
            if (nm == null) { Debug.LogError("ExperimentSetup: no NetworkingManager in " + StartScenePath); return; }
            action(nm);
            if (save)
            {
                EditorUtility.SetDirty(nm);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (opened && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static Object[] ListProperty(Object owner, string path)
    {
        var p = new SerializedObject(owner).FindProperty(path);
        if (p == null || !p.isArray) return new Object[0];
        return Enumerable.Range(0, p.arraySize).Select(i => p.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
    }

    // ---- build settings

    static bool InBuild(string scene)
    {
        return EditorBuildSettings.scenes.Any(s => s.enabled && (s.path == scene || Path.GetFileNameWithoutExtension(s.path) == scene));
    }

    static string ScenePath(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return null;
        if (scene.EndsWith(".unity") && File.Exists(scene)) return scene;
        return AssetDatabase.FindAssets("t:Scene " + scene).Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == scene);
    }

    /// <summary>Adds (or enables) the scenes of the registered experiments in the build settings; returns what was added.</summary>
    public static List<string> RegisterScenes(NetworkingManager nm)
    {
        var added = new List<string>();
        var scenes = EditorBuildSettings.scenes.ToList();
        void Ensure(string path)
        {
            if (path == null) return;
            var i = scenes.FindIndex(s => s.path == path);
            if (i >= 0 && scenes[i].enabled) return;
            if (i >= 0) scenes[i] = new EditorBuildSettingsScene(path, true);
            else scenes.Add(new EditorBuildSettingsScene(path, true));
            added.Add(path);
        }
        Ensure(StartScenePath);
        foreach (var exp in nm.Experiments ?? new ExperimentDefinition[0])
        {
            if (exp == null) continue;
            if (!string.IsNullOrEmpty(exp.Scene)) Ensure(ScenePath(exp.Scene));
            if (!string.IsNullOrEmpty(exp.Map)) Ensure(OsmScenePath);
        }
        if (added.Count > 0) EditorBuildSettings.scenes = scenes.ToArray();
        return added;
    }

    [MenuItem("Coupled Sim/Experiments/Register experiment scenes in build settings")]
    static void RegisterScenesMenu()
    {
        WithStartScene(nm =>
        {
            var added = RegisterScenes(nm);
            Debug.Log(added.Count == 0 ? "ExperimentSetup: all experiment scenes are in the build settings" : "ExperimentSetup: added to the build settings: " + string.Join(", ", added));
        }, false);
    }

    /// <summary>Adds an experiment prefab to StartScene's experiment list (if not there yet); returns its index.</summary>
    public static int Register(ExperimentDefinition exp)
    {
        int index = -1;
        WithStartScene(nm =>
        {
            var list = (nm.Experiments ?? new ExperimentDefinition[0]).ToList();
            index = list.IndexOf(exp);
            if (index < 0) { list.Add(exp); index = list.Count - 1; nm.Experiments = list.ToArray(); }
            RegisterScenes(nm);
        }, true);
        return index;
    }

    [MenuItem("Assets/Coupled Sim/Register experiment in StartScene")]
    static void RegisterSelected()
    {
        foreach (var go in Selection.gameObjects)
            if (go.TryGetComponent<ExperimentDefinition>(out var exp) && PrefabUtility.IsPartOfPrefabAsset(go))
                Debug.Log($"ExperimentSetup: {go.name} is experiment {Register(exp)}");
    }

    [MenuItem("Assets/Coupled Sim/Register experiment in StartScene", true)]
    static bool RegisterSelectedValid() => Selection.gameObjects.Any(g => g.GetComponent<ExperimentDefinition>() != null);

    // ---- validation

    [MenuItem("Coupled Sim/Experiments/Validate")]
    public static void ValidateMenu()
    {
        WithStartScene(nm =>
        {
            var added = RegisterScenes(nm);
            foreach (var a in added) Debug.Log("ExperimentSetup: added to the build settings: " + a);
            var issues = Validate(nm, true);
            foreach (var i in issues)
                if (i.Error) Debug.LogError("Experiment setup: " + i.Message, i.Context); else Debug.LogWarning("Experiment setup: " + i.Message, i.Context);
            Debug.Log($"Experiment setup: {issues.Count(i => i.Error)} errors, {issues.Count(i => !i.Error)} warnings");
        }, false);
    }

    public static List<Issue> Validate(NetworkingManager nm, bool includeUnregistered = false)
    {
        var issues = new List<Issue>();
        void Err(string m, Object c = null) => issues.Add(new Issue { Error = true, Message = m, Context = c });
        void Warn(string m, Object c = null) => issues.Add(new Issue { Error = false, Message = m, Context = c });

        var ps = nm.GetComponent<PlayerSystem>();
        var drivers = ps != null ? ListProperty(ps, "_AvatarPrefabDriver") : new Object[0];
        var twoWheelers = ps != null ? ListProperty(ps, "_AvatarPrefabTwoWheeler") : new Object[0];
        if (ps != null && new SerializedObject(ps).FindProperty("_AvatarPrefab")?.objectReferenceValue == null)
            Err("PlayerSystem has no pedestrian avatar prefab", ps);
        var aiCars = new HashSet<Object>(ListProperty(nm, "_aiCarSystem.Prefabs").Where(o => o != null));

        var experiments = nm.Experiments ?? new ExperimentDefinition[0];
        if (experiments.Length == 0) Err("NetworkingManager has no experiments", nm);
        for (int i = 0; i < experiments.Length; i++)
        {
            var exp = experiments[i];
            if (exp == null) { Err($"experiment {i} in NetworkingManager ➡️ Experiments is empty (missing prefab?)", nm); continue; }
            if (Array.IndexOf(experiments, exp) != i) Warn($"experiment {exp.name} is registered twice (also as {Array.IndexOf(experiments, exp)})", nm);
            ValidateExperiment(exp, $"experiment {i} ({exp.name})", drivers, twoWheelers, aiCars, Err, Warn);
        }

        // trials
        if (!string.IsNullOrEmpty(nm.TrialFile))
        {
            var errors = new List<string>();
            var trials = TrialSequence.Load(nm.TrialFile, experiments, 0, null, nm.Order, ps != null ? ps.PlayerInputMode : PlayerSystem.InputMode.Flat, errors);
            foreach (var e in errors) Err("trial file: " + e, nm);
            if (trials != null && trials.Length == 0) Err($"trial file {nm.TrialFile} has no trials", nm);
        }
        else if (nm.trials != null)
        {
            for (int t = 0; t < nm.trials.Length; t++)
            {
                var trial = nm.trials[t];
                if (trial.experimentIndex < 0 || trial.experimentIndex >= experiments.Length) { Err($"trial {t}: experiment index {trial.experimentIndex} does not exist", nm); continue; }
                var exp = experiments[trial.experimentIndex];
                if (exp != null && (trial.roleIndex < 0 || exp.Roles == null || trial.roleIndex >= exp.Roles.Length))
                    Err($"trial {t}: {exp.name} has no role {trial.roleIndex}", nm);
            }
        }

        if (includeUnregistered)
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { ExperimentFolder }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.TryGetComponent<ExperimentDefinition>(out var e) && !experiments.Contains(e))
                    Warn($"{path} is not registered (Assets ➡️ Coupled Sim ➡️ Register experiment in StartScene to use it)", go);
            }
        return issues;
    }

    static void ValidateExperiment(ExperimentDefinition exp, string what, Object[] drivers, Object[] twoWheelers, HashSet<Object> aiCars,
        Action<string, Object> Err, Action<string, Object> Warn)
    {
        if (string.IsNullOrEmpty(exp.ShortName)) Warn($"{what} has no ShortName (used in log and video names)", exp);
        if (!string.IsNullOrEmpty(exp.Scene))
        {
            var path = ScenePath(exp.Scene);
            if (path == null) Err($"{what}: scene '{exp.Scene}' does not exist", exp);
            else if (!InBuild(path)) Err($"{what}: scene {path} is not in the build settings", exp);
        }
        else if (string.IsNullOrEmpty(exp.Map)) Err($"{what} has neither a Scene nor a Map", exp);
        if (!string.IsNullOrEmpty(exp.Map) && MapCatalog.IndexOf(exp.Map) < 0 && !OsmMap.TryParseLocation(exp.Map, out _, out _, out _))
            Err($"{what}: map '{exp.Map}' is not in MapCatalog (and not 'lat,lon[,radius]')", exp);
        if (!string.IsNullOrEmpty(exp.Map) && !InBuild(OsmScenePath)) Err($"{what}: {OsmScenePath} is not in the build settings", exp);

        var roles = exp.Roles ?? new ExperimentRoleDefinition[0];
        if (roles.Length == 0) Err($"{what} has no roles", exp);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int r = 0; r < roles.Length; r++)
        {
            var role = roles[r];
            var rw = $"{what}, role {r} ({role.Name})";
            if (string.IsNullOrEmpty(role.Name)) Warn($"{what}, role {r} has no name", exp);
            else if (!names.Add(role.Name)) Warn($"{rw}: another role has the same name", exp);
            if (role.SpawnPoint.Point == null) { Err($"{rw} has no spawn point", exp); continue; }
            switch (role.SpawnPoint.Type)
            {
                case SpawnPointType.PlayerControlingCar:
                case SpawnPointType.PlayerInAIControlledCar:
                    if (role.carIdx < 0 || role.carIdx >= drivers.Length || drivers[role.carIdx] == null)
                        Err($"{rw}: car {role.carIdx} is not in PlayerSystem ➡️ Avatar Prefab Driver ({drivers.Length} cars)", exp);
                    if (role.SpawnPoint.Type == SpawnPointType.PlayerInAIControlledCar && role.AutonomousPath == null)
                        Err($"{rw}: an AV passenger role needs an AutonomousPath", exp);
                    break;
                case SpawnPointType.PlayerRidingTwoWheeler:
                    if (role.twoWheelerIdx < 0 || role.twoWheelerIdx >= twoWheelers.Length || twoWheelers[role.twoWheelerIdx] == null)
                        Err($"{rw}: two-wheeler {role.twoWheelerIdx} is not in PlayerSystem ➡️ Avatar Prefab Two Wheeler", exp);
                    break;
            }
        }
        if (exp.AIPedestrians == null) Warn($"{what} has no AIPedestrians system (no AI pedestrians)", exp);
        if (exp.CarSpawners != null && exp.CarSpawners.Any(c => c == null)) Err($"{what}: an entry of CarSpawners is empty", exp);

        // AI car prefabs used by the experiment must be in AICarSyncSystem's list
        var reported = new HashSet<Object>();
        foreach (var c in exp.GetComponentsInChildren<Component>(true))
        {
            if (c == null) { Warn($"{what} has a missing script", exp); continue; }
            var so = new SerializedObject(c);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var o = it.objectReferenceValue;
                if (o == null || reported.Contains(o)) continue;
                var go = o is GameObject g ? g : o is Component cc ? cc.gameObject : null;
                if (go == null || !PrefabUtility.IsPartOfPrefabAsset(go) || go.transform.IsChildOf(exp.transform)) continue;
                if (!go.TryGetComponent<AICar>(out var car)) continue;
                if (!aiCars.Contains(car) && !aiCars.Contains(go))
                {
                    reported.Add(o);
                    Err($"{what}: AI car prefab {go.name} (used by {c.GetType().Name} on {c.name}) is not in NetworkingManager ➡️ AI Car System ➡️ Prefabs", exp);
                }
            }
        }
    }

    // before Play: register scenes, check, and ask whether to play when there are errors
    [InitializeOnLoadMethod]
    static void HookPlayMode()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode || !EditorPrefs.GetBool(ValidateBeforePlayKey, true)) return;
            var scene = SceneManager.GetSceneByPath(StartScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;
            var nm = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<NetworkingManager>(true)).FirstOrDefault(x => x != null);
            if (nm == null) return;
            foreach (var a in RegisterScenes(nm)) Debug.Log("ExperimentSetup: added to the build settings: " + a);
            var issues = Validate(nm);
            foreach (var i in issues)
                if (i.Error) Debug.LogError("Experiment setup: " + i.Message, i.Context); else Debug.LogWarning("Experiment setup: " + i.Message, i.Context);
            var errors = issues.Where(i => i.Error).ToList();
            if (errors.Count == 0 || Application.isBatchMode) return;
            var text = string.Join("\n", errors.Take(8).Select(e => "• " + e.Message)) + (errors.Count > 8 ? $"\n… and {errors.Count - 8} more (Console)" : "");
            if (!EditorUtility.DisplayDialog("Experiment setup problems", text, "Play anyway", "Cancel"))
                EditorApplication.isPlaying = false;
        };
    }

    [MenuItem("Coupled Sim/Experiments/Check experiments before Play", false, 100)]
    static void ToggleValidateBeforePlay() => EditorPrefs.SetBool(ValidateBeforePlayKey, !EditorPrefs.GetBool(ValidateBeforePlayKey, true));

    [MenuItem("Coupled Sim/Experiments/Check experiments before Play", true)]
    static bool ToggleValidateBeforePlayValid()
    {
        Menu.SetChecked("Coupled Sim/Experiments/Check experiments before Play", EditorPrefs.GetBool(ValidateBeforePlayKey, true));
        return true;
    }

    // ---- creating experiments

    public class NewExperiment
    {
        public string Name = "MyExperiment";
        public string ShortName = "myexp";
        public ExperimentDefinition Template;
        /// <summary>null: the template's scene or map; else a MapCatalog name or "lat,lon[,radius]".</summary>
        public string Map;
        public bool[] KeepRoles;
        public bool Traffic = true;
        public bool Animals = true;
        public float TimeOfDay = 13f;
        public Weather Weather = Weather.PartlyCloudy;
    }

    /// <summary>Creates Assets/ExperimentDefinitions/{Name}.prefab from the template, registers it and returns its path.</summary>
    public static string Create(NewExperiment n)
    {
        if (n.Template == null) throw new ArgumentException("no template");
        var safe = string.Concat(n.Name.Split(Path.GetInvalidFileNameChars())).Trim();
        if (safe.Length == 0) throw new ArgumentException("no name");
        var path = AssetDatabase.GenerateUniqueAssetPath($"{ExperimentFolder}/{safe}.prefab");
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(n.Template), path)) throw new Exception("could not copy " + AssetDatabase.GetAssetPath(n.Template));
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var exp = root.GetComponent<ExperimentDefinition>();
            exp.Name = n.Name;
            exp.ShortName = string.IsNullOrEmpty(n.ShortName) ? safe : n.ShortName;
            if (n.Map != null)
            {
                exp.Map = n.Map;
                // a city map is built in the OsmMap scene
                exp.Scene = "";
            }
            if (n.KeepRoles != null && exp.Roles != null)
                exp.Roles = exp.Roles.Where((r, i) => i >= n.KeepRoles.Length || n.KeepRoles[i]).ToArray();
            if (!n.Traffic)
            {
                foreach (var s in root.GetComponentsInChildren<CarSpawnerBase>(true).Where(s => s is TrafficSpawner || s is SumoBridge).ToArray())
                    Object.DestroyImmediate(s.gameObject);
                exp.CarSpawners = (exp.CarSpawners ?? new CarSpawnerBase[0]).Where(s => s != null).ToArray();
            }
            foreach (var a in root.GetComponentsInChildren<AnimalSpawner>(true))
                if (!n.Animals) Object.DestroyImmediate(a.gameObject);
            foreach (var env in root.GetComponentsInChildren<EnvironmentController>(true))
            {
                env.TimeOfDay = n.TimeOfDay;
                env.Weather = n.Weather;
                if (n.Map != null && MapCatalog.TryGet(n.Map, out var info))
                {
                    env.Latitude = (float)info.Latitude;
                    env.Longitude = (float)info.Longitude;
                    env.UtcOffset = info.UtcOffsetJune;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        var created = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ExperimentDefinition>();
        int index = Register(created);
        Debug.Log($"ExperimentSetup: created {path} (experiment {index})");
        return path;
    }

    /// <summary>Opens the experiment for placing spawn points: in its scene (as an instance to edit and apply), or the prefab for map experiments.</summary>
    public static void OpenForEditing(ExperimentDefinition exp)
    {
        var scenePath = ScenePath(exp.Scene);
        if (scenePath == null)
        {
            AssetDatabase.OpenAsset(exp.gameObject);
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(exp.gameObject);
        Selection.activeGameObject = inst;
        SceneView.lastActiveSceneView?.FrameSelected();
        EditorUtility.DisplayDialog("Edit the experiment",
            $"{exp.name} is in the scene {Path.GetFileNameWithoutExtension(scenePath)} now. Move its spawn points, waypoints and spawners, then " +
            "apply the changes to the prefab (Overrides ➡️ Apply All) and delete the instance from the scene before saving it.", "OK");
    }

    /// <summary>Writes StartScene's inline trial list to a CSV file (for editing in a spreadsheet).</summary>
    public static string ExportTrials(NetworkingManager nm, string path)
    {
        var table = new TrialTable { Order = TrialOrder.None };
        table.Columns.AddRange(new[] { "experiment", "role", "input", "block", "recordingStart", "recordingDuration" });
        var experiments = nm.Experiments ?? new ExperimentDefinition[0];
        foreach (var t in nm.trials ?? new NetworkingManager.Trial[0])
        {
            var exp = t.experimentIndex >= 0 && t.experimentIndex < experiments.Length ? experiments[t.experimentIndex] : null;
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["experiment"] = exp != null ? (string.IsNullOrEmpty(exp.ShortName) ? exp.name : exp.ShortName) : t.experimentIndex.ToString(),
                ["role"] = exp != null && exp.Roles != null && t.roleIndex < exp.Roles.Length && !string.IsNullOrEmpty(exp.Roles[t.roleIndex].Name) ? exp.Roles[t.roleIndex].Name : t.roleIndex.ToString(),
                ["input"] = t.InputMode.ToString(),
                ["block"] = "",
                ["recordingStart"] = t.recordingStartTime.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["recordingDuration"] = t.recordingDuration.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            foreach (var p in t.experimentParameters ?? new NetworkingManager.ExperimentParameter[0])
            {
                if (!table.Columns.Contains(p.name)) table.Columns.Add(p.name);
                row[p.name] = p.value;
            }
            table.Rows.Add(row);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, table.ToCsv());
        AssetDatabase.Refresh();
        return path;
    }
}

/// <summary>Coupled Sim ➡️ Experiments ➡️ Setup: new experiments, trial files, checks.</summary>
public class ExperimentSetupWindow : EditorWindow
{
    int _tab;
    Vector2 _scroll;
    // new experiment
    readonly ExperimentSetup.NewExperiment _new = new ExperimentSetup.NewExperiment();
    ExperimentDefinition[] _registered = new ExperimentDefinition[0];
    int _templateIdx, _mapIdx;
    string _location = "51.4483,5.4908,400";
    string _lastCreated;
    // trials
    string _trialFile;
    int _participant = 1;
    TrialOrder _order = TrialOrder.Latin;
    NetworkingManager.Trial[] _preview;
    ExperimentDefinition[] _previewExperiments;
    readonly List<string> _previewErrors = new List<string>();
    // checks
    List<ExperimentSetup.Issue> _issues;

    [MenuItem("Coupled Sim/Experiments/Setup…", false, 0)]
    public static void Open() => GetWindow<ExperimentSetupWindow>("Experiment setup").Refresh();

    void OnEnable() => Refresh();

    void Refresh()
    {
        ExperimentSetup.WithStartScene(nm =>
        {
            _registered = (nm.Experiments ?? new ExperimentDefinition[0]).Where(e => e != null).ToArray();
            if (_trialFile == null) _trialFile = nm.TrialFile ?? "";
            _order = nm.Order;
        }, false);
    }

    void OnGUI()
    {
        _tab = GUILayout.Toolbar(_tab, new[] { "New experiment", "Trials", "Check" });
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        switch (_tab)
        {
            case 0: NewExperimentGUI(); break;
            case 1: TrialsGUI(); break;
            case 2: CheckGUI(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    void NewExperimentGUI()
    {
        if (_registered.Length == 0) { EditorGUILayout.HelpBox("No experiments in StartScene to use as a template.", MessageType.Warning); return; }
        _new.Name = EditorGUILayout.TextField("Name", _new.Name);
        _new.ShortName = EditorGUILayout.TextField(new GUIContent("Short name", "Used in log and video file names."), _new.ShortName);
        int t = EditorGUILayout.Popup("Template", _templateIdx, _registered.Select(e => $"{e.name} ({e.ShortName})").ToArray());
        if (t != _templateIdx || _new.Template == null) { _templateIdx = t; _new.Template = _registered[Mathf.Clamp(t, 0, _registered.Length - 1)]; _new.KeepRoles = null; }
        var tpl = _new.Template;

        var places = new List<string> { string.IsNullOrEmpty(tpl.Scene) ? $"Template's map ({tpl.Map})" : $"Template's scene ({tpl.Scene})" };
        places.AddRange(MapCatalog.Maps.Select(m => $"City map: {m.Name} ({m.Country})"));
        places.Add("Any place by coordinates");
        _mapIdx = EditorGUILayout.Popup(new GUIContent("Where", "Hand-built scene of the template, a city map built from OpenStreetMap, or any place."), _mapIdx, places.ToArray());
        if (_mapIdx == places.Count - 1)
        {
            _location = EditorGUILayout.TextField(new GUIContent("Latitude, longitude, radius (m)"), _location);
            _new.Map = _location;
        }
        else _new.Map = _mapIdx == 0 ? null : MapCatalog.Maps[_mapIdx - 1].Name;
        if (_new.Map != null && string.IsNullOrEmpty(tpl.Map))
            EditorGUILayout.HelpBox("This template was made for a hand-built scene: its spawn points and paths will not fit a city map. Use CityMap as the template for city maps (its spawn points find a road, pavement or crossing on any map).", MessageType.Warning);

        var roles = tpl.Roles ?? new ExperimentRoleDefinition[0];
        if (_new.KeepRoles == null || _new.KeepRoles.Length != roles.Length) _new.KeepRoles = Enumerable.Repeat(true, roles.Length).ToArray();
        EditorGUILayout.LabelField("Roles", EditorStyles.boldLabel);
        for (int i = 0; i < roles.Length; i++)
            _new.KeepRoles[i] = EditorGUILayout.ToggleLeft($"{roles[i].Name}  ({roles[i].SpawnPoint.Type})", _new.KeepRoles[i]);

        EditorGUILayout.LabelField("World", EditorStyles.boldLabel);
        _new.Traffic = EditorGUILayout.Toggle(new GUIContent("Background traffic", "Keep the template's traffic spawners and SUMO coupling."), _new.Traffic);
        _new.Animals = EditorGUILayout.Toggle("Animals", _new.Animals);
        _new.TimeOfDay = EditorGUILayout.Slider("Time of day (h)", _new.TimeOfDay, 0f, 24f);
        _new.Weather = (Weather)EditorGUILayout.EnumPopup("Weather", _new.Weather);
        EditorGUILayout.HelpBox("Time, weather, map, animals and more can also change per trial with experiment parameters (Trials tab).", MessageType.None);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_new.Name) || !_new.KeepRoles.Any(k => k)))
            if (GUILayout.Button("Create and register", GUILayout.Height(28)))
            {
                try
                {
                    _lastCreated = ExperimentSetup.Create(_new);
                    Refresh();
                    _issues = null;
                }
                catch (Exception e) { EditorUtility.DisplayDialog("Could not create the experiment", e.Message, "OK"); }
            }
        if (_lastCreated != null)
        {
            var exp = AssetDatabase.LoadAssetAtPath<GameObject>(_lastCreated);
            if (exp != null)
            {
                EditorGUILayout.HelpBox($"Created {_lastCreated} as experiment {Array.IndexOf(_registered, exp.GetComponent<ExperimentDefinition>())}. Next: place the spawn points and paths, then add trials.", MessageType.Info);
                if (GUILayout.Button("Open it to place spawn points")) ExperimentSetup.OpenForEditing(exp.GetComponent<ExperimentDefinition>());
            }
        }
    }

    void TrialsGUI()
    {
        EditorGUILayout.HelpBox("A trial file is a CSV (or JSON) table with one row per trial: experiment, role, input, block, recordingStart, recordingDuration, and one column per experiment parameter (weather, time, map, animals, ...). Rows with the same block are counterbalanced between participants (balanced Latin square). Builds take -trials <file> -participant <n>.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            _trialFile = EditorGUILayout.TextField("Trial file", _trialFile);
            if (GUILayout.Button("…", GUILayout.Width(28)))
            {
                var dir = Path.Combine(Application.streamingAssetsPath, "Trials");
                var p = EditorUtility.OpenFilePanel("Trial file", Directory.Exists(dir) ? dir : Application.streamingAssetsPath, "csv,json");
                if (!string.IsNullOrEmpty(p))
                {
                    var trialsDir = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
                    _trialFile = Path.GetFullPath(p).StartsWith(trialsDir) ? Path.GetFullPath(p).Substring(trialsDir.Length) : p;
                }
            }
        }
        _participant = EditorGUILayout.IntField(new GUIContent("Participant", "1, 2, ...; 0 = the file's order"), _participant);
        _order = (TrialOrder)EditorGUILayout.EnumPopup(new GUIContent("Order", "Used when the file has no #order=... line"), _order);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview")) Preview();
            if (GUILayout.Button("Use in StartScene"))
                ExperimentSetup.WithStartScene(nm => { nm.TrialFile = _trialFile; nm.Order = _order; }, true);
            if (GUILayout.Button("Open file") && TrialSequence.Resolve(_trialFile) != null) EditorUtility.OpenWithDefaultApp(TrialSequence.Resolve(_trialFile));
        }
        if (GUILayout.Button("New trial file from StartScene's trial list"))
        {
            var path = EditorUtility.SaveFilePanel("New trial file", Path.Combine(Application.streamingAssetsPath, "Trials"), "trials.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                ExperimentSetup.WithStartScene(nm => ExperimentSetup.ExportTrials(nm, path), false);
                _trialFile = path;
            }
        }
        foreach (var e in _previewErrors) EditorGUILayout.HelpBox(e, MessageType.Error);
        if (_preview != null)
        {
            EditorGUILayout.LabelField($"Participant {_participant}: {_preview.Length} trials", EditorStyles.boldLabel);
            for (int i = 0; i < _preview.Length; i++)
                EditorGUILayout.LabelField($"{i + 1}.", TrialSequence.Describe(_preview[i], _previewExperiments));
        }
    }

    ExperimentDefinition[] CurrentExperiments()
    {
        ExperimentDefinition[] list = null;
        ExperimentSetup.WithStartScene(nm => list = nm.Experiments, false);
        return list;
    }

    void Preview()
    {
        _previewErrors.Clear();
        _previewExperiments = CurrentExperiments();
        _preview = TrialSequence.Load(_trialFile, _previewExperiments, _participant, null, _order, PlayerSystem.InputMode.Flat, _previewErrors);
    }

    void CheckGUI()
    {
        if (GUILayout.Button("Check experiments and trials", GUILayout.Height(24)))
            ExperimentSetup.WithStartScene(nm =>
            {
                foreach (var a in ExperimentSetup.RegisterScenes(nm)) Debug.Log("ExperimentSetup: added to the build settings: " + a);
                _issues = ExperimentSetup.Validate(nm, true);
            }, false);
        if (_issues == null) return;
        if (_issues.Count == 0) EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
        foreach (var i in _issues)
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(i.Message, i.Error ? MessageType.Error : MessageType.Warning);
                if (i.Context != null && GUILayout.Button("Select", GUILayout.Width(50))) { Selection.activeObject = i.Context; EditorGUIUtility.PingObject(i.Context); }
            }
    }
}
