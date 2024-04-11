using UnityEngine;
using UnityEngine.XR;


public class CameraCounter : MonoBehaviour
{
    [SerializeField] private bool m_allowRecentering = false;

    private Transform _childCamera;


    private void Awake()
    {
        _childCamera = transform.GetChild(0);
    }


    private void Update()
    {
        if (!m_allowRecentering)
        {
            return;
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            InputTracking.Recenter();
        }
    }


    private void LateUpdate()
    {
        var invertedPosition = -_childCamera.localPosition;
        transform.localPosition = invertedPosition;
    }
}