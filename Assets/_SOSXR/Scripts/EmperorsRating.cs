// Enumeration to specify the handedness of the controller used for the rating

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using Random = UnityEngine.Random;


public enum Handedness
{
    Right,
    Left
}


[Serializable]
public struct Rating
{
    [Header("Don't edit these manually")]
    public string DateAndTime;
    public Handedness RatingHand;
    public Quaternion StartRotationQuaternion;
    public Vector3 StartRotation;
    public Quaternion EndRotationQuaternion;
    public Vector3 EndRotation;
}


/// <summary>
///     The emperor's rating handles the input from an XR controller allows a participant to rate something with a value
///     going from 0 (excellent), to 180 (not so excellent) via rotation and button presses.
///     It's called the emperor's rating due to the semblance of thumbs-up / thumbs-down in the Arena.
///     The class handles setting a haptic reminder every X seconds, so that participants don't forget to provide a rating.
///     Then they press and hold a button (suggested is to use the trigger), while rotating the controller around the
///     length-wise axis.
///     Testing should indicate whether this axis is the one desired, or that another axis should be measured instead.
/// </summary>
public class EmperorsRating : MonoBehaviour
{
    [Tooltip("With what hand should we do the rating?")]
    public Handedness RatingHand = Handedness.Right;

    [Header("Input References: Left")]
    [SerializeField] private XRBaseController m_leftController;
    [SerializeField] private InputActionReference m_leftButtonPressRef;
    [SerializeField] private InputActionReference m_leftButtonReleaseRef;
    [SerializeField] private InputActionReference m_leftRotateRef;

    [Header("Input References: Right")]
    [SerializeField] private XRBaseController m_rightController;
    [SerializeField] private InputActionReference m_rightButtonPressRef;
    [SerializeField] private InputActionReference m_rightButtonReleaseRef;
    [SerializeField] private InputActionReference m_rightRotateRef;

    [Header("Reminder Haptics")]
    [Tooltip("Seconds between haptic reminders.")]
    [SerializeField] [Range(10, 120)] private int m_reminderHapticsInterval = 30;
    [Tooltip("Duration of the haptic reminder pulse.")]
    [SerializeField] [Range(0.1f, 5f)] private float m_reminderHapticsDuration = 2.5f;
    [Tooltip("Intensity of the haptic reminder pulse.")]
    [SerializeField] [Range(0.1f, 2f)] private float m_reminderHapticsIntensity = 0.5f;

    [Header("Active Haptics")]
    [Tooltip("Seconds between active haptic feedback.")]
    [SerializeField] [Range(0.1f, 1f)] private float m_activeHapticsInterval = 0.5f;
    [Tooltip("Duration of the active haptic pulse.")]
    [SerializeField] [Range(0.1f, 2.5f)] private float m_activeHapticsDuration = 0.15f;
    [Tooltip("Intensity of the active haptic pulse.")]
    [SerializeField] [Range(0.1f, 2f)] private float m_activeHapticsIntensity = 0.25f;

    [Header("Rotation measurement settings")]
    [Tooltip("Interval in seconds for measuring the controller rotation.")]
    [SerializeField] [Range(0.001f, 0.5f)] private float m_rotationMeasureInterval = 0.01f;

    [Header("These fields store runtime values and are not meant to be edited directly.")]
    [SerializeField] private List<Rating> _ratingValues = new();

    [Header("Debug: make up random measurements instead of controller rotations")]
    [SerializeField] private bool m_debug = true;

    private Rating _currentRating = new();

    private XRBaseController _controller;

    private InputAction _buttonPressAction;
    private InputAction _buttonReleaseAction;
    private InputAction _rotationAction;

    private Coroutine _hapticsReminderCR;
    private Coroutine _hapticsActiveCR;
    private Coroutine _measureRotationCR;

    private const string dateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public List<Rating> RatingValues => _ratingValues;


    private void Awake()
    {
        Setup();
    }


    [ContextMenu(nameof(Setup))]
    private void Setup()
    {
        _ratingValues = new List<Rating>();


        if (RatingHand == Handedness.Right) // Selects the characteristics based on the hand chosen.
        {
            _buttonPressAction = m_rightButtonPressRef.action;
            _buttonReleaseAction = m_rightButtonReleaseRef.action;
            _rotationAction = m_rightRotateRef.action;
            _controller = m_rightController;
        }
        else if (RatingHand == Handedness.Left)
        {
            _buttonPressAction = m_leftButtonPressRef.action;
            _buttonReleaseAction = m_leftButtonReleaseRef.action;
            _rotationAction = m_leftRotateRef.action;
            _controller = m_leftController;
        }
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

        HapticTest();
    }


    [ContextMenu(nameof(HapticTest))]
    private void HapticTest()
    {
        _controller.SendHapticImpulse(1, 1);
    }


    /// <summary>
    ///     Coroutine to handle repeated sending of haptic impulses at specified intervals.
    /// </summary>
    private IEnumerator HapticsCR(float interval, float amplitude, float duration)
    {
        for (;;)
        {
            yield return new WaitForSeconds(interval);
            _controller.SendHapticImpulse(amplitude, duration);
        }
    }


    /// <summary>
    ///     Is fired when the button (trigger) is pressed down, starting the rotation measurement and the coroutine for the
    ///     'active haptics'.
    /// </summary>
    private void ButtonPressed(InputAction.CallbackContext context)
    {
        ButtonPressed();
    }


    [ContextMenu(nameof(ButtonPressed))]
    private void ButtonPressed()
    {
        Debug.Log("You pressed the button. Well done.");

        if (_measureRotationCR != null)
        {
            Debug.LogWarning("The measure CR wasn't null when we wanted to start a new measurement, this doesn't seem to be ok?");
            StopCoroutine(_measureRotationCR);
            _measureRotationCR = null;
        }

        _measureRotationCR = StartCoroutine(MeasureRotationCR());

        if (_hapticsActiveCR != null)
        {
            Debug.LogWarning("The active haptics coroutine wasn't null when we pressed the (trigger) button anew, this doesn't seem to be ok?");
            StopCoroutine(_hapticsActiveCR);
            _hapticsActiveCR = null;
        }

        _hapticsActiveCR = StartCoroutine(HapticsCR(m_activeHapticsInterval, m_activeHapticsIntensity, m_activeHapticsDuration));
    }


    /// <summary>
    ///     Coroutine to continuously measure the rotation of the controller.
    /// </summary>
    private IEnumerator MeasureRotationCR()
    {
        var waitSeconds = new WaitForSeconds(m_rotationMeasureInterval);

        _currentRating = new Rating();

        _currentRating.DateAndTime = DateTime.Now.ToString(dateTimeFormat);
        _currentRating.RatingHand = RatingHand;

        _currentRating.StartRotationQuaternion = m_debug ? GetRandomQuaternion() : _rotationAction.ReadValue<Quaternion>();

        for (;;)
        {
            _currentRating.EndRotationQuaternion = m_debug ? GetRandomQuaternion() : _rotationAction.ReadValue<Quaternion>();

            yield return waitSeconds;
        }
    }


    /// <summary>
    ///     For debug purposes
    /// </summary>
    /// <returns></returns>
    private Quaternion GetRandomQuaternion()
    {
        return Quaternion.Euler(Random.Range(0f, 360), Random.Range(0f, 360), Random.Range(0f, 360));
    }


    /// <summary>
    ///     Handles the button release event, stopping the rotation measurement and the 'active haptics'.
    /// </summary>
    private void ButtonReleased(InputAction.CallbackContext context)
    {
        ButtonReleased();
    }


    [ContextMenu(nameof(ButtonReleased))]
    private void ButtonReleased()
    {
        Debug.Log("Released the button");

        if (_hapticsActiveCR != null)
        {
            StopCoroutine(_hapticsActiveCR);
            _hapticsActiveCR = null;
        }
        else
        {
            Debug.LogWarning("The active haptics were already turned off when I released the button, that doesn't seem right.");
        }

        if (_measureRotationCR != null)
        {
            StopCoroutine(_measureRotationCR);
            _measureRotationCR = null;
        }
        else
        {
            Debug.LogWarning("The measurement coroutine had already been stopped prior to our call to stop it (on the release of the trigger button), this doesn't seem correct");
        }

        RecalculateRotation();
    }


    /// <summary>
    ///     Recalculates Quaternion to more acceptable Vector3 formats.
    ///     Recalculates the rotation of the controller to maintain it within a 0-180 range.
    /// </summary>
    private void RecalculateRotation()
    {
        _currentRating.StartRotation = _currentRating.StartRotationQuaternion.eulerAngles;
        _currentRating.EndRotation = _currentRating.EndRotationQuaternion.eulerAngles;

        StoreRotationValues();
    }


    /// <summary>
    ///     Stores the recalculated rotation value for further analysis.
    /// </summary>
    private void StoreRotationValues()
    {
        _ratingValues.Add(_currentRating);
    }


    /// <summary>
    ///     Disables the input actions and stops all coroutines when the object is disabled.
    /// </summary>
    private void OnDisable()
    {
        _buttonPressAction.performed -= ButtonPressed;
        _buttonReleaseAction.performed -= ButtonReleased;

        _buttonPressAction.Disable();
        _buttonReleaseAction.Disable();
        _rotationAction.Disable();

        StopAllCoroutines();
        _hapticsActiveCR = null;
        _measureRotationCR = null;
        _hapticsReminderCR = null;
    }
}