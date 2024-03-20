using UnityEngine;


public class OnTriggerEnterDisableCar : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var aiCar = other.transform.root.gameObject.GetComponentInChildren<AICar>();

        if (aiCar != null)
        {
            aiCar.enabled = false;

            Debug.Log("Disabled car");
        }
    }
}