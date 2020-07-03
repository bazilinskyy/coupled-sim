using UnityEngine;
using System.Collections;
using Varjo;

// To Solve: position setting during turns.
public enum enumRot
{
    Zero,
    NineZero,
    OneEightZero,
    TwoSevenZero,
}

[ExecuteInEditMode]
public class PositionSetter : MonoBehaviour {
    public Transform target;
    public bool Use_HMD;

    private Vector3 hmdPosition;
    private Vector3 changePos;
    private float rot;
    private enumRot rot_;


    void LateUpdate () {
        if (Use_HMD == true)
        {
            // Get HMD pos
            hmdPosition = VarjoManager.Instance.GetHMDPosition(VarjoPlugin.PoseType.CENTER);

            // Get target pos
            changePos = target.position;

            // Effect of HMD on target pos
            rot = gameObject.transform.rotation.eulerAngles.y;
            determineAngle(rot);
            effectHMD(rot_);

            transform.position = changePos;
        }
        else if(Use_HMD == false)
        {
            transform.position = target.position; //  + new Vector3(-0.03f,+0.16f,-0.7f);
        } 
    }

    private void determineAngle(float rot)
    {
        if (rot < 40 || rot > 320)          // 0 +- 40
        {
            rot_ = enumRot.Zero;
        }
        else if (rot > 50 && rot < 130)     // 90 +- 40
        {
            rot_ = enumRot.NineZero;
        }
        else if (rot > 140 && rot < 220)    // 180 +- 40
        {
            rot_ = enumRot.OneEightZero;
        }
        else if (rot > 230 && rot < 310)    // 270 +- 40
        {
            rot_ = enumRot.TwoSevenZero;
        }
    }

    private void effectHMD(enumRot rot_)
    {
        switch (rot_)
        {
            case enumRot.Zero:
                changePos.x = changePos.x - hmdPosition.x; 
                changePos.z = changePos.z - hmdPosition.z;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case enumRot.NineZero:
                changePos.x = changePos.x - hmdPosition.z;
                changePos.z = changePos.z + hmdPosition.x;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case enumRot.OneEightZero:
                changePos.x = changePos.x + hmdPosition.x;
                changePos.z = changePos.z + hmdPosition.z;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case enumRot.TwoSevenZero:
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
