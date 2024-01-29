using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.Serialization;


public class Obey : MonoBehaviour
{
    private CarTrafficLight m_carTrafficLight;
    private SpeedSettings _speedSettings;
    private BoxCollider _boxCollider;
    private SpeedSettings[] _allSpeedSettings;
    [SerializeField] [Range(1,50)] private float m_colliderLength = 20f;
    [SerializeField] [Range(-5,-1)] private float m_deceleration = -3f;
    [SerializeField] [Range(1,5)] private float m_acceleration = 2f;
    [SerializeField] [Range(5,50)] private float m_speedWhenGoing = 30f;
    [SerializeField] private bool m_obey = true;

    [SerializeField] private UnityEvent m_eventOnRunningRedLight;

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
                Debug.Log("One other SpeedSetting component was in my way, I disabled their GameObject completely.");
            }
        }
    }


    private void Update()
    {
        if (!m_obey)
        {
            Debug.Log("We're not listening to this traffic light here");
            return;
        }
        
        if (m_carTrafficLight.State == LightState.RED)
        {
            _speedSettings.speed = 0f;
            _speedSettings.acceleration = m_deceleration;
            Debug.Log("Boem is ho");
        }
        else if (m_carTrafficLight.State == LightState.GREEN)
        {
            _speedSettings.speed = m_speedWhenGoing;
            _speedSettings.acceleration = m_acceleration;
            Debug.Log("Green is go");
        }
        else 
        {
             Debug.Log("These light colours are not yet implemented");
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

        if (m_carTrafficLight.State != LightState.RED)
        {
            return;
        }
        
        m_eventOnRunningRedLight?.Invoke();
        Debug.Log("We ran a red light and now are firing this event. This is where you'd hook up the audio events for instance");
    }
}
