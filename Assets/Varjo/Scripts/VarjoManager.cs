// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Varjo
{
    public class VarjoManager : VarjoLayer
    {
        private static VarjoManager _instance = null;
        public static VarjoManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("Varjo VR Manager")
                    {
                        hideFlags = HideFlags.DontSave
                    };
                    Instance = go.AddComponent<VarjoManager>();
                }
                return _instance;
            }
            private set { _instance = value; }
        }

        [Tooltip("If true, enables VR support when Varjo system is present.")]
        public bool forceVRSupport = true;

        [Tooltip("If true, disables VSync when Varjo system is present.")]
        public bool disableVSync = true;

        /// <summary>
        /// Have we called BeginFrame without matching EndFrame.
        /// </summary>
        private bool beginFrameCalled = false;

        /// <summary>
        /// Thread that Unity is running. Used for making sure the loggings are displayed correctly even if they
        /// are called from other threads via plugin. This is set in Awake() to guarantee it's in main thread.
        /// </summary>
        private static Thread unityThread = null;
        /// <summary>
        /// A queue of log messages sent from other threads. Displayed and cleared per frame in main thread.
        /// </summary>
        private static Queue<string> logMessages = new Queue<string>();

        /// <summary>
        /// Cache the end of frame yield to reduce GC trashing.
        /// </summary>
        private YieldInstruction yieldEndOfFrame = new WaitForEndOfFrame();

        [NonSerialized]
        private bool debug = true;

        /// <summary>
        /// Last polled event type
        /// </summary>
        private ulong eventType = 0;

        /// <summary>
        /// Visibility status of application's layer in Varjo Compositor.
        /// </summary>
        public bool layerVisible { get; private set; }

        public delegate void VisibilityEvent(bool visible);
        public static event VisibilityEvent OnVisibilityEvent;

        public delegate void StandbyEvent(bool onStandby);
        public static event StandbyEvent OnStandbyEvent;

        public delegate void ForegroundEvent(bool onForeground);
        public static event ForegroundEvent OnForegroundEvent;

        public delegate void MRDeviceStatusEvent(bool connected);
        public static event MRDeviceStatusEvent OnMRDeviceStatusEvent;

        public delegate void MRCameraPropertyChangeEvent(VarjoCameraPropertyType type);
        public static event MRCameraPropertyChangeEvent OnMRCameraPropertyChangeEvent;

        public delegate void DataStreamStartEvent(long streamId);
        public static event DataStreamStartEvent OnDataStreamStartEvent;

        public delegate void DataStreamStopEvent(long streamId);
        public static event DataStreamStopEvent OnDataStreamStopEvent;

        private bool inStandBy = false;

        private static List<VarjoLayer> layers = new List<VarjoLayer>();

        public static void RegisterLayer(VarjoLayer layer)
        {
            layers.Add(layer);
        }

        public static void UnregisterLayer(VarjoLayer layer)
        {
            layers.Remove(layer);
        }

        /// <summary>
        /// List of events stored this frame.
        /// </summary>
        private List<VarjoPlugin.EventButton> buttonEvents = new List<VarjoPlugin.EventButton>();

        public bool VarjoSystemPresent
        {
            get;
            private set;
        }

        /// <summary>
        /// Return reference to VarjoPlugin
        /// </summary>
        public VarjoPlugin Plugin
        {
            get;
            private set;
        }

        public VarjoManager()
        {
            layerVisible = true;
        }

        private new void Awake()
        {
            unityThread = Thread.CurrentThread;

            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Debug.LogError("Multiple instances of VarjoManager. Destroying the new one.");
                Destroy(gameObject);
                return;
            }

            Plugin = new VarjoPlugin();

            VarjoSystemPresent = VarjoPlugin.IsVarjoSystemInstalled();
            if (!VarjoSystemPresent)
            {
                Debug.LogWarning("Varjo system not found.");
                gameObject.SetActive(false);
                return;
            }

            if (forceVRSupport)
            {
                XRSettings.enabled = true;
                if (XRSettings.supportedDevices != null && XRSettings.supportedDevices.Contains("OpenVR"))
                {
                    if (XRSettings.loadedDeviceName != "OpenVR")
                    {
                        XRSettings.LoadDeviceByName("OpenVR");
                    }
                }
                else
                {
                    Debug.LogError("OpenVR is not in the list of supported Virtual Reality SDKs. You need to add it to the list in Player Settings.");
                }
            }
            else
            {
                if (!XRSettings.enabled)
                {
                    Debug.LogError("Virtual Reality Support is not enabled. Enable Force VR Support in VarjoManager or enable Virtual Reality Support in Player Settings.");
                }
            }


            if (disableVSync)
            {
                QualitySettings.vSyncCount = 0;
            }

            base.Awake();

            if (varjoCamera)
            {
                varjoCamera.gameObject.tag = "MainCamera";
            }
            else
            {
                GameObject go = new GameObject("Varjo Camera");
                go.transform.SetParent(transform);
                varjoCamera = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }

            DisableVRCameras();
            DisableExtraAudioListeners();

            StartCoroutine(InitializeSession());
        }

        private void Start()
        {
            OnVisibilityEvent += VisibilityChange;
            OnStandbyEvent += StandbyChange;
            OnForegroundEvent += ForegroundChange;
        }

        IEnumerator InitializeSession()
        {
            Plugin.AttemptSessionInit();

            // If we failed to initialize the session, attempt to initialize a valid session until we get one.
            if (!VarjoPlugin.SessionValid)
            {
                Debug.LogWarning("Failed to initialize a Varjo session. Entering into poll mode...");

                EnableAllViewportCameras(false);

                while (!VarjoPlugin.SessionValid)
                {
                    bool wait = true;
                    Plugin.AttemptSessionInitThreaded(() => wait = false);
                    while (wait) yield return new WaitForSecondsRealtime(0.5f);
                }

                EnableAllViewportCameras(true);
            }

            int viewCount = VarjoPlugin.GetViewCount();
            if (viewCount != 4)
            {
                Debug.LogErrorFormat("Only 4 views are supported by this plugin, however Varjo Runtime reports {0} views", viewCount);
                EnableAllViewportCameras(false);
                while (true) yield return new WaitForSecondsRealtime(0.5f);
            }

            Debug.Log("Varjo session init successful");

            StartCoroutine(EndFrameCoroutine());
        }

        private void OnApplicationQuit()
        {
            if (Plugin != null)
            {
                Plugin.Destroy();
            }
        }

        private VarjoPlugin.FramePoseData latestFramePose = new VarjoPlugin.FramePoseData();

        public void GetPose(VarjoPlugin.PoseType type, out VarjoPlugin.Matrix matrix)
        {
            matrix = latestFramePose.eyePoses[(int)type - 1];
        }

        public Vector3 GetHMDPosition(VarjoPlugin.PoseType type)
        {
            VarjoPlugin.Matrix matrix;
            GetPose(type, out matrix);

            var unityView = VarjoMatrixUtils.WorldMatrixToUnity(matrix.value);
            return unityView.GetColumn(3);
        }

        public Quaternion GetHMDOrientation(VarjoPlugin.PoseType type)
        {
            VarjoPlugin.Matrix matrix;
            GetPose(type, out matrix);

            var unityView = VarjoMatrixUtils.WorldMatrixToUnity(matrix.value);
            return Quaternion.LookRotation(unityView.GetColumn(2), unityView.GetColumn(1));
        }

        public VarjoPlugin.ViewInfo GetViewInfo(int viewIndex)
        {
            return latestFramePose.views[viewIndex];
        }

        private void Update()
        {
            // Empty the lock message queue
            lock (logMessages)
            {
                while (logMessages.Count > 0)
                {
                    var txt = logMessages.Dequeue();
                    if (debug) Debug.Log(txt);
                }
            }

            if (!VarjoPlugin.SessionValid)
                return;

            // Update events
            buttonEvents.Clear();
            Profiler.BeginSample("Varjo.PollEvents");
            while (VarjoPlugin.PollEvent(ref eventType))
            {
                switch ((VarjoPlugin.EventType)eventType)
                {
                    // Keep track of application visibility and standby status
                    // and enable and disable rendering based on that
                    case VarjoPlugin.EventType.EVENT_VISIBILITY:
                        layerVisible = VarjoPlugin.GetEventVisibility().visible != 0;
                        if (OnVisibilityEvent != null)
                            OnVisibilityEvent(layerVisible);
                        break;

                    case VarjoPlugin.EventType.EVENT_HEADSET_STANDBY_STATUS:
                        inStandBy = VarjoPlugin.GetEventHeadsetStandbyStatus().onStandby != 0;
                        if (OnStandbyEvent != null)
                            OnStandbyEvent(inStandBy);
                        break;

                    case VarjoPlugin.EventType.EVENT_FOREGROUND:
                        if (OnForegroundEvent != null)
                            OnForegroundEvent(VarjoPlugin.GetEventForeground().isForeground != 0);
                        break;

                    // Update headset button states
                    case VarjoPlugin.EventType.EVENT_BUTTON:
                        buttonEvents.Add(VarjoPlugin.GetEventButton());
                        break;

                    case VarjoPlugin.EventType.EVENT_MR_DEVICE_STATUS:
                        if (OnMRDeviceStatusEvent != null)
                            OnMRDeviceStatusEvent(VarjoPlugin.GetEventMRDeviceStatus().status == VarjoPlugin.MRDeviceStatus.Connected);
                        break;

                    case VarjoPlugin.EventType.EVENT_MR_CAMERA_PROPERTY_CHANGE:
                        if (OnMRCameraPropertyChangeEvent != null)
                            OnMRCameraPropertyChangeEvent(VarjoPlugin.GetEventMRCameraPropertyChange().type);
                        break;

                    case VarjoPlugin.EventType.EVENT_DATA_STREAM_START:
                        if (OnDataStreamStartEvent != null)
                            OnDataStreamStartEvent(VarjoPlugin.GetEventDataStreamStart().streamId);
                        break;

                    case VarjoPlugin.EventType.EVENT_DATA_STREAM_STOP:
                        if (OnDataStreamStopEvent != null)
                            OnDataStreamStopEvent(VarjoPlugin.GetEventDataStreamStop().streamId);
                        break;
                }
            }
            Profiler.EndSample();

            // Call waitsync and sync the render thread
            if (layerVisible && !inStandBy)
            {
                Profiler.BeginSample("Varjo.WaitSync");
                Plugin.IssuePluginEvent(VarjoPlugin.VARJO_RENDER_EVT_WAITSYNC);
                VarjoPlugin.SyncRenderThread();
                Profiler.EndSample();
            }

            // Fetch fresh pose data and cache it
            VarjoPlugin.GetFramePoseData(ref latestFramePose);

            // Update layers
            foreach (var layer in layers)
                layer.UpdateFromManager();

            // Start rendering only if layer is visible
            if (layerVisible && !inStandBy)
            {
                if (!beginFrameCalled)
                {
                    EnableAllViewportCameras(true);
                    Plugin.IssuePluginEvent(VarjoPlugin.VARJO_RENDER_EVT_BEGIN_FRAME);
                    beginFrameCalled = true;
                }
            }
            else
            {
                EnableAllViewportCameras(false);
            }
        }

        void VisibilityChange(bool visible)
        {
            EnableAllViewportCameras(visible && !inStandBy);
        }

        void StandbyChange(bool onStandby)
        {
            EnableAllViewportCameras(layerVisible && !inStandBy);
        }

        void ForegroundChange(bool inForeground)
        {
            // Nothing to do here, just make sure the delegate is not null
        }

        // Enable/disable all viewport cameras in all layers
        void EnableAllViewportCameras(bool enable)
        {
            foreach (var layer in layers)
                layer.EnableCameras(enable);
        }

        // Disable all existing stereo cameras, but make sure we always have one dummy camera for OpenVR.
        void DisableVRCameras()
        {
            if (XRSettings.eyeTextureHeight > 18)
            {
                XRSettings.eyeTextureResolutionScale = 18f / (float)XRSettings.eyeTextureHeight;
            }

            var stereoCamerasFound = false;

            foreach (var camera in Camera.allCameras)
            {
                if (camera != varjoCamera)
                {
                    if (camera.stereoTargetEye != StereoTargetEyeMask.None)
                    {
                        DisableVRCamera(camera);
                        if (stereoCamerasFound)
                        {
                            camera.enabled = false;
                        }
                        stereoCamerasFound = true;
                    }
                    if (camera.tag.Equals("MainCamera"))
                    {
                        camera.tag = "Untagged";
                    }
                }
            }

            if (!stereoCamerasFound)
            {
                var go = new GameObject("[Dummy VR Camera]");
                go.transform.SetParent(varjoCamera.transform);
                var dummyCamera = go.AddComponent<Camera>();
                dummyCamera.stereoTargetEye = StereoTargetEyeMask.Both;
                DisableVRCamera(dummyCamera);
            }
        }

        void DisableVRCamera(Camera camera)
        {
            XRDevice.DisableAutoXRCameraTracking(camera, true);
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.cullingMask = 0;
            camera.depth = -99;
        }

        // Disable all existing AudioListeners except the one on VarjoCamera.
        void DisableExtraAudioListeners()
        {
            AudioListener[] audioListeners = FindObjectsOfType(typeof(AudioListener)) as AudioListener[];

            foreach (var audioListener in audioListeners)
            {
                if (audioListener.gameObject != varjoCamera.gameObject)
                {
                    audioListener.enabled = false;
                }
            }
        }

        private static List<VarjoPlugin.MultiProjLayer> submission = new List<VarjoPlugin.MultiProjLayer>();

        private IEnumerator EndFrameCoroutine()
        {
            while (true)
            {
                yield return yieldEndOfFrame;

                if (!VarjoPlugin.SessionValid || !beginFrameCalled)
                {
                    Profiler.BeginSample("Varjo.EndOfFrame.ThrottleFor100ms");
                    // Sleep for 100ms so that we won't hog the CPU
                    Thread.Sleep(100);
                    // Still poll events if we have a session
                    if (VarjoPlugin.SessionValid)
                        Plugin.IssuePluginEvent(VarjoPlugin.VARJO_RENDER_EVT_POLL_EVENTS);
                    GL.Flush();
                    Profiler.EndSample();
                    continue;
                }

                Profiler.BeginSample("Varjo.EndOfFrame");

                if (VarjoManager.Instance.viewportCameras == null || VarjoManager.Instance.viewportCameras.Count != 4)
                {
                    VarjoManager.LogError("VarjoViewCombiner can't access a proper viewport array.");
                    continue;
                }

                GL.sRGBWrite = true;

                Profiler.BeginSample("Varjo.Submit");

                submission.Clear();
                // Sort the layers according to layer depth
                foreach (var varjoLayer in layers.OrderBy(l => l.layerOrder))
                {
                    if (varjoLayer.layerEnabled)
                        submission.Add(varjoLayer.PrepareForSubmission());
                }

                var subArray = submission.ToArray();
                VarjoPlugin.QueueSubmission(subArray.Length, subArray);

                Profiler.EndSample();

                // Blit to screen if SRPs are in use
                if (GraphicsSettings.renderPipelineAsset != null)
                {
                    Profiler.BeginSample("Varjo.BlitToScreen");
                    // Blit left context of the main layer to screen
                    if (flipY)
                        Graphics.Blit(GetRenderTextureForCamera(VarjoViewCamera.CAMERA_ID.CONTEXT_LEFT), (RenderTexture)null, new Vector2(contextDisplayFactor, contextDisplayFactor), new Vector2(0.0f, 1.0f - contextDisplayFactor));
                    else
                        Graphics.Blit(GetRenderTextureForCamera(VarjoViewCamera.CAMERA_ID.CONTEXT_LEFT), (RenderTexture)null, new Vector2(contextDisplayFactor, -1.0f * contextDisplayFactor), new Vector2(0.0f, contextDisplayFactor));

                    Profiler.EndSample();

                }

                Plugin.IssuePluginEvent(VarjoPlugin.VARJO_RENDER_EVT_SUBMIT);
                GL.InvalidateState();

                beginFrameCalled = false;
                Profiler.EndSample();

            }
        }

        public static void Log(string txt)
        {
            txt = DateTime.Now.Ticks + ": " + txt;

            // If keepLogOrder is set to true, the order of the logs will be real. The downside is that all the logs are
            // postponed until VarjoManager.Update is called - also those called from the main thread. This can cause you
            // to lose some logs if Unity happens to crash or freeze.
            bool keepLogOrder = false;

            if (keepLogOrder || Thread.CurrentThread != unityThread)
            {
                lock (logMessages)
                {
                    logMessages.Enqueue(txt);
                }
                return;
            }

            if (Instance.debug) Debug.Log(txt);
            //Console.WriteLine(txt);
        }

        /// <summary>
        /// Returns true when headset button gets pressed.
        /// </summary>
        /// <param name="buttonId">Id of headset button. 0 is application button.</param>
        /// <returns></returns>
        public bool GetButtonDown(int buttonId = 0)
        {
            for (int i = buttonEvents.Count - 1; i >= 0; --i)
            {
                if (buttonEvents[i].buttonId == buttonId)
                {
                    return buttonEvents[i].pressed != 0;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when headset button gets released.
        /// </summary>
        /// <param name="buttonId">Id of headset button. 0 is application button.</param>
        /// <returns></returns>
        public bool GetButtonUp(int buttonId = 0)
        {
            for (int i = buttonEvents.Count - 1; i >= 0; --i)
            {
                if (buttonEvents[i].buttonId == buttonId)
                {
                    return buttonEvents[i].pressed == 0;
                }
            }

            return false;
        }

        /// <summary>
        /// Is this application currently visible in Varjo Compositor.
        /// </summary>
        /// <returns></returns>
        public bool IsLayerVisible()
        {
            return layerVisible;
        }

        /// <summary>
        /// Is headset currently in stand by.
        /// </summary>
        /// <returns></returns>
        public bool IsInStandBy()
        {
            return inStandBy;
        }

        public static void LogWarning(string txt) { Debug.LogWarning(txt); }
        public static void LogError(string txt) { Debug.LogError(txt); }
    }
}
