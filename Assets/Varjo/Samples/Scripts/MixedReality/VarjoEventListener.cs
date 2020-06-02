using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

namespace VarjoExample
{
    /// <summary>
    /// Demonstrates the usage of Mixed Reality related events.
    ///
    /// In particular reacts to MR device connected / disconnected:
    /// - Renders video see through when available the device is connected.
    /// - Renders the Unity Skybox when the device is disconnected.
    /// </summary>
    public class VarjoEventListener : MonoBehaviour
    {
        public GameObject varjoCamera;

        void Start()
        {
            // Register to receive event notifications.
            VarjoManager.OnDataStreamStartEvent += OnDataStreamStart;
            VarjoManager.OnDataStreamStopEvent += OnDataStreamStop;
            VarjoManager.OnMRCameraPropertyChangeEvent += OnCameraPropertyChange;
            VarjoManager.OnMRDeviceStatusEvent += OnMRDeviceStatus;

            // Try to start rendering. If there is no MR support available, this call fails and we
            // fall back to Skybox as the background.
            if (VarjoPluginMR.StartRender())
            {
                SetClearColorSolidColor();
            }
            else
            {
                SetClearColorSkybox();
            }
        }

        /// <summary>
        /// Change all cameras to clear with RGBA(0,0,0,0).
        /// </summary>
        private void SetClearColorSolidColor()
        {
            var cameras = varjoCamera.GetComponentsInChildren<Camera>();
            foreach (var cam in cameras)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
            }
        }

        /// <summary>
        /// Change all cameras to clear with Skybox.
        /// </summary>
        private void SetClearColorSkybox()
        {
            var cameras = varjoCamera.GetComponentsInChildren<Camera>();
            foreach (var cam in cameras)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
            }
        }

        private void OnMRDeviceStatus(bool connected)
        {
            Debug.Log("MR Device status: " + (connected ? "connected" : "disconnected"));

            if (connected)
            {
                VarjoPluginMR.StartRender();
                SetClearColorSolidColor();
            }
            else
            {
                SetClearColorSkybox();
                VarjoPluginMR.StopRender();
            }
        }

        private void OnDataStreamStart(long streamId)
        {
            Debug.Log("Data stream started: " + streamId);
        }

        private void OnDataStreamStop(long streamId)
        {
            Debug.Log("Data stream stopped: " + streamId);
        }
        private void OnCameraPropertyChange(VarjoCameraPropertyType type)
        {
            Debug.Log("Camera property changed: " + type);
        }
    }
}
