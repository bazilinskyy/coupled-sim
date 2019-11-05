using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class eHMIfield : MonoBehaviour
{
    public GameObject eHMIinputfield
        ;
    private void OnTriggerStay()
    {
        eHMIinputfield.SetActive(true);
    }
}
