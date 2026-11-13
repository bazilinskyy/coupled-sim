using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CoupledSim.Experiments;
using UnityEngine;
using UnityEngine.SceneManagement;

//logic entry point 
// - presents main menu
// - updates client/host logic
public class NetworkingManager : MonoBehaviour
{
    [System.Serializable]
    public class ExperimentParameter
    {
        public string name;
        public string value;
    }

    [System.Serializable]
    public class Trial
    {
        public int experimentIndex;
        public int roleIndex;
        public PlayerSystem.InputMode InputMode;
        public ExperimentParameter [] experimentParameters;
        public float recordingStartTime;
        public float recordingDuration;
    }

    public static NetworkingManager Instance
    {
        get;
        private set;
    }

    NetworkSystem _netSystem;
    public NetworkSystem NetSystem => _netSystem;
    public bool IsHost => _netSystem is Host;
    public LevelManager Levels => _levelManager;
    public Host HostSystem => _netSystem as Host;
    public int CurrentTrial => CurrentTrialIndex;

    /// <summary>Starts the host (as the "Start host" button does).</summary>
    public void StartHost()
    {
        if (_netSystem != null) return;
        var trial = trials != null && trials.Length > 0 ? trials[CurrentTrialIndex] : new Trial { experimentParameters = new ExperimentParameter[0] };
        _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trial);
    }

    /// <summary>Ends the current trial: next trial of the sequence, or quits after the last (as Esc).</summary>
    public void RequestNextTrial() => NextTrial();
    LevelManager _levelManager;
    PlayerSystem _playerSystem;
    public PlayerSystem PlayerSystem => _playerSystem;
    WorldLogger _logger;
    WorldLogger _fixedLogger;
    LogConverter _logConverter;
    public float RealtimeLogInterval = 0.2f;

    [SerializeField]
    AICarSyncSystem _aiCarSystem;
    public ExperimentDefinition[] Experiments;

    [Header("Trial sequence from a file")]
    [Tooltip("CSV or JSON trial file (path, or name in StreamingAssets/Trials). Empty: the Trials list below. Command line: -trials <file>.")]
    public string TrialFile;
    [Tooltip("Participant number (1, 2, ...) for counterbalancing and log names; 0 = the file's order. Command line: -participant <n>.")]
    public int Participant;
    [Tooltip("Block order when the file does not say (#order=...). Command line: -order latin|random|none.")]
    public TrialOrder Order = TrialOrder.Latin;

    /// <summary>Participant number of this session (0 = none).</summary>
    public static int SessionParticipant { get; private set; }
    /// <summary>"P03-" when a participant number is set, for log file names.</summary>
    public static string LogPrefix => SessionParticipant > 0 ? $"P{SessionParticipant:D2}-T{CurrentTrialIndex + 1:D2}-" : "";
    /// <summary>Problems found when loading the trial file (shown on the start screen).</summary>
    public static readonly List<string> TrialFileErrors = new List<string>();
    static bool _sessionLoaded;
    static Trial[] _sessionTrials;

    static string Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // once per session: command-line options and the trial file (the manager is re-created for every trial)
    void LoadSession()
    {
        if (!_sessionLoaded)
        {
            _sessionLoaded = true;
            var args = Environment.GetCommandLineArgs();
            var file = Arg(args, "-trials") ?? TrialFile;
            SessionParticipant = int.TryParse(Arg(args, "-participant"), out var p) ? p : Participant;
            TrialOrder? order = TrialTable.TryParseOrder(Arg(args, "-order"), out var o) && Arg(args, "-order") != null ? o : (TrialOrder?)null;
            if (args.Contains("-autoRun")) _autoRun = true;
            if (int.TryParse(Arg(args, "-clients"), out var c)) ExpectedClients = Math.Max(0, c);
            else ExpectedClients = WaitForClients;
            _connectTo = Arg(args, "-connect");
            if (args.Contains("-host")) _startHost = true;
            // -quality Fantastic|Balanced|Performance: graphics quality for slower PCs
            var quality = Arg(args, "-quality");
            if (quality != null)
            {
                int q = Array.FindIndex(QualitySettings.names, n => string.Equals(n, quality, StringComparison.OrdinalIgnoreCase));
                if (q >= 0) { QualitySettings.SetQualityLevel(q, true); Debug.Log("Quality: " + QualitySettings.names[q]); }
                else Debug.LogWarning($"Quality level '{quality}' not found ({string.Join(", ", QualitySettings.names)})");
            }
            // -screenshot <seconds> <file>: a screenshot that long after the start (tests, documentation); -quitAfter <seconds>
            if (float.TryParse(Arg(args, "-screenshot"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sAt))
            {
                int i = Array.IndexOf(args, "-screenshot");
                _screenshotAt = sAt; _screenshotFile = i + 2 < args.Length ? args[i + 2] : "screenshot.png";
            }
            Benchmark.FromCommandLine();
            if (float.TryParse(Arg(args, "-quitAfter"), NumberStyles.Float, CultureInfo.InvariantCulture, out var qAt)) _quitAt = qAt;
            if (!string.IsNullOrEmpty(file))
            {
                _sessionTrials = TrialSequence.Load(file, Experiments, SessionParticipant, order, Order, GetComponent<PlayerSystem>().PlayerInputMode, TrialFileErrors);
                foreach (var e in TrialFileErrors) Debug.LogError("Trials: " + e);
                if (_sessionTrials != null) Debug.Log($"Trials: {_sessionTrials.Length} from {file}" + (SessionParticipant > 0 ? $" for participant {SessionParticipant}" : ""));
            }
            if (SessionParticipant > 0 || _sessionTrials != null) WriteSessionFile(_sessionTrials ?? trials);
        }
        if (_sessionTrials != null && _sessionTrials.Length > 0) trials = _sessionTrials;
        if (_autoRun) RunTrialSequenceAutomatically = true;
    }

    static bool _autoRun, _startHost;
    static float _screenshotAt = -1f, _quitAt = -1f;
    static string _screenshotFile;
    static string _connectTo;

    [Tooltip("Automatic trials: wait until this many clients are connected, then give them roles (trial parameter clientRoles, e.g. \"Pedestrian,2\"; else the next free roles). Command line: -clients <n>.")]
    public int WaitForClients;
    /// <summary>Clients the host waits for in automatic trials.</summary>
    public static int ExpectedClients { get; private set; }

    // ExperimentLogs/Session-P03-{date}.csv: the order of the trials this participant got
    void WriteSessionFile(Trial[] list)
    {
        try
        {
            Directory.CreateDirectory("ExperimentLogs");
            var name = $"ExperimentLogs/Session-{(SessionParticipant > 0 ? $"P{SessionParticipant:D2}-" : "")}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv";
            using (var w = new StreamWriter(name))
            {
                w.WriteLine("participant,trial,experiment,role,input,parameters");
                for (int i = 0; i < (list?.Length ?? 0); i++)
                {
                    var t = list[i];
                    var exp = t.experimentIndex >= 0 && t.experimentIndex < Experiments.Length && Experiments[t.experimentIndex] != null ? Experiments[t.experimentIndex] : null;
                    var role = exp != null && exp.Roles != null && t.roleIndex < exp.Roles.Length ? exp.Roles[t.roleIndex].Name : t.roleIndex.ToString(CultureInfo.InvariantCulture);
                    var ps = t.experimentParameters == null ? "" : string.Join(";", t.experimentParameters.Select(x => x.name + "=" + x.value));
                    w.WriteLine($"{SessionParticipant},{i + 1},\"{exp?.ShortName}\",\"{role}\",{t.InputMode},\"{ps.Replace("\"", "'")}\"");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not write the session file: " + e.Message);
        }
    }

    void Awake()
    {
        if (Instance == null) {
            Instance = this;
        }
        DontDestroyOnLoad(gameObject);
        LoadSession();
        _playerSystem = GetComponent<PlayerSystem>();
        _levelManager = new LevelManager(_playerSystem, Experiments);
        _logger = new WorldLogger(_playerSystem, _aiCarSystem);
        _logger.RealtimeLogInterval = RealtimeLogInterval;
        _fixedLogger = new WorldLogger(_playerSystem, _aiCarSystem);
        _logConverter = new LogConverter(_playerSystem.PedestrianPrefab);
    }

    public bool hideGui = false;
    public bool RunTrialSequenceAutomatically;
    public bool recordVideos = false;

    static int CurrentTrialIndex;

    void NextTrial()
    {
        CurrentTrialIndex++;
        Destroy(gameObject);
        if (CurrentTrialIndex < trials.Length)
        {
            (_netSystem as Host)?.Shutdown();
            SceneManager.LoadScene(0);
        } else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    public PlayerSystem Players => _playerSystem;
    public AICarSyncSystem AICars => _aiCarSystem;
    string[] _recordings;
    string _replayPick, _replayError;
    ExperimentDefinition _replayExp;
    static bool _replayChecked;

    void Update()
    {
        if (ReplayPlayer.Active != null) return;   // a replay runs: it has its own keys
        if (!_replayChecked)
        {
            _replayChecked = true;
            var replay = ReplayPlayer.CommandLineReplay();
            if (replay != null)
            {
                // -replayExperiment <short name> [-replayMap <map>]: for recordings without .replay.json
                var exp = Experiments.FirstOrDefault(e => e != null && e.ShortName == Arg(Environment.GetCommandLineArgs(), "-replayExperiment"));
                var err = ReplayPlayer.Begin(replay, this, exp, Arg(Environment.GetCommandLineArgs(), "-replayMap"));
                if (err != null) Debug.LogError("Replay: " + err);
                return;
            }
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            //Application.Quit();
            NextTrial();
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            hideGui = !hideGui;
        }
        if (_screenshotAt > 0f && Time.realtimeSinceStartup >= _screenshotAt)
        {
            _screenshotAt = -1f;
            ScreenCapture.CaptureScreenshot(_screenshotFile);
            Debug.Log("Screenshot: " + _screenshotFile);
        }
        if (_quitAt > 0f && Time.realtimeSinceStartup >= _quitAt) Application.Quit();
        // -connect <ip>: start as a client and connect; -host: start as host
        if (_netSystem == null && !string.IsNullOrEmpty(_connectTo))
        {
            var client = new Client(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger);
            _netSystem = client;
            client.Connect(_connectTo);
        }
        if (_netSystem == null && _startHost && !RunTrialSequenceAutomatically) StartHost();
        if (RunTrialSequenceAutomatically && _netSystem == null && trials != null && trials.Length > 0)
        {
            _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trials[CurrentTrialIndex]);
        }
        if (_netSystem != null)
        {
            if (RunTrialSequenceAutomatically && _netSystem is Host host)
            {
                host.UpdateAutomatic();
            }
            _netSystem.Update();
        }
    }
    void FixedUpdate()
    {
        if (_netSystem != null)
        {
            _netSystem.FixedUpdate();
        }
    }

    public Trial[] trials;

    void OnGUI()
    {
        if (ReplayPlayer.Active != null) return;
        if (hideGui)
        {
            if (_netSystem == null)
            {
                // automatic trials are started from Update()
            } else
            {
                _netSystem.OnGUI(RunTrialSequenceAutomatically);
            }
        }
        else
        {
            if (_netSystem == null)
            {
                if (!RunTrialSequenceAutomatically && GUILayout.Button("Start host"))
                {
                    var trial = trials != null && trials.Length > 0 ? trials[CurrentTrialIndex] : new Trial { experimentParameters = new ExperimentParameter[0] };
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trial);
                }
                if (!RunTrialSequenceAutomatically && GUILayout.Button("Start client"))
                {
                    _netSystem = new Client(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger);
                }
                if (SessionParticipant > 0 || _sessionTrials != null)
                    GUILayout.Label($"{(SessionParticipant > 0 ? $"Participant {SessionParticipant}, " : "")}trial {CurrentTrialIndex + 1} of {trials.Length}: {TrialSequence.Describe(trials[Mathf.Min(CurrentTrialIndex, trials.Length - 1)], Experiments)}");
                foreach (var e in TrialFileErrors) GUILayout.Label("Trial file: " + e);

                _logConverter.OnGUI();

                // replay of a recorded trial (recordings made since the replay exists have a .replay.json next to them)
                if (_recordings == null && GUILayout.Button("Replay a recording"))
                {
                    _recordings = System.IO.Directory.Exists("ExperimentLogs")
                        ? System.IO.Directory.GetFiles("ExperimentLogs", "*.binLog").OrderByDescending(System.IO.File.GetLastWriteTime).ToArray()
                        : new string[0];
                    _replayPick = null; _replayExp = null; _replayError = null;
                }
                if (_recordings != null)
                {
                    if (GUILayout.Button("Close")) _recordings = null;
                    else if (_replayPick == null)
                    {
                        if (_recordings.Length == 0) GUILayout.Label("No recordings to replay yet: run an experiment first.");
                        if (_replayError != null) GUILayout.Label(_replayError);
                        foreach (var f in _recordings.Take(40))
                        {
                            bool known = System.IO.File.Exists(ReplayInfo.PathFor(f));
                            if (!GUILayout.Button(System.IO.Path.GetFileNameWithoutExtension(f) + (known ? "" : "  (older: choose the experiment)"))) continue;
                            if (known) { _replayError = ReplayPlayer.Begin(f, this); if (_replayError == null) _recordings = null; }
                            else _replayPick = f;
                            break;
                        }
                    }
                    else
                    {
                        // a recording without .replay.json: which experiment (and city map) was it?
                        GUILayout.Label(System.IO.Path.GetFileName(_replayPick) + ": in which experiment was it recorded?");
                        if (GUILayout.Button("Back")) { _replayPick = null; _replayExp = null; }
                        else if (_replayExp == null)
                        {
                            foreach (var e in Experiments.Where(e => e != null))
                                if (GUILayout.Button(e.Name))
                                {
                                    if (string.IsNullOrEmpty(e.Map)) Start(e, null); else _replayExp = e;
                                    break;
                                }
                        }
                        else
                        {
                            GUILayout.Label("Which city map?");
                            foreach (var m in MapCatalog.Maps)
                                if (GUILayout.Button(m.City + " (" + m.Country + ")")) { Start(_replayExp, m.Name); break; }
                        }
                        void Start(ExperimentDefinition e, string map)
                        {
                            _replayError = ReplayPlayer.Begin(_replayPick, this, e, map);
                            _replayPick = null; _replayExp = null;
                            if (_replayError == null) _recordings = null;
                        }
                    }
                }
            }
            else
            {
                _netSystem.OnGUI(RunTrialSequenceAutomatically);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        _logger.EndLog();
        _fixedLogger.EndLog();
    }

    internal void StartRecording()
    {
#if UNITY_EDITOR
        if (recordVideos) {
            StartCoroutine(RecordAndRunNextTrial());
        }
#endif
    }

#if UNITY_EDITOR
    public Recorder recorder;
    private IEnumerator RecordAndRunNextTrial()
    {
        var trial = trials[CurrentTrialIndex];
        recorder.Init();
        yield return new WaitForSeconds(trial.recordingStartTime);
        recorder.StartRecording(_levelManager.GetFilename(trial, CurrentTrialIndex));
        yield return new WaitForSeconds(trial.recordingDuration);
        recorder.StopRecording();
        yield return null;
        NextTrial();
    }
#endif

}
