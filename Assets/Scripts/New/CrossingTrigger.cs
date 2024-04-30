using UnityEngine;


// The intended use of this script was that it would detect when the participant crossed the zebra halfway and then pause/reset the simulation.
// It has not been used in the experiment of Kooijman.


public class CrossingTrigger : MonoBehaviour
{
    public GameObject TryoutPlayer;
    public GameObject Marker;
    public bool HalfWay;
    public bool crossbutton = false;

    private float pauseUntil;
    private bool paused = false;
    private Rect windowRect = new(425, 225, 200, 50);


    private void Update()
    {
        CheckPause();
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Pedestrian"))
        {
            HalfWay = true;
        }
    }


    private void OnGUI()
    {
        if (HalfWay)
        {
            // Register the window. Notice the 3rd parameter
            windowRect = GUI.Window(0, windowRect, DoMyWindow, "Crossing");
            paused = true;
        }
    }


    // Make the contents of the window
    private void DoMyWindow(int windowID)
    {
        if (GUI.Button(new Rect(25, 20, 150, 20), "You've Crossed Safely!"))
        {
            crossbutton = true;
        }
    }


    private void CheckPause()
    {
        if (paused)
        {
            Time.timeScale = 0.0f;
        }

        if (crossbutton)
        {
            Time.timeScale = 1.0f;
            HalfWay = false;
            crossbutton = false;
            paused = false;
        }
    }
}