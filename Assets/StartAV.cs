using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartAV : MonoBehaviour
{
    public bool InitiateAV = false;
    private float Timer1;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (InitiateAV == true && Timer1 < 5f)
        {
            Timer1 += Time.deltaTime;
        }
        else if(InitiateAV == true && Timer1 > 5f)
        {
            InitiateAV = false;
            Timer1 = 0f;
        }
    }
    void OnTriggerEnter(Collider other)
    {
        // Do nothing if trigger isn't enabled
        if (this.enabled == false)
        {
            return;
        }
        else if (other.gameObject.CompareTag("ManualCar"))
        {
            InitiateAV = true;
        }
    }
}
