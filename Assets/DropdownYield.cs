using UnityEngine;
using UnityEngine.UI;

public class DropdownYield : MonoBehaviour
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
        { HMIBehaviourSelector.Yield = 1; }
        if (m_Dropdown.value == 1)
        { HMIBehaviourSelector.Yield = 0; }
        
    }
}
