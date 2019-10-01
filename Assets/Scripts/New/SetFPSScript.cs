using UnityEngine;
using UnityEngine.UI;
// This script is partially copied from http://wiki.unity3d.com/index.php/FramesPerSecond && https://answers.unity.com/questions/46745/how-do-i-find-the-frames-per-second-of-my-game.html
// It is used to compute the amount of Frames that are drawn per second in Unity.
public class SetFPSScript : MonoBehaviour
{
    public Text fps;
    int m_frameCounter = 0;
    float m_timeCounter = 0.0f;
    float m_lastFramerate = 0.0f;
    public float m_refreshTime = 0.5f;

    // Update is called once per frame
    void Update ()
    {
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
        SetFPS();
    }

    void SetFPS()
    {
        fps.text = "FPS: " + m_lastFramerate.ToString();
    }
}
