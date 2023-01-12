using UnityEngine;
using UnityEngine.XR;
using System.Collections;

public class CameraCounter : MonoBehaviour {

    Transform
        childCamera;

	// Use this for initialization
	void Start () {
        childCamera = transform.GetChild(0);
	}

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.XR.InputTracking.Recenter();
        }
    }
    // Update is called once per frame
    void LateUpdate () {
        Vector3 invertedPosition = -childCamera.localPosition;
        transform.localPosition = invertedPosition;
	}
}
