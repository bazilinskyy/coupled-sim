using System;
using System.Net;
using UnityEngine;
using UnityEngine.Assertions;

public class Client : NetworkSystem
{
    UNetClient _client;

    MessageDispatcher _msgDispatcher;
    LevelManager _lvlManager;
    PlayerSystem _playerSys;
    WorldLogger _logger;
    HMIManager _hmiManager;
    VisualSyncManager _visualSyncManager;
    float _lastPoseUpdateSent;
    const float PoseUpdateInterval = 0.01f;

    public Client(LevelManager lvlManager, PlayerSystem playerSys, AICarSyncSystem aiCarSystem, WorldLogger logger)
    {
        _playerSys = playerSys;
        _lvlManager = lvlManager;
        _logger = logger;
        _client = new UNetClient();
        _client.Init();

        _hmiManager = GameObject.FindObjectOfType<HMIManager>();
        Assert.IsNotNull(_hmiManager, "Missing HMI manager");
        _visualSyncManager = GameObject.FindObjectOfType<VisualSyncManager>();
        Assert.IsNotNull(_visualSyncManager, "Missing VS manager");

        _currentState = NetState.Disconnected;
        _msgDispatcher = new MessageDispatcher();
        _msgDispatcher.AddStaticHandler((int)MsgId.S_StartGame, OnGameStart);
        _msgDispatcher.AddStaticHandler((int)MsgId.S_UpdateClientPoses, OnUpdatePoses);
        _msgDispatcher.AddStaticHandler((int)MsgId.S_AllReady, OnAllReady);
        _msgDispatcher.AddStaticHandler((int)MsgId.S_VisualSync, OnCustomMessage);
        _hmiManager.InitClient(_client, _msgDispatcher);
        aiCarSystem.InitClient(_msgDispatcher);
        _msgDispatcher.HandleConnect = () =>
        {
            Assert.AreEqual(_currentState, NetState.Client_Connecting);
            _currentState = NetState.Lobby;
        };
    }

    private void OnCustomMessage(ISynchronizer sync, int srcPlayerId)
    {
        _visualSyncManager.DisplayMarker();
    }

    private void OnAllReady(ISynchronizer sync, int srcPlayerId)
    {
        Debug.Log("AllReady");
        var lights = GameObject.FindObjectOfType<StreetLightsSystem>();
        lights?.RegisterHandlers(_msgDispatcher);
        _playerSys.ActivatePlayerAICar();
        _currentState = NetState.InGame;
        _logger.BeginLog("ClientLog", _lvlManager.ActiveExperiment, lights);
    }

    void OnGameStart(ISynchronizer sync, int _)
    {
        _msgDispatcher.ClearLevelMessageHandlers();
        var msg = NetMsg.Read<StartGameMsg>(sync);
        _lvlManager.LoadLevelWithLocalPlayer(msg.Experiment, _client.MyPlayerId, msg.StartingPositionIdxs);
        _transitionPhase = TransitionPhase.LoadingLevel;
    }

    void OnUpdatePoses(ISynchronizer sync, int _)
    {
        var msg = NetMsg.Read<UpdatePoses>(sync);
        _playerSys.ApplyPoses(msg.Poses);
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
                        _transitionPhase = TransitionPhase.None;
                        break;
                }
                break;
            case NetState.InGame:
                _client.Update(_msgDispatcher);
                UpdateGame();
                _logger.LogFrame();
                break;
        }
    }

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
        }
    }

    string _ip = "192.168.1.11";
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
