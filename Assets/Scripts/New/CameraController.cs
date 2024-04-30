using UnityEngine;
using UnityEngine.XR;


// Script obtained from MVN to reset the camera of the Oculus onto the head of the MVN Avatar.
// Used in combination with AnchorController.cs


public class CameraController : MonoBehaviour
{
    [SerializeField] private KeyCode m_recenterKey = KeyCode.Alpha0;
    [SerializeField] private bool m_allowRecentering = false;

    private Transform _childCamera;
    private XRInputSubsystem _xrInputSubsystem;


    private void Awake()
    {
        _xrInputSubsystem = new XRInputSubsystem();
    }


    private void Start()
    {
        _childCamera = transform.parent;
    }


    private void Update()
    {
        if (!m_allowRecentering)
        {
            return;
        }

        if (Input.GetKeyDown(m_recenterKey))
        {
            _xrInputSubsystem.TryRecenter();
        }
    }


    private void LateUpdate()
    {
        var invertedPosition = -_childCamera.localPosition;
        var invertedRotation = Quaternion.Inverse(_childCamera.localRotation);
        transform.localPosition = invertedPosition;
        transform.localRotation = invertedRotation;
    }
}