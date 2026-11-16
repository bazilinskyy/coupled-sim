using UnityEngine;

/// <summary>
/// In a replay: where a vehicle's driver looks from, relative to the vehicle, taken from its own camera when the
/// replay places it. The participant's own view in the replay uses it, so it stays on the driver's seat whatever moves
/// the camera's parents later (the replay poses the driver's figure, whose head may carry the camera).
/// </summary>
public class ReplayEye : MonoBehaviour
{
    public Vector3 LocalPosition;
    public float LocalYaw;
}
