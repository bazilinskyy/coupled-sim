using UnityEngine;

/// <summary>
/// Assets the map generator uses (Resources/MapAssets): a URP Lit material to copy (so the shader is
/// in builds), street lamp and tree prefabs.
/// </summary>
[CreateAssetMenu(menuName = "Coupled Sim/Map assets")]
public class MapAssets : ScriptableObject
{
    public Material LitTemplate;
    public GameObject StreetLamp;
    public GameObject[] Trees;

    static MapAssets _instance;
    public static MapAssets Instance => _instance != null ? _instance : (_instance = Resources.Load<MapAssets>("MapAssets"));
}
