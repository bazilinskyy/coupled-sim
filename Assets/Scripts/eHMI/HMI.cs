using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//base interface for controlling HMI displays
public class HMI : MonoBehaviour
{
    public HMIState CurrentState;
    public virtual void Display(HMIState state)
    {
        CurrentState = state;
    }
}
