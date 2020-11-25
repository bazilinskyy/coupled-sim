using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class Network_local_varjo : MonoBehaviour
{
    public string hmdserial = "LHR-7863A1E8"; //LHR-7863A1E8 //LHR-85C3EF8C
    // Define data to network for NetworkObject_0
    public int experimentNr;

    // Update is called once per frame
    void FixedUpdate()
    {
        // Get data for NetworkObject_0
        experimentNr = PersistentManager.Instance.experimentnr;

        // Apply the right poses according to the gameobject name
        if (gameObject.name == "NetworkObject_0" && SteamVR.instance.hmd_SerialNumber == hmdserial)
        {
            transform.position = new Vector3(experimentNr, 0, 0);
            //Debug.LogError($"0 - expnr = {experimentNr}");
            //Debug.LogError($"0 - trans pos = {transform.position}");
        }
    }
}
