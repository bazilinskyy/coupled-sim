using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableOnAwake : MonoBehaviour
{
    // Start is called before the first frame update
    void Awake()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<ExperimentInput>().environment = gameObject;
        gameObject.SetActive( false);
    }

}
