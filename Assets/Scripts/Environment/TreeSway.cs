using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trees of a city map sway in the wind of <see cref="EnvironmentController"/> (WindSpeed, WindDirection): each leans
/// downwind by an amount that grows with the square of the wind speed and swings around it at its own slow pace
/// (bigger trees slower), with gusts. Only the trees' rotations change (about their foot), so it costs little.
/// </summary>
public class TreeSway : MonoBehaviour
{
    public List<Transform> Trees = new List<Transform>();
    Quaternion[] _base;
    Vector3[] _pos;
    float[] _phase, _freq;
    float _nextFarUpdate;
    Vector3 _lastFarWind = new Vector3(float.NaN, 0f, 0f);

    void Start()
    {
        _base = new Quaternion[Trees.Count]; _pos = new Vector3[Trees.Count]; _phase = new float[Trees.Count]; _freq = new float[Trees.Count];
        var rng = new System.Random(Trees.Count);
        for (int i = 0; i < Trees.Count; i++)
        {
            if (Trees[i] == null) continue;
            _base[i] = Trees[i].rotation;
            _pos[i] = Trees[i].position;
            _phase[i] = (float)rng.NextDouble() * 100f;
            // natural sway of a tree: about 0.2-0.5 Hz, slower for taller trees
            // (by the tree's height: 4 m to 20 m and taller)
            var r = Trees[i].GetComponentInChildren<Renderer>();
            float height = r != null ? r.bounds.size.y : 10f * Trees[i].lossyScale.y;
            _freq[i] = Mathf.Lerp(0.5f, 0.22f, Mathf.InverseLerp(4f, 20f, height)) * (0.9f + 0.2f * (float)rng.NextDouble());
        }
    }

    /// <summary>Lean (degrees) of a tree's top in a wind of <paramref name="speed"/> m/s, without the swings.</summary>
    public static float Lean(float speed) => Mathf.Min(6f, 0.012f * speed * speed);

    void Update()
    {
        var env = EnvironmentController.Instance;
        if (env == null || _base == null) return;
        var wind = env.Wind;
        float speed = wind.magnitude;
        if (speed < 0.3f) return;
        var dir = wind / speed;
        float lean = Lean(speed), t = Time.time;
        var cam = Camera.main;
        bool hasCam = cam != null;
        var camPos = hasCam ? cam.transform.position : Vector3.zero;
        const float nearDist2 = 140f * 140f;
        bool updateFar = !hasCam || (t >= _nextFarUpdate && (float.IsNaN(_lastFarWind.x) || (wind - _lastFarWind).sqrMagnitude > 0.25f));
        if (updateFar)
        {
            _nextFarUpdate = t + 0.5f;
            _lastFarWind = wind;
        }
        for (int i = 0; i < Trees.Count; i++)
        {
            if (hasCam && !updateFar)
            {
                float dx = _pos[i].x - camPos.x, dz = _pos[i].z - camPos.z;
                if (dx * dx + dz * dz > nearDist2) continue;
            }
            var tr = Trees[i];
            if (tr == null) continue;
            float gust = Mathf.PerlinNoise(t * 0.15f + _phase[i] * 0.01f, _phase[i]) * 1.2f;
            float deg = lean * (0.6f + gust) + (0.15f + lean * 0.35f) * Mathf.Sin(t * _freq[i] * 2f * Mathf.PI + _phase[i]);
            var up = (Vector3.up + dir * Mathf.Tan(deg * Mathf.Deg2Rad)).normalized;
            tr.rotation = Quaternion.FromToRotation(Vector3.up, up) * _base[i];
        }
    }
}
