using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Assertions;

// high level client networking script
// - handles messages recieved from the host 
// - displays client GUI
// - sends local player position updates
public class Client : NetworkSystem
{
    UNetClient _client;

    MessageDispatcher _msgDispatcher;
    LevelManager _lvlManager;
    PlayerSystem _playerSys;
    WorldLogger _logger;
    WorldLogger _fixedTimeLogger;
    HMIManager _hmiManager;
    VisualSyncManager _visualSyncManager;
    float _lastPoseUpdateSent;
    float _lastPingSent;
    const float PoseUpdateInterval = 0.01f;
    List<int> _roles;

    public Client(LevelManager lvlManager, PlayerSystem playerSys, AICarSyncSystem aiCarSystem, WorldLogger logger, WorldLogger fixedLogger)
    {
        _playerSys = playerSys;
        _lvlManager = lvlManager;
        _logger = logger;
        _fixedTimeLogger = fixedLogger;
        _client = new UNetClient();
        _client.Init();

        _hmiManager = GameObject.FindObjectOfType<HMIManager>();
        Assert.IsNotNull(_hmiManager, "Missing HMI manager");
        _visualSyncManager = GameObject.FindObjectOfType<VisualSyncManager>();
        Assert.IsNotNull(_visualSyncManager, "Missing VS manager");

        _currentState = NetState.Disconnected;
        _msgDispatcher = new MessageDispatcher();

        //set up message handlers
        _msgDispatcher.AddStaticHandler((int)MsgId.S_StartGame, OnGameStart);
        _msgDispatcher.AddStaticHandler((int)MsgId.S_UpdateClientPoses, OnUpdatePoses);
        _msgDispatcher.AddStaticHandler((int)MsgId.S_AllReady, OnAllReady);
        _msgDispatcher.AddStaticHandler((int)MsgId.S_VisualSync, OnCustomMessage);
        _msgDispatcher.AddStaticHandler((int)MsgId.B_Ping, HandlePing);
        _hmiManager.InitClient(_client, _msgDispatcher);
        aiCarSystem.InitClient(_msgDispatcher);
        _msgDispatcher.HandleConnect = () =>
        {
            Assert.AreEqual(_currentState, NetState.Client_Connecting);
            _currentState = NetState.Lobby;
        };
    }

    //visual syncing message handling
    private void OnCustomMessage(ISynchronizer sync, int srcPlayerId)
    {
        _visualSyncManager.DisplayMarker();
    }

    //handles "all players ready" message - starts the simulation, logging etc.
    private void OnAllReady(ISynchronizer sync, int srcPlayerId)
    {
        Debug.Log("AllReady");
        var lights = GameObject.FindObjectOfType<TrafficLightsSystem>();
        lights?.RegisterHandlers(_msgDispatcher);
        ExperimentRoleDefinition experimentRoleDefinition = _lvlManager.ActiveExperiment.Roles[_roles[_client.MyPlayerId]];
        if (experimentRoleDefinition.AutonomousPath != null) {
            _playerSys.ActivatePlayerAICar();
        }
        _currentState = NetState.InGame;
        var roleName = experimentRoleDefinition.Name;
        _logger.BeginLog($"ClientLog-{roleName}-", _lvlManager.ActiveExperiment, lights, Time.realtimeSinceStartup, true);
        _fixedTimeLogger.BeginLog($"ClientFixedTimeLog-{roleName}-", _lvlManager.ActiveExperiment, lights, Time.fixedTime, false);
    }
    //handles game configuration message - spawns level and players
    void OnGameStart(ISynchronizer sync, int _)
    {
        _msgDispatcher.ClearLevelMessageHandlers();
        var msg = NetMsg.Read<StartGameMsg>(sync);
        _lvlManager.LoadLevelWithLocalPlayer(msg.Experiment, _client.MyPlayerId, msg.Roles);
        _roles = msg.Roles;
        _transitionPhase = TransitionPhase.LoadingLevel;
    }
    //handles player position updates
    void OnUpdatePoses(ISynchronizer sync, int _)
    {
        var msg = NetMsg.Read<UpdatePoses>(sync);
        _playerSys.ApplyPoses(msg.Poses);
    }

    public override void FixedUpdate()
    {
        if (_currentState == NetState.InGame)
        {
            _fixedTimeLogger.LogFrame(_currentPing, Time.fixedTime);
        }
    }

    public override void Update()
    {
        switch (_currentState)
        {
            case NetState.Disconnected:
                break;
            case NetState.Client_Connecting:
            {
                _client.Update(_msgDispatcher);
                break;
            }
            case NetState.Lobby:
                _client.Update(_msgDispatcher);
                switch (_transitionPhase)
                {
                    case TransitionPhase.None: break;
                    case TransitionPhase.LoadingLevel:
                        if (!_lvlManager.Loading)
                        {
                            _transitionPhase = TransitionPhase.WaitingForAwakes;
                        }
                        break;
                    case TransitionPhase.WaitingForAwakes:
                        _client.SendReliable(new ReadyMsg());
                        _lvlManager.ActiveExperiment.AIPedestrians.InitClient(_msgDispatcher);
                        _transitionPhase = TransitionPhase.None;
                        break;
                }
                break;
            case NetState.InGame:
                _client.Update(_msgDispatcher);
                UpdateGame();
                _logger.LogFrame(_currentPing, Time.realtimeSinceStartup);
                break;
        }
    }

    int _pingId = 0;
    bool _lastPingRespondedTo = true;
    const float PingTimeout = 1;

    float _currentPing;

    //sends position of the local player and pings the server
    void UpdateGame()
    {
        if (Time.realtimeSinceStartup - _lastPoseUpdateSent > PoseUpdateInterval)
        {
            var msg = new UpdateClientPose
            {
                Pose = _playerSys.LocalPlayer.GetPose(),
            };
            _client.SendUnreliable(msg);
            _lastPoseUpdateSent = Time.realtimeSinceStartup;

            if (_lastPingRespondedTo || Time.realtimeSinceStartup - _lastPingSent > PingTimeout)
            {
                _client.SendUnreliable(new PingMsg { PingId = _pingId++, Timestamp = Time.realtimeSinceStartup });
                _lastPingSent = Time.realtimeSinceStartup;
            }
        }
    }

    //handles pingback message
    void HandlePing(ISynchronizer sync, int _)
    {
        var msg = NetMsg.Read<PingMsg>(sync);
        // make sure it's the ping we are waiting for and not some super late packet
        // that got lost on the wire
        if (msg.PingId == _pingId - 1)
        {
            _currentPing = Time.realtimeSinceStartup - msg.Timestamp;
            _lastPingRespondedTo = true;
        }
    }

    string _ip = "192.168.1.11";
    //displays client GUI
    public override void OnGUI()
    {
        GUILayout.Label($"Client mode: {_currentState}");
        switch (_currentState)
        {
            case NetState.Disconnected:
            {
                if (!_client.ConnectionEstablished)
                {
                    _ip = GUILayout.TextField(_ip);
                    IPAddress addr;
                    if (IPAddress.TryParse(_ip, out addr))
                    {
                        if (GUILayout.Button("Connect"))
                        {
                            _client.Connect(_ip);
                            _currentState = NetState.Client_Connecting;
                        }
                    }
                }
                break;
            }
            case NetState.Client_Connecting:
            {
                if (_client.HasError)
                {
                    GUILayout.Label($"Client error: {_client.GetError()}");
                }
                if (GUILayout.Button("Cancel"))
                {
                    _client.Disconnect();
                    _currentState = NetState.Disconnected;
                }
                break;
            }
            case NetState.Lobby:
            {
                GUILayout.Label("Lobby");
                _playerSys.SelectModeGUI();
                break;
            }
            case NetState.InGame:
            {
                GUILayout.Label("In Game");
                GUILayout.Label($"My playerID: {_client.MyPlayerId}");
                break;
            }
        }
    }
}
