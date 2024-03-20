using UnityEngine;
using UnityEngine.Events;


public class OnTriggerEnterEvent : MonoBehaviour
{
    [SerializeField] protected UnityEvent m_eventToFire;
    [SerializeField] [TagSelector] protected string m_tagToCheckFor = "ManualCar";

    public UnityEvent EventToFire => m_eventToFire;


    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag(m_tagToCheckFor))
        {
            m_eventToFire?.Invoke();
        }
    }
}