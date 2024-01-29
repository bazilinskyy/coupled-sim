using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// This component needs to live on its own GameObject as a child to each Car traffic light.
/// It asks the traffic light it's light state (red/green), and modifies it's own created SpeedSettings component.
/// It spawns a pretty BoxCollider. The car, when inside this box, picks up the SpeedSettings setting in this BoxCollider.
/// For this to work, the AICar needed to have their checking done in OnTriggerStay instead of OnTriggerEnter. Shouldn't matter too much.
/// This way, we can set the Speed to 0 and the acceleration to a negative number, when light == red. When green: pedal to the metal.
/// Disabling the obey-toggle disables this system, allowing the car to run a red light.
/// It will then fire an event. Hook this to the audio system for instance for a half-hearted apology by the (AI-) driver.
/// </summary>
public class Obey : MonoBehaviour
{
    [SerializeField] [Range(1,50)] private float m_colliderLength = 20f;
    [SerializeField] [Range(-5,-1)] private float m_deceleration = -3f;
    [SerializeField] [Range(1,5)] private float m_acceleration = 2f;
    [SerializeField] [Range(5,50)] private float m_speedWhenGoing = 30f;
    [SerializeField] private bool m_obey = true;

    [SerializeField] private UnityEvent m_eventOnRunningRedLight;

    private CarTrafficLight m_carTrafficLight;
    private SpeedSettings _speedSettings;
    private BoxCollider _boxCollider;
    private SpeedSettings[] _allSpeedSettings;
    
    
    private void Awake()
    {
        gameObject.tag = "WP";
        
        m_carTrafficLight = GetComponentInParent<CarTrafficLight>();
        
        _speedSettings = gameObject.AddComponent<SpeedSettings>();
        _speedSettings.speed = 30; 
        _speedSettings.customBehaviourData = Array.Empty<CustomBehaviourData>();
       
       _boxCollider = gameObject.AddComponent<BoxCollider>();
       _boxCollider.size = new Vector3(m_colliderLength, 2f, 2f);
       _boxCollider.center = new Vector3(m_colliderLength/3, -2, 0);
       _boxCollider.isTrigger = true;

       _allSpeedSettings = FindObjectsOfType<SpeedSettings>();
    }


    private void Start()
    {
        DisableConflictingSpeedSetters();
    }


    private void DisableConflictingSpeedSetters()
    {
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
        else
        {
            // This is where you might wanna put other states as well. 
        }
    }


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


    private void OnTriggerExit(Collider other)
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

        if (m_obey)
        {
            return;
        }

        if (m_carTrafficLight.State == LightState.RED)
        {
            m_eventOnRunningRedLight?.Invoke();
            Debug.Log("We ran a red light and now are firing this UnityEvent. Pick this up by any other component. E.g.: this is where you'd hook up the audio system ('SORRY NOT SORRY!') for instance");
        }
        else 
        {
            Debug.Log("This could also be a place to add multiple functions for other light states. Like what to do when light was Yellow? Is not a traffic violation, but might be something interesting anyway.");
        }
    }
}
