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
/// Also used for displays inside the car (<see cref="InCarDisplay"/>), which add needle gauges and
/// bars driven by live values ("value": "{speed}"), elements shown only while a condition holds
/// ("when": "left") and timed messages.
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
        public string when = "";            // shown only while this holds (in-car displays: left, right, automated, ...); "!x" = not
        public string value = "";           // gauge, bar: the number shown, e.g. "{speed}" or "{rpm}"
        public float min = 0f, max = 100f;  // gauge, bar: range of the value
        public float startAngle = 225f, endAngle = -45f;   // gauge: degrees (0 = right, 90 = up)
        public int labels = 0;              // gauge: number of scale labels (min..max), 0 = none
        public float redFrom = float.MaxValue;   // gauge: red scale from this value (e.g. the rev limit)
        public float labelScale = 1f;       // gauge: labels show value × labelScale (e.g. 0.001 for rpm × 1000)
    }
    /// <summary>A message shown for a while: at a time, a distance driven, or when a state starts (in-car displays).</summary>
    [Serializable] public class Message
    {
        public float at = -1f;              // seconds after the start of the trial (-1: not by time)
        public float atDistance = -1f;      // metres driven
        public string onState = "";         // when this state starts (MANUAL, AUTOMATED, TAKEOVER)
        public float seconds = 5f;          // how long it stays
        public string display = "";         // schedule files: hud, centre, cluster (empty: every display)
        public string text = "";
        public string color = "#FFFFFF";
        public string background = "#000000B0";
        public float x = 0.05f, y = 0.05f, w = 0.9f, h = 0.22f;
        public string animation = "steady";
        public float period = 1f;
    }
    [Serializable] public class StateDesign { public string state = "WALK"; public Element[] elements = new Element[0]; }
    [Serializable] public class DesignFile
    {
        public string name = "";
        public float width = 1.2f, height = 0.15f;   // metres
        public string background = "#000000";         // with alpha, e.g. #00000000 for none
        public float resolution = 0f;                 // pixels per metre (0: the component's)
        public StateDesign[] states = new StateDesign[0];
        public Message[] messages = new Message[0];
    }

    public DesignFile Loaded { get; private set; }

    Texture2D _tex;
    Color32[] _pixels;
    RawImage _image;
    readonly List<Text> _texts = new List<Text>();
    HMIManager _manager;
    float _stateSince;
    int _w, _h;

    public static string PathOf(string design, string folder = "eHMI") => Path.Combine(Application.streamingAssetsPath, folder, design.EndsWith(".json") ? design : design + ".json");

    public static DesignFile Load(string design, string folder = "eHMI")
    {
        var path = PathOf(design, folder);
        if (!File.Exists(path)) { Debug.LogWarning("DesignedEhmi: no design file " + path); return null; }
        try { return JsonUtility.FromJson<DesignFile>(File.ReadAllText(path)); }
        catch (Exception e) { Debug.LogWarning($"DesignedEhmi: cannot read {path}: {e.Message}"); return null; }
    }

    /// <summary>Folder of the design files in StreamingAssets.</summary>
    protected virtual string Folder => "eHMI";
    /// <summary>Name of the state whose elements are shown (elements of the state "ANY" are always shown).</summary>
    protected virtual string StateKey => CurrentState.ToString();
    /// <summary>Text of a text element with its placeholders filled in.</summary>
    protected virtual string FillText(string text) => text;
    /// <summary>Whether a condition ("when") holds; unknown conditions hold.</summary>
    protected virtual bool When(string condition) => true;
    /// <summary>Elements shown on top of the state's (e.g. messages); kept as the same objects while shown.</summary>
    protected virtual void ExtraElements(List<Element> into) { }

    string _lastKey;

    protected virtual void Start()
    {
        Loaded = Load(Design, Folder) ?? new DesignFile();
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
        base.Display(state);
    }

    void Build()
    {
        if (Loaded.resolution > 0f) Resolution = Loaded.resolution;
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

    protected virtual void Update()
    {
        if (_tex == null) return;
        if (FollowEhmi >= 0 && _manager != null && FollowEhmi < _manager.Count) Display(_manager.GetState(FollowEhmi));
        var key = StateKey;
        if (key != _lastKey) { _lastKey = key; _stateSince = Time.time; }
        Draw(Time.time - _stateSince);
    }

    StateDesign Find(string name)
    {
        foreach (var s in Loaded.states) if (string.Equals(s.state, name, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    // ------------------------------------------------------------------ drawing

    readonly List<Element> _visible = new List<Element>();
    readonly List<RectTransform> _dyn = new List<RectTransform>();
    int _lastSignature = int.MinValue;

    bool Shown(Element e)
    {
        if (string.IsNullOrEmpty(e.when)) return true;
        var w = e.when.Trim();
        return w.StartsWith("!") ? !When(w.Substring(1).Trim()) : When(w);
    }

    void Draw(float t)
    {
        _visible.Clear();
        foreach (var state in new[] { Find("ANY"), Find(_lastKey ?? StateKey) })
            if (state != null) foreach (var e in state.elements) if (Shown(e)) _visible.Add(e);
        ExtraElements(_visible);
        // the texture holds what changes rarely (shapes, scales); redrawn only when that changes
        int sig = 17;
        unchecked
        {
            foreach (var e in _visible)
            {
                var type = (e.type ?? "rect").ToLowerInvariant();
                if (type == "text") continue;
                sig = sig * 31 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(e);
                sig = sig * 31 + Mathf.RoundToInt(Level(e, t, 0.5f) * 8f);
                if (type == "lightbar" || type == "chevrons" || type == "rect") sig = sig * 31 + Mathf.RoundToInt(Mathf.Repeat(t / Mathf.Max(0.05f, e.period), 1f) * 16f) * (e.animation != null && e.animation.StartsWith("sweep") ? 1 : 0);
            }
        }
        if (sig != _lastSignature)
        {
            _lastSignature = sig;
            var bg32 = (Color32)ParseColor(Loaded.background, new Color(0, 0, 0, 1));
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = bg32;
            foreach (var e in _visible)
            {
                var c = ParseColor(e.color, Color.cyan);
                switch ((e.type ?? "rect").ToLowerInvariant())
                {
                    case "lightbar": LightBar(e, c, t); break;
                    case "circle": Shape(e, c, t, (u, v) => (u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f) <= 0.25f); break;
                    case "arrow": Shape(e, c, t, (u, v) => InArrow(Orient(e.direction, u, v))); break;
                    case "chevrons": Chevrons(e, c, t); break;
                    case "gauge": GaugeScale(e, c); break;
                    case "bar": Shape(e, new Color(c.r, c.g, c.b, c.a * 0.22f), t, (u, v) => true); break;
                    case "text": break;
                    default: Shape(e, c, t, (u, v) => true); break;
                }
            }
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);
        }
        // texts, needles and bar fills: every frame (cheap, drawn by the GPU)
        int ti = 0, di = 0;
        foreach (var e in _visible)
        {
            var c = ParseColor(e.color, Color.cyan);
            switch ((e.type ?? "rect").ToLowerInvariant())
            {
                case "text": SetText(ti++, e, c, t); break;
                case "gauge": Gauge(e, c, t, ref ti, ref di); break;
                case "bar": BarFill(e, c, t, ref di); break;
            }
        }
        for (int i = ti; i < _texts.Count; i++) _texts[i].enabled = false;
        for (int i = di; i < _dyn.Count; i++) _dyn[i].gameObject.SetActive(false);
    }

    float Value(Element e)
    {
        var s = FillText(e.value);
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : e.min;
    }

    // the gauge's dial in pixels: centre and radius (round whatever the element's shape)
    void Dial(Element e, out float cx, out float cy, out float r)
    {
        Rect(e, out var x0, out var y0, out var x1, out var y1);
        cx = (x0 + x1) / 2f; cy = (y0 + y1) / 2f; r = Mathf.Min(x1 - x0, y1 - y0) / 2f;
    }

    float AngleOf(Element e, float value) => Mathf.Lerp(e.startAngle, e.endAngle, Mathf.InverseLerp(e.min, e.max, value));

    void GaugeScale(Element e, Color c)
    {
        Dial(e, out var cx, out var cy, out var r);
        int majors = Mathf.Max(1, e.segments);
        float sweep = e.startAngle - e.endAngle;
        float redF = e.redFrom < e.max ? Mathf.InverseLerp(e.min, e.max, e.redFrom) : 2f;
        var red = new Color(0.95f, 0.15f, 0.12f, c.a);
        int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r), y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
        float px = 1f / Mathf.Max(1f, r);   // one pixel in dial radii
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f - cx) / r, dy = (y + 0.5f - cy) / r, rr = Mathf.Sqrt(dx * dx + dy * dy);
                if (rr > 1f || rr < 0.7f) { if (rr < 0.09f) Blend(x, y, c, 1f); continue; }   // hub
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                // fraction along the scale (start -> end, either direction)
                float f = sweep >= 0f ? Mathf.Repeat(e.startAngle - ang, 360f) / Mathf.Max(1e-3f, sweep)
                                      : Mathf.Repeat(ang - e.startAngle, 360f) / Mathf.Max(1e-3f, -sweep);
                if (f > 1f + 0.5f / Mathf.Max(1, majors) * 0.05f) { if (Mathf.Repeat(f * Mathf.Abs(sweep), 360f) > 359.5f) f = 0f; else continue; }
                float onMajor = Mathf.Abs(f * majors - Mathf.Round(f * majors)) / majors * Mathf.Abs(sweep) * Mathf.Deg2Rad * rr;
                float onMinor = Mathf.Abs(f * majors * 2f - Mathf.Round(f * majors * 2f)) / (majors * 2f) * Mathf.Abs(sweep) * Mathf.Deg2Rad * rr;
                var col = f >= redF ? red : c;
                if (rr >= 0.97f && f >= 0f && f <= 1f) Blend(x, y, f >= redF ? red : new Color(c.r, c.g, c.b, c.a * 0.5f), 1f);   // rim
                else if (rr >= 0.78f && onMajor < 1.8f * px) Blend(x, y, col, 1f);
                else if (rr >= 0.86f && onMinor < 1.1f * px) Blend(x, y, col, 0.8f);
            }
    }

    RectTransform Dyn(int i)
    {
        while (_dyn.Count <= i)
        {
            var go = new GameObject("Needle", typeof(RectTransform));
            go.transform.SetParent(_image.transform.parent, false);
            go.layer = gameObject.layer;
            go.AddComponent<Image>();
            _dyn.Add((RectTransform)go.transform);
        }
        var rt = _dyn[i];
        rt.gameObject.SetActive(true);
        rt.SetAsLastSibling();
        return rt;
    }

    void Gauge(Element e, Color c, float t, ref int ti, ref int di)
    {
        Dial(e, out var cx, out var cy, out var r);
        // scale labels
        for (int k = 0; k < e.labels; k++)
        {
            float v = e.labels > 1 ? Mathf.Lerp(e.min, e.max, k / (float)(e.labels - 1)) : e.min;
            float a = AngleOf(e, v) * Mathf.Deg2Rad;
            float lx = cx + Mathf.Cos(a) * r * 0.58f, ly = cy + Mathf.Sin(a) * r * 0.58f, s = r * 0.25f;
            var label = new Element { type = "text", text = Mathf.RoundToInt(v * e.labelScale).ToString(CultureInfo.InvariantCulture),
                x = (lx - s) / _w, y = (ly - s * 0.5f) / _h, w = 2f * s / _w, h = s / _h, color = e.color };
            SetText(ti++, label, c, t);
        }
        // needle: from the hub outwards, turned to the value
        var needle = Dyn(di++);
        float value = Value(e);
        needle.anchorMin = needle.anchorMax = new Vector2(cx / _w, cy / _h);
        float scale = Loaded.width * 1000f / _w;     // canvas units (mm) per pixel
        needle.sizeDelta = new Vector2(Mathf.Max(1.5f, r * 0.05f) * scale, r * 0.9f * scale);
        needle.pivot = new Vector2(0.5f, 0.08f);
        needle.anchoredPosition = Vector2.zero;
        needle.localRotation = Quaternion.Euler(0f, 0f, AngleOf(e, value) - 90f);
        needle.GetComponent<Image>().color = e.redFrom < float.MaxValue && value >= e.redFrom ? new Color(1f, 0.25f, 0.2f, c.a) : new Color(1f, 0.42f, 0.1f, c.a);
    }

    void BarFill(Element e, Color c, float t, ref int di)
    {
        var fill = Dyn(di++);
        float f = Mathf.Clamp01(Mathf.InverseLerp(e.min, e.max, Value(e)));
        bool vertical = (e.direction ?? "").ToLowerInvariant() == "up";
        fill.anchorMin = new Vector2(e.x, e.y);
        fill.anchorMax = vertical ? new Vector2(e.x + e.w, e.y + e.h * f) : new Vector2(e.x + e.w * f, e.y + e.h);
        fill.pivot = new Vector2(0.5f, 0.5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.localRotation = Quaternion.identity;
        c.a *= Level(e, t, 0.5f);
        fill.GetComponent<Image>().color = c;
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
            // shrink long text to fit its box
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 1;
            txt.fontStyle = FontStyle.Bold;
            _texts.Add(txt);
        }
        var tx = _texts[i];
        tx.enabled = true;
        Stretch((RectTransform)tx.transform, e.x, e.y, e.x + e.w, e.y + e.h);
        tx.text = FillText(e.text);
        tx.fontSize = tx.resizeTextMaxSize = Mathf.Max(1, Mathf.RoundToInt(e.h * Loaded.height * 1000f * 0.8f));
        c.a *= Level(e, t, 0.5f);
        tx.color = c;
    }

    public static Color ParseColor(string s, Color fallback) =>
        !string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c) ? c : fallback;
}
