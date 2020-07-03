using UnityEngine;
using System.Collections;
using Varjo;

// In the coupled-sim it functions in the y-direction, but not in the x- and z-direction.
// In the varjo testing scene it functions in all the directions.

[ExecuteInEditMode]
public class PositionSetter : MonoBehaviour {
    public Transform target;
    private Vector3 hmdPosition;
    private Vector3 changePos;
    private float rot;

    public bool Use_HMD;
    private void Start()
    {
        rot = gameObject.transform.rotation.eulerAngles.y;
        Debug.LogError($"Rotation = {gameObject.transform.rotation.eulerAngles.y}");
    }

    void LateUpdate () {
        if (Use_HMD == true)
        {
            // Get HMD pos
            hmdPosition = VarjoManager.Instance.GetHMDPosition(VarjoPlugin.PoseType.CENTER);
            Debug.LogError($"1 HMD pos = {hmdPosition}");
            // Get target pos
            changePos = target.position;
            Debug.LogError($"2 target position = {target.position}");
            // Effect of HMD on target pos
            effectHMD(rot);
            /*changePos.x = changePos.x + hmdPosition.x; // left pos for world, left neg for hmd
            changePos.z = changePos.z + hmdPosition.z;
            if (hmdPosition.y > 0.0f)
            {
                changePos.y = changePos.y - hmdPosition.y; 
            }*/

            transform.position = changePos;
            Debug.LogError($"3 (final) transform pos = {transform.position}");
        }
        else if(Use_HMD == false)
        {
            transform.position = target.position; //  + new Vector3(-0.03f,+0.16f,-0.7f);
        } 
    }

    private void effectHMD(float rot)
    {
        switch (rot)
        {
            case 0:
                changePos.x = changePos.x - hmdPosition.x; 
                changePos.z = changePos.z - hmdPosition.z;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case 90:
                changePos.x = changePos.x - hmdPosition.z;
                changePos.z = changePos.z + hmdPosition.x;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case 180:
                changePos.x = changePos.x + hmdPosition.x;
                changePos.z = changePos.z + hmdPosition.z;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case 270:
                changePos.x = changePos.x + hmdPosition.z;
                changePos.z = changePos.z - hmdPosition.x;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
        }
    }
}
