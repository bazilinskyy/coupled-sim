using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Takes the screenshots of the README's step-by-step guide: walks through the editor windows and
/// the simulator's screens, and asks Tools/readme_screenshots.sh (macOS, screencapture) to take a
/// picture of each. Run that script; it starts the editor with -executeMethod ReadmeScreenshots.Run.
/// Needs the screen unlocked; the editor is used for about 10 minutes.
/// </summary>
[InitializeOnLoad]
public static class ReadmeScreenshots
{
    const string KeyActive = "ReadmeShots.Active", KeyStep = "ReadmeShots.Step";
    static string Dir => Path.GetFullPath("ReadmeShots");
    static double _waitUntil = -1;
    static bool _requested;

    struct Step
    {
        public string Name;          // picture name; null = an action only
        public Action Setup;
        public Func<Rect> Area;      // screen rectangle to take
        public float Wait;           // seconds between setup and picture
        public Func<bool> Ready;     // optional: wait until true
        public bool GameView;        // taken by Unity itself (ScreenCapture), works with the screen locked
    }

    // -readmeShots game: only the simulator's screens (Game view)
    static bool Skip(Step s) => s.Name != null && Environment.GetCommandLineArgs().Contains("game") && !s.GameView;

    static ReadmeScreenshots()
    {
        if (SessionState.GetBool(KeyActive, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        Directory.CreateDirectory(Dir);
        SessionState.SetBool(KeyActive, true);
        SessionState.SetInt(KeyStep, 0);
        EditorApplication.update += Tick;
    }

    // ---- window helpers

    static Type T(string name) => typeof(EditorWindow).Assembly.GetType(name);

    static Rect MainWindowRect()
    {
        var cw = T("UnityEditor.ContainerWindow");
        var windows = (Array)cw.GetProperty("windows", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
        foreach (var w in windows)
        {
            int mode = Convert.ToInt32(cw.GetProperty("showMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(w));
            if (mode == 4) return (Rect)cw.GetProperty("position").GetValue(w);
        }
        return new Rect(0, 0, 1920, 1200);
    }

    static void SetMainWindowRect(Rect r)
    {
        var cw = T("UnityEditor.ContainerWindow");
        var windows = (Array)cw.GetProperty("windows", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
        foreach (var w in windows)
        {
            int mode = Convert.ToInt32(cw.GetProperty("showMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(w));
            if (mode == 4) cw.GetProperty("position").SetValue(w, r);
        }
    }

    static EditorWindow Window(string typeName) => Resources.FindObjectsOfTypeAll(T(typeName)).OfType<EditorWindow>().FirstOrDefault();
    static Rect WindowRect(string typeName) { var w = Window(typeName); return w != null ? Pad(w.position) : MainWindowRect(); }
    static Rect Pad(Rect r) => new Rect(r.x, r.y - 22, r.width, r.height + 22); // include the tab

    static GameObject Managers() => GameObject.Find("Managers");

    // only the given components of the selection unfolded in the inspector
    static void Unfold(GameObject go, params Type[] open)
    {
        foreach (var c in go.GetComponents<Component>())
            if (c != null) InternalEditorUtility.SetIsInspectorExpanded(c, open.Contains(c.GetType()));
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }

    static void Expand(UnityEngine.Object o, params string[] paths)
    {
        var so = new SerializedObject(o);
        foreach (var p in paths) { var sp = so.FindProperty(p); if (sp != null) sp.isExpanded = true; }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(o);
    static void SetField(object o, string name, object v) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(o, v);

    static EditorWindow SetupWindow(int tab)
    {
        var w = EditorWindow.GetWindow<ExperimentSetupWindow>(false, "Experiment setup", true);
        w.position = new Rect(560, 120, 620, 860);
        SetField(w, "_tab", tab);
        w.Repaint();
        return w;
    }

    static Host HostSystem => NetworkingManager.Instance != null ? NetworkingManager.Instance.NetSystem as Host : null;

    static readonly Step[] Steps =
    {
        new Step { Setup = () =>
        {
            EditorUtility.LoadWindowLayout(Path.Combine(EditorApplication.applicationContentsPath, "Resources/Layouts/Default.wlt"));
            SetMainWindowRect(new Rect(0, 25, 1920, 1150));
            EditorSceneManager.OpenScene(ExperimentSetup.StartScenePath);
        }, Wait = 4 },
        new Step { Name = "editor_overview", Setup = () => { Selection.activeGameObject = Managers(); Unfold(Managers(), typeof(NetworkingManager)); }, Area = MainWindowRect, Wait = 4 },
        new Step { Name = "inspector_networking_manager", Setup = () => Unfold(Managers(), typeof(NetworkingManager)), Area = () => WindowRect("UnityEditor.InspectorWindow"), Wait = 2 },
        new Step { Name = "inspector_player_system", Setup = () =>
        {
            var ps = Managers().GetComponent<PlayerSystem>();
            Expand(ps, "_AvatarPrefabDriver", "_AvatarPrefabTwoWheeler");
            Unfold(Managers(), typeof(PlayerSystem));
        }, Area = () => WindowRect("UnityEditor.InspectorWindow"), Wait = 2 },
        new Step { Name = "project_experiments", Setup = () =>
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ExperimentSetup.ExperimentFolder);
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ExperimentSetup.ExperimentFolder + "/Base.prefab");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }, Area = () => WindowRect("UnityEditor.ProjectBrowser"), Wait = 3 },
        new Step { Name = "inspector_experiment_definition", Setup = () =>
        {
            var exp = AssetDatabase.LoadAssetAtPath<GameObject>(ExperimentSetup.ExperimentFolder + "/Base.prefab");
            Expand(exp.GetComponent<ExperimentDefinition>(), "Roles", "Roles.Array.data[0]", "Roles.Array.data[0].SpawnPoint");
            Selection.activeObject = exp;
            Unfold(exp, typeof(ExperimentDefinition));
        }, Area = () => WindowRect("UnityEditor.InspectorWindow"), Wait = 3 },
        new Step { Name = "setup_new_experiment", Setup = () => SetupWindow(0), Area = () => Pad(EditorWindow.GetWindow<ExperimentSetupWindow>().position), Wait = 3 },
        new Step { Name = "setup_trials", Setup = () =>
        {
            var w = SetupWindow(1);
            SetField(w, "_trialFile", "example.csv");
            SetField(w, "_participant", 3);
            w.GetType().GetMethod("Preview", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(w, null);
            w.Repaint();
        }, Area = () => Pad(EditorWindow.GetWindow<ExperimentSetupWindow>().position), Wait = 3 },
        new Step { Name = "setup_check", Setup = () =>
        {
            var w = SetupWindow(2);
            ExperimentSetup.WithStartScene(nm => SetField(w, "_issues", ExperimentSetup.Validate(nm, true)), false);
            w.Repaint();
        }, Area = () => Pad(EditorWindow.GetWindow<ExperimentSetupWindow>().position), Wait = 3 },
        new Step { Setup = () =>
        {
            EditorWindow.GetWindow<ExperimentSetupWindow>().Close();
            Selection.activeGameObject = Managers();
            EditorApplication.isPlaying = true;
        }, Ready = () => EditorApplication.isPlaying && NetworkingManager.Instance != null, Wait = 4 },
        new Step { Name = "game_start_screen", GameView = true, Wait = 3 },
        new Step { Name = "game_host_lobby", Setup = () => NetworkingManager.Instance.StartHost(), GameView = true, Wait = 3 },
        new Step { Name = "game_host_ready", Setup = () =>
        {
            var h = HostSystem;
            h.SelectedExperiment = 0;
            h.SetRole(0, 0);
            h.Initialise();
        }, Ready = () => HostSystem != null && HostSystem.StateName == "Ready to start", GameView = true, Wait = 6 },
        new Step { Name = "game_driving", Setup = () => HostSystem.StartSimulation(), Ready = () => HostSystem != null && HostSystem.InGame, GameView = true, Wait = 6 },
        new Step { Name = "game_wheel_calibration", Setup = () =>
        {
            var wc = UnityEngine.Object.FindAnyObjectByType<WheelCalibration>();
            var f = wc.GetType().GetField("_step", BindingFlags.Instance | BindingFlags.NonPublic);
            f.SetValue(wc, Enum.Parse(f.FieldType, "Overview"));
        }, GameView = true, Wait = 3 },
        new Step { Setup = () => EditorApplication.isPlaying = false, Ready = () => !EditorApplication.isPlaying, Wait = 2 },
    };

    static void Tick()
    {
        int i = SessionState.GetInt(KeyStep, 0);
        if (i >= Steps.Length)
        {
            SessionState.SetBool(KeyActive, false);
            File.WriteAllText(Path.Combine(Dir, "request.txt"), "DONE");
            EditorApplication.update -= Tick;
            EditorApplication.Exit(0);
            return;
        }
        var s = Steps[i];
        if (Skip(s)) { SessionState.SetInt(KeyStep, i + 1); return; }
        try
        {
            if (_waitUntil < 0)
            {
                s.Setup?.Invoke();
                _waitUntil = EditorApplication.timeSinceStartup + s.Wait;
                _requested = false;
                return;
            }
            if (s.Ready != null && !s.Ready())
            {
                _waitUntil = Math.Max(_waitUntil, EditorApplication.timeSinceStartup + 1);
                return;
            }
            if (EditorApplication.timeSinceStartup < _waitUntil) return;
            if (s.GameView)
            {
                var file = Path.Combine(Dir, "raw", s.Name + ".png");
                if (!_requested) { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.Delete(file); ScreenCapture.CaptureScreenshot(file); _requested = true; _waitUntil = EditorApplication.timeSinceStartup + 2; return; }
                if (!File.Exists(file)) { _waitUntil = EditorApplication.timeSinceStartup + 0.5; return; }
                Debug.Log("ReadmeScreenshots: took " + s.Name);
            }
            else if (s.Name != null)
            {
                var ack = Path.Combine(Dir, "ack.txt");
                if (!_requested)
                {
                    var r = s.Area();
                    File.WriteAllText(Path.Combine(Dir, "request.txt"), $"{s.Name} {(int)r.x} {(int)r.y} {(int)r.width} {(int)r.height}");
                    _requested = true;
                    return;
                }
                if (!File.Exists(ack) || File.ReadAllText(ack).Trim() != s.Name) return;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"ReadmeScreenshots: step {i} ({s.Name}): {e}");
        }
        _waitUntil = -1;
        SessionState.SetInt(KeyStep, i + 1);
    }
}
