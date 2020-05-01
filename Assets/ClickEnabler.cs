using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickEnabler : MonoBehaviour
{
    private Vector3 zeroVec, orgScale;
    private GameObject[] gameObjects;
    public static int Enable_On;

    // Start is called before the first frame update
    void Start()
    {
        // gameObject.SetActive(false);
        zeroVec = new Vector3(0, 0, 0);
        orgScale = transform.localScale;
        transform.localScale = zeroVec;

        if (Enable_On == 1)
        {
            transform.localScale = orgScale;
        }

    }
    // Update is called once per frame
    void Update()
    {
       // if (Input.GetKeyDown(KeyCode.Alpha6))
       // {

         //   transform.localScale = orgScale;
        //}
    }
}
