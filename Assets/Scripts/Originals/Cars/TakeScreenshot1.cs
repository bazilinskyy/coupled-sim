using UnityEngine;
using System.IO;

public class ScreenshotCapture : MonoBehaviour
{
    // Define the folder name where you want to save the screenshots
    public string screenshotFolderName = "Screenshots"; // Change this to your desired folder name

    // Define the target time in seconds at which you want to capture the screenshot
    public float targetCaptureTime = 8.5f; // Change this to your desired time in the Inspector

    private bool screenshotCaptured = false;

    void Update()
    {
        // Check if the current time in the game has reached the targetCaptureTime
        if (!screenshotCaptured && Time.time >= targetCaptureTime)
        {
            // Create the full folder path
            string folderPath = Path.Combine(Application.persistentDataPath, screenshotFolderName);

            // Create the folder if it doesn't exist
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Define a file name and path for the screenshot with JPG extension
            string screenshotFileName = "Screenshot.jpg";
            string screenshotFilePath = Path.Combine(folderPath, screenshotFileName);

            // Capture the screenshot and save it as a JPG image file with a specified quality (80 in this example)
            ScreenCapture.CaptureScreenshot(screenshotFilePath, 80);

            // Output a message to the console to confirm the screenshot has been taken
            Debug.Log("Screenshot captured at time: " + Time.time + " and saved to: " + screenshotFilePath);

            // Set the screenshotCaptured flag to true to prevent multiple captures
            screenshotCaptured = true;
        }
    }
}
