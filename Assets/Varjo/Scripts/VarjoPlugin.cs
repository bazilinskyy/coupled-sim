// Copyright 2019 Varjo Technologies Oy. All rights reserved.

// Do note that defining USE_DEBUG_LOG makes Unity freeze upon quit.
//#define VARJO_USE_DEBUG_LOG

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Varjo
{
    public class VarjoPlugin
    {
        public const int VARJO_RENDER_EVT_BEGIN_FRAME = 10;
        public const int VARJO_RENDER_EVT_SUBMIT = 11;
        // Process the swapchain create and delete queues in the gfx thread
        public const int VARJO_RENDER_EVT_PROCESS_SWAPCHAINS = 13;
        public const int VARJO_RENDER_EVT_POLL_EVENTS = 14;
        public const int VARJO_RENDER_EVT_WAITSYNC = 20;

        public const int RESET_ROTATION_NONE = 0;
        public const int RESET_ROTATION_YAW = 1;
        public const int RESET_ROTATION_ALL = 7;

        [StructLayout(LayoutKind.Sequential)]
        public struct GazeRay
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public double[] position;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public double[] forward;
        }

        public enum GazeStatus : long
        {
            INVALID = 0,
            ADJUST = 1,
            VALID = 2
        }

        public enum GazeEyeStatus : long
        {
            EYE_INVALID = 0,
            EYE_VISIBLE = 1,
            EYE_COMPENSATED = 2,
            EYE_TRACKED = 3
        }

        public enum GazeEyeCalibrationQuality
        {
            INVALID = 0,
            LOW = 1,
            MEDIUM = 2,
            HIGH = 3
        }

        public enum EventType
        {
            EVENT_VISIBILITY = 1,
            EVENT_BUTTON = 2,
            EVENT_HEADSET_STANDBY_STATUS = 6,
            EVENT_FOREGROUND = 7,
            EVENT_MR_DEVICE_STATUS = 8,
            EVENT_MR_CAMERA_PROPERTY_CHANGE = 9,
            EVENT_DATA_STREAM_START = 10,
            EVENT_DATA_STREAM_STOP = 11,
        }

        public enum PoseType
        {
            LEFT_EYE = 1,
            CENTER = 2,
            RIGHT_EYE = 3
        }

        public enum ResetRotation
        {
            NONE = 0,
            YAW = 1,
            ALL = 7
        }

        public enum EulerOrder
        {
            XYZ = 0,
            ZYX = 1,
            XZY = 2,
            YZX = 3,
            YXZ = 4,
            ZXY = 5
        }

        public enum RotationDirection
        {
            Clockwise = -1,
            CounterClockwise = 1
        }

        public enum Handedness
        {
            RightHanded = 1,
            LeftHanded = -1
        }

        public enum MRDeviceStatus : long
        {
            Connected = 1,
            Disconnected = 2,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GazeData
        {
            public GazeRay left;
            public GazeRay right;
            public GazeRay gaze;
            public double focusDistance;
            public double focusStability;
            public long captureTime;
            public GazeEyeStatus leftStatus;
            public GazeEyeStatus rightStatus;
            public GazeStatus status;
            public long frameNumber;
            public double leftPupilSize;
            public double rightPupilSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GazeCalibrationQuality
        {
            public GazeEyeCalibrationQuality left;
            public GazeEyeCalibrationQuality right;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GazeCalibrationParameters
        {
            [MarshalAs(UnmanagedType.LPStr)] public string key;
            [MarshalAs(UnmanagedType.LPStr)] public string value;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RenderTextureFormat
        {
            public int width;
            public int height;
            public int format;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Matrix
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public double[] value;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct Vector3D
        {
            public double x;
            public double y;
            public double z;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventBase
        {
            public ulong type;
            public long timeStamp;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventVisibility
        {
            public uint visible;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventButton
        {
            public uint pressed;
            public byte buttonId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventHeadsetStandbyStatus
        {
            public uint onStandby;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventForeground
        {
            public uint isForeground;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventMRDeviceStatus
        {
            public MRDeviceStatus status;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventMRCameraPropertyChange
        {
            public VarjoCameraPropertyType type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventDataStreamStart
        {
            public long streamId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EventDataStreamStop
        {
            public long streamId;
        }

        [DllImport("VarjoUnity")] public static extern IntPtr GetRenderEventFunc();
#if VARJO_USE_DEBUG_LOG
        [DllImport("VarjoUnity")] private static extern void SetDebugLog(IntPtr fp);
#endif

        [DllImport("VarjoUnity")] private static extern bool InitSession();
        [DllImport("VarjoUnity")] private static extern void EndSession();
        [DllImport("VarjoUnity")] private static extern bool IsSessionNull();
        [DllImport("VarjoUnity")] public static extern void SetSessionPriority(int priority);

        [DllImport("VarjoUnity")] public static extern bool IsVarjoSystemInstalled();
        [DllImport("VarjoUnity")] public static extern bool IsHMDConnected();

        [DllImport("VarjoUnity")] public static extern int GetViewCount();

        [DllImport("VarjoUnity")] public static extern RenderTextureFormat GetRenderTextureFormat(int cameraId);

        [DllImport("VarjoUnity")] public static extern bool PollEvent(ref ulong eventType);
        [DllImport("VarjoUnity")] public static extern EventVisibility GetEventVisibility();
        [DllImport("VarjoUnity")] public static extern EventButton GetEventButton();
        [DllImport("VarjoUnity")] public static extern EventHeadsetStandbyStatus GetEventHeadsetStandbyStatus();
        [DllImport("VarjoUnity")] public static extern EventForeground GetEventForeground();
        [DllImport("VarjoUnity")] public static extern EventMRDeviceStatus GetEventMRDeviceStatus();
        [DllImport("VarjoUnity")] public static extern EventMRCameraPropertyChange GetEventMRCameraPropertyChange();
        [DllImport("VarjoUnity")] public static extern EventDataStreamStart GetEventDataStreamStart();
        [DllImport("VarjoUnity")] public static extern EventDataStreamStop GetEventDataStreamStop();

        [DllImport("VarjoUnity")] private static extern void GetEulerAngles(ref Matrix matrix, long order, long rotation, long handedness, ref Vector3D eulerAngles);

        [DllImport("VarjoUnity")] public static extern int GetError();
        [DllImport("VarjoUnity")] private static extern IntPtr GetErrorMessage(int error);

        [DllImport("VarjoUnity")] public static extern bool InitGaze();
        [DllImport("VarjoUnity")] public static extern bool IsGazeAllowed();
        [DllImport("VarjoUnity")] public static extern bool IsGazeCalibrated();
        [DllImport("VarjoUnity")] public static extern bool IsGazeCalibrating();

        [DllImport("VarjoUnity")] public static extern GazeCalibrationQuality GetGazeCalibrationQuality();

        [DllImport("VarjoUnity")] public static extern GazeData GetGaze();
        [DllImport("VarjoUnity")] public static extern bool GetOldestGazeIfAvailable(ref GazeData data);

        [DllImport("VarjoUnity")] public static extern void RequestGazeCalibration();
        [DllImport("VarjoUnity")] private static extern void RequestGazeCalibrationWithParameters(GazeCalibrationParameters[] parameters, int parametersCount);

        [DllImport("VarjoUnity")] public static extern double GetIPDEstimate();

        [DllImport("VarjoUnity")] private static extern void ResetPose(bool position, int rotation);

        public const long varjo_TextureFormat_R8G8B8A8_SRGB = 0x1;  //!< sRgb 8-bit RGBA format
        public const long varjo_TextureFormat_B8G8R8A8_SRGB = 0x2;  //!< sRgb 8-bit BGRA format
        public const long varjo_DepthTextureFormat_D32_FLOAT = 0x3;

        public const int varjo_SpaceLocal = 0x0;
        public const int varjo_SpaceView = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        public struct SwapchainConfig
        {
            public long format;            //!< Texture format (varjo_TextureFormat)
            public int numberOfTextures;  //!< Number of swap chain textures.
            public int textureWidth;      //!< Texture width in pixels.
            public int textureHeight;     //!< Texture height in pixels.
            public int arraySize;         //!< Texture array size or 1 if not a texture array
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VarjoTexture
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public long[] reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SwapchainViewport
        {
            public IntPtr swapchain;
            public int x, y;
            public int width, height;
            public int arrayIndex;
            public int reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MultiProjView
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public double[] projectionMatrix;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public double[] invViewMatrix;

            public SwapchainViewport color;
            public IntPtr unityColorTex;
            public int hasDepth; // If non-0, has depth as well
            public SwapchainViewport depth;
            public IntPtr unityDepthTex;
            public double minDepth;
            public double maxDepth;
            public double nearZ;
            public double farZ;
            public double depthTestNearZ;
            public double depthTestFarZ;
            public int depthTestRangeEnabled;
        }

        public const int varjo_LayerFlag_Opaque = 0x1;
        public const int varjo_LayerFlag_AlphaBlend = 0x2;
        public const int varjo_LayerFlag_DepthTesting = 0x4;
        public const int varjo_LayerFlag_UseOcclusionMesh = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        public struct MultiProjLayer
        {
            public int space;
            public int flags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public MultiProjView[] views;
        }

        [DllImport("VarjoUnity")] private static extern IntPtr QueueCreateD3D11Swapchain(SwapchainConfig config);

        [DllImport("VarjoUnity")] public static extern IntPtr QueueDestroySwapchain(IntPtr swapchain);

        // Enqueue a frame submission to take place the next time VARJO_RENDER_EVENT_GFX_SUBMIT_FRAME plugin event gets called
        [DllImport("VarjoUnity")] public static extern void QueueSubmission(int layerCount, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] MultiProjLayer[] layers);

        public static IntPtr CreateD3D11Swapchain(SwapchainConfig config)
        {
            var res = QueueCreateD3D11Swapchain(config);
            GL.IssuePluginEvent(GetRenderEventFunc(), VARJO_RENDER_EVT_PROCESS_SWAPCHAINS);
            return res;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ViewInfo
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public double[] projectionMatrix;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public double[] invViewMatrix;
            public int preferredWidth;
            public int preferredHeight;
            public int enabled;
            public int reserved;
        }

        public class FramePoseData
        {
            public ViewInfo[] views = new VarjoPlugin.ViewInfo[4] {
                new VarjoPlugin.ViewInfo
                { projectionMatrix = new double[16], invViewMatrix = new double[16] },
                new VarjoPlugin.ViewInfo
                { projectionMatrix = new double[16], invViewMatrix = new double[16] },
                new VarjoPlugin.ViewInfo
                { projectionMatrix = new double[16], invViewMatrix = new double[16] },
                new VarjoPlugin.ViewInfo
                { projectionMatrix = new double[16], invViewMatrix = new double[16] }
            };
            public Matrix[] eyePoses = new VarjoPlugin.Matrix[3] {
                new VarjoPlugin.Matrix { value = new double[16] },
                new VarjoPlugin.Matrix { value = new double[16] },
                new VarjoPlugin.Matrix { value = new double[16] }
            };
        }

        private static readonly int m_ViewInfoSize = 16 * 8 + 16 * 8 + 4 * 4;
        private static readonly int m_EyePoseSize = 16 * 8;
        private static readonly int m_DataSize = 4 * m_ViewInfoSize + 3 * m_EyePoseSize;
        private static readonly IntPtr m_FramePoseNativeBuffer = Marshal.AllocHGlobal(m_DataSize);

        ~VarjoPlugin()
        {
            Marshal.FreeHGlobal(m_FramePoseNativeBuffer);
        }

        // Get the frame info and view poses atomically (guaranteed to all be from the same frame)
        [DllImport("VarjoUnity", EntryPoint = "GetFramePoseData")] private static extern void GetFramePoseDataRaw(IntPtr outData);

        public static void GetFramePoseData(ref FramePoseData outData)
        {
            IntPtr pNativeData = m_FramePoseNativeBuffer;
            GetFramePoseDataRaw(pNativeData);

            // The actual functionality: Fill out the native FramePoseData
            for (int viewI = 0; viewI < 4; ++viewI)
            {
                ViewInfo vi = outData.views[viewI];
                Marshal.Copy(pNativeData, vi.projectionMatrix, 0, 16);
                pNativeData = (IntPtr)(pNativeData.ToInt64() + 16 * 8);
                Marshal.Copy(pNativeData, vi.invViewMatrix, 0, 16);
                pNativeData = (IntPtr)(pNativeData.ToInt64() + 16 * 8);
                vi.preferredWidth = Marshal.ReadInt32(pNativeData);
                pNativeData = (IntPtr)(pNativeData.ToInt64() + 4);
                vi.preferredHeight = Marshal.ReadInt32(pNativeData);
                pNativeData = (IntPtr)(pNativeData.ToInt64() + 4);
                vi.enabled = Marshal.ReadInt32(pNativeData);
                pNativeData = (IntPtr)(pNativeData.ToInt64() + 4);
                vi.reserved = Marshal.ReadInt32(pNativeData);
                pNativeData = (IntPtr)(pNativeData.ToInt64() + 4);
                outData.views[viewI] = vi;
            }
            for (int eyeI = 0; eyeI < 3; ++eyeI)
            {
                Marshal.Copy(pNativeData, outData.eyePoses[eyeI].value, 0, 16);
                pNativeData = (IntPtr)(pNativeData.ToInt64() + 16 * 8);
            }

            foreach (var pose in outData.eyePoses)
                makeIdentityIfZeroMatrix(pose.value);
            foreach (var view in outData.views)
            {
                makeIdentityIfZeroMatrix(view.invViewMatrix);
                makeIdentityIfZeroMatrix(view.projectionMatrix);
            }
        }

        [DllImport("VarjoUnity")] public static extern void SyncRenderThread();

        [DllImport("VarjoUnity", EntryPoint = "CreateOcclusionMesh")] private static extern IntPtr CreateOcclusionMesh(int viewIndex, out int vertexCount, out IntPtr vertexData);

        [DllImport("VarjoUnity")] private static extern void FreeOcclusionMesh(IntPtr mesh);

        // Retrieve the occlusion mesh, store it as a triangle list into a float[], with alternating x and y coordinates
        public static float[] GetOcclusionMesh(int viewIndex)
        {
            int vertCount = 0;
            IntPtr vertexData;
            var occMesh = CreateOcclusionMesh((int)viewIndex, out vertCount, out vertexData);
            if (occMesh != (IntPtr)0)
            {
                var res = new float[vertCount * 2];
                if (vertCount > 0)
                    System.Runtime.InteropServices.Marshal.Copy(vertexData, res, 0, vertCount * 2);

                VarjoPlugin.FreeOcclusionMesh(occMesh);
                return res;
            }
            else
                return new float[0];
        }

        private readonly object lockObj = new object();

        public static bool SessionValid
        {
            get; private set;
        }

        public VarjoPlugin()
        {
#if VARJO_USE_DEBUG_LOG
            // Register callback function for debug texts
            MyDelegate callbackDelegate = new MyDelegate(CallbackFunction);
            IntPtr intPtrDelegate = Marshal.GetFunctionPointerForDelegate(callbackDelegate);
            SetDebugLog(intPtrDelegate);
#endif

#if VARJO_USE_DEBUG_LOG
            // Register callback function for debug texts
            MyDelegate callbackDelegate = new MyDelegate(CallbackFunction);
            IntPtr intPtrDelegate = Marshal.GetFunctionPointerForDelegate(callbackDelegate);
            SetDebugLog(intPtrDelegate);
#endif

        }

        public void AttemptSessionInit()
        {
            lock (lockObj)
            {
                if (!IsSessionNull())
                {
                    EndSession();
                    SessionValid = false;
                }

#if VARJO_CUSTOM_PLUGIN
                var vcp = new VarjoCustomPlugin();
                SessionValid = vcp.InitSession();
#else
                SessionValid = InitSession();
#endif
            }
        }

        public void AttemptSessionInitThreaded(Action callback)
        {
            ThreadPool.QueueUserWorkItem((obj) =>
            {
                AttemptSessionInit();

                if (callback != null)
                {
                    callback();
                }
            });
        }

        public void Destroy()
        {
            if (SessionValid)
            {
                EndSession();
                SessionValid = false;
            }
        }

        delegate void EventFunc(int eventId);

        public void IssuePluginEvent(int eventId, bool callFromRenderThread = true)
        {
            if (callFromRenderThread)
            {
                GL.IssuePluginEvent(GetRenderEventFunc(), eventId);
            }
            else
            {
                var d = (EventFunc)Marshal.GetDelegateForFunctionPointer(GetRenderEventFunc(), typeof(EventFunc));
                d(eventId);
            }
        }

        public static string GetErrorMsg(int error)
        {
            IntPtr msgPtr = GetErrorMessage(error);
            var msgStr = Marshal.PtrToStringAnsi(msgPtr);
            return msgStr;
        }

#if VARJO_USE_DEBUG_LOG
        // Use StringBuilder to minimize GC trashing
        private static StringBuilder debugSB = new StringBuilder();
        // Callback functions for the debug text
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MyDelegate(string str);
        private static void CallbackFunction(string str) {
            debugSB.Length = 0;
            debugSB.Append("[VarjoPlugin] ");
            debugSB.Append(str);
            VarjoManager.Log(debugSB.ToString());
        }
#endif

        public static void GetEulerAngles(ref Matrix matrix, EulerOrder order, RotationDirection rotation, Handedness handedness, ref Vector3D eulerAngles)
        {
            GetEulerAngles(ref matrix, (long)order, (long)rotation, (long)handedness, ref eulerAngles);
            eulerAngles.x *= Mathf.Rad2Deg;
            eulerAngles.y *= Mathf.Rad2Deg;
            eulerAngles.z *= Mathf.Rad2Deg;
        }

        [Obsolete("This method has been deprecated, please use VarjoManager.GetHMDPosition instead")]
        public static Vector3 FrameGetPosition(PoseType type)
        {
            return VarjoManager.Instance.GetHMDPosition(type);
        }

        [Obsolete("This method has been deprecated, please use VarjoManager.GetHMDOrientation instead")]
        public static Quaternion FrameGetOrientation(PoseType type)
        {
            return VarjoManager.Instance.GetHMDOrientation(type);
        }

        public static void ResetPose(bool position, ResetRotation rotation)
        {
            ResetPose(position, (int)rotation);
        }

        public static List<GazeData> GetGazeList()
        {
            List<GazeData> list = new List<GazeData>();
            GazeData gaze = default(GazeData);
            while (GetOldestGazeIfAvailable(ref gaze))
                list.Add(gaze);
            return list;
        }

        public static void RequestGazeCalibrationWithParameters(GazeCalibrationParameters[] parameters)
        {
            RequestGazeCalibrationWithParameters(parameters, parameters.Length);
        }

        private static void makeIdentityIfZeroMatrix(double[] values)
        {
            for (int i = 0; i < 16; ++i)
            {
                if (values[i] != 0) return;
            }

            values[0] = 1;
            values[5] = 1;
            values[10] = 1;
            values[15] = 1;
        }
    }
}
