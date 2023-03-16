using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// This script was originally used by De Clercq. It has been omitted from the experiment of Kooijman.

public class SpeedSettings : MonoBehaviour
{
    public static class Defaults
    {
        public const int WaypointType = 1;
        public const float Speed = 5;
        public const float Acceleration = 5;
        public const float Jerk = 0;
        public const int BlinkerState = 0;
    }

    public enum WaypointType
    {
        InitialSetSpeed,
        SetSpeedTarget,
        Delete,
    }

    [HideInInspector]
    public AICar targetAICar;
    //Simple Kinematics
    [FormerlySerializedAs("WaypointNumber")]
    public WaypointType Type = WaypointType.SetSpeedTarget;
    public float speed = Defaults.Speed;                    //km/h
    public float acceleration = Defaults.Acceleration;      //m/s^2
    public BlinkerState BlinkerState = BlinkerState.None;

    //Advanced Kinematics
    public float jerk = Defaults.Jerk;                      //m/s^3
    //public bool resetSpeedAfterStop = false;
    //Yielding
    public bool causeToYield;
    [FormerlySerializedAs("lookAtPlayerWhileYielding")]
    public bool EyeContactWhileYielding;
    [FormerlySerializedAs("lookAtPlayerAfterYielding")]
    public bool EyeContactAfterYielding;

    public float yieldTime;
    public float brakingAcceleration; //must be negative
    [FormerlySerializedAs("lookAtPedFromSeconds")]
    public float YieldingEyeContactSince;
    [FormerlySerializedAs("lookAtPedToSeconds")]
    public float YieldingEyeContactUntil;
    //Dynamics
    public CustomBehaviourData[] customBehaviourData;

    private void OnTriggerEnter(Collider other)
    {
        var aiCar = other.GetComponent<AICar>();
        if (aiCar == null || (targetAICar != null && targetAICar != aiCar))
        {
            return;
        }
        if (other.gameObject.CompareTag("ManualCar") && causeToYield)
        {
            var eyeContact = other.gameObject.GetComponentInChildren<EyeContact>();
            if (eyeContact != null) {
                StartCoroutine(LookAtPlayerAfterCarStops(other.gameObject.GetComponent<AICar>(), eyeContact));
            }
        }
        foreach (var bd in customBehaviourData) {
            aiCar.TriggerCustomBehaviours(bd);
        }
    }

    float startTime;


    private IEnumerator LookAtPlayerAfterCarStops(AICar car, EyeContact driver)
    {
        while (car.state != AICar.CarState.STOPPED)
        {
            yield return new WaitForFixedUpdate();
        }
        driver.Tracking = EyeContactAfterYielding;
        if (EyeContactWhileYielding) {
            driver.TrackingWhileYielding = false;
            startTime = Time.fixedTime;
            while (YieldingEyeContactSince > Time.fixedTime - startTime) {
                yield return new WaitForFixedUpdate();
            }
            driver.TrackingWhileYielding = true;
            while (YieldingEyeContactUntil > Time.fixedTime - startTime)
            {
                yield return new WaitForFixedUpdate();
            }
            driver.TrackingWhileYielding = false;
        }
    }

    internal string GetCustomBehaviourDataString()
    {
        string result = "";
        foreach(var cbd in customBehaviourData)
        {
            if (!string.IsNullOrWhiteSpace(result))
            {
                result += "%";
            }
            result += (cbd.name + "#" + cbd.GetInstanceID());
        }
        return result;
    }
}
//switch 
//        {
//            case Behavior.Simple:           //Adjustable: v, ENUM Set: a,jmax, jmin + Dynamics
//                wpControlScript.speed = EditorGUILayout.FloatField("Velocity in km/h", wpControlScript.speed);
//                behaviorSimple = (BehaviorSimple)EditorGUILayout.EnumPopup("Maneuvre", (BehaviorSimple)wpControlScript.j);
//                wpControlScript.j = (int)behaviorSimple;        //Save the users choice regarding Maneuver

//                wpControlScript.acceleration = wpControlScript.accValues[(int)behaviorSimple];
//                wpControlScript.jerk = wpControlScript.jerkValues[(int)behaviorSimple];

//                EditorGUILayout.LabelField("Acceleration in m/s^2", wpControlScript.acceleration.ToString());
//                EditorGUILayout.LabelField("Jerk in m/s^3", wpControlScript.jerk.ToString());

//                wpControlScript.i = 0;                          //Save the users choice regarding Behavior
//                wpControlScript.dynamics = false;
//                break;


//            case Behavior.Advanced:         //Adjustable: v, a, jmax, jmin; Set: Dynamics
//                wpControlScript.speed = EditorGUILayout.FloatField("Velocity in km/h", wpControlScript.speed);
//                wpControlScript.acceleration = EditorGUILayout.FloatField("Acceleration in m/s^2", wpControlScript.acceleration);
//                wpControlScript.jerk = EditorGUILayout.FloatField("Jerk in m/s^3", wpControlScript.jerk);

//                //EditorGUILayout.HelpBox("Make sure, to input sensible values. A velocity increase requires positive acceleration and jerk values, vice versa",MessageType.Warning);
//                wpControlScript.i = 1;                          //Save the users choice regarding Behavior
//                break;
//            case Behavior.Destroy:         //Adjustable: v, a, j, YawPitchRoll, RotationRate
//                wpControlScript.i = 2;                          //Save the users choice regarding Behavior
//                break;
//            case Behavior.Input:            //Input from File: v(t); x,y,z(t); Adjustable: Filetype, 
//                EditorGUILayout.LabelField("Function not yet available", EditorStyles.boldLabel);
//                wpControlScript.i = 3;                          //Save the users choice regarding Behavior
//                break;
//            case Behavior.Routine:
//                //EditorGUILayout.LabelField("Function not yet available", EditorStyles.boldLabel);
//                wpControlScript.HMIcontrol = EditorGUILayout.IntField("CoRoutine", wpControlScript.HMIcontrol);
//                wpControlScript.i = 4;                          //Save the users choice regarding Behavior
//                break;

//            case Behavior.RoutineAndChange:
//                //Number corresponding to Co-Routine in HMI-Control Script
//                wpControlScript.HMIcontrol = EditorGUILayout.IntField("CoRoutine", wpControlScript.HMIcontrol);
//                wpControlScript.speed = EditorGUILayout.FloatField("Velocity in km/h", wpControlScript.speed);
//                wpControlScript.acceleration = EditorGUILayout.FloatField("Acceleration in m/s^2", wpControlScript.acceleration);
//                wpControlScript.jerk = EditorGUILayout.FloatField("Jerk in m/s^3", wpControlScript.jerk);

//                wpControlScript.i = 5;                          //Save the users choice regarding Behavior
//                break;
//        }

//    }
//}
