using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script was originally used by De Clercq. It has been omitted from the experiment of Kooijman.

public class SpeedSettings : MonoBehaviour
{
    //Simple Kinematics
    public float speed = 50;            //km/h
    public float acceleration = 0;      //m/s^2
    //Advanced Kinematics
    public float jerk = 5;              //m/s^3
    public bool resetSpeedAfterStop = false;
    //Dynamics
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
