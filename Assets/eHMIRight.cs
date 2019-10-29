using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class eHMIRight : MonoBehaviour
{
    public GameObject ShoweHMI;
    private void OnTriggerEnter()
    {
        ShoweHMI.SetActive(true);
    }
}
