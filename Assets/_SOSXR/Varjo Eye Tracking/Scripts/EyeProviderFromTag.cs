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
        if (_eyeTrackingExample.LeftEyeTransform == null || _eyeTrackingExample.RightEyeTransform == null)
        {
            Debug.Log("SOSXR: Try to find transforms via tags");

            if (transform.root.FindChildByTag(m_leftEyeTag) != null)
            {
                _eyeTrackingExample.LeftEyeTransform = transform.root.FindChildByTag(m_leftEyeTag);
            }

            if (transform.root.FindChildByTag(m_rightEyeTag) != null)
            {
                _eyeTrackingExample.RightEyeTransform = transform.root.FindChildByTag(m_rightEyeTag);
            }
        }

        if (_eyeTrackingExample.LeftEyeTransform != null && _eyeTrackingExample.RightEyeTransform != null)
        {
            Debug.Log("SOSXR: Found eyeball transforms via tag, will disable EyeProviderFromTag now");
            enabled = false;
        }
    }
}