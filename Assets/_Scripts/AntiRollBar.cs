using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AntiRollBar : MonoBehaviour
{
    //From https://forum.unity.com/threads/how-to-make-a-physically-real-stable-car-with-wheelcolliders.50643/?_ga=2.86117656.1245993592.1600858458-503217333.1590668079
    //Add two anti-roll scripts to your vehicle, one per axle (front - rear). 
    //Set the left-right wheels on each one and adjust the AntiRoll value.
    //emember to reset center of mass to the real position, or don't touch it at all!
    public WheelCollider WheelL;
    public WheelCollider WheelR;
    public float AntiRoll = 5000.0f;

    void FixedUpdate()
    {
        WheelHit hit;
        float travelL = 1.0f;
        float travelR = 1.0f;

        var groundedL = WheelL.GetGroundHit(out hit);
        if (groundedL)
            travelL = (-WheelL.transform.InverseTransformPoint(hit.point).y - WheelL.radius) / WheelL.suspensionDistance;

        var groundedR = WheelR.GetGroundHit(out hit);
        if (groundedR)
            travelR = (-WheelR.transform.InverseTransformPoint(hit.point).y - WheelR.radius) / WheelR.suspensionDistance;

        var antiRollForce = (travelL - travelR) * AntiRoll;

        if (groundedL)
            GetComponent<Rigidbody>().AddForceAtPosition(WheelL.transform.up * -antiRollForce,
                   WheelL.transform.position);
        if (groundedR)
            GetComponent<Rigidbody>().AddForceAtPosition(WheelR.transform.up * antiRollForce,
                   WheelR.transform.position);
    }
}

