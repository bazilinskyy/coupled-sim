using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Should broadcast scene loading network message.
// Host first loads new scene after which the client loads the new scene too
public class SceneNetworkManager : MonoBehaviour
{
    // Function to broadcast the message from host to client
    public void SendLoadMessage(Host host)
    {
        host.BroadcastMessage(new LoadSceneMessage());
        PersistentManager.Instance.stopLogging = true;
        PersistentManager.Instance.nextScene = true;
    }

    public void SendEndGameMessage(Host host)
    {
        host.BroadcastMessage(new EndGameMessage());
    }

    // Action to be taken by Client
    public void ClientLoadScene()
    {
        SceneManager.LoadSceneAsync("StartScene");
    }

    public void ClientEndGame()
    {
        //Application.Quit(); // build version
        UnityEditor.EditorApplication.isPlaying = false; // editor version
    }
}
