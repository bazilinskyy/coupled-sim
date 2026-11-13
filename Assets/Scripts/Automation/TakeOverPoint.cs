using UnityEngine;

/// <summary>A trigger on the road: the participant's AV issues a take-over request when it reaches it.</summary>
[RequireComponent(typeof(Collider))]
public class TakeOverPoint : MonoBehaviour
{
    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        var ad = other.GetComponentInParent<AutomatedDriving>();
        if (ad != null) ad.RequestTakeOver("TakeOverPoint " + name);
    }
}
