using System;
using System.Collections.Generic;
using System.Linq;
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
    AIPedestrianSyncSystem _aiPedestrianSyncSystem;
    WorldLogger _logger;
    WorldLogger _fixedTimeLogger;
    HMIManager _hmiManager;
    VisualSyncManager _visualSyncManager;

    List<int> _playerRoles = new List<int>();
    bool[] _playerReadyStatus = new bool[UNetConfig.MaxPlayers];
    float _lastPoseUpdateSent;
    int _selectedExperiment;
    const float PoseUpdateInterval = 0.01f;

    TrafficLightsSystem _lights;
    NetworkingManager.Trial currentTrial;

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

        _hmiManager = GameObject.FindAnyObjectByType<HMIManager>();
        Assert.IsNotNull(_hmiManager, "Missing HMI manager");
        _visualSyncManager = GameObject.FindAnyObjectByType<VisualSyncManager>();
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

    // ---- for the experimenter dashboard
    public bool InGame => _currentState == NetState.InGame;
    public string StateName => _currentState == NetState.Lobby && _transitionPhase == TransitionPhase.WaitingForAwakes ? "Ready to start"
        : _currentState == NetState.Lobby && _transitionPhase == TransitionPhase.LoadingLevel ? "Loading" : _currentState.ToString();
    public int SelectedExperiment { get => _selectedExperiment; set { if (_currentState == NetState.Lobby && !started) _selectedExperiment = value; } }
    public bool IsPlayerConnected(int player) => player == PlayerId || _host.PlayerConnected(player);
    public int GetRole(int player) => _playerRoles[player];
    public bool IsReady(int player) => _playerReadyStatus[player];
    public void SetRole(int player, int role)
    {
        if (_currentState == NetState.Lobby && !started && player >= 0 && player < _playerRoles.Count) _playerRoles[player] = role;
    }
    public bool IsMapExperiment => !string.IsNullOrEmpty(_lvlManager.Experiments[_selectedExperiment].Map);
    public string CurrentMap => _lvlManager.MapFor(_selectedExperiment, currentTrial);
    /// <summary>Chooses the city (MapCatalog index) or a location "lat,lon[,radius]" for map experiments.</summary>
    public void SetMap(int index, string location = null)
    {
        if (_currentState != NetState.Lobby || started) return;
        _lvlManager.MapOverride = index;
        _lvlManager.LocationOverride = OsmMap.TryParseLocation(location, out _, out _, out _) ? location : null;
    }

    /// <summary>Loads the experiment on all agents (as "Initialise experiment").</summary>
    public void Initialise()
    {
        if (_currentState == NetState.Lobby && _transitionPhase == TransitionPhase.None) PrepareSimulation();
    }
    /// <summary>Starts the simulation once everything is loaded (as "Start simulation").</summary>
    public void StartSimulation()
    {
        if (_currentState == NetState.Lobby && _transitionPhase == TransitionPhase.WaitingForAwakes && !startSimulation)
        {
            NetworkingManager.Instance.StartRecording();
            startSimulation = true;
        }
    }

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
                            _lights = GameObject.FindAnyObjectByType<TrafficLightsSystem>();
                            foreach (var carSpawner in _lvlManager.ActiveExperiment.CarSpawners)
                            {
                                carSpawner.Init(_aiCarSystem);
                            }
                            _aiPedestrianSyncSystem = _lvlManager.ActiveExperiment.AIPedestrians;
                            _aiPedestrianSyncSystem.InitHost(_host);
                            ExperimentRoleDefinition experimentRoleDefinition;
                            var role = _playerRoles[Host.PlayerId];
                            var roleName = "No role";
                            if (role != -1) {
                                experimentRoleDefinition = _lvlManager.ActiveExperiment.Roles[role];
                                if (experimentRoleDefinition.AutonomousPath != null) {
                                    _playerSys.ActivatePlayerAICar();
                                }
                                roleName = experimentRoleDefinition.Name;
                            }
                            _host.BroadcastReliable(new AllReadyMsg());
                            var environment = GameObject.FindAnyObjectByType<EnvironmentController>();
                            if (environment != null)
                            {
                                _host.BroadcastReliable(environment.GetState());
                            }
                            _transitionPhase = TransitionPhase.None;
                            _currentState = NetState.InGame;
                            Time.timeScale = 1f;
                            _logger.BeginLog($"HostLog-{NetworkingManager.LogPrefix}{roleName}-", _lvlManager.ActiveExperiment, _lights, Time.realtimeSinceStartup, true);
                            _fixedTimeLogger.BeginLog($"HostFixedTimeLog-{NetworkingManager.LogPrefix}{roleName}-", _lvlManager.ActiveExperiment, _lights, Time.fixedTime, false);
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

    bool startSimulation = false;
    bool AllReady()
    {
        return startSimulation;
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
    static void SelectRoleGUI(int player, Host host, ExperimentRoleDefinition[] roles, bool RunTrialSequenceAutomatically)
    {
        if (RunTrialSequenceAutomatically)
        {
            host._playerRoles[player] = host.currentTrial.roleIndex;
        }
        else 
        {
            GUILayout.BeginHorizontal();
            string playerName = player == Host.PlayerId ? "Host" : $"Player {player}";
            GUILayout.Label($"{playerName} role: {host._playerRoles[player]}");
            for (int i = 0; i < roles.Length; i++)
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
    void PlayerRolesGUI(bool RunTrialSequenceAutomatically)
    {
        GUILayout.Label($"Role binding:");
        var roles = _lvlManager.Experiments[_selectedExperiment].Roles;
        SelectRoleGUI(Host.PlayerId, this, roles, RunTrialSequenceAutomatically);
        ForEachConnectedPlayer((player, host) => SelectRoleGUI(player, host, roles, RunTrialSequenceAutomatically));
    }

    bool started;
    //initializes experiment - sets it up locally and broadcasts experiment configuration message
    void PrepareSimulation()
    {
        if (started)
        {
            return;
        } else
        {
            started = true;
        }
        startSimulation = false;

        _msgDispatcher.ClearLevelMessageHandlers();
        _host.BroadcastReliable(new StartGameMsg
        {
            Roles = _playerRoles,
            Experiment = _selectedExperiment,
            Map = MapCatalog.IndexOf(_lvlManager.MapFor(_selectedExperiment, currentTrial)),
            LocationLat = Location(0), LocationLon = Location(1), LocationRadius = Location(2),
        });
        for (int i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            _playerReadyStatus[i] = false;
        }
        if (_playerRoles[Host.PlayerId] == -1)
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

    // the coordinates of a map chosen by location (0 if a catalogue map or none)
    float Location(int i)
    {
        var map = _lvlManager.MapFor(_selectedExperiment, currentTrial);
        if (map == null || !OsmMap.TryParseLocation(map, out var lat, out var lon, out var r)) return 0f;
        return i == 0 ? (float)lat : i == 1 ? (float)lon : r;
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
        _aiPedestrianSyncSystem.UpdateHost();
        AnimalSpawner.Current?.UpdateHost(_host);
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

    // Runs a trial without anyone clicking: binds the roles from the trial definition, loads the
    // experiment and starts the simulation as soon as the level is up. Called every frame from
    // NetworkingManager.Update, so it also works in batch mode, with the GUI hidden or minimised.
    public void UpdateAutomatic()
    {
        _selectedExperiment = currentTrial.experimentIndex;
        if (_currentState != NetState.Lobby)
        {
            return;
        }
        if (_transitionPhase == TransitionPhase.WaitingForAwakes)
        {
            if (!startSimulation)
            {
                NetworkingManager.Instance.StartRecording();
                startSimulation = true;
            }
        }
        else if (_transitionPhase == TransitionPhase.None && !started)
        {
            // bind roles before StartGameMsg is broadcast, so clients get them too
            _playerRoles[Host.PlayerId] = currentTrial.roleIndex;
            _playerSys.SelectMode(currentTrial.InputMode);
            PrepareSimulation();
        }
    }

    //displays host GUI
    public override void OnGUI(bool RunTrialSequenceAutomatically)
    {
        if (RunTrialSequenceAutomatically)
        {
            // everything happens in UpdateAutomatic(); only show the state
            GUILayout.Label($"Automatic trial: {_currentState}");
            if (_currentState == NetState.InGame)
            {
                _hmiManager.DoHostGUI(this);
                _visualSyncManager.DoHostGUI(this);
            }
            return;
        }
        GUILayout.Label($"Host mode: {_currentState}");
        GUILayout.Label("Connected: " + _host.NumRemotePlayers);
        switch (_currentState)
        {
            case NetState.Lobby:
            {
                    if (_transitionPhase == TransitionPhase.WaitingForAwakes)
                    {
                        string playerReadyStr = "Ready:\t";
                        string playerRolesStr = "Role :\t";
                        for (int i = 0; i < UNetConfig.MaxPlayers; i++)
                        {
                            playerReadyStr += (_playerReadyStatus[i]?"1":"0") + "\t";
                            playerRolesStr += _playerRoles[i] + "\t";
                        }
                        GUILayout.Label(playerReadyStr);
                        GUILayout.Label(playerRolesStr);
                        if (GUILayout.Button("Start simulation"))
                        {
                            if (!startSimulation) {
                                NetworkingManager.Instance.StartRecording();
                            }
                            startSimulation = true;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Initialise experiment"))
                        {
                            PrepareSimulation();
                        }
                        GUILayout.Label("Experiment:");
                        for (int i = 0; i < _lvlManager.Experiments.Length; i++)
                        {
                            if (GUILayout.Button(_lvlManager.Experiments[i].Name + (i == _selectedExperiment ? " <--" : "")))
                            {
                                _selectedExperiment = i;
                            }
                        }
                        // map experiments: choose the city, like CARLA's towns
                        if (!string.IsNullOrEmpty(_lvlManager.Experiments[_selectedExperiment].Map))
                        {
                            GUILayout.Label("Map: " + (_lvlManager.MapFor(_selectedExperiment, currentTrial) ?? ""));
                            var names = MapCatalog.Maps.Select(m => m.City).ToArray();
                            int current = MapCatalog.IndexOf(_lvlManager.MapFor(_selectedExperiment, currentTrial));
                            int chosen = GUILayout.SelectionGrid(current, names, 5);
                            if (chosen != current) _lvlManager.MapOverride = chosen;
                        }
                        PlayerRolesGUI(false);
                        _playerSys.SelectModeGUI();
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
