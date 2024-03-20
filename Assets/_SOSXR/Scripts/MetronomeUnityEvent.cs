using System.Collections;
using UnityEngine;
using UnityEngine.Events;


public class MetronomeUnityEvent : MonoBehaviour
{
    [SerializeField] private UnityEvent m_eventToFire;
    
    [Space(20)]
    [SerializeField] [Range(0, 120)] private int m_initialDelay = 2;
    [SerializeField] [Range(1, 120)] private int m_playInterval = 45;


    private void Start()
    {
        StartCoroutine(IntervalCR());
    }


    private IEnumerator IntervalCR()
    {
        Debug.LogFormat("Will wait for {0}s before first fire", m_initialDelay);

        yield return new WaitForSeconds(m_initialDelay);

        for (;;)
        {
            m_eventToFire?.Invoke();

            Debug.LogFormat("Will wait for {0}s in between firing event", m_playInterval);

            yield return new WaitForSeconds(m_playInterval);
        }
    }


    private void OnDisable()
    {
        StopAllCoroutines();
    }
}