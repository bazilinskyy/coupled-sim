using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Singleton which exists throughout all scenes.
public class PersistentManager : MonoBehaviour
{
    public static PersistentManager Instance { get; private set; }

    // Experiment metadata
    public int ParticipantNr = 1;
    public int ExpDefNr = 1;
    public int TrialNr = 1;

    // Scene-change logic parameters
    public bool switchScene;
    public bool stopLogging;

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
