using UnityEngine;
using UnityEngine.SceneManagement;

// This script controls the button in the test scene to jump back to the main scene.
public class TestScene : MonoBehaviour
{
    public void OnMainButton()
    {
        SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
    }
}