using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InvisibleComponent : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
