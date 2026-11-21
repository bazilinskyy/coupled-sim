using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rides a two-wheeler prefab in an empty scene (a flat ground with a 1 m grid, no networking) and saves the frames of
/// chase cameras, for looking at the riders' animation without a whole trial:
/// <c>Unity -batchmode -projectPath . -executeMethod RiderPreview.Run -previewPrefab Bicycle -previewPlan "go:4:1;coast:2;brake:1.5:0.6;stop:3;go:4:1" -previewCams "side=rel:3,1,0.3,-90,5,40;back=rel:0,1.6,-3.6,0,8,62" -previewOut dir</c>.
/// Plan steps (seconds, effort, steer): go (pedal), coast, brake, stop, turn (pedal and steer). -previewAI also rides
/// an AI version (controller off, its parts shown from a scripted motion: a circle) beside it; -previewParked adds a
/// parked one (no controller: its rider's pose read from its stillness).
/// </summary>
[InitializeOnLoad]
public static class RiderPreview
{
    const string Key = "RiderPreview.";
    static double _start = -1;
    static GameObject _bike, _ai, _parked;
    static TwoWheelerController _tw, _aiTw;
    static (string name, Vector3 pos, float yaw, float pitch, float fov)[] _cams;
    static Camera _cam;
    static RenderTexture _rt;
    static Texture2D _read;
    static int _frame;
    static float _aiAngle;

    static RiderPreview()
    {
        if (SessionState.GetBool(Key + "Active", false)) EditorApplication.update += Tick;
    }

    static string Arg(string name, string def)
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
    }

    public static void Run()
    {
        EditorUtility.audioMasterMute = true;
        SessionState.SetBool(Key + "Active", true);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    static void Setup()
    {
        int fps = int.Parse(Arg("-previewFps", "30"));
        Time.captureDeltaTime = 1f / fps;
        Directory.CreateDirectory(Arg("-previewOut", "Preview"));
        // ground: 200 m square, a 1 m grid
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(20, 1, 20);
        var tex = new Texture2D(64, 64);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) tex.SetPixel(x, y, (x < 2 || y < 2) ? new Color(0.25f, 0.25f, 0.27f) : new Color(0.55f, 0.55f, 0.56f));
        tex.Apply(); tex.wrapMode = TextureWrapMode.Repeat;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", new Vector2(200, 200)); mat.SetFloat("_Smoothness", 0.1f);
        ground.GetComponent<Renderer>().sharedMaterial = mat;
        var light = UnityEngine.Object.FindAnyObjectByType<Light>();
        if (light != null) { light.transform.rotation = Quaternion.Euler(40, -30, 0); light.shadows = LightShadows.Soft; }

        string name = Arg("-previewPrefab", "Bicycle");
        _bike = Spawn(name, new Vector3(0, 0, -40), 0f);
        _tw = _bike.GetComponent<TwoWheelerController>();
        _tw.ReadInput = true; _tw.UseExternalInput = true; _tw.enabled = true;
        if (Environment.GetCommandLineArgs().Contains("-previewAI"))
        {
            _ai = Spawn("AI" + name, new Vector3(6, 0, -40), 0f);
            _aiTw = _ai.GetComponent<TwoWheelerController>();
            _aiTw.enabled = false;
        }
        if (Environment.GetCommandLineArgs().Contains("-previewParked"))
        {
            _parked = Spawn(name, new Vector3(-2.5f, 0, -36), 0f);
            UnityEngine.Object.DestroyImmediate(_parked.GetComponent<TwoWheelerController>());
        }
        _cams = Arg("-previewCams", "back=rel:0,1.8,-3.6,0,8,62").Split(';').Select(c =>
        {
            var kv = c.Split('='); var v = kv[1].Substring(4).Split(',').Select(F).ToArray();
            return (kv[0], new Vector3(v[0], v[1], v[2]), v[3], v[4], v.Length > 5 ? v[5] : 60f);
        }).ToArray();
        foreach (var c in _cams) Directory.CreateDirectory(Path.Combine(Arg("-previewOut", "Preview"), c.name));
        var size = Arg("-previewSize", "1280x720").Split('x').Select(int.Parse).ToArray();
        _rt = new RenderTexture(size[0], size[1], 24);
        _read = new Texture2D(size[0], size[1], TextureFormat.RGB24, false);
        _cam = new GameObject("Preview camera").AddComponent<Camera>();
        _cam.targetTexture = _rt; _cam.nearClipPlane = 0.05f;
        foreach (var c in Camera.allCameras) if (c != _cam) c.enabled = false;
    }

    static GameObject Spawn(string name, Vector3 pos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/TwoWheelers/{name}.prefab");
        var holder = new GameObject("holder"); holder.SetActive(false);
        var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.Euler(0, yaw, 0), holder.transform);
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(mb is TwoWheelerController) && !(mb is RiderIK)) UnityEngine.Object.DestroyImmediate(mb);
        foreach (var cam in go.GetComponentsInChildren<Camera>(true)) cam.gameObject.SetActive(false);
        foreach (var al in go.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(al);
        foreach (var an in go.GetComponentsInChildren<Animator>(true)) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        go.transform.SetParent(null, true);
        UnityEngine.Object.Destroy(holder);
        go.name = name;
        return go;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (_start < 0) { Setup(); _start = 0; return; }
        float t = Time.time;
        // the plan
        float at = 0f; var input = new CoupledSim.TwoWheelers.TwoWheelerInput(); bool done = true;
        foreach (var step in Arg("-previewPlan", "go:6:1").Split(';'))
        {
            var p = step.Split(':'); float len = F(p[1]);
            if (t < at + len)
            {
                done = false;
                switch (p[0])
                {
                    case "go": input.Drive = p.Length > 2 ? F(p[2]) : 0.7f; break;
                    case "turn": input.Drive = p.Length > 2 ? F(p[2]) : 0.5f; input.Steer = p.Length > 3 ? F(p[3]) : 0.4f; break;
                    case "brake": input.Brake = p.Length > 2 ? F(p[2]) : 0.6f; break;
                    case "stop": input.Brake = 1f; break;
                }
                break;
            }
            at += len;
        }
        _tw.Input = input;
        // the AI one: round a 15 m circle at 18 km/h (30 for a race bike), slowing to a stop for 3 s every 20 s
        if (_ai != null)
        {
            float dt = Time.deltaTime, cycle = t % 20f;
            float v = (cycle < 14f ? Mathf.Min(1f, cycle / 4f) : cycle < 17f ? Mathf.Max(0f, 1f - (cycle - 14f) / 2f) : 0f) * (_aiTw.Type == CoupledSim.TwoWheelers.TwoWheelerType.RaceBike ? 8.3f : 5f);
            float prev = _aiTw.State.Speed;
            float yawRate = v / 15f;
            _aiAngle += yawRate * dt;
            _ai.transform.position = new Vector3(6f + 15f - 15f * Mathf.Cos(_aiAngle), 0f, -40f + 15f * Mathf.Sin(_aiAngle));
            _ai.transform.rotation = Quaternion.Euler(0, _aiAngle * Mathf.Rad2Deg, 0);
            _aiTw.ShowMotion(v, yawRate, (v - prev) / Mathf.Max(dt, 1e-4f), dt);
        }
        // the cameras
        var root = _ai != null && Environment.GetCommandLineArgs().Contains("-previewFollowAI") ? _ai.transform : _bike.transform;
        var yawOnly = Quaternion.Euler(0, root.eulerAngles.y, 0);
        foreach (var c in _cams)
        {
            _cam.transform.SetPositionAndRotation(root.position + yawOnly * c.pos, yawOnly * Quaternion.Euler(c.pitch, c.yaw, 0));
            _cam.fieldOfView = c.fov;
            _cam.Render();
            var old = RenderTexture.active; RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0); _read.Apply();
            RenderTexture.active = old;
            File.WriteAllBytes(Path.Combine(Arg("-previewOut", "Preview"), c.name, $"{_frame:D5}.jpg"), _read.EncodeToJPG(88));
        }
        if (_frame % 15 == 0)
            Debug.Log($"RiderPreview t={t:F2} v={_tw.SpeedKmh:F1} km/h gear {_tw.Legs.Gear + 1} cadence {_tw.Legs.Cadence:F0} crank {_tw.Legs.CrankAngle:F0} foot {_tw.Legs.FootDown:F2} stand {_tw.Legs.Standing:F2} effort {_tw.Legs.Effort:F2} lean {_tw.LeanDeg:F1}");
        _frame++;
        if (done)
        {
            SessionState.SetBool(Key + "Active", false);
            EditorApplication.Exit(0);
        }
    }
}
