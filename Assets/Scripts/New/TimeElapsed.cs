using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeElapsed : MonoBehaviour
{
    private float timeValue = 0;
    private float twoDecimals = 100.0f;
    
    // Update is called once per frame
    void Update()
    {
        timeValue += Time.deltaTime;        
    }

    void OnApplicationQuit()
    {
        
        timeValue = Mathf.Round(timeValue * twoDecimals) / twoDecimals;
        Debug.Log("This scene lasted " + timeValue + " seconds");
    }
}
