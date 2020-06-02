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
        LineDrawer lineDrawer;

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
                    VarjoGazeTarget target = gazeRayHit.collider.gameObject.GetComponent<VarjoGazeTarget>(); // Now set to return msg when gazetarget is hit. To do later: change to pedestrian as target
                    if (target != null)
                    {
                        target.OnHit(); // Define what to do when the target is hit.
                    }

                    lineDrawer.DrawLineInGameView(gameObject, gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.green);

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

    // Helper function gaze vector visualization
    public struct LineDrawer
    {
        private LineRenderer lineRenderer;

        /*public LineDrawer(float lineSize = 0.2f)
        {
            this.gameObject.AddComponent<LineRenderer>();
            lineRenderer = gameObject.GetComponent<LineRenderer>();
            //Particles/Additive
            lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));

            this.lineSize = lineSize;
        }*/

        private void init(GameObject gameObject)
        {
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                //Particles/Additive
                lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));
            }
        }

        //Draws lines through the provided vertices
        public void DrawLineInGameView(GameObject gameObject, Vector3 start, Vector3 end, Color color)
        {
            if (lineRenderer == null)
            {
                init(gameObject);
            }

            //Set color
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            //Set width
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;

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
