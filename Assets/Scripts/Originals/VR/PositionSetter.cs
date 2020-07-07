using UnityEngine;
using System.Collections;
using Varjo;

// To Solve: position setting during turns.
public enum enumRot
{
    None,
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
    public float head_corr = 0.20f;


    void LateUpdate () {
        if (Use_HMD == true)
        {
            // Get HMD pos
            hmdPosition = VarjoManager.Instance.GetHMDPosition(VarjoPlugin.PoseType.CENTER);

            // Get target pos
            changePos = target.position;

            // Effect of HMD on target pos
            rot = gameObject.transform.rotation.eulerAngles.y;
            determineAngle(rot, 25);
            effectHMD(rot_);

            transform.position = changePos;
        }
        else if(Use_HMD == false)
        {
            transform.position = target.position; //  + new Vector3(-0.03f,+0.16f,-0.7f);
        } 
    }

    private void determineAngle(float rot, float diff)
    {
        if (rot < (0+diff) || rot > (360-diff))          
        {
            rot_ = enumRot.Zero;
        }
        else if (rot > (90-diff) && rot < (90+diff))     
        {
            rot_ = enumRot.NineZero;
        }
        else if (rot > (180-diff) && rot < (180+diff))    
        {
            rot_ = enumRot.OneEightZero;
        }
        else if (rot > (270-diff) && rot < (270+diff))    
        {
            rot_ = enumRot.TwoSevenZero;
        }
        else
        {
            rot_ = enumRot.None;
        }
    }

    private void effectHMD(enumRot rot_)
    {
        switch (rot_)
        {
            case enumRot.Zero:
                changePos.x = changePos.x - hmdPosition.x; 
                changePos.z = changePos.z - hmdPosition.z + head_corr;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case enumRot.NineZero:
                changePos.x = changePos.x - hmdPosition.z + head_corr;
                changePos.z = changePos.z + hmdPosition.x;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case enumRot.OneEightZero:
                changePos.x = changePos.x + hmdPosition.x;
                changePos.z = changePos.z + hmdPosition.z - head_corr;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case enumRot.TwoSevenZero:
                changePos.x = changePos.x + hmdPosition.z - head_corr;
                changePos.z = changePos.z - hmdPosition.x;
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
            case enumRot.None:
                if (hmdPosition.y > 0.0f)
                {
                    changePos.y = changePos.y - hmdPosition.y;
                }
                break;
        }
    }
}
