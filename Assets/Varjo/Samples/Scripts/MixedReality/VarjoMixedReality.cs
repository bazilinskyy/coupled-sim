// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using Varjo;

namespace VarjoExample
{
    /// <summary>
    /// Controls for mixed reality related functionalities.
    /// </summary>
    public class VarjoMixedReality : MonoBehaviour
    {
        [Header("Mixed Reality Features")]
        public bool videoSeeThrough = true;
        public bool depthEstimation = false;
        [Range(0f, 1.0f)]
        public float VREyeOffset = 1.0f;


        [Header("Real Time Environment")]
        public bool environmentReflections = false;
        public bool environmentLighting = false;
        public int lightingRefreshRate = 10;
        [Range(0f, 8.0f)]
        public float lightingIntensity = 3f;
        public Material environmentSkyboxMaterial;

        private bool videoSeeThroughEnabled = false;
        private bool environmentReflectionsEnabled = false;
        private bool environmentLightingEnabled = false;
        private bool depthEstimationEnabled = false;
        private float currentVREyeOffset = 1f;

        private bool originalSubmitDepthValue = false;

        private VarjoManager varjoManager;
        private VarjoEnvironmentCubemapStream.VarjoEnvironmentCubemapFrame cubemapFrame;

        private void Start()
        {
            varjoManager = VarjoManager.Instance;
            varjoManager.opaque = false;
        }

        void Update()
        {
            UpdateMRFeatures();
        }

        void UpdateMRFeatures()
        {
            UpdateVideoSeeThrough();
            UpdateDepthEstimation();
            UpdateVREyeOffSet();
            UpdateEnvironmentReflections();
            UpdateEnvironmentLighting();
        }

        void UpdateVideoSeeThrough()
        {
            if (videoSeeThrough != videoSeeThroughEnabled)
            {
                if (videoSeeThrough)
                {
                    videoSeeThrough = VarjoPluginMR.StartRender();
                }
                else
                {
                    VarjoPluginMR.StopRender();
                }
                videoSeeThroughEnabled = videoSeeThrough;
            }
        }

        void UpdateDepthEstimation()
        {
            if (depthEstimation != depthEstimationEnabled)
            {
                if (depthEstimation)
                {
                    originalSubmitDepthValue = varjoManager.submitDepth;
                    varjoManager.submitDepth = true;
                    varjoManager.depthTesting = true;
                    depthEstimation = VarjoPluginMR.EnableDepthEstimation();
                }
                else
                {
                    varjoManager.submitDepth = originalSubmitDepthValue;
                    varjoManager.depthTesting = false;
                    VarjoPluginMR.DisableDepthEstimation();
                }
                depthEstimationEnabled = depthEstimation;
            }
        }

        void UpdateVREyeOffSet()
        {
            if (VREyeOffset != currentVREyeOffset)
            {
                VarjoPluginMR.SetVRViewOffset(VREyeOffset);
                currentVREyeOffset = VREyeOffset;
            }
        }

        void UpdateEnvironmentReflections()
        {
            if (environmentReflections != environmentReflectionsEnabled)
            {
                if (environmentReflections)
                {
                    if (VarjoPluginMR.environmentCubemapStream.IsSupported())
                    {
                        if (!environmentSkyboxMaterial)
                        {
                            environmentSkyboxMaterial = new Material(Shader.Find("Skybox/Cubemap"));
                        }
                        RenderSettings.skybox = environmentSkyboxMaterial;
                        environmentReflections = VarjoPluginMR.environmentCubemapStream.Start();
                    }
                    else
                    {
                        VarjoPluginMR.environmentCubemapStream.Stop();
                    }
                }
                environmentReflectionsEnabled = environmentReflections;
            }
            if (environmentReflectionsEnabled)
            {
                cubemapFrame = VarjoPluginMR.environmentCubemapStream.GetFrame();
                environmentSkyboxMaterial.SetTexture("_Tex", cubemapFrame.cubemap);
            }
        }

        void UpdateEnvironmentLighting()
        {
            if (environmentLighting != environmentLightingEnabled)
            {
                if (environmentLighting)
                {
                    environmentLightingEnabled = true;
                    StartCoroutine("UpdateLighting");
                    if (!environmentReflectionsEnabled)
                    {
                        environmentReflections = true;
                    }
                }
                else
                {
                    environmentLightingEnabled = false;
                    StopCoroutine("UpdateLighting");
                }
            }
        }

        IEnumerator UpdateLighting()
        {
            while (environmentLightingEnabled)
            {
                RenderSettings.ambientIntensity = lightingIntensity;
                DynamicGI.UpdateEnvironment();
                yield return new WaitForSeconds(1f / lightingRefreshRate);
            }
        }

        void OnDisable()
        {
            videoSeeThrough = false;
            depthEstimation = false;
            environmentReflections = false;
            environmentLighting = false;
            UpdateMRFeatures();
        }
    }
}