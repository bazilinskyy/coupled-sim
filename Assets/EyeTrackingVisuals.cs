using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using System.Linq;
public class EyeTrackingVisuals : MonoBehaviour
{

    public VarjoPlugin.GazeData gazeData;
    public Transform cam;
    public Transform gazeHighlights;
    public KeyCode switchGazeHighlight;
    public bool highlightGaze = false;

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(switchGazeHighlight)) { Debug.Log("Pressing key!"); highlightGaze = !highlightGaze; }
        if (highlightGaze)
        {

            gazeData = VarjoPlugin.GetGaze();
            if (gazeData.status == Varjo.VarjoPlugin.GazeStatus.VALID)
            {
                RenderGaze(gazeData);
            }
            /*else
            {
                HideGazeRender();
            }*/

            /*foreach (var data in dataSinceLastUpdate)
            {
                RenderGaze(data);
            }*/
        }
    }

    void RenderGaze(VarjoPlugin.GazeData data)
    {
        bool invalid = data.status == VarjoPlugin.GazeStatus.INVALID;
        RaycastHit hit;

        if (!invalid)
        {
            //cast ray with gaze direction and gaze starting position:
            Vector3 start = new Vector3((float)data.gaze.position[0], (float)data.gaze.position[1], (float)data.gaze.position[2]);
            
            start += cam.position;
            Vector3 gazeDirection = new Vector3((float)data.gaze.forward[0], (float)data.gaze.forward[1], (float)data.gaze.forward[2]);

            //if hit something draw a little circle
            if (Physics.Raycast(start, gazeDirection, out hit, 100f, Physics.AllLayers))
            {
                Vector3 hitPosition = new Vector3(hit.point.x, hit.point.y, hit.point.z);
                gazeHighlights.transform.position = hitPosition;
            }
            else
            {
                //hide if no hits are gathered
                HideGazeRender();
            }


        }
    }

    void HideGazeRender()
    {
        gazeHighlights.transform.position = new Vector3(0, -1f, 0);
    }
}
