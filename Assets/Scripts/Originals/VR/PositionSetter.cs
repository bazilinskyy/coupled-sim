using UnityEngine;
using System.Collections;
using Varjo;

[ExecuteInEditMode]
public class PositionSetter : MonoBehaviour {
    public Transform target;
    private Vector3 hmdPosition;
    private Vector3 changePos;
    private Vector3 nextPosition;
    private float moveSpeed = 1;

    public bool Use_HMD;
	void LateUpdate () {
        if (Use_HMD == true)
        {
            // Position setting
            hmdPosition = VarjoManager.Instance.GetHMDPosition(VarjoPlugin.PoseType.CENTER);
            changePos = target.position;
            if (hmdPosition.y > 0.0f)
            {
                changePos.y = changePos.y - hmdPosition.y;
            }
            transform.position = changePos;

            // Smoothening
            //transform.position = Vector3.Lerp(changePos, nextPosition, Time.deltaTime * moveSpeed);
        }
        else if(Use_HMD == false)
        {
            transform.position = target.position; //  + new Vector3(-0.03f,+0.16f,-0.7f);
        }

        //Debug.LogError($"Target pos = {target.position}");
        //Debug.LogError($"HMD pos = {hmdPosition}");
        //Debug.LogError($"change pos = {changePos}");
        //Debug.LogError($"transform position {gameObject.transform.position}");
        //Debug.LogError($"transform local pos = {transform.localPosition}");
    }
}
