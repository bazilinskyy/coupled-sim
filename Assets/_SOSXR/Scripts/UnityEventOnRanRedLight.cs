using UnityEngine;
using UnityEngine.Events;


public class UnityEventOnRanRedLight : MonoBehaviour
{
    [SerializeField] private UnityEvent m_eventToFire;


    private void OnEnable()
    {
        Obey.RanRedLight += FireEvent;
    }


    private void FireEvent()
    {
        Debug.Log("The Action/Event 'RanRedLight' has been invoked somewhere, I respond with firing this UnityEvent");
        m_eventToFire?.Invoke();
    }


    private void OnDisable()
    {
        Obey.RanRedLight -= FireEvent;
    }
}