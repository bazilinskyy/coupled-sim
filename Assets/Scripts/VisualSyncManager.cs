using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualSyncManager : MonoBehaviour
{

    public void DoHostGUI(Host host)
    {
        if (GUILayout.Button($"Visual syncing"))
        {
            host.BroadcastMessage(new VisualSyncMessage());
            DisplayMarker();
        }
    }

    public void DisplayMarker()
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }
        coroutine = ShowMarkerCoroutine();
        StartCoroutine(coroutine);
    }

    IEnumerator coroutine;

    IEnumerator ShowMarkerCoroutine()
    {
        GetComponent<Renderer>().enabled = true;
        yield return new WaitForSeconds(1f);
        GetComponent<Renderer>().enabled = false;
    }
}
