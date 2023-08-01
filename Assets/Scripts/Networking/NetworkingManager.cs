using System;
using System.Collections;
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

    void Awake()
    {
        if (Instance == null) {
            Instance = this;
        }
        DontDestroyOnLoad(gameObject);
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
            (_netSystem as Host).Shutdown();
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            //Application.Quit();
            NextTrial();
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            hideGui = !hideGui;
        }
        if (_netSystem != null)
        {
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
        if (hideGui)
        {
            if (_netSystem == null)
            {
                if (RunTrialSequenceAutomatically)
                {
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trials[CurrentTrialIndex]);
                }
            } else
            {
                _netSystem.OnGUI(RunTrialSequenceAutomatically);
            }
        }
        else
        {
            if (_netSystem == null)
            {
                if (RunTrialSequenceAutomatically || GUILayout.Button("Start Host"))
                {
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trials[CurrentTrialIndex]);
                }
                if (!RunTrialSequenceAutomatically && GUILayout.Button("Start Client"))
                {
                    _netSystem = new Client(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger);
                }

                _logConverter.OnGUI();
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
