using UnityEngine;

/// <summary>
/// A spawn point in a map experiment: when the experiment is placed in a city map, it moves to a
/// suitable place of that map (a driving lane, the road edge, a cycle path or a pavement near the
/// centre), so the same experiment works in every city.
/// </summary>
public class OsmSpawnPoint : MonoBehaviour
{
    public OsmMap.SpawnKind Kind = OsmMap.SpawnKind.RoadLane;
    [Tooltip("0 = the nearest suitable place to the map centre, 1 = the next one, ...")]
    public int Index;

    void Awake() => Resolve();

    public bool Resolve()
    {
        var map = OsmMap.Current;
        if (map == null || !map.FindSpawn(Kind, Index, out var p, out var r)) return false;
        transform.SetPositionAndRotation(p, r);
        return true;
    }
}
