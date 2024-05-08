using UnityEngine;


[RequireComponent(typeof(IHaveEyes))]
public class EyeProviderFromTag : MonoBehaviour
{
    [TagSelector] [SerializeField] private string m_leftEyeTag = "Eye_Left";
    [TagSelector] [SerializeField] private string m_rightEyeTag = "Eye_Right";

    private IHaveEyes _varjoEyeTracking;


    private void Awake()
    {
        _varjoEyeTracking = GetComponent<IHaveEyes>();
        FindEyes();
    }


    private void Update()
    {
        FindEyes();
    }


    private void FindEyes()
    {
        if (_varjoEyeTracking.LeftEyeTransform == null || _varjoEyeTracking.RightEyeTransform == null)
        {
            Debug.Log("SOSXR: Try to find transforms via tags");

            if (transform.root.FindChildByTag(m_leftEyeTag) != null)
            {
                _varjoEyeTracking.LeftEyeTransform = transform.root.FindChildByTag(m_leftEyeTag);
            }

            if (transform.root.FindChildByTag(m_rightEyeTag) != null)
            {
                _varjoEyeTracking.RightEyeTransform = transform.root.FindChildByTag(m_rightEyeTag);
            }
        }

        if (_varjoEyeTracking.LeftEyeTransform != null && _varjoEyeTracking.RightEyeTransform != null)
        {
            Debug.Log("SOSXR: Found eyeball transforms via tag, will disable EyeProviderFromTag now");
            enabled = false;
        }
    }
}