using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

[RequireComponent(typeof(MyGazeLogger))]
public class MyVarjoGazeRay : MonoBehaviour
{
    [Header("Eye to use for raycasting into the world")]
    public Eye eye = Eye.both;

    [Header("Radius of the selection ray")]
    public float gazeRayRadius = 0.01f;

    [Header("Should we draw debug lines to scene view")]
    public bool drawDebug = true;

    public bool logData = true;
    public VarjoPlugin.GazeData data;
    RaycastHit gazeRayHit;
    Vector3 gazeRayForward;
    Vector3 gazeRayDirection;
    Vector3 gazePosition;
    Vector3 gazeRayOrigin;
    public LayerMask layerMask = ~0;
    public MyGazeLogger gazeLogger;

    public enum Eye
    {
        both,
        left,
        right
    }

    private void Start()
    {
        StartUpFunction();
    }
   
    void StartUpFunction()
    {
        if(MyUtils.GetExperimentInput().camType == MyCameraType.Normal) { enabled = false; return; }
        if (logData) { gazeLogger = GetComponent<MyGazeLogger>(); }
       
        // InitGaze must be called before using or calibrating gaze tracking.
        if (!VarjoPlugin.InitGaze())
        {
            Debug.LogError("Failed to initialize gaze");
            GetComponent<MyVarjoGazeRay>().enabled = false;
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
            if (Physics.SphereCast(gazeRayOrigin, gazeRayRadius, gazeRayDirection, out gazeRayHit, 1000f, layerMask))
            {
                //By default we are looking at the world
                bool lookingAtWorld = true;
                //If hti is a target execute OnHit function
                if (gazeRayHit.collider.tag.Equals(LoggedTags.Target.ToString()))
                {
                    Target target = gazeRayHit.collider.gameObject.GetComponent<Target>();
                    target.OnHit();
                }

                if (logData)
                {
                    //Go through all tags that we log
                    foreach (LoggedTags tag in EnumUtil.GetValues<LoggedTags>())
                    {
                        if (gazeRayHit.collider.tag.Equals(tag.ToString()))
                        {
                            gazeLogger.FixatingOn(tag);
                            lookingAtWorld = false;
                        }
                    }

                    //If hit colliders not tagged we assume it is the world:
                    if (lookingAtWorld) { gazeLogger.FixatingOn(LoggedTags.World); }
                }
                if (drawDebug)
                {
                    Debug.DrawLine(gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.green);
                }
            }
            else
            {
                //Not hitting any colliders so looking at the sky or something...
                if (logData) { gazeLogger.FixatingOn(LoggedTags.World); }

                if (drawDebug)
                {
                    Debug.DrawLine(gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.white);
                }
            }

        }
        //Invailable eye data....
        else { if (logData) { gazeLogger.FixatingOn(LoggedTags.Unknown); } }
    }
}

