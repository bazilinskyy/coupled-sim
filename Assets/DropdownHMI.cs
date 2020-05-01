using UnityEngine;
using UnityEngine.UI;

public class DropdownHMI : MonoBehaviour
{
    //Attach this script to a Dropdown GameObject
    Dropdown m_Dropdown;

    void Start()
    {
        //Fetch the DropDown component from the GameObject
        m_Dropdown = GetComponent<Dropdown>();



    }

    void Update()
    {
        if (m_Dropdown.value == 0)
        {
            { HMIBehaviourSelector.TextualHMI = 0; }
            { HMIBehaviourSelector.LightStripHMI = 0; }
            { HMIBehaviourSelector.PostureHMI = 0; }
            { HMIBehaviourSelector.GestureHMI = 0; }
            { HMIBehaviourSelector.ColourHMI = 0; }
        }
        if (m_Dropdown.value == 1)
        {
            { HMIBehaviourSelector.TextualHMI = 1; }
            { HMIBehaviourSelector.LightStripHMI = 0; }
            { HMIBehaviourSelector.PostureHMI = 0; }
            { HMIBehaviourSelector.GestureHMI = 0; }
            { HMIBehaviourSelector.ColourHMI = 0; }
        }
        if (m_Dropdown.value == 2)
        { 
        { HMIBehaviourSelector.TextualHMI = 0; }
        { HMIBehaviourSelector.LightStripHMI = 1; }
        { HMIBehaviourSelector.PostureHMI = 0; }
        { HMIBehaviourSelector.GestureHMI = 0; }
        { HMIBehaviourSelector.ColourHMI = 0; }
    }
        if (m_Dropdown.value == 3)
        {
            { HMIBehaviourSelector.TextualHMI = 0; }
            { HMIBehaviourSelector.LightStripHMI = 0; }
            { HMIBehaviourSelector.PostureHMI = 1; }
            { HMIBehaviourSelector.GestureHMI = 0; }
            { HMIBehaviourSelector.ColourHMI = 0; }
        }
        if (m_Dropdown.value == 4)
        {
            { HMIBehaviourSelector.TextualHMI = 0; }
            { HMIBehaviourSelector.LightStripHMI = 0; }
            { HMIBehaviourSelector.PostureHMI = 0; }
            { HMIBehaviourSelector.GestureHMI = 1; }
            { HMIBehaviourSelector.ColourHMI = 0; }
        }
        if (m_Dropdown.value == 5)
        {
            { HMIBehaviourSelector.TextualHMI = 0; }
            { HMIBehaviourSelector.LightStripHMI = 0; }
            { HMIBehaviourSelector.PostureHMI = 0; }
            { HMIBehaviourSelector.GestureHMI = 0; }
            { HMIBehaviourSelector.ColourHMI = 1; }
        }
    }
}
