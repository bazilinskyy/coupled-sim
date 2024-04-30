using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;
using static Varjo.XR.VarjoEyeTracking;


public enum GazeDataSource
{
    InputSubsystem,
    GazeAPI
}


public class EyeTrackingExample : MonoBehaviour
{
    
    [Header("Debug Gaze")]
    [SerializeField] private KeyCode m_checkGazeAllowed = KeyCode.PageUp;
    [SerializeField] private KeyCode m_checkGazeCalibrated = KeyCode.PageDown;
    
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

    
    public Transform LeftEyeTransform;
    [SerializeField] private Vector3 m_leftEyeRotationOffset = new(0, 0, 84.354f); // SOSXR
    public Transform RightEyeTransform;

    [SerializeField] private Vector3 m_VarjoEye;
    [SerializeField] private Vector3 m_rightEyeRotationOffset = new(0, 0, 84.354f); // SOSXR

    [SerializeField] private Vector3 m_offsettedEye;

    [Tooltip("SOSXR: We don't want to set the localPosition of the eyes if it is in a model")]
    [SerializeField] private bool m_setEyePosition = false; // SOSXR

    [Tooltip("XR camera")]
    [SerializeField] private Camera m_XRCamera;

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
    [SerializeField] private bool m_logFramerate = false;

    private static readonly string[] _columnNames = {"Frame", "CaptureTime", "LogTime", "HMDPosition", "HMDRotation", "GazeStatus", "CombinedGazeForward", "CombinedGazePosition", "InterPupillaryDistanceInMM", "LeftEyeStatus", "LeftEyeForward", "LeftEyePosition", "LeftPupilIrisDiameterRatio", "LeftPupilDiameterInMM", "LeftIrisDiameterInMM", "RightEyeStatus", "RightEyeForward", "RightEyePosition", "RightPupilIrisDiameterRatio", "RightPupilDiameterInMM", "RightIrisDiameterInMM", "FocusDistance", "FocusStability"};

    private readonly List<InputDevice> _devices = new();
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
    private Vector3 _leftEyePosition;
    private Quaternion _leftEyeRotation;
    private bool _logging = false;
    private Vector3 _rayOrigin;
    private Vector3 _rightEyePosition;
    private Quaternion _rightEyeRotation;
    private StreamWriter _streamWriter = null;
    private const string _ValidString = "VALID";
    private const string _InvalidString = "INVALID";


    private void GetDevice()
    {
        InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, _devices);
        _inputDevice = _devices.FirstOrDefault();
    }


    private void OnEnable()
    {
        if (!_inputDevice.isValid)
        {
            GetDevice();
        }
    }


    private void Start()
    {
        SetGazeOutputFrequency(m_frequency);

        //Hiding the gazetarget if gaze is not available or if the gaze calibration is not done
        if (IsGazeAllowed() && IsGazeCalibrated())
        {
            m_gazeTarget.SetActive(true);
        }
        else
        {
            m_gazeTarget.SetActive(false);
        }

        if (m_fixationPointTransform != null)
        {
            if (m_showFixationPoint)
            {
                m_fixationPointTransform.gameObject.SetActive(true);
            }
            else
            {
                m_fixationPointTransform.gameObject.SetActive(false);
            }
        }
    }


    private void Update()
    {
        if (_logging && m_logFramerate)
        {
            _gazeTimer += Time.deltaTime;

            if (_gazeTimer >= 1.0f)
            {
                Debug.Log("Gaze data rows per second: " + _gazeDataCount);
                _gazeDataCount = 0;
                _gazeTimer = 0f;
            }
        }

        // Request gaze calibration
        if (Input.GetKeyDown(m_calibrationRequestKey))
        {
            RequestGazeCalibration(m_gazeCalibrationMode);
        }

        // Set output filter type
        if (Input.GetKeyDown(m_setOutputFilterTypeKey))
        {
            SetGazeOutputFilterType(m_gazeOutputFilterType);
            Debug.Log("Gaze output filter type is now: " + GetGazeOutputFilterType());
        }

        // Check if gaze is allowed
        if (Input.GetKeyDown(m_checkGazeAllowed))
        {
            Debug.Log("Gaze allowed: " + IsGazeAllowed());
        }

        // Check if gaze is calibrated
        if (Input.GetKeyDown(m_checkGazeCalibrated))
        {
            Debug.Log("Gaze calibrated: " + IsGazeCalibrated());
        }

        // Toggle gaze target visibility
        if (Input.GetKeyDown(m_toggleGazeTarget))
        {
            m_gazeTarget.GetComponentInChildren<MeshRenderer>().enabled = !m_gazeTarget.GetComponentInChildren<MeshRenderer>().enabled;
        }

        // GetEyeData(); Moved to LateUpdate

        // SphereCast(); Moved to LateUpdate

        if (Input.GetKeyDown(m_loggingToggleKey))
        {
            if (!_logging)
            {
                StartLogging();
            }
            else
            {
                StopLogging();
            }

            return;
        }

        if (_logging)
        {
            var dataCount = GetGazeList(out _dataSinceLastUpdate, out _eyeMeasurementsSinceLastUpdate);

            if (m_logFramerate)
            {
                _gazeDataCount += dataCount;
            }

            for (var i = 0; i < dataCount; i++)
            {
                LogGazeData(_dataSinceLastUpdate[i], _eyeMeasurementsSinceLastUpdate[i]);
            }
        }

        if (Input.GetKeyDown(KeyCode.Keypad9))
        {
            Debug.Log(m_VarjoEye);
            Debug.Log(m_offsettedEye);
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
        if (IsGazeAllowed() && IsGazeCalibrated())
        {
            //Get device if not valid
            if (!_inputDevice.isValid)
            {
                GetDevice();
            }

            // Show gaze target
            m_gazeTarget.SetActive(true);

            if (m_gazeDataSource == GazeDataSource.InputSubsystem)
            {
                // Get data for eye positions, rotations and the fixation point
                if (_inputDevice.TryGetFeatureValue(CommonUsages.eyesData, out _eyes))
                {
                    if (m_setEyePosition) // SOSXR: We don't want to set the localPosition of the eyes if it is in a model
                    {
                        if (_eyes.TryGetLeftEyePosition(out _leftEyePosition))
                        {
                            LeftEyeTransform.localPosition = _leftEyePosition;
                        }

                        if (_eyes.TryGetRightEyePosition(out _rightEyePosition))
                        {
                            RightEyeTransform.localPosition = _rightEyePosition;
                        }
                    }


                    if (_eyes.TryGetLeftEyeRotation(out _leftEyeRotation))
                    {
                        LeftEyeTransform.localRotation = _leftEyeRotation;
                        var offset = Quaternion.Euler(m_leftEyeRotationOffset);
                        LeftEyeTransform.localRotation *= offset;
                    }

                    if (_eyes.TryGetRightEyeRotation(out _rightEyeRotation))
                    {
                        RightEyeTransform.localRotation = _rightEyeRotation;
                        m_VarjoEye = _rightEyeRotation.eulerAngles;
                        var offset = Quaternion.Euler(m_rightEyeRotationOffset);
                        RightEyeTransform.localRotation *= offset;
                        m_offsettedEye = RightEyeTransform.localRotation.eulerAngles;
                        // Debug.log(rightEyeTransform.localRotation.eulerAngles); // print eyerotation value after offset
                    }

                    if (_eyes.TryGetFixationPoint(out _fixationPoint))
                    {
                        if (m_fixationPointTransform != null)
                        {
                            m_fixationPointTransform.localPosition = _fixationPoint;
                        }
                    }
                }

                // Set raycast origin point to VR camera position
                _rayOrigin = m_XRCamera.transform.position;

                // Direction from VR camera towards fixation point
                _direction = (m_fixationPointTransform.position - m_XRCamera.transform.position).normalized;
            }
            else
            {
                _gazeData = GetGaze();

                if (_gazeData.status != GazeStatus.Invalid)
                {
                    // GazeRay vectors are relative to the HMD pose so they need to be transformed to world space
                    if (_gazeData.leftStatus != GazeEyeStatus.Invalid)
                    {
                        // leftEyeTransform.position = xrCamera.transform.TransformPoint(gazeData.left.origin);
                        LeftEyeTransform.rotation = Quaternion.LookRotation(m_XRCamera.transform.TransformDirection(_gazeData.left.forward));
                        var offset = Quaternion.Euler(m_leftEyeRotationOffset);
                        LeftEyeTransform.localRotation *= offset;
                    }

                    if (_gazeData.rightStatus != GazeEyeStatus.Invalid)
                    {
                        // rightEyeTransform.position = xrCamera.transform.TransformPoint(gazeData.right.origin);
                        RightEyeTransform.rotation = Quaternion.LookRotation(m_XRCamera.transform.TransformDirection(_gazeData.right.forward));
                        var offset = Quaternion.Euler(m_rightEyeRotationOffset);
                        RightEyeTransform.localRotation *= offset;
                    }

                    // Set gaze origin as raycast origin
                    _rayOrigin = m_XRCamera.transform.TransformPoint(_gazeData.gaze.origin);

                    // Set gaze direction as raycast direction
                    _direction = m_XRCamera.transform.TransformDirection(_gazeData.gaze.forward);

                    // Fixation point can be calculated using ray origin, direction and focus distance
                    m_fixationPointTransform.position = _rayOrigin + _direction * _gazeData.focusDistance;
                }
            }
        }
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

            // Prefer layers or tags to identify looked objects in your application
            // This is done here using GetComponent for the sake of clarity as an example
            var rotateWithGaze = _hit.collider.gameObject.GetComponent<RotateWithGaze>();

            if (rotateWithGaze != null)
            {
                rotateWithGaze.RayHit();
            }
        }
        else
        {
            // If gaze ray didn't hit anything, the gaze target is shown at fixed distance
            m_gazeTarget.transform.position = _rayOrigin + _direction * m_floatingGazeTargetDistance;
            m_gazeTarget.transform.LookAt(_rayOrigin, Vector3.up);
            m_gazeTarget.transform.localScale = Vector3.one * m_floatingGazeTargetDistance;
        }
    }


    private void LogGazeData(GazeData data, EyeMeasurements eyeMeasurements)
    {
        var logData = new string[23];

        // Gaze data frame number
        logData[0] = data.frameNumber.ToString();

        // Gaze data capture time (nanoseconds)
        logData[1] = data.captureTime.ToString();

        // Log time (milliseconds)
        logData[2] = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString();

        // HMD
        logData[3] = m_XRCamera.transform.localPosition.ToString("F3");
        logData[4] = m_XRCamera.transform.localRotation.ToString("F3");

        // Combined gaze
        var invalid = data.status == GazeStatus.Invalid;
        logData[5] = invalid ? _InvalidString : _ValidString;
        logData[6] = invalid ? "" : data.gaze.forward.ToString("F3");
        logData[7] = invalid ? "" : data.gaze.origin.ToString("F3");

        // IPD
        logData[8] = invalid ? "" : eyeMeasurements.interPupillaryDistanceInMM.ToString("F3");

        // Left eye
        var leftInvalid = data.leftStatus == GazeEyeStatus.Invalid;
        logData[9] = leftInvalid ? _InvalidString : _ValidString;
        logData[10] = leftInvalid ? "" : data.left.forward.ToString("F3");
        logData[11] = leftInvalid ? "" : data.left.origin.ToString("F3");
        logData[12] = leftInvalid ? "" : eyeMeasurements.leftPupilIrisDiameterRatio.ToString("F3");
        logData[13] = leftInvalid ? "" : eyeMeasurements.leftPupilDiameterInMM.ToString("F3");
        logData[14] = leftInvalid ? "" : eyeMeasurements.leftIrisDiameterInMM.ToString("F3");

        // Right eye
        var rightInvalid = data.rightStatus == GazeEyeStatus.Invalid;
        logData[15] = rightInvalid ? _InvalidString : _ValidString;
        logData[16] = rightInvalid ? "" : data.right.forward.ToString("F3");
        logData[17] = rightInvalid ? "" : data.right.origin.ToString("F3");
        logData[18] = rightInvalid ? "" : eyeMeasurements.rightPupilIrisDiameterRatio.ToString("F3");
        logData[19] = rightInvalid ? "" : eyeMeasurements.rightPupilDiameterInMM.ToString("F3");
        logData[20] = rightInvalid ? "" : eyeMeasurements.rightIrisDiameterInMM.ToString("F3");

        // Focus
        logData[21] = invalid ? "" : data.focusDistance.ToString();
        logData[22] = invalid ? "" : data.focusStability.ToString();

        Log(logData);
    }


    // Write given values in the log file
    private void Log(string[] values)
    {
        if (!_logging || _streamWriter == null)
        {
            return;
        }

        var line = "";

        for (var i = 0; i < values.Length; ++i)
        {
            values[i] = values[i].Replace("\r", "").Replace("\n", ""); // Remove new lines so they don't break csv
            line += values[i] + (i == values.Length - 1 ? "" : ";"); // Do not add semicolon to last data string
        }

        _streamWriter.WriteLine(line);
    }


    public void StartLogging()
    {
        if (_logging)
        {
            Debug.LogWarning("Logging was on when StartLogging was called. No new log was started.");

            return;
        }

        _logging = true;

        var logPath = m_useCustomLogPath ? m_customLogPath : Application.dataPath + "/Logs/";
        Directory.CreateDirectory(logPath);

        var now = DateTime.Now;
        var fileName = $"{now.Year}-{now.Month:00}-{now.Day:00}-{now.Hour:00}-{now.Minute:00}";

        var path = logPath + fileName + ".csv";
        _streamWriter = new StreamWriter(path);

        Log(_columnNames);
        Debug.Log("Log file started at: " + path);
    }


    private void StopLogging()
    {
        if (!_logging)
        {
            return;
        }

        if (_streamWriter != null)
        {
            _streamWriter.Flush();
            _streamWriter.Close();
            _streamWriter = null;
        }

        _logging = false;
        Debug.Log("Logging ended");
    }


    private void OnApplicationQuit()
    {
        StopLogging();
    }
}