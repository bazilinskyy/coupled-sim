using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;


public class RecenterXROrigin : MonoBehaviour
{
    [SerializeField] [Range(0f, 10f)] private readonly float m_fireDelay = 2f;

    [SerializeField] private readonly bool m_debug = false;
    private Transform _xrCamera;
    private XROrigin _xrOrigin;

    public Transform RecenterTo { get; set; }


    private void Awake()
    {
        GetRequiredComponents();
    }


    private void GetRequiredComponents()
    {
        _xrOrigin = GetComponent<XROrigin>();

        if (_xrOrigin == null)
        {
            return;
        }

        _xrCamera = _xrOrigin.GetComponentInChildren<Camera>().transform;
    }


    private void OnEnable()
    {
        Invoke(nameof(RecenterAndFlatten), m_fireDelay);
        Debug.LogFormat("SOSXR: Will run {0} in {1} seconds", nameof(RecenterAndFlatten), m_fireDelay);
    }


    [ContextMenu(nameof(RecenterAndFlatten))]
    public virtual void RecenterAndFlatten()
    {
        RecenterPosition(true);
        RecenterRotation();

        Debug.LogFormat("SOSXR: We just ran {0}", nameof(RecenterAndFlatten));
    }


    [ContextMenu(nameof(RecenterWithoutFlatten))]
    public virtual void RecenterWithoutFlatten()
    {
        RecenterPosition(false);
        RecenterRotation();

        Debug.LogFormat("SOSXR: We just ran {0}", nameof(RecenterWithoutFlatten));
    }


    private void RecenterPosition(bool flatten)
    {
        if (RecenterTo == null)
        {
            Debug.LogWarning("I don't have a RecenterTo object. Have you tried setting this some way?");
            return;
        }
        
        var distanceDiff = RecenterTo.transform.position - _xrCamera.position;
        _xrOrigin.transform.position += distanceDiff;

        if (flatten && _xrOrigin.CurrentTrackingOriginMode == TrackingOriginModeFlags.Floor)
        {
            Debug.LogWarning("SOSXR: You want to flatten the _xrCamera on the _xrRig, but the CurrenTrackingOrigin-mode on the aforementioned Rig is set to 'floor', which doesn't allow setting the Y component");

            return;
        }

        if (flatten)
        {
            _xrOrigin.transform.position = _xrOrigin.transform.position.Flatten();
        }
    }


    private void RecenterRotation()
    {
        var rotationAngleY = RecenterTo.transform.rotation.eulerAngles.y - _xrCamera.transform.rotation.eulerAngles.y;

        _xrOrigin.transform.Rotate(0, rotationAngleY, 0);
    }


    private void Update()
    {
        if (!m_debug)
        {
            return;
        }

        RecenterAndFlatten();
        Debug.Log("SOSXR: You're recentering the rig every frame. This is only supposed to happen when you want to find out the correct position for the HMD. Because this is A) expensive, B) mightily uncomfortable");
    }
}