﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;


//sets up a scene and players avatars based on experiment definition
public class LevelManager
{
    public PlayerSystem _playerSystem;
    public ExperimentDefinition[] Experiments;
    [NonSerialized]
    public ExperimentDefinition ActiveExperiment;
    private AsyncOperation _mainLoadOp;
    private int _localPlayerIdx;
    private List<int> _roles;


    public LevelManager(PlayerSystem playerSys, ExperimentDefinition[] experiments)
    {
        _playerSystem = playerSys;
        Experiments = experiments;
    }


    public bool Loading => _mainLoadOp != null && !_mainLoadOp.isDone;


    public void LoadLevelWithLocalPlayer(int experiment, int localPlayerIdx, List<int> roles, NetworkingManager.Trial trial)
    {
        var expPrefab = Experiments[experiment];
        _mainLoadOp = SceneManager.LoadSceneAsync(expPrefab.Scene);

        _mainLoadOp.completed += op =>
        {
            ActiveExperiment = Object.Instantiate(expPrefab);

            for (var player = 0; player < roles.Count; player++)
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
            
            foreach (IExperimentModifier em in ActiveExperiment.GetComponentsInChildren(typeof(IExperimentModifier)))
            {
                em.SetParameter(trial.experimentParameters);
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
        var result = currentTrialIndex + "_" + Experiments[currentTrial.experimentIndex].ShortName + "_roleIdx-" + currentTrial.roleIndex;

        foreach (var param in currentTrial.experimentParameters)
        {
            result += "_" + param.name + "-" + param.value;
        }

        return result + DateTime.Now.ToString("_yy-MM-dd_hh-mm");
    }
    #pragma warning disable 0414
    private bool _mainLoaded = false;
    private bool _expLoaded = false;
    #pragma warning restore 0414
}