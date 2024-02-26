using System.Collections;
using SOSXR;
using UnityEngine;


// This script was originally used by De Clercq. It has been omitted from the experiment of Kooijman.


public class SpeedSettings : MonoBehaviour
{
    public enum WaypointType
    {
        InitialSetSpeed,
        SetSpeedTarget,
        Delete
    }


    [HideInInspector]
    public AICar targetAICar;
    //Simple Kinematics

    [Range(0, 80)] public float speed = Defaults.Speed; //km/h
    [Range(1, 5)] public float acceleration = Defaults.Acceleration; //m/s^2
    [Range(-5, -1)] public float brakingAcceleration = Defaults.Deceleration; //must be negative

    [DisableEditing] public WaypointType Type = WaypointType.SetSpeedTarget;
    [DisableEditing] public BlinkerState BlinkerState = BlinkerState.None;

    //Advanced Kinematics
    [DisableEditing] public float jerk = Defaults.Jerk; //m/s^3
    //public bool resetSpeedAfterStop = false;
    //Yielding
    [DisableEditing] public bool causeToYield;

    [DisableEditing] public bool EyeContactWhileYielding;

    [DisableEditing] public bool EyeContactAfterYielding;

    [DisableEditing] public float yieldTime;
    [DisableEditing] public float YieldingEyeContactSince;

    [DisableEditing] public float YieldingEyeContactUntil;
    private float startTime;
    //Dynamics
    public CustomBehaviourData[] CustomBehaviourData { get; set; }


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

            if (eyeContact != null)
            {
                StartCoroutine(LookAtPlayerAfterCarStops(other.gameObject.GetComponent<AICar>(), eyeContact));
            }
        }

        foreach (var bd in CustomBehaviourData)
        {
            aiCar.TriggerCustomBehaviours(bd);
        }
    }


    private IEnumerator LookAtPlayerAfterCarStops(AICar car, EyeContact driver)
    {
        while (car.state != AICar.CarState.STOPPED)
        {
            yield return new WaitForFixedUpdate();
        }

        driver.Tracking = EyeContactAfterYielding;

        if (EyeContactWhileYielding)
        {
            driver.TrackingWhileYielding = false;
            startTime = Time.fixedTime;

            while (YieldingEyeContactSince > Time.fixedTime - startTime)
            {
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
        var result = "";

        foreach (var cbd in CustomBehaviourData)
        {
            if (!string.IsNullOrWhiteSpace(result))
            {
                result += "%";
            }

            result += cbd.name + "#" + cbd.GetInstanceID();
        }

        return result;
    }
}


public static class Defaults
{
    public const int WaypointType = 1;
    public const float Speed = 5;
    public const float Acceleration = 5;
    public const float Jerk = 0;
    public const int BlinkerState = 0;
    public const float Deceleration = -3;
}