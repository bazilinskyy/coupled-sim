using UnityEngine;


[RequireComponent(typeof(EyeTrackingExample))]
public class EyeProviderFromTag : MonoBehaviour
{
    [TagSelector] [SerializeField] private string m_leftEyeTag = "Eye_Left";
    [TagSelector] [SerializeField] private string m_rightEyeTag = "Eye_Right";

    private EyeTrackingExample _eyeTrackingExample;


    private void Awake()
    {
        _eyeTrackingExample = GetComponent<EyeTrackingExample>();
        FindEyes();
    }


    private void Update()
    {
        FindEyes();
    }


    private void FindEyes()
    {
        if (_eyeTrackingExample.m_leftEyeTransform == null || _eyeTrackingExample.m_rightEyeTransform == null)
        {
            Debug.Log("SOSXR: Try to find transforms via tags");

            if (transform.root.FindChildByTag(m_leftEyeTag) != null)
            {
                _eyeTrackingExample.m_leftEyeTransform = transform.root.FindChildByTag(m_leftEyeTag);
            }

            if (transform.root.FindChildByTag(m_rightEyeTag) != null)
            {
                _eyeTrackingExample.m_rightEyeTransform = transform.root.FindChildByTag(m_rightEyeTag);
            }
        }

        if (_eyeTrackingExample.m_leftEyeTransform != null && _eyeTrackingExample.m_rightEyeTransform != null)
        {
            enabled = false;
        }
    }
}