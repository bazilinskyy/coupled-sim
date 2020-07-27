
// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Varjo
{
    public class VarjoLayer : MonoBehaviour
    {
        [Tooltip("If false, the layer is disabled and won't get rendered or submitted")]
        public bool layerEnabled = true;

        // Layer ordering
        [Tooltip("Layer ordering: smaller numbers are rendered behind larger numbers. Main rig is rendered at ordering 0.")]
        public int layerOrder = 0;

        [Tooltip("If true, the layer is locked to the HMD pose")]
        public bool faceLocked = false;

        [Tooltip("If true, also passes the depth buffer to Varjo compositor")]
        public bool submitDepth = false;

        [Tooltip("If true, the depth test range is enabled. Use Depth Test Near Z and Far Z to control the range inside which the depth test will be evaluated.")]
        public bool depthTestRangeEnabled = false;

        [Range(0.0f, 50.0f)]
        [Tooltip("Minimum depth included in the depth test range")]
        public double depthTestNearZ = 0.0f;

        [Range(0.0f, 50.0f)]
        [Tooltip("Maximum depth included in the depth test range")]
        public double depthTestFarZ = 50.0f;

        /// <summary>
        /// Cached render textures for cameras.
        /// </summary>
        protected RenderTexture[] camRenderTextures = new RenderTexture[4];
        protected IntPtr[] camSwapchains = new IntPtr[4];
        protected IntPtr[] camDepthSwapchains = new IntPtr[4];

        [HideInInspector]
        public List<VarjoViewCamera> viewportCameras;

        [Range(0.2f, 1.0f)]
        [Tooltip("Rendering size reduction")]
        public float contextDisplayFactor = 1.0f;
        [Range(0.2f, 1.0f)]
        [Tooltip("Rendering size reduction")]
        public float focusDisplayFactor = 1.0f;
        [Tooltip("Flip the viewport upside down. Needed when image effects are in use.")]
        public bool flipY = false;

        [Tooltip("Disable alpha blending for this layer")]
        public bool opaque = true;

        [FormerlySerializedAs("depthSorting")]
        [Tooltip("Participate in depth testing (Submit Depth should be enabled)")]
        public bool depthTesting = false;

        public enum MSAAMode
        {
            None = 0,
            MSAA_2X = 1,
            MSAA_4X = 2,
            MSAA_8X = 3,
        };

        [Tooltip("The antialiasing mode to be used for the view textures")]
        public MSAAMode antiAliasing = MSAAMode.None;

        /// <summary>
        /// If copyCameraComponents is set to true, VarjoViewCameras's will copy the components of the main camera,
        /// attempting to copy all the fields and properties.
        /// </summary>
        [Tooltip("If set to true, VarjoCamera's components are copied to each VarjoViewCamera during the runtime camera creation.")]
        public bool copyCameraComponents;

        [Tooltip("Use the occlusion mesh provided by Varjo Runtime")]
        public bool useOcclusionMesh = true;

        [Tooltip("The material to draw the occlusion mesh with. Defaults to Hidden/OcclusionMesh.")]
        public Material occlusionMaterial;

        /// <summary>
        /// The camera we use to copy the settings from for view cameras.
        /// </summary>
        public Camera varjoCamera;

        /// <summary>
        /// Transform to get current head position and rotation
        /// </summary>
        public Transform HeadTransform
        {
            get;
            private set;
        }

        // Commandbuffer for blitting the left context texture to the display
        private CommandBuffer cameraBlitCB;
        private bool blitCBInjected = false;

        // A static submit struct to prevent GC spikes
        protected VarjoPlugin.MultiProjLayer submitLayer = new VarjoPlugin.MultiProjLayer
        {
            space = 0,
            flags = 0,
            views = new VarjoPlugin.MultiProjView[4]
            {
                new VarjoPlugin.MultiProjView { color = new VarjoPlugin.SwapchainViewport(), depth = new VarjoPlugin.SwapchainViewport() },
                new VarjoPlugin.MultiProjView { color = new VarjoPlugin.SwapchainViewport(), depth = new VarjoPlugin.SwapchainViewport() },
                new VarjoPlugin.MultiProjView { color = new VarjoPlugin.SwapchainViewport(), depth = new VarjoPlugin.SwapchainViewport() },
                new VarjoPlugin.MultiProjView { color = new VarjoPlugin.SwapchainViewport(), depth = new VarjoPlugin.SwapchainViewport() }
            }
        };

        private void OnEnable()
        {
            // Register the layer to the manager
            VarjoManager.RegisterLayer(this);
        }

        private void OnDisable()
        {
            VarjoManager.UnregisterLayer(this);
        }

        protected void Awake()
        {
            // Setup view cameras
            if (viewportCameras == null || viewportCameras.Count <= 0)
            {
                CreateCameras();
            }
            // Make sure all cameras start out as enabled
            EnableCameras(true);

            HeadTransform = varjoCamera.gameObject.transform;
            if (varjoCamera != null)
            {
                varjoCamera.clearFlags = CameraClearFlags.Color;
                varjoCamera.backgroundColor = Color.black;
                varjoCamera.cullingMask = 0;
            }

            if (occlusionMaterial == null)
            {
                var shader = Shader.Find("Hidden/OcclusionMesh");
                if (!shader)
                    Debug.LogWarning("Could not locate occlusion mesh shader (Hidden/OcclusionMesh)");
                else
                    occlusionMaterial = new Material(shader);
            }
        }

        public void CreateCameras()
        {
            viewportCameras = new List<VarjoViewCamera>();
            for (int i = 0; i < 4; i++)
            {
                string side = (i & 1) == 0 ? "Left" : "Right";
                string depth = (i / 2) == 0 ? "Context" : "Focus";

                GameObject camGo = new GameObject("Varjo " + side + " " + depth);
                var varjoCam = camGo.AddComponent<VarjoViewCamera>();
                varjoCam.SetupCamera((VarjoViewCamera.CAMERA_ID)i, varjoCamera, copyCameraComponents, this);

                if (varjoCamera != null)
                {
                    camGo.transform.SetParent(varjoCamera.transform, false);
                }
                viewportCameras.Add(varjoCam);
            }
        }

        private void SetupCameras()
        {
            for (int i = 0; i < viewportCameras.Count; ++i)
            {
                viewportCameras[i].SetupCamera((VarjoViewCamera.CAMERA_ID)i, varjoCamera, copyCameraComponents, this);
            }
        }

        private void OnDestroy()
        {
            if (varjoCamera != null && cameraBlitCB != null && blitCBInjected)
            {
                varjoCamera.RemoveCommandBuffer(CameraEvent.AfterImageEffects, cameraBlitCB);
                blitCBInjected = false;
            }
            if (camRenderTextures != null)
            {
                foreach (var rt in camRenderTextures)
                {
                    if (rt != null)
                    {
                        rt.Release();
                        Destroy(rt);
                    }
                }
            }
            if (camSwapchains != null)
            {
                if (VarjoPlugin.SessionValid)
                {
                    foreach (var swapchain in camSwapchains)
                    {
                        VarjoPlugin.QueueDestroySwapchain(swapchain);
                    }
                    VarjoManager.Instance.Plugin.IssuePluginEvent(VarjoPlugin.VARJO_RENDER_EVT_PROCESS_SWAPCHAINS);
                }
            }
        }

        public void UpdateFromManager()
        {
            if (!VarjoPlugin.SessionValid)
                return;

            if (varjoCamera != null && !blitCBInjected)
            {
                // Hook up blit to screen
                cameraBlitCB = new CommandBuffer();
                cameraBlitCB.name = "Varjo Layer blit to screen";
                if (flipY)
                    cameraBlitCB.Blit(GetRenderTextureForCamera(VarjoViewCamera.CAMERA_ID.CONTEXT_LEFT), BuiltinRenderTextureType.CameraTarget, new Vector2(contextDisplayFactor, contextDisplayFactor), new Vector2(0.0f, 1.0f - contextDisplayFactor));
                else
                    cameraBlitCB.Blit(GetRenderTextureForCamera(VarjoViewCamera.CAMERA_ID.CONTEXT_LEFT), BuiltinRenderTextureType.CameraTarget, new Vector2(contextDisplayFactor, -1.0f * contextDisplayFactor), new Vector2(0.0f, contextDisplayFactor));
                varjoCamera.AddCommandBuffer(CameraEvent.AfterImageEffects, cameraBlitCB);
                blitCBInjected = true;
            }

            if (!faceLocked)
            {
                VarjoPlugin.Matrix matrix;
                Profiler.BeginSample("Varjo.GetFramePose");
                VarjoManager.Instance.GetPose(VarjoPlugin.PoseType.CENTER, out matrix);
                Profiler.EndSample();

                var worldMatrix = VarjoMatrixUtils.WorldMatrixToUnity(matrix.value);

                HeadTransform.localPosition = worldMatrix.GetColumn(3);
                HeadTransform.localRotation = Quaternion.LookRotation(worldMatrix.GetColumn(2), worldMatrix.GetColumn(1));
            }

            foreach (var cam in viewportCameras)
            {
                cam.UpdateFromLayer();
            }
        }

        /// <summary>
        /// Gets a render texture for a camera with correct dimensions and format.
        /// Creates a texture if it's not created. VarjoManager takes care of texture destruction.
        /// </summary>
        /// <param name="cameraId">Camera Id</param>
        /// <returns>Render texture</returns>
        public RenderTexture GetRenderTextureForCamera(VarjoViewCamera.CAMERA_ID cameraId)
        {
            Profiler.BeginSample("Varjo.GetRenderTextureForCamera");
            if (!VarjoPlugin.SessionValid)
            {
                Debug.LogError("GetRenderTextureForCamera called without a valid session.");
                Profiler.EndSample();
                return null;
            }

            var texDesc = VarjoPlugin.GetRenderTextureFormat((int)cameraId);

            if (texDesc.width <= 0 || texDesc.height <= 0)
            {
                Debug.Log(string.Format("Invalid texture descriptor: {0}x{1} {2}", texDesc.width, texDesc.height, texDesc.format));
                Profiler.EndSample();
                return null;
            }

            int camIndex = (int)cameraId;
            if (camRenderTextures[camIndex] != null)
            {
                Profiler.EndSample();
                return camRenderTextures[camIndex];
            }

            // TODO: framebuffer dimensions can change per frame.

            Debug.Log(string.Format("Creating render target: {0}x{1}", texDesc.width, texDesc.height));
            int msaaSamples = 1;
            switch (antiAliasing)
            {
                case MSAAMode.None: msaaSamples = 1; break;
                case MSAAMode.MSAA_2X: msaaSamples = 2; break;
                case MSAAMode.MSAA_4X: msaaSamples = 4; break;
                case MSAAMode.MSAA_8X: msaaSamples = 8; break;
                default: break;
            }

            RenderTextureDescriptor rtd = new RenderTextureDescriptor()
            {
                width = texDesc.width,
                height = texDesc.height,
                colorFormat = ConvertPluginRenderTextureFormat(texDesc.format),
                depthBufferBits = 32,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                msaaSamples = msaaSamples,
                sRGB = true,
            };

            camRenderTextures[camIndex] = new RenderTexture(rtd);
            camRenderTextures[camIndex].Create();
            // Also create the swapchain
            var cfg = new VarjoPlugin.SwapchainConfig()
            {
                format = VarjoPlugin.varjo_TextureFormat_R8G8B8A8_SRGB,
                numberOfTextures = 4,
                textureWidth = texDesc.width,
                textureHeight = texDesc.height,
                arraySize = 1
            };

            camSwapchains[camIndex] = VarjoPlugin.CreateD3D11Swapchain(cfg);
            // And depth swapchain if requested
            if (submitDepth)
            {
                cfg.format = VarjoPlugin.varjo_DepthTextureFormat_D32_FLOAT;
                camDepthSwapchains[camIndex] = VarjoPlugin.CreateD3D11Swapchain(cfg);
            }

            // Hook up the texture directly to the view struct so that we don't have to call GetNativeTexturePtr() every frame
            var view = submitLayer.views[camIndex];
            view.unityColorTex = camRenderTextures[camIndex].GetNativeTexturePtr();
            if (submitDepth)
                view.unityDepthTex = camRenderTextures[camIndex].GetNativeDepthBufferPtr();

            submitLayer.views[camIndex] = view;

            Profiler.EndSample();
            return camRenderTextures[camIndex];
        }

        public IntPtr GetSwapchainForCamera(VarjoViewCamera.CAMERA_ID cameraId)
        {
            // Ensure it's created
            GetRenderTextureForCamera(cameraId);
            return camSwapchains[(int)cameraId];
        }

        public IntPtr GetDepthSwapchainForCamera(VarjoViewCamera.CAMERA_ID cameraId)
        {
            // Ensure it's created
            GetRenderTextureForCamera(cameraId);
            return camDepthSwapchains[(int)cameraId];
        }

        private static RenderTextureFormat ConvertPluginRenderTextureFormat(int pluginFormat)
        {
            switch (pluginFormat)
            {
                case 87:    // DXGI_FORMAT_B8G8R8A8_UNORM
                case 88:    // DXGI_FORMAT_B8G8R8X8_UNORM
                case 90:    // DXGI_FORMAT_B8G8R8A8_TYPELESS
                case 91:    // DXGI_FORMAT_B8G8R8A8_UNORM_SRGB
                case 92:    // DXGI_FORMAT_B8G8R8X8_TYPELESS
                case 93:    // DXGI_FORMAT_B8G8R8X8_UNORM_SRGB
                    return RenderTextureFormat.BGRA32;

                case 27:
                    return RenderTextureFormat.ARGB32;

                default:
                    Debug.Log("Unknown texture format. Varjo rendering likely not working.");
                    return RenderTextureFormat.ARGB32;
            }
        }

        public VarjoPlugin.MultiProjLayer PrepareForSubmission()
        {
            var layer = submitLayer;

            layer.space = faceLocked ? VarjoPlugin.varjo_SpaceView : VarjoPlugin.varjo_SpaceLocal;
            layer.flags = 0;
            if (!opaque) layer.flags |= VarjoPlugin.varjo_LayerFlag_AlphaBlend;
            if (depthTesting) layer.flags |= VarjoPlugin.varjo_LayerFlag_DepthTesting;
            if (useOcclusionMesh) layer.flags |= VarjoPlugin.varjo_LayerFlag_UseOcclusionMesh;

            for (int i = 0; i < 4; i++)
            {
                var tex = GetRenderTextureForCamera((VarjoViewCamera.CAMERA_ID)i);
                var swapchain = GetSwapchainForCamera((VarjoViewCamera.CAMERA_ID)i);
                var depthSwapchain = GetDepthSwapchainForCamera((VarjoViewCamera.CAMERA_ID)i);

                float sizeFactor = i < 2 ? contextDisplayFactor : focusDisplayFactor;
                var view = layer.views[i];

                view.projectionMatrix = viewportCameras[i].GetProjectionMatrixForSubmission();
                view.invViewMatrix = viewportCameras[i].GetViewMatrixForSubmission();
                var col = view.color;

                col.swapchain = swapchain;
                col.x = 0;
                col.y = 0;
                col.width = (int)(tex.width * sizeFactor);
                col.height = (int)(tex.height * sizeFactor);
                col.arrayIndex = 0;
                col.reserved = 0;

                view.hasDepth = (depthSwapchain != (IntPtr)0) ? 1 : 0;
                if (view.hasDepth != 0)
                {
                    var depth = col;
                    depth.swapchain = depthSwapchain;
                    view.depth = depth;
                    view.minDepth = 0.0;
                    view.maxDepth = 1.0;
                    view.nearZ = viewportCameras[i].cam.farClipPlane;
                    view.farZ = viewportCameras[i].cam.nearClipPlane;
                    view.depthTestNearZ = depthTestNearZ;
                    view.depthTestFarZ = depthTestFarZ;
                    view.depthTestRangeEnabled = depthTestRangeEnabled ? 1 : 0;
                }
                if (flipY)
                {
                    col.y = (int)(tex.height * (1.0f - sizeFactor));
                    view.projectionMatrix[5] *= -1.0;
                }
                view.color = col;

                layer.views[i] = view;
            }

            return layer;
        }

        // Enable / disable view cameras
        public void EnableCameras(bool enable)
        {
            foreach (var cam in viewportCameras)
            {
                cam.enabled = enable;
                cam.GetCamera().enabled = enable;
            }
        }

    }


}
