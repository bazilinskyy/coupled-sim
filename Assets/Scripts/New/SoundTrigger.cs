using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundTrigger : MonoBehaviour
{
    public AudioSource _source;

    private void OnTriggerEnter(Collider other)
    {
        // Play the sound
        if (other.gameObject.CompareTag("AutonomousCar"))
        {
            _source.Play();
        }
    }
}
