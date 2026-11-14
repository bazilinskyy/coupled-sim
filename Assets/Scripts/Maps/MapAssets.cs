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
    /// <summary>URP Lit with normal, parallax and emission maps: façade materials copy its shader variant (kept in builds).</summary>
    public Material FacadeTemplate;
    [Header("Photo textures of walls (CC0, ambientCG), as detail maps on the façades")]
    [Tooltip("In the order of the wall kinds: red brick, dark brick, yellow brick, plaster, concrete. Each scaled to a mean of mid grey: the wall's colour stays, the photo adds bricks, mortar and stains.")]
    public Texture2D[] WallDetail;
    public Texture2D[] WallNormal;
    [Tooltip("Metres of wall one texture covers (width, height).")]
    public Vector2[] WallDetailSize;
    public GameObject StreetLamp;
    public GameObject[] Trees;
    [Header("Street furniture and parked vehicles (MapPropBuilder)")]
    public GameObject Bench;
    public GameObject WasteBin;
    public GameObject[] ParkedBicycles;
    public GameObject[] ParkedCars;
    [Header("Traffic lights on city maps")]
    public GameObject CarSignal;
    public GameObject PedestrianSignal;

    static MapAssets _instance;
    public static MapAssets Instance => _instance != null ? _instance : (_instance = Resources.Load<MapAssets>("MapAssets"));
}
