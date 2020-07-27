// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System;
using UnityEngine;
using UnityEngine.Events;
using Varjo;

namespace VarjoExample
{
    /// <summary>
    /// Controls for mixed reality stream related functionalities.
    /// </summary>
    public class VarjoMixedRealityStreams : MonoBehaviour
    {
        [Serializable]
        public class DistortedColorFrameEvent : UnityEvent<Varjo.VarjoDistortedColorStream.VarjoDistortedColorFrame> { }
        [Serializable]
        public class CubemapFrameEvent : UnityEvent<Varjo.VarjoEnvironmentCubemapStream.VarjoEnvironmentCubemapFrame> { }

        [Header("Streams")]
        public bool distortedColor = false;
        public bool environmentCubemap = false;

        [Header("Camera update callbacks")]
        [Header("Distorted Color")]
        public DistortedColorFrameEvent onNewFrame = new DistortedColorFrameEvent();

        [Header("Environment Cubemap")]
        public CubemapFrameEvent onCubemapFrame = new CubemapFrameEvent();

        private bool distortedColorEnabled = false;
        private bool cubemapEnabled = false;

        private long previousDistortedColorTimestamp = -1;
        private long previousCubemapTimestamp = -1;

        private VarjoDistortedColorStream distortedColorStream;
        private VarjoDistortedColorStream.VarjoDistortedColorFrame distortedColorFrame;

        private VarjoEnvironmentCubemapStream cubemapStream;
        private VarjoEnvironmentCubemapStream.VarjoEnvironmentCubemapFrame cubemapFrame;


        void Update()
        {
            UpdateDistortedColorStream();
            UpdateCubemapStream();
        }

        void UpdateDistortedColorStream()
        {
            if (distortedColor != distortedColorEnabled)
            {
                if (distortedColor)
                {
                    if (onNewFrame.GetPersistentEventCount() > 0)
                    {
                        distortedColorStream = VarjoPluginMR.distortedColorStream;
                        distortedColor = distortedColorStream.Start();
                    }
                }
                else
                {
                    if (distortedColorStream != null)
                    {
                        distortedColorStream.Stop();
                        distortedColor = false;
                    }
                }
                distortedColorEnabled = distortedColor;
            }

            if (distortedColorEnabled)
            {
                distortedColorFrame = distortedColorStream.GetFrame();
                if (distortedColorFrame.timestamp != previousDistortedColorTimestamp)
                {
                    onNewFrame.Invoke(distortedColorFrame);
                    previousDistortedColorTimestamp = distortedColorFrame.timestamp;
                }
            }
        }

        void UpdateCubemapStream()
        {
            if (environmentCubemap != cubemapEnabled)
            {
                if (environmentCubemap)
                {
                    if (onCubemapFrame.GetPersistentEventCount() > 0)
                    {
                        cubemapStream = VarjoPluginMR.environmentCubemapStream;
                        environmentCubemap = cubemapStream.Start();
                    }
                }
                else
                {
                    if (cubemapStream != null)
                    {
                        cubemapStream.Stop();
                        environmentCubemap = false;
                    }
                }
                cubemapEnabled = environmentCubemap;
            }

            if (cubemapEnabled)
            {
                cubemapFrame = cubemapStream.GetFrame();
                if (cubemapFrame.timestamp != previousCubemapTimestamp)
                {
                    onCubemapFrame.Invoke(cubemapFrame);
                    previousCubemapTimestamp = cubemapFrame.timestamp;
                }
            }
        }

        void OnDisable()
        {
            if (distortedColorEnabled && distortedColorStream != null)
            {
                distortedColorStream.Stop();
            }
            distortedColorEnabled = false;

            if (cubemapEnabled && cubemapStream != null)
            {
                cubemapStream.Stop();
            }
            cubemapEnabled = false;
        }
    }
}