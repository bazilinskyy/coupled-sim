// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

namespace VarjoExample
{
    /// <summary>
    /// Shoots rays to where user is looking at and send hit events to VarjoGazeTargets.
    /// Requires gaze calibration first.
    /// </summary>
    public class VarjoGazeRay : MonoBehaviour
    {
        [Header("Eye to use for raycasting into the world")]
        public Eye eye = Eye.both;

        [Header("Radius of the selection ray")]
        public float gazeRayRadius = 0.01f;

        [Header("Should we draw debug lines to scene view")]
        public bool drawDebug = true;

        VarjoPlugin.GazeData data;
        RaycastHit gazeRayHit;
        Vector3 gazeRayForward;
        Vector3 gazeRayDirection;
        Vector3 gazePosition;
        Vector3 gazeRayOrigin;

        public enum Eye
        {
            both,
            left,
            right
        }

        private void Start()
        {
            // InitGaze must be called before using or calibrating gaze tracking.
            if (!VarjoPlugin.InitGaze()) {
                Debug.LogError("Failed to initialize gaze");
                gameObject.SetActive(false);
            }
        }

        void Update()
        {
            // Returns current state of the gaze
            data = VarjoPlugin.GetGaze();

            // Check if gaze data is valid and calibrated
            if (data.status != VarjoPlugin.GazeStatus.INVALID)
            {
                switch (eye)
                {
                    case Eye.both:
                        // Gaze data forward and position comes as 3 doubles: x,y,z. You need to construct a vector from them to a desired format.
                        gazeRayForward = new Vector3((float)data.gaze.forward[0], (float)data.gaze.forward[1], (float)data.gaze.forward[2]);
                        gazePosition = new Vector3((float)data.gaze.position[0], (float)data.gaze.position[1], (float)data.gaze.position[2]);
                        break;

                    case Eye.left:
                        gazeRayForward = new Vector3((float)data.left.forward[0], (float)data.left.forward[1], (float)data.left.forward[2]);
                        gazePosition = new Vector3((float)data.left.position[0], (float)data.left.position[1], (float)data.left.position[2]);
                        break;

                    case Eye.right:
                        gazeRayForward = new Vector3((float)data.right.forward[0], (float)data.right.forward[1], (float)data.right.forward[2]);
                        gazePosition = new Vector3((float)data.right.position[0], (float)data.right.position[1], (float)data.right.position[2]);
                        break;
                }

                // Fetch head pose
                transform.position = VarjoManager.Instance.HeadTransform.position;
                transform.rotation = VarjoManager.Instance.HeadTransform.rotation;

                // Transform gaze direction and origin from HMD space to world space
                gazeRayDirection = transform.TransformVector(gazeRayForward);
                gazeRayOrigin = transform.TransformPoint(gazePosition);

                // Raycast into world
                if (Physics.SphereCast(gazeRayOrigin, gazeRayRadius, gazeRayDirection, out gazeRayHit))
                {
                    // Use layers or tags preferably to identify looked objects in your application.
                    // This is done here via GetComponent for clarity's sake as example.
                    VarjoGazeTarget target = gazeRayHit.collider.gameObject.GetComponent<VarjoGazeTarget>();
                    if (target != null)
                    {
                        target.OnHit();
                    }

                    if (drawDebug)
                    {
                        Debug.DrawLine(gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.green);
                    }
                }
                else
                {
                    if (drawDebug)
                    {
                        Debug.DrawLine(gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.white);
                    }
                }

            }

        }
    }
}
