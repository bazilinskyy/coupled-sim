using System.Linq;
using UnityEngine;

/// <summary>
/// Steering wheel calibration (F2 in the simulator): shows the connected devices and live raw axes
/// and buttons, then detects the steering axis (turn fully left and right), the throttle, brake and
/// clutch (press each fully and release) and the gear and blinker buttons, and saves the profile for
/// this PC. Works with any wheel the operating system sees as a game controller.
/// </summary>
public class WheelCalibration : MonoBehaviour
{
    public KeyCode ToggleKey = KeyCode.F2;

    enum Step { Off, Overview, Steer, Throttle, Brake, Clutch, GearForward, GearReverse, BlinkerLeft, BlinkerRight, BlinkerClear, Done }
    Step _step = Step.Off;
    WheelProfile _profile;
    float[] _min = new float[WheelProfile.RawAxisCount], _max = new float[WheelProfile.RawAxisCount], _start = new float[WheelProfile.RawAxisCount];
    Rect _window = new Rect(20, 20, 560, 520);

    void Update()
    {
        if (Input.GetKeyDown(ToggleKey)) _step = _step == Step.Off ? Step.Overview : Step.Off;
        if (_step == Step.Off) return;
        for (int i = 0; i < WheelProfile.RawAxisCount; i++)
        {
            float v = WheelProfile.Raw(i);
            _min[i] = Mathf.Min(_min[i], v);
            _max[i] = Mathf.Max(_max[i], v);
        }
        if (_step >= Step.GearForward && _step <= Step.BlinkerClear)
        {
            for (int b = 0; b < 20; b++)
            {
                if (!Input.GetKeyDown(KeyCode.JoystickButton0 + b)) continue;
                switch (_step)
                {
                    case Step.GearForward: _profile.GearForward = b; break;
                    case Step.GearReverse: _profile.GearReverse = b; break;
                    case Step.BlinkerLeft: _profile.BlinkerLeft = b; break;
                    case Step.BlinkerRight: _profile.BlinkerRight = b; break;
                    case Step.BlinkerClear: _profile.BlinkerClear = b; break;
                }
                _step++;
                break;
            }
        }
    }

    void ResetRange()
    {
        for (int i = 0; i < WheelProfile.RawAxisCount; i++) _start[i] = _min[i] = _max[i] = WheelProfile.Raw(i);
    }

    // the axis that moved most since the step started (excluding axes already used)
    int MostMoved(params int[] exclude)
    {
        int best = -1;
        float range = 0.3f;
        for (int i = 0; i < WheelProfile.RawAxisCount; i++)
        {
            if (exclude.Contains(i)) continue;
            float r = _max[i] - _min[i];
            if (r > range) { range = r; best = i; }
        }
        return best;
    }

    void OnGUI()
    {
        if (_step == Step.Off) return;
        _window = GUILayout.Window(77001, _window, DrawWindow, "Steering wheel calibration (F2)");
    }

    void DrawWindow(int id)
    {
        var names = Input.GetJoystickNames().Where(n => !string.IsNullOrEmpty(n)).ToArray();
        GUILayout.Label("Devices: " + (names.Length > 0 ? string.Join(", ", names) : "none found (check the USB connection and the driver)"));
        var active = WheelProfile.Active;
        GUILayout.Label("Profile in use: " + (active != null ? active.Name : "none (keyboard only)"));
        var axes = string.Join("  ", Enumerable.Range(0, 10).Select(i => $"{i}:{WheelProfile.Raw(i):+0.00;-0.00}"));
        GUILayout.Label("Raw axes " + axes);
        var pressed = Enumerable.Range(0, 20).Where(b => Input.GetKey(KeyCode.JoystickButton0 + b)).ToArray();
        GUILayout.Label("Buttons held: " + (pressed.Length > 0 ? string.Join(", ", pressed) : "-"));
        if (active != null)
            GUILayout.Label($"Mapped: steer {active.Steer:+0.00;-0.00}  throttle {active.Throttle:0.00}  brake {active.Brake:0.00}  clutch {active.Clutch:0.00}");
        GUILayout.Space(8);

        switch (_step)
        {
            case Step.Overview:
                GUILayout.Label("Calibrate to map any wheel and pedals. Keep the pedals released and the wheel centred, then press Start.");
                if (GUILayout.Button("Start calibration"))
                {
                    _profile = new WheelProfile { Name = "Calibrated: " + (names.FirstOrDefault() ?? "wheel"), DeviceNameContains = names.FirstOrDefault() ?? "" };
                    ResetRange();
                    _step = Step.Steer;
                }
                if (GUILayout.Button("Forget the calibration (use the built-in profile)")) WheelProfile.ForgetSaved();
                break;
            case Step.Steer:
                GUILayout.Label("Turn the wheel fully to the LEFT, then fully to the RIGHT, then back to the centre. Then press Next.");
                if (GUILayout.Button("Next"))
                {
                    int a = MostMoved();
                    if (a < 0) { GUILayout.Label("No axis moved."); break; }
                    _profile.SteerAxis = a;
                    // the value while turned left was seen first: compare the extremes with the start
                    _profile.SteerLeft = _min[a];
                    _profile.SteerRight = _max[a];
                    ResetRange();
                    _step = Step.Throttle;
                }
                break;
            case Step.Throttle:
            case Step.Brake:
            case Step.Clutch:
                GUILayout.Label($"Press the {_step.ToString().ToUpper()} pedal fully and release it. Then press Next{(_step == Step.Clutch ? " (or Skip without a clutch)" : "")}.");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Next"))
                {
                    int a = MostMoved(_profile.SteerAxis, _profile.ThrottleAxis, _step == Step.Clutch ? _profile.BrakeAxis : -9);
                    if (a >= 0)
                    {
                        // rest = the value at the start of the step; full = the extreme farthest from it
                        float rest = _start[a];
                        float full = Mathf.Abs(_max[a] - rest) > Mathf.Abs(_min[a] - rest) ? _max[a] : _min[a];
                        if (_step == Step.Throttle) { _profile.ThrottleAxis = a; _profile.ThrottleRest = rest; _profile.ThrottleFull = full; }
                        else if (_step == Step.Brake) { _profile.BrakeAxis = a; _profile.BrakeRest = rest; _profile.BrakeFull = full; }
                        else { _profile.ClutchAxis = a; _profile.ClutchRest = rest; _profile.ClutchFull = full; }
                        ResetRange();
                        _step++;
                    }
                }
                if (_step == Step.Clutch && GUILayout.Button("Skip")) { _profile.ClutchAxis = -1; _step++; }
                GUILayout.EndHorizontal();
                break;
            case Step.GearForward:
            case Step.GearReverse:
            case Step.BlinkerLeft:
            case Step.BlinkerRight:
            case Step.BlinkerClear:
                GUILayout.Label($"Press the button for: {_step}  (or Skip)");
                if (GUILayout.Button("Skip"))
                {
                    _step++;
                }
                break;
            case Step.Done:
                GUILayout.Label($"Steer axis {_profile.SteerAxis} ({_profile.SteerLeft:0.00} .. {_profile.SteerRight:0.00}), throttle axis {_profile.ThrottleAxis}, brake axis {_profile.BrakeAxis}, clutch axis {_profile.ClutchAxis}; " +
                                $"buttons: forward {_profile.GearForward}, reverse {_profile.GearReverse}, blinkers {_profile.BlinkerLeft}/{_profile.BlinkerRight}/{_profile.BlinkerClear}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save")) { _profile.Save(); _step = Step.Overview; }
                if (GUILayout.Button("Start again")) _step = Step.Overview;
                GUILayout.EndHorizontal();
                break;
        }
        if (_step != Step.Done && _step != Step.Overview && GUILayout.Button("Cancel")) _step = Step.Overview;
        if (GUILayout.Button("Close")) _step = Step.Off;
        GUI.DragWindow();
    }
}
