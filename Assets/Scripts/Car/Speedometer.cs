using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Speedometer : MonoBehaviour
{
    [SerializeField]
    private Text speedometerText;

    [SerializeField]
    private Rigidbody carBody;

    private void Update()
    {
        speedometerText.text = (carBody.velocity.magnitude * 3.6f).ToString("0");
    }
}
