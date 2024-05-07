using System.Collections;
using System.Collections.Generic;
using SOSXR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using static UnityEngine.InputSystem.InputAction;
using static UnityEngine.XR.InputDevices;
using InputDevice = UnityEngine.XR.InputDevice;


public class TrustRater : MonoBehaviour
{
    [Header("Handedness && Input Action References")]
    [Tooltip("Select either Left or Right")]
    [SerializeField] private InputDeviceCharacteristics m_handedness;
    [Tooltip("Would be handy if you actually use these references on the same chirality as we have the handedness")]
    [SerializeField] private InputActionReference m_buttonPressRef;
    [SerializeField] private InputActionReference m_buttonReleaseRef;
    [SerializeField] private InputActionReference m_rotateRef;

    [Header("Reminder Haptics")]
    [Tooltip("Seconds")]
    [SerializeField] [Range(10, 120)] private int m_reminderHapticsInterval = 30;
    [Tooltip("Seconds")]
    [SerializeField] [Range(0.1f, 2f)] private float m_reminderHapticsDuration = 2.5f;
    [Tooltip("Amplitude")]
    [SerializeField] [Range(0.1f, 2f)] private float m_reminderHapticsIntensity = 0.5f;

    [Header("Active Haptics")]
    [Tooltip("Seconds")]
    [SerializeField] [Range(0.1f, 1f)] private float m_activeHapticsInterval = 0.5f;
    [Tooltip("Seconds")]
    [SerializeField] [Range(0.1f, 2f)] private float m_activeHapticsDuration = 0.15f;
    [Tooltip("Amplitude")]
    [SerializeField] [Range(0.1f, 2f)] private float m_activeHapticsIntensity = 0.25f;

    [Header("Active Values")]
    [SerializeField] [DisableEditing] private bool _measureRotation;
    [SerializeField] [DisableEditing] private Vector3 _rotation;
    [SerializeField] [DisableEditing] private float _zRot;

    [Header("Storing / Stored Values")]
    [SerializeField] [DisableEditing] private bool _valueHasBeenStored;
    [SerializeField] [DisableEditing] private List<float> _storedValues = new();

    private readonly List<InputDevice> _devices = new();
    private Coroutine _hapticsReminder;
    private Coroutine _hapticsActive;


    private void OnValidate()
    {
        if (m_handedness is InputDeviceCharacteristics.Left or InputDeviceCharacteristics.Right)
        {
            // This is what it should be. So no action needed here. 
        }
        else if (m_handedness == InputDeviceCharacteristics.None)
        {
            Debug.Log("Handedness needs to be something, cannot be None. Needs to be Left XOR Right.");
        }
        else
        {
            Debug.LogWarning("Handedness needs to be either Left XOR Right. Not both, and not something else");
        }
    }


    private void Start()
    {
        var characteristics = InputDeviceCharacteristics.Controller & m_handedness; // Is this correct? https://docs.unity3d.com/ScriptReference/XR.InputDevices.GetDevicesWithCharacteristics.html
        GetDevicesWithCharacteristics(characteristics, _devices);
        Debug.LogFormat("We now have {0} devices that match the characteristics", _devices.Count);

        if (_hapticsReminder != null)
        {
            StopCoroutine(_hapticsReminder);
        }

        _hapticsReminder = StartCoroutine(HapticsCR(m_reminderHapticsInterval, m_reminderHapticsIntensity, m_reminderHapticsDuration));
    }


    private void OnEnable()
    {
        m_buttonPressRef.action.performed += ButtonPressed;
        m_buttonReleaseRef.action.performed += ButtonReleased;

        m_buttonPressRef.action.Enable();
        m_buttonReleaseRef.action.Enable();
        m_rotateRef.action.Enable();
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
        _measureRotation = true;

        if (_hapticsActive != null)
        {
            StopCoroutine(_hapticsActive);
        }

        _hapticsActive = StartCoroutine(HapticsCR(m_activeHapticsInterval, m_activeHapticsIntensity, m_activeHapticsDuration));

        Debug.Log("Pressed the button");
    }


    private void Update()
    {
        if (_measureRotation)
        {
            _rotation = m_rotateRef.action.ReadValue<Quaternion>().eulerAngles;

            _zRot = _rotation.z;

            _valueHasBeenStored = false;
        }
        else
        {
            if (_valueHasBeenStored)
            {
                return;
            }

            var temp = _zRot;

            if (temp > 180)
            {
                temp = 360 - temp;
                Debug.LogFormat("Remapped from {0} to {1}", _zRot, temp);
            }

            _zRot = temp;

            if (_zRot is < 0 or > 180)
            {
                Debug.LogWarningFormat("Measured rotation is {0}, which is outside of the expected range. Something seems wrong?", _zRot);
            }

            _storedValues.Add(_zRot);
            Debug.LogFormat("Stored value is {0}. We can send all kinds of actions and events from here", _storedValues[^1]);

            _rotation = Vector3.zero; // Resetting values
            _zRot = 0f;

            _valueHasBeenStored = true; // Making sure this bit only happens once every time we store a value
        }
    }


    private void ButtonReleased(CallbackContext context)
    {
        Debug.Log("Released the button");
        _measureRotation = false;

        if (_hapticsActive != null)
        {
            StopCoroutine(_hapticsActive);
        }
        else
        {
            Debug.LogWarning("The active haptics were already turned off when I released the button, that doesn't seem right.");
        }
    }


    private void OnDisable()
    {
        m_buttonPressRef.action.performed -= ButtonPressed;
        m_buttonReleaseRef.action.performed -= ButtonReleased;

        m_buttonPressRef.action.Disable();
        m_buttonReleaseRef.action.Disable();

        m_rotateRef.action.Disable();

        StopAllCoroutines();
    }
}