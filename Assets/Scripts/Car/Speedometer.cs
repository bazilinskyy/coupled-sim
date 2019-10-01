using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class Speedometer : MonoBehaviour
{
    private int count;
    private int remainder;
    [SerializeField]
    private Text speedometerText;

    [SerializeField]
    private Rigidbody carBody;

    private void Update()
    {
        count = count + 1;
        Math.DivRem(count, 20, out remainder);
        if (remainder == 0)
        {
            speedometerText.text = (carBody.velocity.magnitude * 3.6f).ToString("0");
        }        
    }
}
