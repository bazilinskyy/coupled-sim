using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HMIBehaviourSelector : MonoBehaviour
{
    // Start is called before the first frame update

    public static int TextualHMI;
    public static int LightStripHMI;
    public static int PostureHMI;
    public static int GestureHMI;
    public static int ColourHMI;
    public static int Yield;

    public colorChange colorChange;
    public posChange posChange;
    public 


    void Start()
    {
        if (TextualHMI == 1)
        {
            TexteHMIChanger.HMI_On = 1;
            TexteHMIChanger.Yielding = Yield;
        }

        if (LightStripHMI == 1)
        {
            LightbarHMI.HMI_On = 1;
            LightbarHMI.Yielding = Yield;
        }

        if (PostureHMI == 1)
        { 
            posChange.HMI_On = 1;
            posChange.Yielding = Yield;
        }
        
        if (GestureHMI == 1)
        {
            ClickEnabler.Enable_On = 1;
            // Fix rest of tha shizzle

            RotateObjFlapLeft.HMI_On = 1;
            RotateObjFlapLeft.Yielding = Yield;

            RotateObjFlapRight.HMI_On = 1;
            RotateObjFlapRight.Yielding = Yield;

            RotateObjFlapTop.HMI_On = 1;
            RotateObjFlapTop.Yielding = Yield;
        }

        if (ColourHMI == 1)
        {
            colorChange.HMI_On = 1;
            colorChange.Yielding = Yield;
        }

        // Switched code here in an unconvenient manner, but que sera sera
        if (Yield == 0)
        { AICar.Yield = 1; }
        else
        {
            AICar.Yield = 0;
        }

    }
}
