using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Singleton which exists throughout all scenes.
public class PersistentManager : MonoBehaviour
{
    public static PersistentManager Instance { get; private set; }

    // Scene-change logic parameters
    public bool switchScene;
    public bool stopLogging;
    public bool doOnlyOnce;
    public bool TrialStarted;
    public int hostRole;
    public int clientRole;
    public int ListNr = 0;
    public int OrderNr;

    // Variables for the experiment order
    public int[] BlockOrder = { 0, 1, 2, 3, 4, 5, 6};
    public int QuestionnaireScene = 14;
    public List<int> ExpOrder = new List<int>();

    // Experiment metadata
    public int ParticipantNr;
    public int ExpDefNr;
    public int TrialNr;

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

        // Create the random order of experiment blocks
        RandomizeBlocks(BlockOrder);

        // Display the order of randomized experiments
        string stringExpOrder = "The randomized experiment order is: [ " + string.Join(", ", BlockOrder) + "]";
        Debug.Log(stringExpOrder);

        // Create the complete list of trials (and their corresponding ExpDefNrs)
        CreateTrials(ExpOrder);

        // Display the complete order of randomized trials based on the randomized blocks
        string stringTrialsOrder = "The randomized order of trials is: [ " + string.Join(", ", ExpOrder) + "]";
        Debug.Log(stringTrialsOrder);
    }

    public void RandomizeBlocks(int[] BlockOrder)
    {
        for (int n = BlockOrder.Length - 1; n > 0; n--)
        {
            int randomInt = Random.Range(0, 7); // Picks a number between 0 and 6.
            int t = BlockOrder[randomInt];
            BlockOrder[randomInt] = BlockOrder[n];
            BlockOrder[n] = t;
        }
    }

    public void CreateTrials(List<int> ExpOrder)
    {
        int i = 0;
        int numTrials = 0;
        int zeroCounter = 0;
        int oneCounter = 0;
        int randomInt = Random.Range(0, 2); // Draws a 0 or a 1.
        while (i < BlockOrder.Length)
        {
            int sceneNr = BlockOrder[i];
            if (randomInt == 0 && zeroCounter < 3)
            {
                ++zeroCounter;
                ++numTrials;
                ExpOrder.Add(sceneNr);
                randomInt = Random.Range(0, 2);
            }
            else if (randomInt == 1 && oneCounter < 3)
            {
                ++oneCounter;
                ++numTrials;
                ExpOrder.Add(sceneNr + 7);
                randomInt = Random.Range(0, 2);
            }
            else if (numTrials > 5)
            {
                numTrials = 0;
                zeroCounter = 0;
                oneCounter = 0;
                ExpOrder.Add(QuestionnaireScene);
                randomInt = Random.Range(0, 2);
                ++i;
            }
            else
            {
                randomInt = Random.Range(0, 2);
            }    
        }
    }
}