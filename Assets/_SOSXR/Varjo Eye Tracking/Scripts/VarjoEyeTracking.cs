using System.Collections.Generic;
using System.Linq;
using SOSXR;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;
using static Varjo.XR.VarjoEyeTracking;


public enum GazeDataSource
{
    InputSubsystem,
    GazeAPI
}


[RequireComponent(typeof(Camera))]
public class EyeTrackingExample : MonoBehaviour
{
    [Header("Debug Gaze")]
    [Tooltip("Will poll Varjos functions: IsGazeAllowed() and IsGazeCalibrated() simultanaously into one neat package")]
    [SerializeField] private KeyCode m_canWeUseGazeKey = KeyCode.Alpha9;

    [Tooltip("Gaze data")]
    [SerializeField] private GazeDataSource m_gazeDataSource = GazeDataSource.InputSubsystem;

    [Tooltip("Gaze calibration settings")]
    [SerializeField] private GazeCalibrationMode m_gazeCalibrationMode = GazeCalibrationMode.Fast;
    [FormerlySerializedAs("calibrationRequestKey")] [SerializeField] private KeyCode m_calibrationRequestKey = KeyCode.Space;

    [Tooltip("Gaze output filter settings")]
    [SerializeField] private GazeOutputFilterType m_gazeOutputFilterType = GazeOutputFilterType.Standard;
    [SerializeField] private KeyCode m_setOutputFilterTypeKey = KeyCode.RightShift;

    [Tooltip("Gaze data output frequency")]
    [SerializeField] private GazeOutputFrequency m_frequency;

    [Tooltip("Toggle gaze target visibility")]
    [SerializeField] private KeyCode m_toggleGazeTarget = KeyCode.Return;

    [Tooltip("Toggle fixation point indicator visibility")]
    [SerializeField] private bool m_showFixationPoint = true;
    [SerializeField] private Transform m_fixationPointTransform;

    [Header("EyeBalls")]
    [Tooltip("SOSXR: We don't want to set the localPosition of the eyes if it is in a model")]
    [SerializeField] private bool m_setEyePosition = false; // SOSXR
    public Transform LeftEyeTransform;
    public Transform RightEyeTransform;
    [Tooltip("Depending on the model, a rotation is needed here.")]
    [SerializeField] private Vector3 m_leftEyeRotationOffset = new(0, 0, 84.354f); // SOSXR
    [SerializeField] private Vector3 m_rightEyeRotationOffset = new(0, 0, 84.354f); // SOSXR

    [Header("EyeBalls - Trying to make sense of incoming data")]
    [SerializeField] private KeyCode m_printVarjoEyeTrackingInformation = KeyCode.Alpha8;
    [Tooltip("This is Varjo's data")]
    [SerializeField] [DisableEditing] private Vector3 m_rightEyeTrackingRotation;
    [Tooltip("This is Varjo's data with the offset")]
    [SerializeField] [DisableEditing] private Vector3 m_rightEyeRotationWithOffset;

    [Tooltip("Gaze point indicator")]
    [SerializeField] private GameObject m_gazeTarget;

    [Tooltip("Gaze ray radius")]
    [SerializeField] private float m_gazeRadius = 0.01f;

    [Tooltip("Gaze point distance if not hit anything")]
    [SerializeField] private float m_floatingGazeTargetDistance = 5f;

    [Tooltip("Gaze target offset towards viewer")]
    [SerializeField] private float m_targetOffset = 0.2f;

    [Header("Gaze data logging")]
    [SerializeField] private KeyCode m_loggingToggleKey = KeyCode.RightControl;
    [SerializeField] private bool m_useCustomLogPath = false;
    [SerializeField] private string m_customLogPath = "";


    private static readonly string[] _columnNames = {"Frame", "CaptureTime", "LogTime", "HMDPosition", "HMDRotation", "GazeStatus", "CombinedGazeForward", "CombinedGazePosition", "InterPupillaryDistanceInMM", "LeftEyeStatus", "LeftEyeForward", "LeftEyePosition", "LeftPupilIrisDiameterRatio", "LeftPupilDiameterInMM", "LeftIrisDiameterInMM", "RightEyeStatus", "RightEyeForward", "RightEyePosition", "RightPupilIrisDiameterRatio", "RightPupilDiameterInMM", "RightIrisDiameterInMM", "FocusDistance", "FocusStability"};

    private readonly List<InputDevice> _devices = new();

    private MeshRenderer _gazeRenderer;

    private Camera _camera;
    private List<GazeData> _dataSinceLastUpdate;
    private InputDevice _inputDevice;
    private Vector3 _direction;
    private float _distance;
    private List<EyeMeasurements> _eyeMeasurementsSinceLastUpdate;
    private Eyes _eyes;
    private Vector3 _fixationPoint;
    private GazeData _gazeData;

    private int _gazeDataCount = 0;
    private float _gazeTimer = 0f;
    private RaycastHit _hit;
    private Vector3 _leftEyeTrackingPosition;
    private Quaternion _leftEyeTrackingRotation;

    private Vector3 _rayOrigin;
    private Vector3 _rightEyeTrackingPosition;
    private Quaternion _rightEyeTrackingRotation;


    private const string _ValidString = "VALID";
    private const string _InvalidString = "INVALID";


    public EyeTrackingExample()
    {
        LOGEyeTracking = new LogEyeTracking(this);
    }


    public LogEyeTracking LOGEyeTracking { get; }


    private void Awake()
    {
        _camera = transform.root.GetComponentInChildren<Camera>();
        _gazeRenderer = m_gazeTarget.GetComponentInChildren<MeshRenderer>();
    }


    private void OnEnable()
    {
        if (_inputDevice.isValid)
        {
            return;
        }

        GetDevice();
    }


    private void GetDevice()
    {
        InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, _devices);
        _inputDevice = _devices.FirstOrDefault();
    }


    private void Start()
    {
        SetGazeOutputFrequency(m_frequency);

        m_gazeTarget.SetActive(CanWeUseGaze());

        m_fixationPointTransform.gameObject.SetActive(m_showFixationPoint);
    }


    private bool CanWeUseGaze()
    {
        return IsGazeAllowed() && IsGazeCalibrated();
    }


    private void Update()
    {
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
            _gazeRenderer.enabled = !_gazeRenderer.enabled;
        }

        if (Input.GetKeyDown(m_printVarjoEyeTrackingInformation))
        {
            Debug.Log(m_rightEyeTrackingRotation);
            Debug.Log(m_rightEyeRotationWithOffset);
        }

        HandleLogging();
    }


    private void HandleLogging()
    {
        if (Input.GetKeyDown(m_loggingToggleKey))
        {
            LOGEyeTracking.ToggleLogging();
        }

        if (!LOGEyeTracking._logging)
        {
            return;
        }

        _gazeTimer += Time.deltaTime;

        if (_gazeTimer >= 1.0f)
        {
            Debug.Log("Gaze data rows per second: " + _gazeDataCount);
            _gazeDataCount = 0;
            _gazeTimer = 0f;
        }

        var dataCount = GetGazeList(out _dataSinceLastUpdate, out _eyeMeasurementsSinceLastUpdate);

        _gazeDataCount += dataCount;

        for (var i = 0; i < dataCount; i++)
        {
            LOGEyeTracking.LogGazeData(_dataSinceLastUpdate[i], _eyeMeasurementsSinceLastUpdate[i]);
        }
    }


    private void LateUpdate()
    {
        GetEyeData();

        SphereCast();
    }


    private void GetEyeData()
    {
        // Get gaze data if gaze is allowed and calibrated
        if (!CanWeUseGaze())
        {
            return;
        }

        if (!_inputDevice.isValid)
        {
            GetDevice();
        }

        m_gazeTarget.SetActive(true);

        if (m_gazeDataSource == GazeDataSource.InputSubsystem)
        {
            Tralla();
        }
        else
        {
            Lala();
        }
    }


    private void Tralla()
    {
        if (!_inputDevice.TryGetFeatureValue(CommonUsages.eyesData, out _eyes))
        {
            return;
        }

        if (m_setEyePosition) // SOSXR: We don't want to set the localPosition of the eyes if it is in a model
        {
            if (_eyes.TryGetLeftEyePosition(out _leftEyeTrackingPosition))
            {
                LeftEyeTransform.localPosition = _leftEyeTrackingPosition;
            }

            if (_eyes.TryGetRightEyePosition(out _rightEyeTrackingPosition))
            {
                RightEyeTransform.localPosition = _rightEyeTrackingPosition;
            }
        }


        if (_eyes.TryGetLeftEyeRotation(out _leftEyeTrackingRotation))
        {
            LeftEyeTransform.localRotation = _leftEyeTrackingRotation;

            LeftEyeTransform.localRotation *= Quaternion.Euler(m_leftEyeRotationOffset);
        }

        if (_eyes.TryGetRightEyeRotation(out _rightEyeTrackingRotation))
        {
            RightEyeTransform.localRotation = _rightEyeTrackingRotation;
            m_rightEyeTrackingRotation = _rightEyeTrackingRotation.eulerAngles;

            RightEyeTransform.localRotation *= Quaternion.Euler(m_rightEyeRotationOffset);
            m_rightEyeRotationWithOffset = RightEyeTransform.localRotation.eulerAngles;
        }

        if (_eyes.TryGetFixationPoint(out _fixationPoint))
        {
            if (m_fixationPointTransform != null)
            {
                m_fixationPointTransform.localPosition = _fixationPoint;
            }
        }


        _rayOrigin = _camera.transform.position;
        _direction = (m_fixationPointTransform.position - _camera.transform.position).normalized;
    }


    private void Lala()
    {
        _gazeData = GetGaze();

        if (_gazeData.status == GazeStatus.Invalid)
        {
            return;
        }

        // Set gaze origin as raycast origin
        _rayOrigin = _camera.transform.TransformPoint(_gazeData.gaze.origin);

        // Set gaze direction as raycast direction
        _direction = _camera.transform.TransformDirection(_gazeData.gaze.forward);

        // Fixation point can be calculated using ray origin, direction and focus distance
        m_fixationPointTransform.position = _rayOrigin + _direction * _gazeData.focusDistance;

        HandleLeftEyeBall();

        HandleRightEyeBall();
    }


    private void HandleRightEyeBall()
    {
        if (_gazeData.rightStatus == GazeEyeStatus.Invalid) // rightEyeTransform.position = xrCamera.transform.TransformPoint(gazeData.right.origin);
        {
            return;
        }

        RightEyeTransform.rotation = Quaternion.LookRotation(_camera.transform.TransformDirection(_gazeData.right.forward));

        RightEyeTransform.localRotation *= Quaternion.Euler(m_rightEyeRotationOffset);
    }


    private void HandleLeftEyeBall()
    {
        if (_gazeData.leftStatus == GazeEyeStatus.Invalid) // GazeRay vectors are relative to the HMD pose so they need to be transformed to world space
        {
            return;
        }

        LeftEyeTransform.rotation = Quaternion.LookRotation(_camera.transform.TransformDirection(_gazeData.left.forward));

        LeftEyeTransform.localRotation *= Quaternion.Euler(m_leftEyeRotationOffset);
    }


    private void SphereCast()
    {
        // Raycast to world from VR Camera position towards fixation point
        if (Physics.SphereCast(_rayOrigin, m_gazeRadius, _direction, out _hit))
        {
            // Put target on gaze raycast position with offset towards user
            m_gazeTarget.transform.position = _hit.point - _direction * m_targetOffset;

            // Make gaze target point towards user
            m_gazeTarget.transform.LookAt(_rayOrigin, Vector3.up);

            // Scale gazetarget with distance so it appears to be always same size
            _distance = _hit.distance;
            m_gazeTarget.transform.localScale = Vector3.one * _distance;
        }
        else
        {
            // If gaze ray didn't hit anything, the gaze target is shown at fixed distance
            m_gazeTarget.transform.position = _rayOrigin + _direction * m_floatingGazeTargetDistance;
            m_gazeTarget.transform.LookAt(_rayOrigin, Vector3.up);
            m_gazeTarget.transform.localScale = Vector3.one * m_floatingGazeTargetDistance;
        }
    }
}