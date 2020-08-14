using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Singleton which exists throughout all scenes.
public class PersistentManager : MonoBehaviour
{
    public static PersistentManager Instance { get; private set; }

    public bool nextScene;
    public bool stopLogging;
    public int experimentnr;
    public bool _StopWithEyeGaze;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
