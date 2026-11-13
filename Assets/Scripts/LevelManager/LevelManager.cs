using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//sets up a scene and players avatars based on experiment definition
public class LevelManager
{
    public PlayerSystem _playerSystem;
    public ExperimentDefinition[] Experiments;
    [NonSerialized]
    public ExperimentDefinition ActiveExperiment;
    public bool Loading => _mainLoadOp != null && !_mainLoadOp.isDone;
    AsyncOperation _mainLoadOp;

    public LevelManager(PlayerSystem playerSys, ExperimentDefinition[] experiments)
    {
        _playerSystem = playerSys;
        Experiments = experiments;
    }
    #pragma warning disable 0414
    bool _mainLoaded = false;
    bool _expLoaded = false;
    #pragma warning restore 0414
    int _localPlayerIdx;
    List<int> _roles;


    /// <summary>Map chosen by the host for map experiments (index in MapCatalog; -1 = the experiment's own).</summary>
    public int MapOverride = -1;

    /// <summary>City map to build for this experiment and trial: trial parameter "map", then MapOverride, then the experiment's Map.</summary>
    /// <summary>A place chosen by coordinates ("lat,lon[,radius]"), sent by the host; null = none.</summary>
    public string LocationOverride;

    public string MapFor(int experiment, NetworkingManager.Trial trial)
    {
        var exp = Experiments[experiment];
        if (string.IsNullOrEmpty(exp.Map)) return null;
        if (trial?.experimentParameters != null)
            foreach (var p in trial.experimentParameters)
            {
                if (p.name.ToLower() == "map" && MapCatalog.IndexOf(p.value) >= 0) return MapCatalog.Maps[MapCatalog.IndexOf(p.value)].Name;
                if (p.name.ToLower() == "map" && OsmMap.IsRoadNetworkFile(p.value)) return p.value;
                if (p.name.ToLower() == "location" && OsmMap.TryParseLocation(p.value, out _, out _, out _)) return p.value;
            }
        if (!string.IsNullOrEmpty(LocationOverride)) return LocationOverride;
        if (MapOverride >= 0 && MapOverride < MapCatalog.Maps.Length) return MapCatalog.Maps[MapOverride].Name;
        return exp.Map;
    }

    public void LoadLevelWithLocalPlayer(int experiment, int localPlayerIdx, List<int> roles, NetworkingManager.Trial trial)
    {
        var expPrefab = Experiments[experiment];
        var map = MapFor(experiment, trial);
        OsmMap.Requested = map;
        if (map != null) Debug.Log($"Map: {map}");
        _mainLoadOp = SceneManager.LoadSceneAsync(map != null && string.IsNullOrEmpty(expPrefab.Scene) ? "OsmMap" : expPrefab.Scene);
        _mainLoadOp.completed += (op) =>
        {
            ActiveExperiment = GameObject.Instantiate(expPrefab);
            for (int player = 0; player < roles.Count; player++)
            {
                var roleIdx = roles[player];
                if (roleIdx != -1)
                {
                    var role = ActiveExperiment.Roles[roleIdx];
                    var spawn = role.SpawnPoint;
                    if (localPlayerIdx == player)
                    {
                        Debug.Log($"Exp: {experiment}, RoleIdx: {roleIdx}, role: {role.Name}");
                        _playerSystem.SpawnLocalPlayer(spawn, player, role);
                    }
                    else
                    {
                        _playerSystem.SpawnRemotePlayer(spawn, player, role);
                    }
                }
            }
            // Clients have no trial definition (trial == null): parameters only apply on the host.
            var parameters = trial?.experimentParameters ?? new NetworkingManager.ExperimentParameter[0];
            foreach(IExperimentModifier em in ActiveExperiment.GetComponentsInChildren(typeof(IExperimentModifier)))
            {
                em.SetParameter(parameters);
            }
            _mainLoadOp = null;
        };
    }

    public void LoadLevelNoLocalPlayer(int experiment, List<int> playerStartingPositions, NetworkingManager.Trial trial)
    {
        LoadLevelWithLocalPlayer(experiment, 0, playerStartingPositions, trial);
    }

    internal string GetFilename(NetworkingManager.Trial currentTrial, int currentTrialIndex)
    {
        string result = currentTrialIndex + "_" + Experiments[currentTrial.experimentIndex].ShortName + "_roleIdx-" + currentTrial.roleIndex;
        foreach(var param in currentTrial.experimentParameters)
        {
            result += ("_" + param.name + "-" + param.value);
        }
        return result + DateTime.Now.ToString("_yy-MM-dd_hh-mm");
    }
}
