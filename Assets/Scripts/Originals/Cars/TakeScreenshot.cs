using UnityEngine;

public class TakeScreenshot : MonoBehaviour
{
    // The time at which you want to capture the screenshot (in seconds)
    public float screenshotTime = 5.0f; // Default value of 5 seconds

    private bool screenshotTaken = false;

    void Update()
    {
        // Check if the screenshot has not been taken and the current time has reached the specified screenshotTime
        if (!screenshotTaken && Time.time >= screenshotTime)
        {
            // Generate a filename for the screenshot based on the time
            string screenshotFilename = "screenshot_" + screenshotTime.ToString("F1") + "s.jpg";

            // Capture the screenshot and save it with the generated filename
            ScreenCapture.CaptureScreenshot(screenshotFilename);

            // Set the flag to indicate that the screenshot has been taken
            screenshotTaken = true;
        }
    }
}
