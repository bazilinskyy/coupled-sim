// Copyright 2019 Varjo Technologies Oy. All rights reserved.
// Adapted by Johnson Mok
// Gaze ray visualized

using System.Collections;
using System.Collections.Generic;
using System.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using Varjo;
using Valve.VR;

namespace VarjoExample
{
    /// <summary>
    /// Shoots rays to where user is looking at and send hit events to VarjoGazeTargets.
    /// Requires gaze calibration first.
    /// </summary>
    public class VarjoGazeRay_CS_1 : MonoBehaviour
    {
        [Header("Eye to use for raycasting into the world")]
        public Eye eye = Eye.both;

        [Header("Radius of the selection ray")]
        public float gazeRayRadius = 0.01f;

        [Header("Should we draw debug lines to scene view")]
        public bool drawDebug = true;

        private AICar aicar;

        VarjoPlugin.GazeData data;
        RaycastHit gazeRayHit;
        public Vector3 gazeRayForward;
        public Vector3 gazeRayDirection;
        public Vector3 gazePosition;
        public Vector3 gazeRayOrigin;
        public string role_varjo;
        public string target;

        public long Frame;
        public long CaptureTime;
        public Vector3 hmdposition;
        public Vector3 hmdrotation;
        public Quaternion hmdrotation_quaternion;
        public double LeftPupilSize;
        public double RightPupilSize;
        public double FocusDistance;
        public double FocusStability;

        private int nr_pa = 0;
        private int nr_pe = 0;

        LineDrawer crossHair;

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
                crossHair = new LineDrawer();
        }

        void Update()
        {
            // Determine whether this script is attached to a passenger or pedestrian:
            if (transform.parent.CompareTag("Pedestrian") && nr_pe<1)
            {
                role_varjo = "Pedestrian";
                target = "Passenger";
                nr_pe++;
            }
            else if (transform.parent.CompareTag("AutonomousCar") && nr_pa<1)
            {
                role_varjo = "Passenger";
                target = "Pedestrian";
                nr_pa++;
            }

            // Returns current state of the gaze
            data = VarjoPlugin.GetGaze();

            // Data for logging
            Frame = data.frameNumber;
            CaptureTime = data.captureTime;
            hmdposition = VarjoManager.Instance.HeadTransform.position;                 
            hmdrotation_quaternion = VarjoManager.Instance.HeadTransform.rotation;
            hmdrotation = VarjoManager.Instance.HeadTransform.rotation.eulerAngles;
            LeftPupilSize = data.leftPupilSize;
            RightPupilSize = data.rightPupilSize;
            FocusDistance = data.focusDistance;
            FocusStability = data.focusStability;

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

                if (SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8") // LHR-85C3EF8C
                {
                    // Raycast into world, only see objects in the "target layer"
                    if (Physics.SphereCast(gazeRayOrigin, gazeRayRadius, gazeRayDirection, out gazeRayHit, Mathf.Infinity, 1 << LayerMask.NameToLayer(target)))
                    {
                        // Use layers or tags preferably to identify looked objects in your application.
                        // Determine when the raycast collides with object with the "Pedestrian" tag
                        if (gazeRayHit.collider.gameObject.CompareTag(target) && target == "Passenger")
                        {
                            //Debug.LogError($"Target {target} hit, called: {gazeRayHit.collider.gameObject.transform.parent.parent.parent.name} with distance {gazeRayHit.distance}");
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

        public RaycastHit getGazeRayHit()
        {
            return gazeRayHit;
        }

        public Vector3 getGazeRayForward() // HMD space
        {
            return gazeRayForward;
        }

        public Vector3 getGazePosition() // HMD space
        {
            return gazePosition;
        }

        public Vector3 getGazeRayDirection() // world space
        {
            return gazeRayDirection;
        }

        public Vector3 getGazeRayOrigin() // world space
        {
            return gazeRayOrigin;
        }

        public string getRoleVarjo()
        {
            return role_varjo;
        }

        public VarjoPlugin.GazeStatus getGazeStatus()
        {
            return data.status;
        }
    }
}
