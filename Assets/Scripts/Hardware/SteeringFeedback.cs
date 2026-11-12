using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Force feedback on Logitech wheels (G25/G27/G29/G920/G923) through the Logitech Steering Wheel
/// SDK: a self-centring spring that gets stiffer with speed (like the self-aligning torque of the
/// tyres), damping, and a jolt on collisions. Needs LogitechSteeringWheelEnginesWrapper.dll from the
/// SDK in Assets/Plugins/x86_64 (Windows; not included, see the README) and Logitech G HUB; without
/// it this does nothing. Other wheels: their own software's centring spring.
/// </summary>
public static class SteeringFeedback
{
    const string Dll = "LogitechSteeringWheelEnginesWrapper";

    [DllImport(Dll)] static extern bool LogiSteeringInitialize(bool ignoreXInputControllers);
    [DllImport(Dll)] static extern bool LogiUpdate();
    [DllImport(Dll)] static extern bool LogiIsConnected(int index);
    [DllImport(Dll)] static extern bool LogiPlaySpringForce(int index, int offsetPercentage, int saturationPercentage, int coefficientPercentage);
    [DllImport(Dll)] static extern bool LogiPlayDamperForce(int index, int coefficientPercentage);
    [DllImport(Dll)] static extern bool LogiPlayFrontalCollisionForce(int index, int percentage);
    [DllImport(Dll)] static extern void LogiSteeringShutdown();

    static bool _tried, _available;

    public static bool Available => _available;

    static bool Init()
    {
        if (_tried) return _available;
        _tried = true;
        if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return false;
        try
        {
            _available = LogiSteeringInitialize(false);
            if (_available)
            {
                Application.quitting += Shutdown;
                Debug.Log("Steering wheel force feedback: Logitech SDK initialised");
            }
        }
        catch (DllNotFoundException) { Debug.Log("Steering wheel force feedback: Logitech SDK DLL not installed (optional)"); }
        catch (EntryPointNotFoundException) { }
        return _available;
    }

    /// <summary>Call every physics step for the participant's car: speed (km/h), and whether it just collided.</summary>
    public static void Update(float speedKmh, bool collision = false)
    {
        if (!Init()) return;
        try
        {
            if (!LogiUpdate() || !LogiIsConnected(0)) return;
            float v = Mathf.Abs(speedKmh);
            // weak centring when parked, firmer at speed (power steering at low speed)
            int coefficient = Mathf.RoundToInt(Mathf.Lerp(15f, 75f, Mathf.Clamp01(v / 80f)));
            LogiPlaySpringForce(0, 0, 70, coefficient);
            LogiPlayDamperForce(0, Mathf.RoundToInt(Mathf.Lerp(25f, 10f, Mathf.Clamp01(v / 50f))));
            if (collision) LogiPlayFrontalCollisionForce(0, 60);
        }
        catch (Exception) { _available = false; }
    }

    static void Shutdown()
    {
        try { LogiSteeringShutdown(); } catch (Exception) { }
    }
}
