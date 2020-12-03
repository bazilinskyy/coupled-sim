using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class DebugCaller : MonoBehaviour
{
    private float lastTime = 0f;
    public  List<float> times = new List<float>();
    public List<string> strings = new List<string>();
    public void DebugThis(string name, dynamic value)
    {
        if (!strings.Contains(name)) { strings.Add(name); times.Add(0f); }
        int index = strings.IndexOf(name);
        float time = times[index];
        if (time + 0.5f < Time.time) { Debug.Log($"{name} = {value}..."); times[index] = Time.time; }
    }
}
