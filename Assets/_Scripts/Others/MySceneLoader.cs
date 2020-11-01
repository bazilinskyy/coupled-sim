using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;

public class MySceneLoader : MonoBehaviour
{
    public GameObject player;
    public Transform startingPosition;
    private bool loading=false;
    UnityEngine.UI.Image blackOutScreen;
    
    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
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
        while (NScenes > 1) { Debug.Log($"Currently on {NScenes} scenes"); yield return null; }

        player.transform.parent = startingPosition;
        player.transform.position = startingPosition.position;
        player.transform.rotation = startingPosition.rotation;

        blackOutScreen.CrossFadeAlpha(0, StaticSceneManager.animationTime*2, true);
        Debug.Log("Moved player!");
    }

    public void LoadNextScene()
    {
        if (!loading && StaticSceneManager.IsNextScene()) { StartCoroutine(LoadYourAsyncScene(StaticSceneManager.GetNextScene()));  }
    } 

    public void LoadWaitingScene()
    {
        if (!loading) { StartCoroutine(LoadYourAsyncScene(StaticSceneManager.waitingRoomScene)); }
    }

    public void AddTargetScene()
    {
        SceneManager.LoadSceneAsync(StaticSceneManager.targetScene, LoadSceneMode.Additive);
    }

    IEnumerator LoadYourAsyncScene(string sceneName)
    {
        loading = true; Debug.Log($"Loading next scene: {sceneName}...");
        player.transform.parent = null;
        DontDestroyOnLoad(player);

        blackOutScreen.CrossFadeAlpha(1f, StaticSceneManager.animationTime, false);
        yield return new WaitForSeconds(StaticSceneManager.animationTime);

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

