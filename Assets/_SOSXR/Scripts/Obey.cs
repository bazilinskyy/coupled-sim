using System;
using UnityEngine;


/// <summary>
///     This component needs to live on its own GameObject as a child to each Car traffic light.
///     It asks the traffic light it's light state (red/green), and modifies it's own created SpeedSettings component.
///     It spawns a pretty BoxCollider. The car, when inside this box, picks up the SpeedSettings setting in this
///     BoxCollider.
///     For this to work, the AICar needed to have their checking done in OnTriggerStay instead of OnTriggerEnter.
///     Shouldn't matter too much.
///     This way, we can set the Speed to 0 and the acceleration to a negative number, when light == red. When green: pedal
///     to the metal.
///     Disabling the obey-toggle disables this system, allowing the car to run a red light.
///     It will then fire an event. Hook this to the audio system for instance for a half-hearted apology by the (AI-)
///     driver.
/// </summary>
public class Obey : MonoBehaviour
{
    [SerializeField] [Range(1, 5)] private float m_acceleration = Defaults.Acceleration;
    [SerializeField] [Range(5, 50)] private float m_speedWhenGoing = Defaults.Speed;
    [SerializeField] [Range(-5, -1)] private float m_deceleration = Defaults.Deceleration;
    [Space(10)]
    [SerializeField] private bool m_obey = true;

    public bool ObeyTrafficLight => m_obey;

    private readonly float _colliderLength = 20f;
    private SpeedSettings[] _allSpeedSettings;
    private BoxCollider _boxCollider;

    private LightState _previousLightState;
    private SpeedSettings _speedSettings;

    private CarTrafficLight m_carTrafficLight;

    public static Action RanRedLight;


    private void Awake()
    {
        gameObject.tag = "WP";

        m_carTrafficLight = GetComponentInParent<CarTrafficLight>();

        CreateNewSpeedSetter();
    }


    private void CreateNewSpeedSetter()
    {
        _speedSettings = gameObject.AddComponent<SpeedSettings>();
        _speedSettings.speed = m_speedWhenGoing;
        _speedSettings.CustomBehaviourData = Array.Empty<CustomBehaviourData>();

        _boxCollider = gameObject.AddComponent<BoxCollider>();
        _boxCollider.size = new Vector3(_colliderLength, 2f, 2f);
        _boxCollider.center = new Vector3(_colliderLength / 3, -2, 0);
        _boxCollider.isTrigger = true;
    }


    private void Start()
    {
        DisableConflictingSpeedSetters();
    }


    private void DisableConflictingSpeedSetters()
    {
        _allSpeedSettings = FindObjectsOfType<SpeedSettings>();

        foreach (var speedSettings in _allSpeedSettings)
        {
            if (speedSettings == _speedSettings)
            {
                continue;
            }

            var theirBox = speedSettings.GetComponent<BoxCollider>();

            if (theirBox.bounds.Intersects(_boxCollider.bounds))
            {
                theirBox.gameObject.SetActive(false);
                Debug.LogWarning("One other SpeedSetting component was in my way, I disabled their GameObject completely.");
                // This needed doing because otherwise that other speedsetter would interfere, and make the car accelerate for instance when it should stop for red light
            }
        }
    }


    private void Update()
    {
        if (!m_obey)
        {
            return;
        }

        if (_previousLightState == m_carTrafficLight.State)
        {
            return;
        }

        if (m_carTrafficLight.State == LightState.RED)
        {
            _speedSettings.speed = 0f;
            _speedSettings.acceleration = m_deceleration;
        }
        else if (m_carTrafficLight.State == LightState.GREEN)
        {
            _speedSettings.speed = m_speedWhenGoing;
            _speedSettings.acceleration = m_acceleration;
        }

        // This is where you might wanna put other states as well. 
        _previousLightState = m_carTrafficLight.State;
    }


    /// <summary>
    ///     Something entered my collider
    ///     Currently this is not doing anything too useful aside from letting us know as soon as the car enters and it is set
    ///     to !m_obey, that this is the case
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter(Collider other)
    {
        var aiCar = other.GetComponent<AICar>();

        if (aiCar == null)
        {
            return;
        }

        if (!aiCar.isActiveAndEnabled)
        {
            return;
        }

        if (!m_obey)
        {
            Debug.LogFormat("We're not listening to this traffic light here! We're bad bad drivers. The light is now {0}", m_carTrafficLight.State.ToString());
        }
    }


    /// <summary>
    ///     Something exited my collider
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerExit(Collider other)
    {
        var aiCar = other.GetComponent<AICar>(); // Do you have a Car component? / Are you a car? 

        if (aiCar == null) // If you're not a Car, don't continue this function
        {
            return;
        }

        if (!aiCar.isActiveAndEnabled) // If you ARE a Car, but somehow are disabled, don't continue this function
        {
            return;
        }

        if (!m_obey) // Only run the following code block if we're not planning on obeying this traffic light.
        {
            if (m_carTrafficLight.State == LightState.RED) // Is the light RED?
            {
                RanRedLight?.Invoke(); // Fire this event.
                Debug.Log("We ran a red light and now are firing this Action / Event. Pick this up by any other component. E.g.: this is where you'd hook up the audio system ('SORRY NOT SORRY!') for instance");
            }
            else // Is the light anything but red?
            {
                Debug.Log("This could also be a place to add multiple functions for other light states. Like what to do when light was Yellow? Is not a traffic violation, but might be something interesting anyway.");
            }
        }
    }
}