using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// How a steering wheel and its pedals map to the simulator's controls: which raw joystick axis is
/// steering, throttle, brake and clutch, their raw values at rest and fully pressed, and which
/// buttons select gears and blinkers. Profiles come from <see cref="WheelCalibration"/> (saved per
/// PC in wheel-profile.json in the simulator's data folder) or the built-in defaults, chosen by the
/// name of the connected device.
/// </summary>
[Serializable]
public class WheelProfile
{
    public string Name = "Generic wheel (Logitech G27 layout)";
    [Tooltip("Matched (case-insensitive) against the connected joystick names; empty matches any.")]
    public string DeviceNameContains = "";

    // axes: 0-based indices of Unity's joystick axes (the Input Manager's "Raw Axis N")
    public int SteerAxis = 0;
    public float SteerLeft = -1f, SteerRight = 1f;
    [Tooltip("Steering wheel rotation lock to lock (degrees), for display and force feedback.")]
    public float SteerRangeDeg = 900f;
    public int ThrottleAxis = 2;
    public float ThrottleRest = 1f, ThrottleFull = -1f;
    public int BrakeAxis = 3;
    public float BrakeRest = 1f, BrakeFull = -1f;
    public int ClutchAxis = -1;
    public float ClutchRest = 1f, ClutchFull = -1f;

    // buttons: 0-based joystick button numbers (-1 = none)
    public int GearForward = 6, GearReverse = 7;
    public int BlinkerLeft = 5, BlinkerRight = 4, BlinkerClear = 16;
    public int Horn = -1;

    public static readonly WheelProfile[] BuiltIn =
    {
        // the lab's wheel: mapping used since the first version of the simulator
        new WheelProfile { Name = "Logitech G27", DeviceNameContains = "G27" },
        // DirectInput layouts as reported by Logitech's drivers; verify with the calibration (F2)
        new WheelProfile { Name = "Logitech G29 / G923 (PlayStation)", DeviceNameContains = "G29", ClutchAxis = 1 },
        new WheelProfile { Name = "Logitech G923", DeviceNameContains = "G923", ClutchAxis = 1 },
        new WheelProfile { Name = "Logitech G920 (Xbox)", DeviceNameContains = "G920", ClutchAxis = 1 },
        new WheelProfile { Name = "Thrustmaster T300/TX/T-GT", DeviceNameContains = "Thrustmaster", SteerRangeDeg = 1080f, ThrottleAxis = 5, BrakeAxis = 1, ClutchAxis = 6 },
        new WheelProfile { Name = "Fanatec CSL/ClubSport", DeviceNameContains = "Fanatec", SteerRangeDeg = 1080f, ThrottleAxis = 1, BrakeAxis = 2, ClutchAxis = 5 },
    };

    public static string SavePath => Path.Combine(Application.persistentDataPath, "wheel-profile.json");

    static WheelProfile _active;
    static float _nextDetect;

    /// <summary>The profile in use: the calibrated one, else a built-in one for the connected wheel; null without a wheel.</summary>
    public static WheelProfile Active
    {
        get
        {
            if (Time.unscaledTime >= _nextDetect)
            {
                _nextDetect = Time.unscaledTime + 5f; // wheels can be plugged in later
                _active = Detect();
            }
            return _active;
        }
    }

    public static WheelProfile Detect()
    {
        var names = Input.GetJoystickNames().Where(n => !string.IsNullOrEmpty(n)).ToArray();
        if (File.Exists(SavePath))
        {
            try
            {
                var saved = JsonUtility.FromJson<WheelProfile>(File.ReadAllText(SavePath));
                if (saved != null && (string.IsNullOrEmpty(saved.DeviceNameContains) || names.Any(n => n.IndexOf(saved.DeviceNameContains, StringComparison.OrdinalIgnoreCase) >= 0)))
                    return saved;
            }
            catch (Exception e) { Debug.LogWarning("WheelProfile: could not read " + SavePath + ": " + e.Message); }
        }
        if (names.Length == 0) return null;
        foreach (var p in BuiltIn)
            if (names.Any(n => n.IndexOf(p.DeviceNameContains, StringComparison.OrdinalIgnoreCase) >= 0)) return p;
        // unknown joystick: assume the G27 layout (as the simulator always did) until calibrated
        return new WheelProfile();
    }

    public void Save()
    {
        File.WriteAllText(SavePath, JsonUtility.ToJson(this, true));
        _active = this;
    }

    public static void ForgetSaved()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        _nextDetect = 0;
    }

    public static float Raw(int axis) => axis < 0 || axis >= RawAxisCount ? 0f : Input.GetAxisRaw("Raw Axis " + axis);
    public const int RawAxisCount = 20;

    static float Pedal(int axis, float rest, float full)
    {
        if (axis < 0 || Mathf.Approximately(rest, full)) return 0f;
        float v = Mathf.InverseLerp(rest, full, Raw(axis));
        return v < 0.03f ? 0f : v; // small dead zone at rest
    }

    public float Steer => SteerAxis < 0 ? 0f : Mathf.Clamp(Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(SteerLeft, SteerRight, Raw(SteerAxis))), -1f, 1f);
    public float Throttle => Pedal(ThrottleAxis, ThrottleRest, ThrottleFull);
    public float Brake => Pedal(BrakeAxis, BrakeRest, BrakeFull);
    public float Clutch => Pedal(ClutchAxis, ClutchRest, ClutchFull);

    public static bool ButtonDown(int button) => button >= 0 && button < 20 && Input.GetKeyDown(KeyCode.JoystickButton0 + button);
}

/// <summary>
/// Inputs of the participant's vehicle: keyboard (Input Manager axes) combined with the steering
/// wheel through the active <see cref="WheelProfile"/>.
/// </summary>
public static class VehicleInput
{
    static float Keyboard(string axis)
    {
        try { return Input.GetAxis(axis); }
        catch (ArgumentException) { return 0f; }
    }

    /// <summary>Axis value: "Horizontal" (steer -1..1), "Throttle" (-1..1, negative = back), "Break"/"Brake" (0..1).</summary>
    /// <summary>Input from a script or external device (tests, a motion platform, a bicycle trainer): return null to use the normal inputs.</summary>
    public static System.Func<string, float?> Override;
    /// <summary>Buttons from a script ("forward", "reverse", ...): return null to use the normal inputs.</summary>
    public static System.Func<string, bool?> ButtonOverride;

    public static float GetAxis(string axis)
    {
        var o = Override?.Invoke(axis);
        if (o.HasValue) return o.Value;
        float key = Keyboard(axis);
        var wheel = WheelProfile.Active;
        if (wheel == null) return key;
        float w;
        switch (axis)
        {
            case "Horizontal": w = wheel.Steer; break;
            case "Throttle": w = wheel.Throttle; break;
            case "Break":
            case "Brake": w = wheel.Brake; break;
            default: return key;
        }
        return Mathf.Abs(w) > Mathf.Abs(key) ? w : key;
    }

    /// <summary>Button pressed this frame: "forward", "reverse", "blinker_left", "blinker_right", "blinker_clear", "horn".</summary>
    public static bool GetButtonDown(string button)
    {
        var o = ButtonOverride?.Invoke(button);
        if (o.HasValue) return o.Value;
        bool key = false;
        try { key = Input.GetButtonDown(button); } catch (ArgumentException) { }
        var wheel = WheelProfile.Active;
        if (key || wheel == null) return key;
        switch (button)
        {
            case "forward": return WheelProfile.ButtonDown(wheel.GearForward);
            case "reverse": return WheelProfile.ButtonDown(wheel.GearReverse);
            case "blinker_left": return WheelProfile.ButtonDown(wheel.BlinkerLeft);
            case "blinker_right": return WheelProfile.ButtonDown(wheel.BlinkerRight);
            case "blinker_clear": return WheelProfile.ButtonDown(wheel.BlinkerClear);
            case "horn": return WheelProfile.ButtonDown(wheel.Horn);
            default: return false;
        }
    }
}
