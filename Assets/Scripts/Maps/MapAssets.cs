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
    /// <summary>The water's material (CoupledSim/Water): the map's water copies it (and the shader is kept in builds).</summary>
    public Material WaterTemplate;
    [Header("Photo textures of walls (CC0, ambientCG), as detail maps on the façades")]
    [Tooltip("In the order of the wall kinds: red brick, dark brick, yellow brick, plaster, concrete. Each scaled to a mean of mid grey: the wall's colour stays, the photo adds bricks, mortar and stains.")]
    public Texture2D[] WallDetail;
    public Texture2D[] WallNormal;
    [Tooltip("Metres of wall one texture covers (width, height).")]
    public Vector2[] WallDetailSize;
    [Header("Photo textures of the streets' surfaces (CC0, Poly Haven) and the lawn")]
    [Tooltip("Names: asphalt, asphalt_light, cycle_red, bricks, parking, paving, cobbles, gravel, lawn (MapSetup.AddGroundTextures).")]
    public string[] GroundNames;
    public Texture2D[] GroundColour;
    public Texture2D[] GroundNormal;
    [Tooltip("Metres of ground one texture covers.")]
    public float[] GroundSize;
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
