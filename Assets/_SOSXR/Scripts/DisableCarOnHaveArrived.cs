using UnityEngine;


[RequireComponent(typeof(AICar))]
public class DisableCarOnHaveArrived : MonoBehaviour
{
    private AICar _aiCar;


    private void Awake()
    {
        _aiCar = GetComponent<AICar>();
    }


    private void OnEnable()
    {
        EventsSystem.HaveArrived += DisableAICar;
    }


    private void DisableAICar()
    {
        _aiCar.enabled = false;
    }


    private void OnDisable()
    {
        EventsSystem.HaveArrived -= DisableAICar;
    }
}