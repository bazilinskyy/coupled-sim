using System.Collections;
using System.Collections.Generic;
using SOSXR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using static UnityEngine.InputSystem.InputAction;
using static UnityEngine.XR.InputDevices;
using InputDevice = UnityEngine.XR.InputDevice;


public enum Handedness
{
    Right,
    Left
}


public class TrustRater : MonoBehaviour
{
    [Tooltip("With what hand should we do the rating?")]
    public Handedness RatingHand = Handedness.Right;

    [Header("Input References: Left")]
    [SerializeField] private InputActionReference m_leftButtonPressRef;
    [SerializeField] private InputActionReference m_leftButtonReleaseRef;
    [SerializeField] private InputActionReference m_leftRotateRef;

    [Header("Input References: Right")]
    [SerializeField] private InputActionReference m_rightButtonPressRef;
    [SerializeField] private InputActionReference m_rightButtonReleaseRef;
    [SerializeField] private InputActionReference m_rightRotateRef;

    [Header("Reminder Haptics")]
    [Tooltip("Seconds")]
    [SerializeField] [Range(10, 120)] private int m_reminderHapticsInterval = 30;
    [Tooltip("Seconds")]
    [SerializeField] [Range(0.1f, 5f)] private float m_reminderHapticsDuration = 2.5f;
    [Tooltip("Amplitude")]
    [SerializeField] [Range(0.1f, 2f)] private float m_reminderHapticsIntensity = 0.5f;

    [Header("Active Haptics")]
    [Tooltip("Seconds")]
    [SerializeField] [Range(0.1f, 1f)] private float m_activeHapticsInterval = 0.5f;
    [Tooltip("Seconds")]
    [SerializeField] [Range(0.1f, 2.5f)] private float m_activeHapticsDuration = 0.15f;
    [Tooltip("Amplitude")]
    [SerializeField] [Range(0.1f, 2f)] private float m_activeHapticsIntensity = 0.25f;

    [Header("Active Values")]
    [SerializeField] [DisableEditing] private Vector3 _rotation;
    [SerializeField] [DisableEditing] private float _rawZRotation;

    [Header("Storing / Stored Values")]
    [Tooltip("We're interested in going from 0-180. We do this by taking 360-raw rotation if that raw rotation was more than 180 to begin with.")]
    [SerializeField] [DisableEditing] private float _recalculatedZRotation;
    [SerializeField] [DisableEditing] private List<float> _allStoredValues = new();

    private readonly List<InputDevice> _devices = new();

    private InputAction _buttonPressAction;
    private InputAction _buttonReleaseAction;
    private InputAction _rotationAction;

    private Coroutine _hapticsReminderCR;
    private Coroutine _hapticsActiveCR;
    private Coroutine _measureRotationCR;


    private void Awake()
    {
        var characteristics = InputDeviceCharacteristics.Controller;

        if (RatingHand == Handedness.Right)
        {
            _buttonPressAction = m_rightButtonPressRef.action;
            _buttonReleaseAction = m_rightButtonReleaseRef.action;
            _rotationAction = m_rightRotateRef.action;

            characteristics &= InputDeviceCharacteristics.Right; // Is this correct? https://docs.unity3d.com/ScriptReference/XR.InputDevices.GetDevicesWithCharacteristics.html
        }
        else if (RatingHand == Handedness.Left)
        {
            _buttonPressAction = m_leftButtonPressRef.action;
            _buttonReleaseAction = m_leftButtonReleaseRef.action;
            _rotationAction = m_leftRotateRef.action;

            characteristics &= InputDeviceCharacteristics.Left; // Is this correct? https://docs.unity3d.com/ScriptReference/XR.InputDevices.GetDevicesWithCharacteristics.html 
        }

        GetDevicesWithCharacteristics(characteristics, _devices);

        Debug.LogFormat("We now have {0} devices that match the characteristics", _devices.Count);
    }


    private void Start()
    {
        if (_hapticsReminderCR != null)
        {
            StopCoroutine(_hapticsReminderCR);
        }

        _hapticsReminderCR = StartCoroutine(HapticsCR(m_reminderHapticsInterval, m_reminderHapticsIntensity, m_reminderHapticsDuration));
    }


    private void OnEnable()
    {
        _buttonPressAction.performed += ButtonPressed;
        _buttonReleaseAction.performed += ButtonReleased;

        _buttonPressAction.Enable();
        _buttonReleaseAction.Enable();
        _rotationAction.Enable();
    }


    private IEnumerator HapticsCR(float interval, float amplitude, float duration)
    {
        for (;;)
        {
            yield return new WaitForSeconds(interval);

            SendHapticImpulse(amplitude, duration);
        }
    }


    public void SendHapticImpulse(float amplitude, float duration)
    {
        foreach (var device in _devices) // Is this correct? https://docs.unity3d.com/Manual/xr_input.html#Haptics
        {
            if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0, amplitude, duration);

                Debug.LogFormat("Send haptics to device {0}", device.name);
            }
        }
    }


    private void ButtonPressed(CallbackContext context)
    {
        Debug.Log("You pressed the button. Well done.");

        if (_measureRotationCR != null)
        {
            Debug.LogWarning("The measure CR wasn't null when we wanted to start a new measurement, this doesn't seem to be ok?");
            StopCoroutine(_measureRotationCR);
        }

        _measureRotationCR = StartCoroutine(MeasureRotationCR());

        if (_hapticsActiveCR != null)
        {
            Debug.LogWarning("The active haptics coroutine wasn't null when we pressed the (trigger) button anew, this doesn't seem to be ok?");
            StopCoroutine(_hapticsActiveCR);
        }

        _hapticsActiveCR = StartCoroutine(HapticsCR(m_activeHapticsInterval, m_activeHapticsIntensity, m_activeHapticsDuration));
    }


    private IEnumerator MeasureRotationCR()
    {
        _recalculatedZRotation = 0f;

        for (;;)
        {
            _rotation = _rotationAction.ReadValue<Quaternion>().eulerAngles;

            _rawZRotation = _rotation.z;

            yield return null;
        }
    }


    private void ButtonReleased(CallbackContext context)
    {
        Debug.Log("Released the button");

        if (_hapticsActiveCR != null)
        {
            StopCoroutine(_hapticsActiveCR);
        }
        else
        {
            Debug.LogWarning("The active haptics were already turned off when I released the button, that doesn't seem right.");
        }

        if (_measureRotationCR != null)
        {
            StopCoroutine(_measureRotationCR);
        }
        else
        {
            Debug.LogWarning("The measurement coroutine had already been stopped prior to our call to stop it (on the release of the trigger button, this doesn't seem correct");
        }

        RecalculateZRotation();
    }


    private void RecalculateZRotation()
    {
        var temp = _rawZRotation;

        if (temp > 180)
        {
            temp = 360 - temp;

            Debug.LogFormat("Recalculated from {0} to {1}", _recalculatedZRotation, temp);
        }

        _recalculatedZRotation = temp;

        CheckForIncorrectValues();
    }


    private void CheckForIncorrectValues()
    {
        if (_recalculatedZRotation is < 0 or > 180)
        {
            Debug.LogWarningFormat("Measured rotation is {0}, which is outside of the expected range. Something seems wrong?", _recalculatedZRotation);
        }

        StoreRotationValues();
    }


    private void StoreRotationValues()
    {
        _allStoredValues.Add(_recalculatedZRotation);

        Debug.LogFormat("Stored value is {0}. We can also send all kinds of actions and events from this part of the code", _allStoredValues[^1]);

        ResetValues();
    }


    private void ResetValues()
    {
        _rotation = Vector3.zero;
        _rawZRotation = 0;
    }


    private void OnDisable()
    {
        _buttonPressAction.performed -= ButtonPressed;
        _buttonReleaseAction.performed -= ButtonReleased;

        _buttonPressAction.Disable();
        _buttonReleaseAction.Disable();
        _rotationAction.Disable();

        StopAllCoroutines();
    }
}