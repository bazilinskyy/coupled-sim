using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainManager : MonoBehaviour
{
    private GameObject player;
    private ExperimentInput experimentInput;
    UnityEngine.UI.Image blackOutScreen;
    
    // Start is called before the first frame update
    private void Start()
    {
      

        player = GameObject.FindGameObjectWithTag("Player");

        experimentInput = player.GetComponent<ExperimentInput>();
        string dateTime = System.DateTime.Now.ToString("MM-dd_HH-mm");
        experimentInput.subjectDataFolder = dateTime + "-" + experimentInput.subjectDataFolder;

        blackOutScreen = GameObject.FindGameObjectWithTag("BlackOutScreen").GetComponent<UnityEngine.UI.Image>();
        blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(0f, 0f, true);

        StartCoroutine(LoadFirstSceneASync());
    }

    IEnumerator LoadFirstSceneASync()
    {
        DontDestroyOnLoad(player);
        blackOutScreen.CrossFadeAlpha(1f, 0f, true);

        string firstScene = experimentInput.GetNextScene();
        // The Application loads the Scene in the background at the same time as the current Scene.
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(firstScene, LoadSceneMode.Single);

        // Wait until the last operation fully loads to return anything
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        
        SceneManager.MoveGameObjectToScene(player, SceneManager.GetSceneByName(firstScene));
    }
}
