using UnityEditor;
using UnityEngine;

/// <summary>Adds RealisticVehicleDynamics to the drivable car prefabs. Menu: Coupled Sim ➡️ Vehicles ➡️ Set up realistic physics.</summary>
public static class VehicleSetup
{
    [MenuItem("Coupled Sim/Vehicles/Set up realistic physics")]
    public static void SetUp()
    {
        Add("Assets/Prefabs/Cars/DrivableSmartCommon.prefab", RealisticVehicleDynamics.CarPreset.SmartFortwo);
        Add("Assets/Prefabs/Cars/DrivableSmartCommon-no_driver.prefab", RealisticVehicleDynamics.CarPreset.SmartFortwo);
        Add("Assets/Prefabs/Cars/DrivableHatchbackCommon.prefab", RealisticVehicleDynamics.CarPreset.Hatchback);
        AssetDatabase.SaveAssets();
    }

    static void Add(string path, RealisticVehicleDynamics.CarPreset preset)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!root.TryGetComponent<RealisticVehicleDynamics>(out var d)) d = root.AddComponent<RealisticVehicleDynamics>();
            d.Car = preset;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("VehicleSetup: " + path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
