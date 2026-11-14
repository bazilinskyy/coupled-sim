using UnityEngine;

/// <summary>
/// Holds the meshes the vehicle builder makes from a model (the body merged per material, the
/// wheels cut out of it, the rear section of an articulated vehicle) as sub-assets, saved in binary
/// so they take little room in the repository.
/// </summary>
[PreferBinarySerialization]
public class VehicleMeshData : ScriptableObject
{
    public string Source;
}
