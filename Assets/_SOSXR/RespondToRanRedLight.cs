using UnityEngine;
using UnityEngine.Events;


public class RespondToRanRedLight : MonoBehaviour
{
    [SerializeField] private UnityEvent m_eventToFire;


    private void OnEnable()
    {
        Obey.RanRedLight += FireEvent;
    }


    private void FireEvent()
    {
        m_eventToFire?.Invoke();
        Debug.Log("Ran red light has been Invoked somewhere, I respond with this UnityEvent");
    }


    private void OnDisable()
    {
        Obey.RanRedLight -= FireEvent;
    }
}