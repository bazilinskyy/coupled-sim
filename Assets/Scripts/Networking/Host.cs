using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Varjo;


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
    HMIManager _hmiManager;
    VisualSyncManager _visualSyncManager;
    SceneSelector _SceneSelector;

    List<int> _playerRoles = new List<int>();
    bool[] _playerReadyStatus = new bool[UNetConfig.MaxPlayers];
    float _lastPoseUpdateSent;
    int _selectedExperiment;
    const float PoseUpdateInterval = 0.01f;

    TrafficLightsSystem _lights;

    public bool gameStarted = false; // test

    public Host(LevelManager levelManager, PlayerSystem playerSys, AICarSyncSystem aiCarSystem, WorldLogger logger, WorldLogger fixedLogger)
    {
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

        // Sceneselector
        _SceneSelector = new SceneSelector(_lvlManager);
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
        //Debug.LogError($"Netstate = {_currentState}");
        if (_currentState == NetState.InGame)
        {
            _fixedTimeLogger.LogFrame(0, Time.fixedTime);
        }

        // Set AV behaviour
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
                            _playerSys.ActivatePlayerAICar();
                            _host.BroadcastReliable(new AllReadyMsg());
                            _transitionPhase = TransitionPhase.None;
                            _currentState = NetState.InGame;
                            var roleName = _lvlManager.ActiveExperiment.Roles[_playerRoles[Host.PlayerId]].Name;

                            // Do not run update if the application is not visible
                            /*if(VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID)
                            {
                                VarjoPlugin.RequestGazeCalibration(); // initiate eye-tracking
                            }*/

                            _logger.BeginLog($"HostLog-{roleName}-", _lvlManager.ActiveExperiment, _lights, Time.realtimeSinceStartup);
                            _fixedTimeLogger.BeginLog($"HostFixedTimeLog-{roleName}-", _lvlManager.ActiveExperiment, _lights, Time.fixedTime);
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
        Debug.LogError("Entered SelectRoleGUI");
        GUILayout.BeginHorizontal();
        string playerName = player == Host.PlayerId ? "Host" : $"Player {player}";
        //GUILayout.Label($"{playerName} role: {host._playerRoles[player]}");

        //test
        GUILayout.Label($"{playerName} role: {roles[Role].Name}");
        host._playerRoles[player] = Role;
        
        /*for (int i = 0; i < roles.Length; i++)
        {
            if (GUILayout.Button(roles[i].Name))
            {
                host._playerRoles[player] = i;
            }
        }*/
        GUILayout.EndHorizontal();
    }

    //displays role selection GUI
    void PlayerRolesGUI()
    {
        Debug.LogError("Entered playerRolesGUI");
        var roles = _lvlManager.Experiments[_selectedExperiment].Roles;
        SelectRoleGUI(Host.PlayerId, this, roles, PersistentManager.Instance.hostRole);
        ForEachConnectedPlayer((player, host) => SelectRoleGUI(player, host, roles, PersistentManager.Instance.clientRole));
    }

    //initializes experiment - sets it up locally and broadcasts experiment configuration message
    void StartGame()
    {
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
        GUILayout.Label($"Host mode: {_currentState}");
        GUILayout.Label("Connected: " + _host.NumRemotePlayers);
        switch (_currentState)
        {
            case NetState.Lobby:
            {
                //GUI.enabled = AllRolesSelected();
                    // Remove button during build
                /*if (GUILayout.Button("Start Game"))
                {
                    StartGame();
                }*/

                GUI.enabled = true;
                    //GUILayout.Label("Experiment:");
                    // test: select experiment definition via sceneselector
                    
                    // ERROR:
                    //GUILayout.Label($"Experiment: {_lvlManager.Experiments[_SceneSelector.sceneSelect].Name}"); //GUILayout.Label($"Experiment: {_lvlManager.Experiments[_SceneSelector.getSceneSelect()].Name}");
                    _selectedExperiment = _SceneSelector.sceneSelect; //_SceneSelector.getSceneSelect();
                    
                /*for (int i = 0; i < _lvlManager.Experiments.Length; i++)
                {
                    if (GUILayout.Button(_lvlManager.Experiments[i].Name + (i == _selectedExperiment ? " <--" : "")))
                    {
                        _selectedExperiment = i;
                    }
                }*/
                PlayerRolesGUI();
                _playerSys.SelectModeGUI();

                    // Automatically start game after selecting host (add wait for client)
                    if(gameStarted == false && AllRolesSelected())
                    {
                        StartGame();
                        gameStarted = true;
                    }
                break;
            }
            case NetState.InGame:
            {
                    if(PersistentManager.Instance.nextScene == true)
                    {
                        _currentState = NetState.Lobby;
                        PersistentManager.Instance.nextScene = false;
                        gameStarted = false;
                    }
                // commented out eHMI GUI since it's not needed
                /*if (_playerSys.eHMIFixed == false)
                {
                    _hmiManager.DoHostGUI(this);
                    _visualSyncManager.DoHostGUI(this);
                }*/
            }
            break;
        }
    }
}
