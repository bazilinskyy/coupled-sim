// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VarjoExample
{
    /// <summary>
    /// Increase emission of the gameObject when VarjoGaze hits its collider.
    /// Decrease emission gradually in Update.
    /// Expects use of Unity standard shader.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [RequireComponent(typeof(Collider))]
    public class VarjoGazeTarget : MonoBehaviour
    {
        public Color emissionColor = Color.white;
        public float emissionIncreaseSpeed = 5.0f;
        public float emissionDecreaseSpeed = 2.0f;

        Renderer targetRenderer;
        MaterialPropertyBlock materialPropertyBlock;
        float emissionLevel = 0.0f;

        void Start()
        {
            targetRenderer = GetComponent<Renderer>();
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        public void OnHit()
        {
            emissionLevel += emissionIncreaseSpeed * Time.deltaTime;
        }

        void Update()
        {
            emissionLevel -= emissionDecreaseSpeed * Time.deltaTime;
            emissionLevel = Mathf.Clamp01(emissionLevel);

            materialPropertyBlock.SetColor("_EmissionColor", emissionColor * emissionLevel);
            targetRenderer.SetPropertyBlock(materialPropertyBlock);
        }
    }
}
