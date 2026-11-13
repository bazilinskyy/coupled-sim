using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns an AI car (spawnParams.Car, which must be in AI Car System ➡️ Prefabs) at the start of a
/// path file and drives it along it (see <see cref="ScriptedPath"/>): set speed profiles or
/// recorded trajectories (x, z, speed, t) instead of waypoints in the scene.
/// </summary>
public class ScriptedPathSpawner : CarSpawnerBase
{
    [Tooltip("CSV with x, z (y) and optionally speed (km/h) and t (s); as given or in StreamingAssets/Paths.")]
    public string PathFile = "example_path.csv";
    [Tooltip("Seconds after the start of the trial.")]
    public float Delay = 0f;
    public float DefaultSpeedKmh = 30f;
    public float MaxAcceleration = 2f, MaxDeceleration = 3f;

    protected override bool HasValidSpawnParams => spawnParams.Car != null && ScriptedPath.Resolve(PathFile) != null;

    protected override IEnumerator SpawnCoroutine()
    {
        if (Delay > 0f) yield return new WaitForSeconds(Delay);
        ScriptedPath path;
        try { path = ScriptedPath.Load(PathFile, transform.position.y); }
        catch (System.Exception e) { Debug.LogError($"ScriptedPathSpawner {name}: {e.Message}", this); yield break; }
        var circuit = path.BuildCircuit(name + " path");
        var start = new GameObject(name + " start").transform;
        start.position = path.Points[0];
        var dir = path.Points[1] - path.Points[0]; dir.y = 0;
        start.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward);
        var p = spawnParams;
        p.SpawnPoint = start;
        p.Track = circuit;
        var car = Spawn(p, false);
        car.GetComponent<UnityStandardAssets.Utility.WaypointProgressTracker>().SetProgress(ScriptedPath.LeadIn);
        var driver = car.gameObject.AddComponent<ScriptedPathDriver>();
        driver.DefaultSpeedKmh = DefaultSpeedKmh;
        driver.MaxAcceleration = MaxAcceleration;
        driver.MaxDeceleration = MaxDeceleration;
        driver.Init(path);
    }
}
