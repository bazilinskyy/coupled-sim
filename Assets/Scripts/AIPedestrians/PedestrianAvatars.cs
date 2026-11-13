using UnityEngine;

/// <summary>Pedestrian avatars spawned at run time (PedestrianGroup); Resources/PedestrianAvatars.asset,
/// made by Coupled Sim > Pedestrians > Build avatar list (PedestrianAvatarsBuilder) from Assets/Prefabs/AIPedestrians.</summary>
[CreateAssetMenu(menuName = "Coupled Sim/Pedestrian avatars")]
public class PedestrianAvatars : ScriptableObject
{
    public GameObject[] Prefabs = new GameObject[0];
}
