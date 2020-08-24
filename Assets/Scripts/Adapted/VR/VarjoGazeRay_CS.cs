// Copyright 2019 Varjo Technologies Oy. All rights reserved.
// Adapted by Johnson Mok
// Gaze ray visualized

using System.Collections;
using System.Collections.Generic;
using System.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using Varjo;

namespace VarjoExample
{
    /// <summary>
    /// Shoots rays to where user is looking at and send hit events to VarjoGazeTargets.
    /// Requires gaze calibration first.
    /// </summary>
    public class VarjoGazeRay_CS : MonoBehaviour
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
        Vector3 gazeRayForward;
        Vector3 gazeRayDirection;
        Vector3 gazePosition;
        Vector3 gazeRayOrigin;
        string role_varjo;
        string target;

        LineDrawer lineDrawer;
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
            lineDrawer = new LineDrawer();
            crossHair = new LineDrawer();
        }

        void Update()
        {
            // Determine whether this script is attached to a passenger or pedestrian:
            if (transform.parent.CompareTag("Pedestrian"))
            {
                role_varjo = "Pedestrian";
                target = "Passenger";
            }
            else if (transform.parent.CompareTag("AutonomousCar"))
            {
                role_varjo = "Passenger";
                target = "Pedestrian";
            }

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

                // Visualize the gaze vector for the pedestrian (hide for passenger)
                //lineDrawer.DrawLineInGameView(gameObject, gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.green, 0.01f, true);

                // Crosshair for passenger
                //crossHair.DrawLineInGameView(gameObject, gazeRayOrigin + gazeRayDirection * 5.0f, gazeRayOrigin + gazeRayDirection * 10.0f, Color.green, 0.07f, false);
                crossHair.DrawLineInGameView(gameObject, gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.green, 0.04f, false);

                // Raycast into world, only see objects in the "Pedestrian layer"
                if (Physics.SphereCast(gazeRayOrigin, gazeRayRadius, gazeRayDirection, out gazeRayHit, Mathf.Infinity, 1 << LayerMask.NameToLayer(target)))
                {
                    // Use layers or tags preferably to identify looked objects in your application.
                    // Determine when the raycast collides with object with the "Pedestrian" tag
                    if (gazeRayHit.collider.gameObject.CompareTag(target) && target == "Pedestrian")
                    {
                        // Take action if the distance between the pedestrian and car is smaller than 20m
                        if(gazeRayHit.distance < 25.0f )
                        {
                            this.GetComponentInParent<AICar>().VarjoSaysStop();
                        }
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
    }

    // Helper function gaze vector visualization
    public struct LineDrawer
    {
        private LineRenderer lineRenderer;

        private void init(GameObject gameObject, bool laser)
        {
            if (lineRenderer == null)
            {
                if (laser == false)
                {
                    // Crosshair
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
                }
                else if (laser == true)
                {
                    // Create empty game object for the linerenderer to add layer. Laser
                    var LaserObject = new GameObject();
                    LaserObject.transform.parent = gameObject.transform.parent;
                    LaserObject.layer = LayerMask.NameToLayer("IgnoreForPassenger");
                    lineRenderer = LaserObject.AddComponent<LineRenderer>();
                }
                //Particles/Additive
                lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));
            }
        }

        //Draws lines through the provided vertices
        public void DrawLineInGameView(GameObject gameObject, Vector3 start, Vector3 end, Color color, float width, bool laser)
        {
            if (lineRenderer == null)
            {
                init(gameObject, laser);
            }

            //Set material transparancy
            //lineRenderer.material = new Material(Shader.Find("Particles/Additive(Soft)"));
            color.a = 0.2f;

            //Set color
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            //Set width
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;

            //Set line count which is 2
            lineRenderer.positionCount = 2;

            //Set the postion of both two lines
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        public void Destroy()
        {
            if (lineRenderer != null)
            {
                UnityEngine.Object.Destroy(lineRenderer.gameObject);
            }
        }
    }
}
