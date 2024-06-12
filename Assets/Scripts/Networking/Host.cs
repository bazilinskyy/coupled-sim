using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;


// high level host networking script
public class Host : NetworkSystem
{
    private readonly UNetHost _host;
    private readonly MessageDispatcher _msgDispatcher;
    private readonly LevelManager _lvlManager;
    private readonly PlayerSystem _playerSys;
    private readonly AICarSyncSystem _aiCarSystem;
    private AIPedestrianSyncSystem _aiPedestrianSyncSystem;
    private readonly WorldLogger _logger;
    private readonly WorldLogger _fixedTimeLogger;
    private readonly HMIManager _hmiManager;
    private readonly VisualSyncManager _visualSyncManager;

    private readonly List<int> _playerRoles = new();
    private readonly bool[] _playerReadyStatus = new bool[UNetConfig.MaxPlayers];
    private float _lastPoseUpdateSent;
    private int _selectedExperiment;

    private TrafficLightsSystem _lights;
    private readonly NetworkingManager.Trial currentTrial;

    private bool startSimulation = false;

    private bool started;

    private bool failRoleCheck = false;
    private readonly bool[] _activeRoles = new bool[UNetConfig.MaxPlayers];


    public Host(LevelManager levelManager, PlayerSystem playerSys, AICarSyncSystem aiCarSystem, WorldLogger logger, WorldLogger fixedLogger, NetworkingManager.Trial instantStartParams)
    {
        currentTrial = instantStartParams;
        _playerSys = playerSys;
        _lvlManager = levelManager;
        _aiCarSystem = aiCarSystem;
        _logger = logger;
        _fixedTimeLogger = fixedLogger;
        _host = new UNetHost();
        _host.Init();

        _hmiManager = Object.FindObjectOfType<HMIManager>();
        Assert.IsNotNull(_hmiManager, "Missing HMI manager");
        _visualSyncManager = Object.FindObjectOfType<VisualSyncManager>();
        Assert.IsNotNull(_visualSyncManager, "Missing VS manager");

        _currentState = NetState.Lobby;
        _msgDispatcher = new MessageDispatcher();

        //set up message handlers
        _msgDispatcher.AddStaticHandler((int) MsgId.C_UpdatePose, OnClientPoseUpdate);
        _msgDispatcher.AddStaticHandler((int) MsgId.C_Ready, OnClientReady);
        _msgDispatcher.AddStaticHandler((int) MsgId.B_Ping, OnPing);
        _hmiManager.InitHost(_host, _msgDispatcher);
        aiCarSystem.InitHost(_host);

        for (var i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            _playerRoles.Add(-1);
        }
    }


    public const int PlayerId = 0;
    private const float PoseUpdateInterval = 0.01f;


    public void Shutdown()
    {
        _host.Shutdown();
        _currentState = NetState.Disconnected;
    }


    //handles ping message
    private void OnPing(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<PingMsg>(sync);
        _host.SendUnreliableToPlayer(msg, srcPlayerId);
    }


    //handles client ready message
    private void OnClientReady(ISynchronizer sync, int player)
    {
        NetMsg.Read<ReadyMsg>(sync);
        _playerReadyStatus[player] = true;
    }


    //applies pose updates received from other players
    private void OnClientPoseUpdate(ISynchronizer sync, int player)
    {
        var msg = NetMsg.Read<UpdateClientPose>(sync);
        _playerSys.GetAvatar(player).ApplyPose(msg.Pose);
    }


    public void BroadcastMessage<T>(T msg) where T : INetMessage
    {
        _host.BroadcastReliable(msg);
    }


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
                        _playerReadyStatus[PlayerId] = true;

                        if (AllReady())
                        {
                            _lights = Object.FindObjectOfType<TrafficLightsSystem>();

                            foreach (var carSpawner in _lvlManager.ActiveExperiment.CarSpawners)
                            {
                                carSpawner.Init(_aiCarSystem);
                            }

                            _aiPedestrianSyncSystem = _lvlManager.ActiveExperiment.AIPedestrians;
                            _aiPedestrianSyncSystem.InitHost(_host);
                            ExperimentRoleDefinition experimentRoleDefinition;
                            var role = _playerRoles[PlayerId];
                            var roleName = "No role";

                            if (role != -1)
                            {
                                experimentRoleDefinition = _lvlManager.ActiveExperiment.Roles[role];

                                if (experimentRoleDefinition.AutonomousPath != null)
                                {
                                    _playerSys.ActivatePlayerAICar();
                                }

                                roleName = experimentRoleDefinition.Name;
                            }

                            _host.BroadcastReliable(new AllReadyMsg());
                            _transitionPhase = TransitionPhase.None;
                            _currentState = NetState.InGame;
                            Time.timeScale = 1f;
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


    private bool AllReady()
    {
        return startSimulation;
    }


    private void ForEachConnectedPlayer(Action<int, Host> action)
    {
        for (var i = 1; i < UNetConfig.MaxPlayers; i++)
        {
            if (_host.PlayerConnected(i))
            {
                action(i, this);
            }
        }
    }


    //displays role selection GUI for a single player
    private static void SelectRoleGUI(int player, Host host, ExperimentRoleDefinition[] roles, bool RunTrialSequenceAutomatically)
    {
        if (RunTrialSequenceAutomatically)
        {
            host._playerRoles[player] = host.currentTrial.roleIndex;
        }
        else
        {
            GUILayout.BeginHorizontal();
            var playerName = player == PlayerId ? "Host" : $"Player {player}";
            GUILayout.Label($"{playerName} role: {host._playerRoles[player]}");

            for (var i = 0; i < roles.Length; i++)
            {
                if (GUILayout.Button(roles[i].Name))
                {
                    host._playerRoles[player] = i;
                }
            }

            GUILayout.EndHorizontal();
        }
    }


    //displays role selection GUI
    private void PlayerRolesGUI(bool RunTrialSequenceAutomatically)
    {
        GUILayout.Label("Role binding:");
        var roles = _lvlManager.Experiments[_selectedExperiment].Roles;
        SelectRoleGUI(PlayerId, this, roles, RunTrialSequenceAutomatically);
        ForEachConnectedPlayer((player, host) => SelectRoleGUI(player, host, roles, RunTrialSequenceAutomatically));
    }


    //initializes experiment - sets it up locally and broadcasts experiment configuration message
    private void PrepareSimulation()
    {
        if (started)
        {
            return;
        }

        started = true;
        startSimulation = false;

        _msgDispatcher.ClearLevelMessageHandlers();

        _host.BroadcastReliable(new StartGameMsg
        {
            Roles = _playerRoles,
            Experiment = _selectedExperiment
        });

        for (var i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            _playerReadyStatus[i] = false;
        }

        if (_playerRoles[PlayerId] == -1)
        {
            _lvlManager.LoadLevelNoLocalPlayer(_selectedExperiment, _playerRoles, currentTrial);
        }
        else
        {
            _lvlManager.LoadLevelWithLocalPlayer(_selectedExperiment, 0, _playerRoles, currentTrial);
        }

        _transitionPhase = TransitionPhase.LoadingLevel;
        Time.timeScale = 0;
    }


    private void UpdateGame()
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
        _aiPedestrianSyncSystem.UpdateHost();
    }


    private void CheckRoleConnected(int player, Host host)
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


    private bool AllRolesSelected()
    {
        for (var i = 0; i < _activeRoles.Length; i++)
        {
            _activeRoles[i] = false;
        }

        failRoleCheck = false;
        CheckRoleConnected(PlayerId, this);
        ForEachConnectedPlayer(CheckRoleConnected);

        return !failRoleCheck;
    }


    //displays host GUI
    public override void OnGUI(bool RunTrialSequenceAutomatically)
    {
        if (!RunTrialSequenceAutomatically)
        {
            GUILayout.Label($"Host mode: {_currentState}");
            GUILayout.Label("Connected: " + _host.NumRemotePlayers);
        }
        else
        {
            _selectedExperiment = currentTrial.experimentIndex;
        }

        switch (_currentState)
        {
            case NetState.Lobby:
            {
                if (_transitionPhase == TransitionPhase.WaitingForAwakes)
                {
                    var playerReadyStr = "Ready:\t";
                    var playerRolesStr = "Role :\t";

                    for (var i = 0; i < UNetConfig.MaxPlayers; i++)
                    {
                        playerReadyStr += (_playerReadyStatus[i] ? "1" : "0") + "\t";
                        playerRolesStr += _playerRoles[i] + "\t";
                    }

                    GUILayout.Label(playerReadyStr);
                    GUILayout.Label(playerRolesStr);

                    if (GUILayout.Button("Start simulation") || RunTrialSequenceAutomatically)
                    {
                        if (!startSimulation)
                        {
                            NetworkingManager.Instance.StartRecording();
                        }

                        startSimulation = true;
                    }
                }
                else
                {
                    if (RunTrialSequenceAutomatically)
                    {
                        PrepareSimulation();
                        PlayerRolesGUI(RunTrialSequenceAutomatically);
                        _playerSys.SelectMode(currentTrial.InputMode);
                    }
                    else
                    {
                        //GUI.enabled = AllRolesSelected();
                        if (GUILayout.Button("Initialise experiment"))
                        {
                            PrepareSimulation();
                        }

                        GUI.enabled = true;
                        GUILayout.Label("Experiment:");

                        for (var i = 0; i < _lvlManager.Experiments.Length; i++)
                        {
                            if (GUILayout.Button(_lvlManager.Experiments[i].Name + (i == _selectedExperiment ? " <--" : "")))
                            {
                                _selectedExperiment = i;
                            }
                        }

                        PlayerRolesGUI(RunTrialSequenceAutomatically);
                        _playerSys.SelectModeGUI();
                    }
                }

                break;
            }
            case NetState.InGame:
            {
                _hmiManager.DoHostGUI(this);
                _visualSyncManager.DoHostGUI(this);
            }

                break;
        }
    }
}