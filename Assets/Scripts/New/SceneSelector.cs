using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSelector : MonoBehaviour
{
    LevelManager _lvlManager;
    public int sceneSelect;
    public int hostRole;
    public int manualSelection;
    public bool useManualSelection;

    private void Awake()
    {
        if (PersistentManager.Instance.createOrder == true)
        {
            PersistentManager.Instance.ExpOrder = SceneRandomizer();
            PersistentManager.Instance.createOrder = false;
            if (useManualSelection == false)
            {
                PersistentManager.Instance.experimentnr = PersistentManager.Instance.ExpOrder[0];
            }
            else if(useManualSelection == true)
            {
                PersistentManager.Instance.experimentnr = manualSelection;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.tag == "NEXT")
        {
            if (useManualSelection == false)
            {
                Invoke("nextExperiment", 4);

                PersistentManager.Instance.stopLogging = true;
                PersistentManager.Instance.nextScene = true;

                // Select next exp
                if (PersistentManager.Instance.listNr < PersistentManager.Instance.ExpOrder.Count - 1)
                {
                    PersistentManager.Instance.listNr++;
                    PersistentManager.Instance.experimentnr = PersistentManager.Instance.ExpOrder[PersistentManager.Instance.listNr];
                    Debug.LogError($"persistent experiment nr = {PersistentManager.Instance.experimentnr}");
                }
                else
                {
                    //Application.Quit(); // build version
                    UnityEditor.EditorApplication.isPlaying = false; // editor version
                }
            }
            else if (useManualSelection == true)
            {
                //Application.Quit(); // build version
                UnityEditor.EditorApplication.isPlaying = false; // editor version
            }
        }
    }

    public void nextExperiment()
    {
        SceneManager.LoadSceneAsync("StartScene");
        Debug.LogError("SWITCHING SCENES");
    }

    public SceneSelector(LevelManager levelManager) // actually selects the experiment definition
    {
        _lvlManager = levelManager;
        //Debug.LogError($"persistent experiment nr = {PersistentManager.Instance.experimentnr}");

        // experiment definition selection
        sceneSelect = PersistentManager.Instance.experimentnr;
        if (sceneSelect > _lvlManager.Experiments.Length)
        {
            Debug.LogError("Selected experiment definition out of bounds.");
            Debug.LogError($"experiment nr = {sceneSelect}");
        }
        

        // manually select the role nr for the host for now
        //hostRole = 0;
    }

    public void GazeEffectOnAV()
    {
        if (PersistentManager.Instance.experimentnr < 4 || PersistentManager.Instance.experimentnr > 8)
        {
            PersistentManager.Instance._StopWithEyeGaze = false;
        }
        if (PersistentManager.Instance.experimentnr >= 4 && PersistentManager.Instance.experimentnr <= 8)
        {
            PersistentManager.Instance._StopWithEyeGaze = true;
        }
        Debug.LogError($"Stop with eye gaze = {PersistentManager.Instance._StopWithEyeGaze}");
    }

    public void UseEyeTracking()
    {
        if (PersistentManager.Instance.experimentnr < 4)
        {
            PersistentManager.Instance.useEyeTracking = false;
        }
        if (PersistentManager.Instance.experimentnr >= 4)
        {
            PersistentManager.Instance.useEyeTracking = true;
        }
    }

    private List<int> SceneRandomizer()
    {
        // Randomize exp 0-3, every exp 3 times
        List<int> Block_one = makeList(0, 3, 1);    // (0, 3, 4)
        Block_one = Shuffler(Block_one);

        // Randomize Mapping 1, exp 4-7
        List<int> Block_two = makeList(4, 7, 4); 
        Block_two = Shuffler(Block_two);

        // Randomize Mapping 2, exp 8-11
        List<int> Block_three = makeList(8, 11, 4);
        Block_three = Shuffler(Block_three);

        // Randomize choosing mapping 1 or 2
        int randomInt = Random.Range(1, 2);

        // Set order of the blocks
        /*if(randomInt == 1)
        {
            Block_one.AddRange(Block_two);
            Block_one.AddRange(Block_three);
        }
        else if (randomInt == 2)
        {
            Block_one.AddRange(Block_three);
            Block_one.AddRange(Block_two);
        }*/

        //Debug.LogError($"Randomint = {randomInt}");
        /*for (int i = 0; i < Block_two.Count; i++)
        {
            Debug.LogError($"List one = {Block_two[i]}");
        }*/

        return Block_one;
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
