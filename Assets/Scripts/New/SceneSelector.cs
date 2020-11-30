using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;

public class SceneSelector : MonoBehaviour
{
    LevelManager _lvlManager;
    public int sceneSelect;
    public int manualSelection;
    public bool useManualSelection;
    public int manualHostRole;
    public int manualClientRole;
    private bool passed = false;

    private void Awake()
    {
        PersistentManager.Instance.nextScene = false;
    }

    private void Update()
    {
        // Check if the end of the game is reached & Client has shut down. Shut down host if true
        if (PersistentManager.Instance.ClientClosed == true && PersistentManager.Instance.listNr >= PersistentManager.Instance.ExpOrder.Count - 1)
        {
            Application.Quit(); // build version
            //UnityEditor.EditorApplication.isPlaying = false; // editor version
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.tag == "NEXT" && !passed)
        {
            passed = true;
            // Check if the end of the game is reached. Shut down client if true.
            if (PersistentManager.Instance.listNr >= PersistentManager.Instance.ExpOrder.Count-1)
            {
                PersistentManager.Instance.SendEndGameToClient = true; 
            }
            // The game has not reached the end. Select experiment
            if (useManualSelection == false)
            {
                // Load next scene in 4 seconds
                if(PersistentManager.Instance.SendEndGameToClient == false)
                {
                    Invoke("nextExperiment", 4);
                }               
                
                PersistentManager.Instance.stopLogging = true;
                PersistentManager.Instance.nextScene = true;

                // Select next exp
                if (PersistentManager.Instance.listNr < PersistentManager.Instance.ExpOrder.Count - 1)
                {
                    PersistentManager.Instance.listNr++;
                    PersistentManager.Instance.experimentnr = PersistentManager.Instance.ExpOrder[PersistentManager.Instance.listNr];
                    Debug.LogError($"persistent experiment nr = {PersistentManager.Instance.experimentnr}");
                }
            }
            else if (useManualSelection == true)
            {
                PersistentManager.Instance.stopLogging = true;
                PersistentManager.Instance.nextScene = true;
                PersistentManager.Instance.experimentnr = manualSelection;
            }
        }
    }

    /// <summary>
    /// Load the next scene for the Host. The host only activates the next scene when the scene has been loaded for over 90%. At that point, the host also sends a message
    /// to the client to switch scenes.
    /// </summary>
    public void nextExperiment()
    {
        //SceneManager.LoadSceneAsync("StartScene");
        Debug.LogError("SWITCHING SCENES");

        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync("StartScene", LoadSceneMode.Single);
        // Don't let the Scene activate until you allow it to
        asyncOperation.allowSceneActivation = false;

        // Send a message to the client to load new scene and allow host to activate scene
        if (asyncOperation.progress >= 0.9f)
        {
            Debug.LogError("Next scene allowed to load");
            passed = false;
            asyncOperation.allowSceneActivation = true;
            PersistentManager.Instance.SendLoadMsgToClient = true;
        }
    }

    public SceneSelector(LevelManager levelManager) // actually selects the experiment definition
    {
        _lvlManager = levelManager;

        // experiment definition selection
        sceneSelect = PersistentManager.Instance.experimentnr;
        if (sceneSelect > _lvlManager.Experiments.Length)
        {
            Debug.LogError("Selected experiment definition out of bounds.");
            Debug.LogError($"experiment nr = {sceneSelect}");
        }
    }

    public void CreateExpList()
    {
        if (PersistentManager.Instance.createOrder == true && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8")
        {
            PersistentManager.Instance.ExpOrder = SceneRandomizerBlock(); // SceneRandomizer();
            PersistentManager.Instance.createOrder = false;
            if (useManualSelection == false)
            {
                PersistentManager.Instance.experimentnr = PersistentManager.Instance.ExpOrder[0];
                Debug.LogError($"Experiment nr = {PersistentManager.Instance.experimentnr}");
            }
            else if (useManualSelection == true)
            {
                PersistentManager.Instance.experimentnr = manualSelection;
            }
        }
    }

    /// <summary>
    /// Determines whether the Autonomous Vehicle yields based on eye-gaze.
    /// </summary>
    public void GazeEffectOnAV() 
    {
        if (PersistentManager.Instance.experimentnr < 4 || PersistentManager.Instance.experimentnr >= 8)
        {
            PersistentManager.Instance._StopWithEyeGaze = false;
        }
        if (PersistentManager.Instance.experimentnr >= 4 && PersistentManager.Instance.experimentnr < 8)
        {
            PersistentManager.Instance._StopWithEyeGaze = true;
        }
    }

    /// <summary>
    /// Deteremines whether the eye-gaze visualization is used.
    /// </summary>
    public void VisualizeGaze()
    {
        if (PersistentManager.Instance.experimentnr < 4)
        {
            PersistentManager.Instance._visualizeGaze = false;
        }
        if (PersistentManager.Instance.experimentnr >= 4)
        {
            PersistentManager.Instance._visualizeGaze = true;
        }
    }

    /// <summary>
    /// Creates a list of experiment including all the experiment defintions.
    /// </summary>
    /// <returns></returns>
    private List<int> SceneRandomizer()
    {
        // Randomize exp 0-3, every exp 3 time
        List<int> Block_one = shuffledList(0, 3, 4);

        // Randomize Mapping 1, exp 4-7
        List<int> Block_two = shuffledList(4, 7, 4);

        // Randomize Mapping 2, exp 8-11
        List<int> Block_three = shuffledList(8, 11, 4);

        // Randomize choosing mapping 1 or 2
        int randomInt = Random.Range(1, 2);

        // Set order of the blocks
        if(randomInt == 1)
        {
            Block_one.AddRange(Block_two);
            Block_one.AddRange(Block_three);
        }
        else if (randomInt == 2)
        {
            Block_one.AddRange(Block_three);
            Block_one.AddRange(Block_two);
        }

        // Prints the list for debugging
        //Debug.LogError($"Randomint = {randomInt}");
        /*for (int i = 0; i < Block_one.Count; i++)
        {
            Debug.LogError($"List one = {Block_one[i]}");
        }*/

        return Block_one;
    }

    /// <summary>
    /// Creates a list of experiments including the experiments associated with the mapping chosen in the Unity editor.
    /// </summary>
    /// <returns></returns>
    private List<int> SceneRandomizerBlock()
    {
        List<int> Block = new List<int>();

        if (PersistentManager.Instance.mapping == 0)
        {
            // Baseline, exp 0-3, every exp 4 times
            Block = shuffledList(0, 3, 4);
        }
        else if (PersistentManager.Instance.mapping == 1) // Mapping 1, exp 4-7
        {
            Block = shuffledList(4, 7, 4);
        }
        else if (PersistentManager.Instance.mapping == 2) // Mapping 2, exp 8-11
        {
            Block = shuffledList(8, 11, 4);
        }
        else if (PersistentManager.Instance.mapping == 3) // Practice round ND, Y for all mappings
        {
            Block.Add(0); 
            Block.Add(4);
            Block.Add(8);
        }

        // Prints the list for debugging
        for (int i = 0; i < Block.Count; i++)
        {
            Debug.LogError($"List = {Block[i]}");
        }

        return Block;
    }

    private List<int> shuffledList(int start, int end, int reps)
    {
        List<int> Block =  makeList(start, end, reps);
        Block = Shuffler(Block);
        return Block;
    }

    private List<int> Shuffler(List<int> _Block)
    {
        for (int i = 0; i < _Block.Count; i++)
        {
            int temp = _Block[i];
            int randomIndex = Random.Range(i, _Block.Count);
            _Block[i] = _Block[randomIndex];
            _Block[randomIndex] = temp;
        }
        return _Block;
    }

    private List<int> makeList(int start, int end, int reps)
    {
        List<int> Block = new List<int>();
        for (int i = start; i <= end; i++)
        {
            for (int j = 0; j <= reps-1; j++)
            {
                Block.Add(i);
            }
        }
        return Block;
    }
}
