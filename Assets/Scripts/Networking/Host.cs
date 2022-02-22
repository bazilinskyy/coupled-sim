﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

// high level host networking script
public class Host : NetworkSystem
{
    public const int PlayerId = 0;
    UNetHost _host;
    MessageDispatcher _msgDispatcher;
    LevelManager _lvlManager;
    PlayerSystem _playerSys;
    AICarSyncSystem _aiCarSystem;
    WorldLogger _logger;
    WorldLogger _fixedTimeLogger;
    WorldLogger _fixedLogger;
    HMIManager _hmiManager;
    VisualSyncManager _visualSyncManager;
    SceneChange _SceneChange;

    List<int> _playerRoles = new List<int>();
    bool[] _playerReadyStatus = new bool[UNetConfig.MaxPlayers];
    float _lastPoseUpdateSent;
    int _selectedExperiment;
    const float PoseUpdateInterval = 0.01f;

    TrafficLightsSystem _lights;

    public bool gameStarted = false;

    InstantStartHostParameters _instantStartParams;

    public Host(LevelManager levelManager, PlayerSystem playerSys, AICarSyncSystem aiCarSystem, WorldLogger logger, WorldLogger fixedLogger, InstantStartHostParameters instantStartParams)
    {
        _instantStartParams = instantStartParams;
        _playerSys = playerSys;
        _lvlManager = levelManager;
        _aiCarSystem = aiCarSystem;
        _logger = logger;
        _fixedTimeLogger = fixedLogger;
        _host = new UNetHost();
        _host.Init();

        _hmiManager = GameObject.FindObjectOfType<HMIManager>();
        Assert.IsNotNull(_hmiManager, "Missing HMI manager");
        _visualSyncManager = GameObject.FindObjectOfType<VisualSyncManager>();
        Assert.IsNotNull(_visualSyncManager, "Missing VS manager");

        _currentState = NetState.Lobby;
        _msgDispatcher = new MessageDispatcher();

        //set up message handlers
        _msgDispatcher.AddStaticHandler((int)MsgId.C_UpdatePose, OnClientPoseUpdate);
        _msgDispatcher.AddStaticHandler((int)MsgId.C_Ready, OnClientReady);
        _msgDispatcher.AddStaticHandler((int)MsgId.B_Ping, OnPing);
        _hmiManager.InitHost(_host, _msgDispatcher);
        aiCarSystem.InitHost(_host);
        for (int i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            _playerRoles.Add(-1);
        }
    }

    //handles ping message
    private void OnPing(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<PingMsg>(sync);
        _host.SendUnreliableToPlayer(msg, srcPlayerId);
    }
    
    //handles client ready message
    void OnClientReady(ISynchronizer sync, int player)
    {
        NetMsg.Read<ReadyMsg>(sync);
        _playerReadyStatus[player] = true;
    }

    //applies pose updates received from other players
    void OnClientPoseUpdate(ISynchronizer sync, int player)
    {
        var msg = NetMsg.Read<UpdateClientPose>(sync);
        _playerSys.GetAvatar(player).ApplyPose(msg.Pose);
    }

    public void BroadcastMessage<T>(T msg) where T : INetMessage 
        => _host.BroadcastReliable(msg);

    public override void FixedUpdate()
    {
        if (_currentState == NetState.InGame)
        {
            _fixedTimeLogger.LogFrame(0, Time.fixedTime);
        }
    }

    public override void Update()
    {
        switch (_currentState)
        {
            case NetState.Disconnected:
                break;
            case NetState.Lobby:
                _host.Update(_msgDispatcher);
                switch (_transitionPhase)
                {
                    case TransitionPhase.None:
                        break;
                    case TransitionPhase.LoadingLevel:
                        if (!_lvlManager.Loading)
                        {
                            _transitionPhase = TransitionPhase.WaitingForAwakes;
                        }
                        break;
                    case TransitionPhase.WaitingForAwakes:
                        _playerReadyStatus[Host.PlayerId] = true;
                        if (AllReady())
                        {
                            _lights = GameObject.FindObjectOfType<TrafficLightsSystem>();
                            foreach (var carSpawner in _lvlManager.ActiveExperiment.CarSpawners)
                            {
                                carSpawner.Init(_aiCarSystem);
                            }
                            ExperimentRoleDefinition experimentRoleDefinition = _lvlManager.ActiveExperiment.Roles[_playerRoles[Host.PlayerId]];
                            if (experimentRoleDefinition.AutonomousPath != null) {
                                _playerSys.ActivatePlayerAICar();
                            }
                            _host.BroadcastReliable(new AllReadyMsg());
                            _transitionPhase = TransitionPhase.None;
                            _currentState = NetState.InGame;
                            var roleName = experimentRoleDefinition.Name;
                            _logger.BeginLog($"HostLog-{roleName}-", _lvlManager.ActiveExperiment, _lights, Time.realtimeSinceStartup, true);
                            _fixedTimeLogger.BeginLog($"HostFixedTimeLog-{roleName}-", _lvlManager.ActiveExperiment, _lights, Time.fixedTime, false);
                        }
                        break;
                }
                break;
            case NetState.InGame:
                _host.Update(_msgDispatcher);
                UpdateGame();
                _logger.LogFrame(0, Time.realtimeSinceStartup);
                break;
        }
    }

    bool AllReady()
    {
        for (int i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            if (!_playerReadyStatus[i] && _playerRoles[i] != -1)
            {
                return false;
            }
        }
        return true;
    }

    void ForEachConnectedPlayer(Action<int, Host> action)
    {
        for (int i = 1; i < UNetConfig.MaxPlayers; i++)
        {
            if (_host.PlayerConnected(i))
            {
                action(i, this);
            }
        }
    }

    //displays role selection GUI for a single player
    static void SelectRoleGUI(int player, Host host, ExperimentRoleDefinition[] roles, int Role)
    {
        if (host._instantStartParams.instantStart)
        {
            host._playerRoles[player] = host._instantStartParams.instantStartRole;
        }
        else {
            //GUILayout.BeginHorizontal();
            string playerName = player == Host.PlayerId ? "Host" : $"Player {player}";
            //GUILayout.Label($"{playerName} role: {host._playerRoles[player]}");

            // Automatic selection of roles
            //GUILayout.Label($"{playerName} role: {roles[Role].Name}");
            host._playerRoles[player] = Role;

            // For manual selection of roles per host and client
            /*for (int i = 0; i < roles.Length; i++)
            {
                if (GUILayout.Button(roles[i].Name))
                {
                    host._playerRoles[player] = i;
                }
            }*/
            //GUILayout.EndHorizontal();
        }
    }

    //displays role selection GUI
    void PlayerRolesGUI()
    {
        var roles = _lvlManager.Experiments[_selectedExperiment].Roles;
        SelectRoleGUI(Host.PlayerId, this, roles, PersistentManager.Instance.hostRole);
        ForEachConnectedPlayer((player, host) => SelectRoleGUI(player, host, roles, PersistentManager.Instance.clientRole));
    }

    //initializes experiment - sets it up locally and broadcasts experiment configuration message
    void StartGame()
    {
        Debug.LogError("setting TrialStarted to true");
        PersistentManager.Instance.TrialStarted = true;

        Debug.LogError("setting gameStarted to true");
        gameStarted = true;

        _msgDispatcher.ClearLevelMessageHandlers();
        _host.BroadcastReliable(new StartGameMsg
        {
            Roles = _playerRoles,
            Experiment = _selectedExperiment
        });
        for (int i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            _playerReadyStatus[i] = false;
        }
        if (_playerRoles[Host.PlayerId] == -1)
        {
            _lvlManager.LoadLevelNoLocalPlayer(_selectedExperiment, _playerRoles);
        }
        else
        {
            _lvlManager.LoadLevelWithLocalPlayer(_selectedExperiment, 0, _playerRoles);
        }
        _transitionPhase = TransitionPhase.LoadingLevel;
    }
    
    void UpdateGame()
    {
        if (Time.realtimeSinceStartup - _lastPoseUpdateSent > PoseUpdateInterval)
        {
            _host.BroadcastUnreliable(new UpdatePoses
            {
                Poses = _playerSys.GatherPoses()
            });
            _lastPoseUpdateSent = Time.realtimeSinceStartup;
        }
        _lights?.UpdateHost(_host);
        _aiCarSystem.UpdateHost();
    }

    bool failRoleCheck = false;
    bool[] _activeRoles = new bool[UNetConfig.MaxPlayers];
    void CheckRoleConnected(int player, Host host)
    {
        var role = _playerRoles[player];
        if (role < 0 || role >= _lvlManager.Experiments[_selectedExperiment].Roles.Length)
        {
            failRoleCheck = true;
            return;
        }
        if (_activeRoles[role])
        {
            failRoleCheck = true;
        }
        else
        {
            _activeRoles[role] = true;
        }
    }
    bool AllRolesSelected()
    {
        for (int i = 0; i < _activeRoles.Length; i++)
        {
            _activeRoles[i] = false;
        }
        failRoleCheck = false;
        CheckRoleConnected(Host.PlayerId, this);
        ForEachConnectedPlayer(CheckRoleConnected);
        return !failRoleCheck;
    }

    //displays host GUI
    public override void OnGUI()
    {
        if (!_instantStartParams.instantStart)
        {
            GUILayout.Label($"Host mode: {_currentState}");
            GUILayout.Label("Connected: " + _host.NumRemotePlayers);
        } else
        {
            _selectedExperiment = _instantStartParams.instantStartExperiment;
        }
        switch (_currentState)
        {
            case NetState.Lobby:
            {
                PersistentManager.Instance.switchScene = false;
                PersistentManager.Instance.doOnlyOnce = false;

                GUI.enabled = AllRolesSelected();
                // First trial?
                if (PersistentManager.Instance.TrialStarted == false)
                {
                    if (GUILayout.Button("Start Game"))
                    {
                        StartGame();
                    }
                }
                GUI.enabled = true;

                /*if (gameStarted == true)
                {
                    gameStarted = false;
                }*/
                // Don't show all the experiments to choose from
                /*if (!_instantStartParams.instantStart) {
                    GUILayout.Label("Experiment:");
                    for (int i = 0; i < _lvlManager.Experiments.Length; i++)
                    {
                        if (GUILayout.Button(_lvlManager.Experiments[i].Name + (i == _selectedExperiment ? " <--" : "")))
                        {
                            _selectedExperiment = i;
                        }
                    }
                }*/
                
                PersistentManager.Instance.ExpDefNr = PersistentManager.Instance.ExpOrder[PersistentManager.Instance.ListNr];
                PersistentManager.Instance.OrderNr = PersistentManager.Instance.ListNr;
                _selectedExperiment = PersistentManager.Instance.ExpDefNr;

                PlayerRolesGUI();
                _playerSys.SelectModeGUI();
                if (gameStarted == false && AllRolesSelected() == true && PersistentManager.Instance.TrialStarted == true)
                {
                    StartGame();
                }
                break;
            }
            case NetState.InGame:
            {
                if (PersistentManager.Instance.switchScene == true)
                {
                    Debug.LogError("Setting stopLogging to true");
                    // This triggers in NetworkingManager to stop logging and destroy players and cars
                    PersistentManager.Instance.stopLogging = true;
                    
                    Debug.LogError("Setting gameStarted to false");
                    gameStarted = false;

                    if (PersistentManager.Instance.doOnlyOnce == false)
                    {
                        Debug.LogError("Increment ListNr and TrialNr by 1");
                        ++PersistentManager.Instance.ListNr;
                        ++PersistentManager.Instance.TrialNr;
                        if (PersistentManager.Instance.TrialNr > 5 || PersistentManager.Instance.ExpDefNr == PersistentManager.Instance.QuestionnaireScene)
                        {
                            PersistentManager.Instance.TrialNr = 0;
                        }
                        PersistentManager.Instance.doOnlyOnce = true;
                    }
                    // Quit game if end has been reached
                    if (PersistentManager.Instance.ListNr > PersistentManager.Instance.ExpOrder.Count - 1)
                    {
                        UnityEditor.EditorApplication.isPlaying = false;
                        //Application.Quit();
                    }
                    else
                    {
                        Debug.LogError("Switching from InGame to Lobby");
                        _currentState = NetState.Lobby;
                    }
                    
                }
                //_hmiManager.DoHostGUI(this);
                //_visualSyncManager.DoHostGUI(this);
            }
            break;
        }
    }
}
