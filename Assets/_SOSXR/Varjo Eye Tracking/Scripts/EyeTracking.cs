using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;
using static Varjo.XR.VarjoEyeTracking;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;


public enum GazeDataSource
{
    InputSubsystem,
    GazeAPI
}


public class EyeTracking : MonoBehaviour
{
    [Header("KeyCodes")]
    [Tooltip("Will poll Varjo functions: IsGazeAllowed() and IsGazeCalibrated() simultaneously into one neat package")]
    [SerializeField] private KeyCode m_canWeUseGazeKey = KeyCode.Alpha9;
    [SerializeField] private KeyCode m_calibrationRequestKey = KeyCode.Space;
    [SerializeField] private KeyCode m_setOutputFilterTypeKey = KeyCode.RightShift;
    [SerializeField] private KeyCode m_toggleGazeTarget = KeyCode.Return;
    [FormerlySerializedAs("m_toggleFixationPoint")] [SerializeField] private KeyCode m_toggleFixationPointKey = KeyCode.Alpha5;

    [Header("Settings")]
    [SerializeField] private GazeDataSource m_gazeDataSource = GazeDataSource.InputSubsystem;
    [SerializeField] private GazeCalibrationMode m_gazeCalibrationMode = GazeCalibrationMode.Fast;
    [SerializeField] private GazeOutputFilterType m_gazeOutputFilterType = GazeOutputFilterType.Standard;
    [SerializeField] private GazeOutputFrequency m_gazeOutputFrequency;

    [Space(15)]
    [SerializeField] private Transform m_fixationPointTransform;
    [Tooltip("Gaze point indicator")]
    [SerializeField] private GameObject m_gazeTarget;
    [Tooltip("Gaze ray radius")]
    [SerializeField] private float m_gazeRadius = 0.01f;
    [Tooltip("Gaze point distance if not hit anything")]
    [SerializeField] private float m_floatingGazeTargetDistance = 5f;
    [Tooltip("Gaze target offset towards viewer")]
    [SerializeField] private float m_targetOffset = 0.2f;

    private readonly List<InputDevice> _devices = new();
    private MeshRenderer _gazeRenderer;
    private Camera _camera;
    private InputDevice _inputDevice;
    private Vector3 _direction;
    private float _distance;
    private Eyes _eyes;
    private Vector3 _fixationPoint;
    private GazeData _gazeData;
    private RaycastHit _hit;
    private Vector3 _leftEyeTrackingPosition;
    private Quaternion _leftEyeTrackingRotation;
    private Vector3 _rayOrigin;
    private Vector3 _rightEyeTrackingPosition;
    private Quaternion _rightEyeTrackingRotation;
    private LogEyeTracking _log;


    private void Awake()
    {
        _camera = transform.root.GetComponentInChildren<Camera>();
        _gazeRenderer = m_gazeTarget.transform.root.GetComponentInChildren<MeshRenderer>();
        _log = GetComponent<LogEyeTracking>();
    }


    private void Start()
    {
        SetGazeOutputFrequency(m_gazeOutputFrequency);
    }


    private void OnEnable()
    {
        GetDevice();
    }


    private void GetDevice()
    {
        if (_inputDevice.isValid)
        {
            return;
        }

        InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, _devices);
        _inputDevice = _devices.FirstOrDefault();
    }


    private void Update()
    {
        if (Input.GetKeyDown(m_toggleFixationPointKey))
        {
            m_fixationPointTransform.gameObject.SetActive(!m_fixationPointTransform.gameObject.activeInHierarchy);
        }

        if (Input.GetKeyDown(m_calibrationRequestKey))
        {
            RequestGazeCalibration(m_gazeCalibrationMode);
        }

        if (Input.GetKeyDown(m_setOutputFilterTypeKey))
        {
            SetGazeOutputFilterType(m_gazeOutputFilterType);

            Debug.Log("Gaze output filter type is now: " + GetGazeOutputFilterType());
        }

        if (Input.GetKeyDown(m_canWeUseGazeKey))
        {
            Debug.Log("Can we use gaze? " + CanWeUseGaze());
        }

        if (Input.GetKeyDown(m_toggleGazeTarget))
        {
            if (m_gazeTarget.activeInHierarchy || (!m_gazeTarget.activeInHierarchy && CanWeUseGaze()))
            {
                m_gazeTarget.SetActive(!m_gazeTarget.activeInHierarchy);
            }
        }

        _log.LogFrameEyeTrackingData();
    }


    private void LateUpdate()
    {
        GetEyeData();

        SphereCast();
    }


    private bool CanWeUseGaze()
    {
        return IsGazeAllowed() && IsGazeCalibrated();
    }


    /// <summary>
    ///     Get gaze data if gaze is allowed and calibrated
    /// </summary>
    private void GetEyeData()
    {
        if (!CanWeUseGaze())
        {
            return;
        }

        GetDevice();

        m_gazeTarget.SetActive(true);

        if (m_gazeDataSource == GazeDataSource.InputSubsystem)
        {
            GetGazeDataFromInputSubsystem();
        }
        else if (m_gazeDataSource == GazeDataSource.GazeAPI)
        {
            GetGazeDataFromGazeApi();
        }
    }


    private void GetGazeDataFromInputSubsystem()
    {
        if (!_inputDevice.TryGetFeatureValue(CommonUsages.eyesData, out _eyes))
        {
            return;
        }

        if (_eyes.TryGetFixationPoint(out _fixationPoint) && m_fixationPointTransform != null)
        {
            m_fixationPointTransform.localPosition = _fixationPoint;
        }

        _rayOrigin = _camera.transform.position;

        _direction = (m_fixationPointTransform.position - _camera.transform.position).normalized;
    }


    private void GetGazeDataFromGazeApi()
    {
        _gazeData = GetGaze();

        if (_gazeData.status == GazeStatus.Invalid)
        {
            Debug.LogWarning("GazeStatus is Invalid");

            return;
        }

        _rayOrigin = _camera.transform.TransformPoint(_gazeData.gaze.origin); // Set gaze origin as raycast origin

        _direction = _camera.transform.TransformDirection(_gazeData.gaze.forward); // Set gaze direction as raycast direction

        m_fixationPointTransform.position = _rayOrigin + _direction * _gazeData.focusDistance; // Fixation point can be calculated using ray origin, direction and focus distance
    }


    private void SphereCast()
    {
        if (Physics.SphereCast(_rayOrigin, m_gazeRadius, _direction, out _hit)) // Raycast to world from XR Camera position towards fixation point
        {
            m_gazeTarget.transform.position = _hit.point - _direction * m_targetOffset; // Put target on gaze raycast position with offset towards user

            m_gazeTarget.transform.LookAt(_rayOrigin, Vector3.up); // Make gaze target point towards user

            _distance = _hit.distance; // Scale gaze-target with distance so it appears to be always same size

            m_gazeTarget.transform.localScale = Vector3.one * _distance;
        }
        else // If gaze ray didn't hit anything, the gaze target is shown at fixed distance
        {
            m_gazeTarget.transform.position = _rayOrigin + _direction * m_floatingGazeTargetDistance;
            m_gazeTarget.transform.LookAt(_rayOrigin, Vector3.up);
            m_gazeTarget.transform.localScale = Vector3.one * m_floatingGazeTargetDistance;
        }
    }
}