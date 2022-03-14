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
    public int QuestionnaireScene = 14;
    public int[] BlockOrder = new int[7];
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

        // Order of blocks for testing
        ExpOrder.Add(3); ExpOrder.Add(10); ExpOrder.Add(3); ExpOrder.Add(10);
        /*ExpOrder.Add(0); ExpOrder.Add(1); ExpOrder.Add(2); ExpOrder.Add(3);
        ExpOrder.Add(4); ExpOrder.Add(5); ExpOrder.Add(6);
        ExpOrder.Add(7); ExpOrder.Add(14);*/
        //ExpOrder.Add(8); ExpOrder.Add(9); ExpOrder.Add(10); ExpOrder.Add(11);
        //ExpOrder.Add(12); ExpOrder.Add(13); ExpOrder.Add(14);

        // Display participant number
        string stringParticipantNr = "The participant number is: " + ParticipantNr;
        Debug.Log(stringParticipantNr);

        // Determine the order of experiment blocks
        //OrderOfBlocks(ParticipantNr);

        // Display the order of experiments
        string stringBlockOrder = "The experiment order is: [ " + string.Join(", ", BlockOrder) + "]";
        Debug.Log(stringBlockOrder);

        // Create the complete list of trials (and their corresponding ExpDefNrs)
        //CreateTrials(ExpOrder);

        // Display the complete order of (randomized) trials based on the blocks
        string stringTrialsOrder = "The order of trials is: [ " + string.Join(", ", ExpOrder) + "]";
        Debug.Log(stringTrialsOrder);

        string stringTestTrialsOrder = "The length is: " + ExpOrder.Count;
        Debug.Log(stringTestTrialsOrder);

    }

    public void OrderOfBlocks(int ParticipantNr)
    {
        // The order of blocks is predefined by balanced latin squares
        if (ParticipantNr == 1 || ParticipantNr == 15)
        {
            BlockOrder = new int[] { 0, 1, 6, 2, 5, 3, 4 }; // A-B-G-C-F-D-E
        }
        if (ParticipantNr == 2 || ParticipantNr == 16)
        {
            BlockOrder = new int[] { 5, 4, 6, 3, 0, 2, 1 }; // F-E-G-D-A-C-B
        }
        if (ParticipantNr == 3 || ParticipantNr == 17)
        {
            BlockOrder = new int[] { 2, 3, 1, 4, 0, 5, 6 }; // C-D-B-E-A-F-G
        }
        if (ParticipantNr == 4 || ParticipantNr == 18)
        {
            BlockOrder = new int[] { 0, 6, 1, 5, 2, 4, 3 }; // A-G-B-F-C-E-D
        }
        if (ParticipantNr == 5 || ParticipantNr == 19)
        {
            BlockOrder = new int[] { 4, 5, 3, 6, 2, 0, 1 }; // E-F-D-G-C-A-B
        }
        if (ParticipantNr == 6 || ParticipantNr == 20)
        {
            BlockOrder = new int[] { 2, 1, 3, 0, 4, 6, 5 }; // C-B-D-A-E-G-F
        }
        if (ParticipantNr == 7 || ParticipantNr == 21)
        {
            BlockOrder = new int[] { 6, 0, 5, 1, 4, 2, 3 }; // G-A-F-B-E-C-D
        }
        if (ParticipantNr == 8 || ParticipantNr == 22)
        {
            BlockOrder = new int[] { 4, 3, 5, 2, 6, 1, 0 }; // E-D-F-C-G-B-A
        }
        if (ParticipantNr == 9 || ParticipantNr == 23)
        {
            BlockOrder = new int[] { 1, 2, 0, 3, 6, 4, 5 }; // B-C-A-D-G-E-F
        }
        if (ParticipantNr == 10 || ParticipantNr == 24)
        {
            BlockOrder = new int[] { 6, 5, 0, 4, 1, 3, 2 }; // G-F-A-E-B-D-C
        }
        if (ParticipantNr == 11 || ParticipantNr == 25)
        {
            BlockOrder = new int[] { 3, 4, 2, 5, 1, 6, 0 }; // D-E-C-F-B-G-A
        }
        if (ParticipantNr == 12 || ParticipantNr == 26)
        {
            BlockOrder = new int[] { 1, 0, 2, 6, 3, 5, 4 }; // B-A-C-G-D-F-E
        }
        if (ParticipantNr == 13 || ParticipantNr == 27)
        {
            BlockOrder = new int[] { 5, 6, 4, 0, 3, 1, 2 }; // F-G-E-A-D-B-C
        }
        if (ParticipantNr == 14 || ParticipantNr == 28)
        {
            BlockOrder = new int[] { 3, 2, 4, 1, 5, 0, 6 }; // D-C-E-B-F-A-G
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