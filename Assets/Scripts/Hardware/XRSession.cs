using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Starts virtual reality (OpenXR) when a participant uses a head-mounted display (HMD or suit
/// mode), so PCs without a headset run flat-screen roles without any VR runtime. OpenXR works with
/// Meta Quest (Link / Air Link), Varjo, HTC Vive, Valve Index, HP Reverb, Pico (via their PC
/// runtimes) and Windows Mixed Reality headsets: the active OpenXR runtime of the PC is used.
/// </summary>
public static class XRSession
{
    public static bool Running
    {
        get
        {
            var s = XRGeneralSettings.Instance;
            return s != null && s.Manager != null && s.Manager.activeLoader != null;
        }
    }

    public static bool Start()
    {
        if (Running) return true;
        var settings = XRGeneralSettings.Instance;
        if (settings == null || settings.Manager == null)
        {
            Debug.LogWarning("XR: XR Plug-in Management is not set up (Coupled Sim ➡️ Hardware ➡️ Set up).");
            return false;
        }
        settings.Manager.InitializeLoaderSync();
        if (settings.Manager.activeLoader == null)
        {
            Debug.LogWarning("XR: no headset found. Is it connected and switched on, and is its OpenXR runtime (Meta Quest Link, SteamVR, Varjo Base, Mixed Reality Portal) running and set as the active OpenXR runtime? See the README (Hardware).");
            return false;
        }
        settings.Manager.StartSubsystems();
        Application.quitting += Stop;
        var logger = new GameObject("Eye gaze logger").AddComponent<EyeGazeLogger>();
        Object.DontDestroyOnLoad(logger.gameObject);
        Debug.Log("XR: started " + settings.Manager.activeLoader.name);
        return true;
    }

    public static void Stop()
    {
        var settings = XRGeneralSettings.Instance;
        if (settings == null || settings.Manager == null || settings.Manager.activeLoader == null) return;
        settings.Manager.StopSubsystems();
        settings.Manager.DeinitializeLoader();
    }
}
