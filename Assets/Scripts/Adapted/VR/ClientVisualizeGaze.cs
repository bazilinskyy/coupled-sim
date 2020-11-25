using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClientVisualizeGaze : MonoBehaviour
{
    private GameObject go_N0;
    private int expNr;
    private bool enter = true;
    // Update is called once per frame
    void Update()
    {
        //GameObject go_N3 = gameObject.transform.Find("NetworkObject_3").gameObject;
        //PersistentManager.Instance._visualizeGaze_client = VisualizeGazeClient((int)go_N3.transform.position.z);
        if(go_N0 == null)
        {
            go_N0 = GameObject.Find("NetworkObject_0");
            Debug.LogError($"n0 = {go_N0}");
        }
        if(go_N0 != null && enter == true)
        {
            expNr = (int)go_N0.transform.position.x;
            PersistentManager.Instance._visualizeGaze_client = VisualizeGazeClient(expNr);
            Debug.LogError($"n0 posx = {(int)go_N0.transform.position.x}");
            Debug.LogError($"Visualizegazeclient value = {PersistentManager.Instance._visualizeGaze_client}");
            // For some reason the exp nr is substracted by 1 at random times. This is only a problem for expnr 4 since that is the start of the visualization.
            // At expnr 3 the visualization is turned off.
            if(expNr == 4)
            {
                enter = false;
            }
        }
    }

    public bool VisualizeGazeClient(int expnr)
    {
        bool output = true;
        if (expnr < 4)
        {
            output = false;
        }
        else if (expnr >= 4)
        {
            output = true;
        }
        return output;
    }
}
