using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Continuous responses of the local participant (ExperimentRoleDefinition.Response, or the command
/// line option <c>-response button,slider</c>), logged every frame to
/// ExperimentLogs/Responses-{role}-{date}.csv:
/// - a hold-to-respond button, e.g. "safe to cross" (key B, gamepad right shoulder or right trigger,
///   right controller trigger of a headset), with its analog value 0-1 (triggers);
/// - a slider 0-100, e.g. trust or perceived risk (keys Z / X, gamepad d-pad down / up, right
///   thumbstick of a headset), changed at <see cref="SliderSpeed"/> per second.
/// A small panel in front of the participant's camera shows the label, the button state and the
/// slider (on screens and in headsets); <see cref="ResponseSettings.Hidden"/> hides it.
/// </summary>
public class ResponseInput : MonoBehaviour
{
    public KeyCode ButtonKey = KeyCode.B;
    public KeyCode SliderDownKey = KeyCode.Z;
    public KeyCode SliderUpKey = KeyCode.X;
    [Tooltip("Slider change per second while a key or the d-pad is held (full speed with a thumbstick).")]
    public float SliderSpeed = 40f;
    [Tooltip("Analog value from which a trigger counts as pressed.")]
    public float PressThreshold = 0.5f;

    public ResponseSettings Settings;
    public string RoleName = "";

    /// <summary>Current state: button pressed, its analog value (0-1) and the slider (0-100).</summary>
    public bool Pressed { get; private set; }
    public float ButtonValue { get; private set; }
    public float SliderValue { get; private set; }
    public int Presses { get; private set; }

    public static ResponseInput Local { get; private set; }

    StreamWriter _log;
    Canvas _canvas;
    Text _label;
    Image _buttonLight, _sliderFill;
    readonly List<UnityEngine.XR.InputDevice> _xr = new List<UnityEngine.XR.InputDevice>();

    /// <summary>Settings from the command line (<c>-response button,slider</c>), merged into the role's.</summary>
    public static ResponseSettings WithCommandLine(ResponseSettings s)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-response")
                foreach (var part in args[i + 1].ToLowerInvariant().Split(','))
                {
                    if (part.Trim() == "button") s.Button = true;
                    if (part.Trim() == "slider") s.Slider = true;
                    if (part.Trim() == "hidden") s.Hidden = true;
                }
        return s;
    }

    public void Init(ResponseSettings settings, string roleName)
    {
        Settings = settings;
        RoleName = roleName ?? "";
        SliderValue = Mathf.Clamp(settings.SliderStart, 0f, 100f);
        Local = this;
    }

    void OnDestroy()
    {
        if (Local == this) Local = null;
        _log?.Dispose();
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    void Update()
    {
        if (!Settings.Button && !Settings.Slider) return;
        ReadInput();
        Log();
        if (!Settings.Hidden) Show();
    }

    void ReadInput()
    {
        float trigger = 0f, stick = 0f;
        bool key = Input.GetKey(ButtonKey);
        int dir = (Input.GetKey(SliderUpKey) ? 1 : 0) - (Input.GetKey(SliderDownKey) ? 1 : 0);
        var pad = Gamepad.current;
        if (pad != null)
        {
            trigger = Mathf.Max(trigger, pad.rightTrigger.ReadValue(), pad.rightShoulder.isPressed ? 1f : 0f);
            dir += (pad.dpad.up.isPressed ? 1 : 0) - (pad.dpad.down.isPressed ? 1 : 0);
        }
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, _xr);
        foreach (var d in _xr)
        {
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out var t)) trigger = Mathf.Max(trigger, t);
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out var a) && Mathf.Abs(a.y) > 0.2f) stick = a.y;
        }
        ButtonValue = key ? 1f : trigger;
        bool pressed = key || trigger >= PressThreshold;
        if (pressed && !Pressed) Presses++;
        Pressed = pressed;
        if (Settings.Slider)
        {
            float rate = Mathf.Clamp(dir, -1, 1) + stick;
            SliderValue = Mathf.Clamp(SliderValue + Mathf.Clamp(rate, -1f, 1f) * SliderSpeed * Time.unscaledDeltaTime, 0f, 100f);
        }
    }

    void Log()
    {
        var ci = CultureInfo.InvariantCulture;
        if (_log == null)
        {
            Directory.CreateDirectory("ExperimentLogs");
            var role = string.Join("_", RoleName.Split(Path.GetInvalidFileNameChars()));
            _log = new StreamWriter($"ExperimentLogs/Responses-{NetworkingManager.LogPrefix}{(role.Length > 0 ? role + "-" : "")}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv") { AutoFlush = false };
            _log.WriteLine("utc;time;frame;button;button_value;presses;slider");
        }
        _log.WriteLine(string.Join(";", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", ci), Time.time.ToString("F4", ci), Time.frameCount.ToString(ci),
            Settings.Button ? (Pressed ? "1" : "0") : "", Settings.Button ? ButtonValue.ToString("F3", ci) : "", Settings.Button ? Presses.ToString(ci) : "",
            Settings.Slider ? SliderValue.ToString("F1", ci) : ""));
    }

    // ------------------------------------------------------------------ panel in front of the camera

    void Show()
    {
        var cam = ViewCamera();
        if (cam == null) return;
        if (_canvas == null || _canvas.transform.parent != cam.transform) Build(cam);
        if (_buttonLight != null) _buttonLight.color = Pressed ? new Color(0.2f, 0.85f, 0.3f, 0.95f) : new Color(0.25f, 0.25f, 0.25f, 0.8f);
        if (_sliderFill != null) _sliderFill.rectTransform.anchorMax = new Vector2(SliderValue / 100f, 1f);
        if (_label != null)
        {
            var parts = new List<string>();
            if (Settings.Button && !string.IsNullOrEmpty(Settings.ButtonLabel)) parts.Add(Settings.ButtonLabel);
            if (Settings.Slider) parts.Add($"{(string.IsNullOrEmpty(Settings.SliderLabel) ? "Slider" : Settings.SliderLabel)}: {SliderValue:F0}");
            _label.text = string.Join("   ", parts);
        }
    }

    // the participant's own camera: the enabled camera of this avatar with the highest depth, else the main camera
    Camera ViewCamera()
    {
        Camera best = null;
        foreach (var c in GetComponentsInChildren<Camera>())
            if (c.isActiveAndEnabled && c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
        return best != null ? best : Camera.main;
    }

    void Build(Camera cam)
    {
        if (_canvas != null) Destroy(_canvas.gameObject);
        var go = new GameObject("Response panel", typeof(Canvas));
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        go.transform.SetParent(cam.transform, false);
        // 0.7 m ahead, low in the view; 1 canvas unit = 1 mm
        go.transform.localPosition = new Vector3(0f, -0.16f, 0.7f);
        go.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.001f;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(320f, 60f);
        go.layer = cam.gameObject.layer;

        var bg = Child(go.transform, "Background", new Vector2(0, 0), new Vector2(1, 1));
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var labelGo = Child(go.transform, "Label", new Vector2(0.02f, 0.5f), new Vector2(0.98f, 1f));
        _label = labelGo.AddComponent<Text>();
        _label.font = font; _label.fontSize = 18; _label.alignment = TextAnchor.MiddleCenter; _label.color = Color.white;
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        if (Settings.Button)
        {
            var light = Child(go.transform, "Button", new Vector2(0.03f, 0.1f), new Vector2(0.13f, 0.45f));
            _buttonLight = light.AddComponent<Image>();
        }
        if (Settings.Slider)
        {
            var track = Child(go.transform, "Slider", new Vector2(Settings.Button ? 0.17f : 0.05f, 0.18f), new Vector2(0.95f, 0.37f));
            track.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
            var fill = Child(track.transform, "Fill", Vector2.zero, new Vector2(0.5f, 1f));
            _sliderFill = fill.AddComponent<Image>();
            _sliderFill.color = new Color(0.3f, 0.6f, 1f, 0.95f);
        }
        foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = go.layer;
    }

    static GameObject Child(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }
}
