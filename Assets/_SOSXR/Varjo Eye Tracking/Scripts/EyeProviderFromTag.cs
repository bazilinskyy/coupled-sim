using UnityEngine;


[RequireComponent(typeof(EyeTrackingExample))]
public class EyeProviderFromTag : MonoBehaviour
{
    [TagSelector] [SerializeField] private string m_leftEyeTag;
    [TagSelector] [SerializeField] private string m_rightEyeTag;

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
        if (_eyeTrackingExample.leftEyeTransform == null || _eyeTrackingExample.rightEyeTransform == null)
        {
            Debug.Log("SOSXR: Try to find transforms via tags");
            if (transform.root.FindChildByTag(m_leftEyeTag) != null)
            {
                _eyeTrackingExample.leftEyeTransform = transform.root.FindChildByTag(m_leftEyeTag);
            }
            if (transform.root.FindChildByTag(m_rightEyeTag) != null)
            {
                _eyeTrackingExample.rightEyeTransform = transform.root.FindChildByTag(m_rightEyeTag);
            }
        }

        if (_eyeTrackingExample.leftEyeTransform != null && _eyeTrackingExample.rightEyeTransform != null)
        {
            enabled = false;
        }
    }
}