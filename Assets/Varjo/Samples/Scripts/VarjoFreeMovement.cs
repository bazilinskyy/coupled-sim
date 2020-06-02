// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

namespace VarjoExample
{
    /// <summary>
    /// Allows free movement relative to head orientation using wasd/qe/zx keys.
    /// </summary>
    public class VarjoFreeMovement : MonoBehaviour
    {
        [Header("Attach this script to VarjoUser")]

        [Header("Use wasd/qe/zx to move in Play mode")]
        public float moveSpeed = 10.0f;
        public float turnSpeed = 200.0f;

        [Header("Lock movement to horizontal place")]
        public bool moveOnXZPlane = true;

        void Update()
        {
            float turnLeft = Input.GetKey(KeyCode.Z) ? -1.0f : 0.0f;
            float turnRight = Input.GetKey(KeyCode.X) ? 1.0f : 0.0f;

            float forward = Input.GetKey(KeyCode.W) ? 1.0f : 0.0f;
            float backward = Input.GetKey(KeyCode.S) ? 1.0f : 0.0f;

            float left = Input.GetKey(KeyCode.A) ? 1.0f : 0.0f;
            float right = Input.GetKey(KeyCode.D) ? 1.0f : 0.0f;

            float up = Input.GetKey(KeyCode.E) ? 1.0f : 0.0f;
            float down = Input.GetKey(KeyCode.Q) ? 1.0f : 0.0f;

            transform.RotateAround(VarjoManager.Instance.HeadTransform.position, Vector3.up, turnSpeed * turnLeft * Time.deltaTime);
            transform.RotateAround(VarjoManager.Instance.HeadTransform.position, Vector3.up, turnSpeed * turnRight * Time.deltaTime);

            transform.Translate(VarjoManager.Instance.HeadTransform.up * moveSpeed * up * Time.deltaTime, Space.World);
            transform.Translate(-VarjoManager.Instance.HeadTransform.up * moveSpeed * down * Time.deltaTime, Space.World);

            if (moveOnXZPlane)
            {
                transform.Translate(VectorYToZero(VarjoManager.Instance.HeadTransform.forward) * moveSpeed * forward * Time.deltaTime, Space.World);
                transform.Translate(VectorYToZero(-VarjoManager.Instance.HeadTransform.forward) * moveSpeed * backward * Time.deltaTime, Space.World);

                transform.Translate(VectorYToZero(VarjoManager.Instance.HeadTransform.right) * moveSpeed * right * Time.deltaTime, Space.World);
                transform.Translate(VectorYToZero(-VarjoManager.Instance.HeadTransform.right) * moveSpeed * left * Time.deltaTime, Space.World);
            }
            else
            {
                transform.Translate(VarjoManager.Instance.HeadTransform.forward * moveSpeed * forward * Time.deltaTime, Space.World);
                transform.Translate(-VarjoManager.Instance.HeadTransform.forward * moveSpeed * backward * Time.deltaTime, Space.World);

                transform.Translate(VarjoManager.Instance.HeadTransform.right * moveSpeed * right * Time.deltaTime, Space.World);
                transform.Translate(-VarjoManager.Instance.HeadTransform.right * moveSpeed * left * Time.deltaTime, Space.World);
            }
        }

        Vector3 VectorYToZero(Vector3 v)
        {
            return new Vector3(v.x, 0.0f, v.z);
        }
    }
}
