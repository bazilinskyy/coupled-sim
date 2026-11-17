using UnityEngine;

/// <summary>Moves the ripples of the map's water (its normal map) slowly with the wind.</summary>
public class WaterRipples : MonoBehaviour
{
    public Vector2 Speed = new Vector2(0.012f, 0.007f);   // texture repeats per second (one repeat is 2.5 m)
    Material _m;

    void Start() { var r = GetComponent<Renderer>(); if (r != null) _m = r.sharedMaterial; }

    void Update()
    {
        if (_m != null) _m.SetTextureOffset("_BaseMap", Speed * Time.time);   // (URP Lit: all its maps follow the base map's offset)
    }
}
