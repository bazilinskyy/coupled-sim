using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;

public class MySceneLoader : MonoBehaviour
{
    public GameObject player;
    private ExperimentInput experimentInput;
    

    public Transform startingPosition;
    private bool loading=false;
    UnityEngine.UI.Image blackOutScreen;
    private float timeWaited = 0f;
    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");

        experimentInput = player.GetComponent<ExperimentInput>();
        blackOutScreen = GameObject.FindGameObjectWithTag("BlackOutScreen").GetComponent<UnityEngine.UI.Image>();
        blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(1f, 0f, true);

        if (player == null) { Debug.LogError("Could not find player...."); return; }

        //DontDestroyOnLoad(player); Debug.Log("Found player!");
        
        if (startingPosition == null) { return; }
        StartCoroutine(MovePlayerToDestination());
    }

    IEnumerator MovePlayerToDestination()
    {
        int NScenes = SceneManager.GetAllScenes().Length;
        while (NScenes > 1 && timeWaited < 1f) 
        {
            if (timeWaited == 0f) { Debug.Log($"Waiting, currently on {NScenes} scenes..."); } 
            timeWaited += 0.01f; 
            yield return new WaitForSeconds(0.01f); 
        }
        timeWaited = 0f;

        player.transform.parent = startingPosition;
        player.transform.position = startingPosition.position;
        player.transform.rotation = startingPosition.rotation;

        //if (experimentInput.camType == MyCameraType.Leap) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }

        blackOutScreen.CrossFadeAlpha(0, experimentInput.animationTime*2, true);
        Debug.Log("Moved player!");
    }

    public void LoadNextScene()
    {
        if (!loading && experimentInput.IsNextScene()) { StartCoroutine(LoadYourAsyncScene(experimentInput.GetNextScene()));  }
    } 

    public void LoadWaitingScene()
    {
        if (!loading) { StartCoroutine(LoadYourAsyncScene(experimentInput.waitingRoomScene)); }
    }

    public void AddTargetScene()
    {
        SceneManager.LoadSceneAsync(experimentInput.targetScene, LoadSceneMode.Additive);
    }

    IEnumerator LoadYourAsyncScene(string sceneName)
    {
        loading = true; Debug.Log($"Loading next scene: {sceneName}...");
        player.transform.parent = null;
        DontDestroyOnLoad(player);

        blackOutScreen.CrossFadeAlpha(1f, experimentInput.animationTime, false);
        yield return new WaitForSeconds(experimentInput.animationTime);

        // The Application loads the Scene in the background at the same time as the current Scene.
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        // Wait until the last operation fully loads to return anything
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        Scene newScene = SceneManager.GetSceneByName(sceneName);

        SceneManager.MoveGameObjectToScene(player, newScene);

    }
}

