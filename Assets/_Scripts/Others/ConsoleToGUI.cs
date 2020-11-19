using UnityEngine;
using UnityEngine.UI;
public class ConsoleToGUI : MonoBehaviour
{
    //source https://answers.unity.com/questions/125049/is-there-any-way-to-view-the-console-in-a-build.html
    string myLog = "*begin log";
    string myErrorLog = "*begin Error Log";
    string fileName = "";
    public Text logCanvas;
    public Text fps;
    public bool showLogScreen = true;
    public bool showLogUser = true;
    int kChars = 400;
    
    //fps stuff
    int m_frameCounter = 0;
    float m_timeCounter = 0.0f;
    float m_lastFramerate = 0.0f;
    public float m_refreshTime = 0.5f;


    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L)) { showLogUser = !showLogUser; };

        if (m_timeCounter < m_refreshTime)
        {
            m_timeCounter += Time.deltaTime;
            m_frameCounter++;
        }
        else
        {
            //This code will break if you set your m_refreshTime to 0, which makes no sense.
            m_lastFramerate = (float)m_frameCounter / m_timeCounter;
            m_frameCounter = 0;
            m_timeCounter = 0.0f;
        }
    }
    public void Log(string logString, string stackTrace, LogType type)
    {
        // for onscreen...
        myLog = myLog + "\n" + logString;

        if (type == LogType.Error || type == LogType.Exception) { myErrorLog += "\n" + logString; }

        if (myLog.Length > kChars) { myLog = myLog.Substring(myLog.Length - kChars); }
        if (myErrorLog.Length > kChars) { myErrorLog = myErrorLog.Substring(myLog.Length - kChars); }


        // for the file ...
        if (fileName == "")
        {
            string d = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop).Replace("\\", "/") + "/Data/YOUR_LOGS";

            System.IO.Directory.CreateDirectory(d);
            string r = Random.Range(1000, 9999).ToString();
            fileName = System.DateTime.Now.ToString("MM-dd_HH-mm") + "_" + GetComponent<ExperimentInput>().subjectName;

            fileName = d + "/log-" + fileName + ".txt";
            myLog += $"\nCreated log file {fileName}...";
        }
        try { System.IO.File.AppendAllText(fileName, logString + "\n"); }
        catch { }
    } 
    
   
    void OnGUI()
    {
        if (!showLogUser) { logCanvas.enabled = false; fps.enabled = false; }
        else { logCanvas.enabled = true; fps.enabled = true; }

        if (showLogScreen)
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1200.0f, Screen.height / 800.0f, 1.0f));
            GUI.TextArea(new Rect(10, 10, 540, 370), myLog);
            GUI.TextArea(new Rect(10, 380, 540, 370), myErrorLog);
            GUI.TextArea(new Rect(560, 10, 100, 20), "FPS: " + m_lastFramerate.ToString());
        }

        if(logCanvas != null) { logCanvas.text = myLog; }
    }
}