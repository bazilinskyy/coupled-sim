using UnityEngine;
using UnityEngine.SceneManagement;

// This script is used for jumping between scenes. Jumping between scenes can be seen as loading a new level in a game.
// The main scene contains two sets of objects; KeepOnLoad and DestroyOnLoad. The objects in each of these sets are either taken to the new scene or detroyed.

public class MainScene : MonoBehaviour
{
    public static int SelectSession;
    public void OnTestButton()
    {
        SelectSession = 1;             
        SceneManager.LoadScene("TestScene",LoadSceneMode.Single);
    }

    public void OnTrialButton()
    {
        SelectSession = 2;
        SceneManager.LoadScene("Trial");
    }
}