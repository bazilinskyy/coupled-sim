using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HMIvisibleScript : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject MakeHMIVisible;

    
    private void OnTriggerEnter()
    {
        MakeHMIVisible.SetActive(true);
    }
}
