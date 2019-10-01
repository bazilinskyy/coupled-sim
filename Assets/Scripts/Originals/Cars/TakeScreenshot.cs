// TODO:
// By default, screenshot files are placed next to the executable bundle -- we don't want this in a
// shipping game, as it will fail if the user doesn't have write access to the Applications folder.
// Instead we should place the screenshots on the user's desktop. However, the ~/ notation doesn't
// work, and Unity doesn't have a mechanism to return special paths. Therefore, the correct way to
// solve this is probably with a plug-in to return OS specific special paths.
 
// Mono/.NET has functions to get special paths... see discussion page. --Aarku
 
using UnityEngine;
using System.Collections;
 
public class TakeScreenshot : MonoBehaviour
{    
    private int screenshotCount = 0;
 
    // Check for screenshot key each frame
    void Update()
    {
        // take screenshot on up->down transition of F9 key
        if (Input.GetKeyDown("f9"))
        {        
            string screenshotFilename;
            do
            {
                screenshotCount++;
                screenshotFilename = "screenshot" + screenshotCount + ".png";
 
            } while (System.IO.File.Exists(screenshotFilename));
 
            ScreenCapture.CaptureScreenshot(screenshotFilename);
        }
    }
}