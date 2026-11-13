using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An eHMI described in a file instead of a prefab: StreamingAssets/eHMI/{Design}.json gives the
/// panel's size and, for each eHMI state (DISABLED, STOP, WALK), the elements shown:
/// light bars (segments, colour, animation), rectangles, circles, arrows, chevrons and text, each
/// placed in the panel (x, y, w, h from 0 to 1) and animated (steady, pulse, blink, sweep left or
/// right, sweep out from or in to the middle). It is switched like every eHMI (passenger keys, host,
/// dashboard) and synchronised over the network.
/// Placement on the car: <see cref="ExperimentRoleDefinition.EhmiDesign"/> with its slot (hood, roof,
/// windshield, or projected on the road ahead). It can also be put on any object (a pole at a
/// crossing, a pedestrian's wearable): with <see cref="FollowEhmi"/> it shows the state of the
/// eHMI with that index (0 = the first eHMI spawned on a car).
/// </summary>
public class DesignedEhmi : HMI
{
    [Tooltip("File name in StreamingAssets/eHMI (without .json).")]
    public string Design = "LightBarCyan";
    [Tooltip("Show the state of eHMI number N of the session (-1: switched directly).")]
    public int FollowEhmi = -1;
    [Tooltip("Pixels per metre of the drawn panel.")]
    public float Resolution = 256f;

    [Serializable] public class Element
    {
        public string type = "rect";        // lightbar, rect, circle, arrow, chevrons, text
        public float x, y, w = 1, h = 1;    // in the panel, 0..1, from the bottom left
        public string color = "#00FFFF";
        public string text = "";
        public int segments = 12;           // lightbar: lamps; chevrons: count
        public string direction = "right";  // arrow, chevrons: left, right, up, down
        public string animation = "steady"; // steady, pulse, blink, sweep, sweepleft, sweepout, sweepin
        public float period = 1f;           // seconds per cycle
    }
    [Serializable] public class StateDesign { public string state = "WALK"; public Element[] elements = new Element[0]; }
    [Serializable] public class DesignFile
    {
        public string name = "";
        public float width = 1.2f, height = 0.15f;   // metres
        public string background = "#000000";         // with alpha, e.g. #00000000 for none
        public StateDesign[] states = new StateDesign[0];
    }

    public DesignFile Loaded { get; private set; }

    Texture2D _tex;
    Color32[] _pixels;
    RawImage _image;
    readonly List<Text> _texts = new List<Text>();
    HMIManager _manager;
    float _stateSince;
    int _w, _h;

    public static string PathOf(string design) => Path.Combine(Application.streamingAssetsPath, "eHMI", design.EndsWith(".json") ? design : design + ".json");

    public static DesignFile Load(string design)
    {
        var path = PathOf(design);
        if (!File.Exists(path)) { Debug.LogWarning("DesignedEhmi: no design file " + path); return null; }
        try { return JsonUtility.FromJson<DesignFile>(File.ReadAllText(path)); }
        catch (Exception e) { Debug.LogWarning($"DesignedEhmi: cannot read {path}: {e.Message}"); return null; }
    }

    void Start()
    {
        Loaded = Load(Design) ?? new DesignFile();
        _manager = FindAnyObjectByType<HMIManager>();
        Build();
        // -ehmiState WALK|STOP|DISABLED on the command line: start in that state (previews, screenshots)
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-ehmiState" && Enum.TryParse<HMIState>(args[i + 1], true, out var st)) CurrentState = st;
        Display(CurrentState);
    }

    public override void Display(HMIState state)
    {
        if (state != CurrentState) _stateSince = Time.time;
        base.Display(state);
    }

    void Build()
    {
        _w = Mathf.Clamp(Mathf.RoundToInt(Loaded.width * Resolution), 8, 2048);
        _h = Mathf.Clamp(Mathf.RoundToInt(Loaded.height * Resolution), 8, 2048);
        _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        _pixels = new Color32[_w * _h];
        var go = new GameObject("eHMI panel", typeof(Canvas));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(Loaded.width * 1000f, Loaded.height * 1000f);   // 1 unit = 1 mm
        go.transform.localScale = Vector3.one * 0.001f;
        // the canvas faces -z: turn it to face forward (+z) like the car's other eHMIs
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var img = new GameObject("Image", typeof(RectTransform));
        img.transform.SetParent(go.transform, false);
        Stretch((RectTransform)img.transform, 0, 0, 1, 1);
        _image = img.AddComponent<RawImage>();
        _image.texture = _tex;
        foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = gameObject.layer;
    }

    static void Stretch(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1); rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void Update()
    {
        if (_tex == null) return;
        if (FollowEhmi >= 0 && _manager != null && FollowEhmi < _manager.Count) Display(_manager.GetState(FollowEhmi));
        Draw(Time.time - _stateSince);
    }

    StateDesign Current()
    {
        var name = CurrentState.ToString();
        foreach (var s in Loaded.states) if (string.Equals(s.state, name, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    // ------------------------------------------------------------------ drawing

    void Draw(float t)
    {
        var bg = ParseColor(Loaded.background, new Color(0, 0, 0, 1));
        var bg32 = (Color32)bg;
        for (int i = 0; i < _pixels.Length; i++) _pixels[i] = bg32;
        var state = Current();
        int ti = 0;
        if (state != null)
            foreach (var e in state.elements)
            {
                var c = ParseColor(e.color, Color.cyan);
                switch ((e.type ?? "rect").ToLowerInvariant())
                {
                    case "lightbar": LightBar(e, c, t); break;
                    case "circle": Shape(e, c, t, (u, v) => (u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f) <= 0.25f); break;
                    case "arrow": Shape(e, c, t, (u, v) => InArrow(Orient(e.direction, u, v))); break;
                    case "chevrons": Chevrons(e, c, t); break;
                    case "text": SetText(ti++, e, c, t); break;
                    default: Shape(e, c, t, (u, v) => true); break;
                }
            }
        for (int i = ti; i < _texts.Count; i++) _texts[i].enabled = false;
        _tex.SetPixels32(_pixels);
        _tex.Apply(false);
    }

    // brightness 0..1 of an animated element at time t; pos = 0..1 along it (for sweeps)
    static float Level(Element e, float t, float pos)
    {
        float p = Mathf.Max(0.05f, e.period), ph = Mathf.Repeat(t / p, 1f);
        switch ((e.animation ?? "steady").ToLowerInvariant())
        {
            case "pulse": return 0.25f + 0.75f * (0.5f - 0.5f * Mathf.Cos(ph * 2f * Mathf.PI));
            case "blink": return ph < 0.5f ? 1f : 0f;
            case "sweep": return pos <= ph ? 1f : 0.12f;                      // fills left to right
            case "sweepleft": return 1f - pos <= ph ? 1f : 0.12f;            // fills right to left
            case "sweepout": return Mathf.Abs(pos - 0.5f) * 2f <= ph ? 1f : 0.12f;   // from the middle out
            case "sweepin": return 1f - Mathf.Abs(pos - 0.5f) * 2f <= ph ? 1f : 0.12f;
            default: return 1f;
        }
    }

    void Blend(int x, int y, Color c, float level)
    {
        if (x < 0 || y < 0 || x >= _w || y >= _h) return;
        int i = y * _w + x;
        var d = (Color)_pixels[i];
        float a = c.a * Mathf.Clamp01(level);
        _pixels[i] = (Color32)new Color(d.r + (c.r - d.r) * a, d.g + (c.g - d.g) * a, d.b + (c.b - d.b) * a, Mathf.Max(d.a, a));
    }

    void Rect(Element e, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Mathf.RoundToInt(e.x * _w); y0 = Mathf.RoundToInt(e.y * _h);
        x1 = Mathf.RoundToInt((e.x + e.w) * _w); y1 = Mathf.RoundToInt((e.y + e.h) * _h);
    }

    void Shape(Element e, Color c, float t, Func<float, float, bool> inside)
    {
        Rect(e, out var x0, out var y0, out var x1, out var y1);
        float lw = Mathf.Max(1, x1 - x0), lh = Mathf.Max(1, y1 - y0);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                float u = (x - x0 + 0.5f) / lw, v = (y - y0 + 0.5f) / lh;
                if (inside(u, v)) Blend(x, y, c, Level(e, t, u));
            }
    }

    void LightBar(Element e, Color c, float t)
    {
        Rect(e, out var x0, out var y0, out var x1, out var y1);
        int n = Mathf.Max(1, e.segments);
        float seg = (x1 - x0) / (float)n, gap = Mathf.Max(1f, seg * 0.15f);
        for (int s = 0; s < n; s++)
        {
            float level = Level(e, t, (s + 0.5f) / n);
            int sx0 = Mathf.RoundToInt(x0 + s * seg + gap / 2), sx1 = Mathf.RoundToInt(x0 + (s + 1) * seg - gap / 2);
            for (int y = y0; y < y1; y++)
                for (int x = sx0; x < sx1; x++) Blend(x, y, c, level);
        }
    }

    void Chevrons(Element e, Color c, float t)
    {
        int n = Mathf.Max(1, e.segments);
        Rect(e, out var x0, out var y0, out var x1, out var y1);
        float lw = Mathf.Max(1, x1 - x0), lh = Mathf.Max(1, y1 - y0);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                var (u, v) = Orient(e.direction, (x - x0 + 0.5f) / lw, (y - y0 + 0.5f) / lh);
                float cell = u * n, f = cell - Mathf.Floor(cell);
                // a ">" in each cell, 0.25 of the cell thick
                float tip = Mathf.Abs(v - 0.5f);
                if (Mathf.Abs(f - (0.75f - tip)) < 0.2f) Blend(x, y, c, Level(e, t, (Mathf.Floor(cell) + 0.5f) / n));
            }
    }

    // turns (u, v) so that "right" points along +u
    static (float, float) Orient(string direction, float u, float v)
    {
        switch ((direction ?? "right").ToLowerInvariant())
        {
            case "left": return (1f - u, v);
            case "up": return (v, u);
            case "down": return (1f - v, u);
            default: return (u, v);
        }
    }

    static bool InArrow((float u, float v) p)
    {
        float u = p.u, v = p.v;
        if (u < 0.6f) return Mathf.Abs(v - 0.5f) < 0.15f;      // shaft
        return Mathf.Abs(v - 0.5f) < (1f - u) / 0.4f * 0.5f;    // head
    }

    void SetText(int i, Element e, Color c, float t)
    {
        while (_texts.Count <= i)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_image.transform.parent, false);
            go.layer = gameObject.layer;
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.fontStyle = FontStyle.Bold;
            _texts.Add(txt);
        }
        var tx = _texts[i];
        tx.enabled = true;
        Stretch((RectTransform)tx.transform, e.x, e.y, e.x + e.w, e.y + e.h);
        tx.text = e.text;
        tx.fontSize = Mathf.Max(1, Mathf.RoundToInt(e.h * Loaded.height * 1000f * 0.8f));
        c.a *= Level(e, t, 0.5f);
        tx.color = c;
    }

    public static Color ParseColor(string s, Color fallback) =>
        !string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c) ? c : fallback;
}
