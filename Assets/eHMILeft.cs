using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class eHMILeft : MonoBehaviour
{
    public GameObject ShoweHMI;
    public GameObject HideeHMI;
    private void OnTriggerEnter()
    {
        ShoweHMI.SetActive(true);
        HideeHMI.SetActive(false);
    }
}
