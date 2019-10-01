using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HMI : MonoBehaviour
{
    public HMIState CurrentState;
    public virtual void Display(HMIState state)
    {
        CurrentState = state;
    }
}
