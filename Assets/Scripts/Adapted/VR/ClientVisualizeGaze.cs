using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClientVisualizeGaze : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        GameObject go_N3 = gameObject.transform.Find("NetworkObject_3").gameObject;
        PersistentManager.Instance._visualizeGaze_client = VisualizeGazeClient((int)go_N3.transform.position.z);
    }

    public bool VisualizeGazeClient(int expnr)
    {
        bool output = true;
        if (expnr < 4)
        {
            output = false;
        }
        else if (expnr >= 4)
        {
            output = true;
        }
        return output;
    }
}
