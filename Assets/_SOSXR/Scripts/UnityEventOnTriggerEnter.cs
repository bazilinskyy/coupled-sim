using UnityEngine;
using UnityEngine.Events;


public class UnityEventOnTriggerEnter : MonoBehaviour
{
    [SerializeField] private UnityEvent m_eventToFire;
    [SerializeField] [TagSelector] private string m_tagToCheckFor = "ManualCar";

    public UnityEvent EventToFire => m_eventToFire;


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag(m_tagToCheckFor))
        {
            m_eventToFire?.Invoke();
        }
    }
}