using UnityEngine;

/// <summary>
/// Assets the map generator uses (Resources/MapAssets): a URP Lit material to copy (so the shader is
/// in builds), street lamp and tree prefabs.
/// </summary>
[CreateAssetMenu(menuName = "Coupled Sim/Map assets")]
public class MapAssets : ScriptableObject
{
    public Material LitTemplate;
    [Tooltip("URP Lit with a detail map (_DETAIL_MULX2), so the variant is in builds: the aerial photo's fine grain up close.")]
    public Material DetailTemplate;
    public GameObject StreetLamp;
    public GameObject[] Trees;
    [Header("Street furniture and parked vehicles (MapPropBuilder)")]
    public GameObject Bench;
    public GameObject WasteBin;
    public GameObject[] ParkedBicycles;
    public GameObject[] ParkedCars;

    static MapAssets _instance;
    public static MapAssets Instance => _instance != null ? _instance : (_instance = Resources.Load<MapAssets>("MapAssets"));
}
