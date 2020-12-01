using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class turnOff : MonoBehaviour
{
    public List<myInput> inputs;

    private void Start()
    {
        
    }
    // Update is called once per frame
    void Update()
    {
        foreach(myInput input in inputs)
        {
            if (Input.GetKeyDown(input.keyCode)) { input.Toggle(); }

        }
        
    }
}


[System.Serializable]
public class myInput
{
    public KeyCode keyCode;
    public GameObject section;
    
    public bool status = true;

    public void Toggle()
    {
        section.SetActive(!status);
        status = !status;
    }

}