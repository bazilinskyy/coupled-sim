using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using VarjoExample;

public class HMDRotator : MonoBehaviour
{
    public GameObject Gaze;
    public string hmdserial = "LHR-85C3EF8C"; //LHR-7863A1E8 //LHR-85C3EF8C

    void Update()
    {
        if (SteamVR.instance.hmd_SerialNumber == hmdserial)
        {
            var script = Gaze.GetComponent<VarjoGazeRay_CS_1>();
            transform.rotation = Quaternion.Euler(0, script.hmdrotation.y, 0);
        }
    }
}
