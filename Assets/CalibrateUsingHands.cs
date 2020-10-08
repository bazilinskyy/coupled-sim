using UnityEngine;
public class CalibrateUsingHands : MonoBehaviour
{
    //Usage: Add this script to the head position. Call SetHeadPositionUsingHands() when hands are positioned on the steering wheel. 
    //Returns: bool indicating succes.
    //Workings:
    //(1) Checks if both hands are visible
    //(2) Calculates the vector from average of two hands to the camera
    //(3) Sets the position of this object (i.e., the head position) to match this vector w.r.t. the steeringwheel inside the car

    public Transform LeapRig; // the leap rig.
    

    //LeapMotion rigged hand prefabs
    public Leap.Unity.HandModel leftHand;
    public Leap.Unity.HandModel rightHand;

    //Wrist positions of the steering wheel:
    public Transform steeringWheel;
    private Transform leftWristSteeringWheel;
    private Transform rightWristSteeringWheel;

    private Transform VarjoCamara; //The varjo camera within the Leap rig
    private Vector3 handsToCam = new Vector3();
    private Vector3 handToHand = new Vector3();
    private Vector3 leftWristPos;
    private Vector3 rightWristPos;
    private Vector3 steeringWheelToCam;
    public bool SetPositionUsingHands()
    {
        if(VarjoCamara == null) { VarjoCamara = LeapRig.Find("VarjoCameraRig").Find("VarjoCamera"); }
        if(leftWristSteeringWheel == null) { leftWristSteeringWheel = steeringWheel.transform.Find("LeftWristPosition"); }
        if (rightWristSteeringWheel == null) { rightWristSteeringWheel = steeringWheel.transform.Find("RightWristPosition"); }

        //Some checks
        if (leftWristSteeringWheel == null || rightWristSteeringWheel == null) { Debug.Log("could not find predefined wrist position on steering wheel..."); return false; }
        if(VarjoCamara == null) { Debug.Log("could not find varjo camera..."); return false; }

        if (leftHand.gameObject.activeSelf && rightHand.gameObject.activeSelf)
        {
            leftWristPos = leftHand.palm.position;
            rightWristPos = rightHand.palm.position;

            Vector3 posLeft = leftWristPos + (steeringWheel.position - leftWristSteeringWheel.position);
            Vector3 posRight = rightWristPos + (steeringWheel.position - rightWristSteeringWheel.position);
            steeringWheelToCam = VarjoCamara.position - (posLeft + posRight) / 2;

            //Set some other handy vectors
            handsToCam = VarjoCamara.position - (leftWristPos + rightWristPos) / 2;          
            handToHand = rightWristPos - leftWristPos;

            //Set head position accordingly
            transform.position = steeringWheel.transform.position + steeringWheelToCam;
            Debug.Log($"Succesfully calibrated headposition with hands on steering wheel handsToCam: {handsToCam}...");
            return true;
        }
        else { Debug.Log("Could not set hand position..."); return false; }
    }

    public void SetLeftHand(){ if (leftHand.gameObject.activeSelf) { leftWristPos = leftHand.palm.position; } }
    public void SetRightHand() { if (rightHand.gameObject.activeSelf) { rightWristPos = rightHand.palm.position; } }
    public Vector3 GetHandsToCam() { return handsToCam; }
    public Vector3 GetSteeringWheelToCam(){return steeringWheelToCam; } 
    public Vector3 GetLeftHandPos() { return leftWristPos; }
    public Vector3 GetRightHandPos() { return rightWristPos; }
    public Vector3 GetRightToLeftHand() { return handToHand; }

}
