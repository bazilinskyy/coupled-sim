using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Headlights that light the road at night: after dusk the nearest cars to the viewer (8 by default)
/// get two spot lights (low beam: about 40 m, aimed slightly down) and their front lights switched
/// on; the others keep only their glowing lamps. Called once a second by OsmMap (city maps) and
/// works on any scene through <see cref="Update"/>.
/// </summary>
public static class CarHeadlights
{
    public static int MaxCars = 8;
    public static float Range = 40f, SpotAngle = 70f, Intensity = 8f;

    static readonly Dictionary<Transform, Light[]> _lights = new Dictionary<Transform, Light[]>();
    static readonly List<Transform> _cars = new List<Transform>();

    /// <summary>Updates which cars have lit headlights; <paramref name="sunElevation"/> in degrees.</summary>
    public static void Update(float sunElevation)
    {
        bool night = sunElevation < 1f;
        var cam = Camera.main;
        var at = cam != null ? cam.transform.position : Vector3.zero;
        _cars.Clear();
        if (night)
        {
            foreach (var a in Object.FindObjectsByType<PlayerAvatar>()) if (a.Type == AvatarType.Driver) _cars.Add(a.transform);
            foreach (var c in Object.FindObjectsByType<TrafficCar>()) if (!_cars.Contains(c.transform)) _cars.Add(c.transform);
            _cars.Sort((x, y) => (x.position - at).sqrMagnitude.CompareTo((y.position - at).sqrMagnitude));
            if (_cars.Count > MaxCars) _cars.RemoveRange(MaxCars, _cars.Count - MaxCars);
        }
        var keep = new HashSet<Transform>(_cars);
        var drop = new List<Transform>();
        foreach (var kv in _lights) if (kv.Key == null || !keep.Contains(kv.Key)) drop.Add(kv.Key);
        foreach (var t in drop)
        {
            if (_lights.TryGetValue(t, out var ls)) foreach (var l in ls) if (l != null) Object.Destroy(l.gameObject);
            _lights.Remove(t);
        }
        foreach (var t in _cars)
        {
            if (_lights.ContainsKey(t)) continue;
            _lights[t] = Add(t);
            var avatar = t.GetComponent<PlayerAvatar>();
            if (avatar != null && avatar.frontLights != null) avatar.frontLights.SetActive(true);
        }
    }

    static Light[] Add(Transform car)
    {
        // the front of the car from its colliders (renderers include light beams)
        float front = 1.8f, halfWidth = 0.7f, height = 0.65f;
        foreach (var col in car.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || col is WheelCollider) continue;
            var b = col.bounds;
            var f = car.InverseTransformPoint(b.center + car.forward * b.extents.magnitude);
            front = Mathf.Max(front, Mathf.Min(f.z, 3f));
        }
        var lights = new Light[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(i == 0 ? "Headlight left" : "Headlight right");
            go.transform.SetParent(car, false);
            go.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * halfWidth, height, front);
            go.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = Range;
            l.spotAngle = SpotAngle;
            l.innerSpotAngle = SpotAngle * 0.5f;
            l.intensity = Intensity;
            l.color = new Color(1f, 0.96f, 0.88f);
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            lights[i] = l;
        }
        return lights;
    }
}
