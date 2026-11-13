using UnityEngine;
using UnityEngine.SceneManagement;

// This script controls the button in the trial scene to jump back to the main scene and to pause and unpause the simulation.

public class Trial : MonoBehaviour
{
    public void OnMainButton()
    {
        SceneManager.LoadScene("MainScene");
    }
    public void OnPauseButton()
    {
        Time.timeScale = 0.0f;
    }
    public void OnStartButton()
    {
        Time.timeScale = 1.0f;
    }
}
