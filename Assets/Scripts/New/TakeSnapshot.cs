using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakeSnapshot : MonoBehaviour
{
    public SnapshotCamera snapCam;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            snapCam.CallTakeSnapshot();
        }
    }
}
