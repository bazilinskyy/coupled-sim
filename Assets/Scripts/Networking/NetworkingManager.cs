using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;


//logic entry point 
// - presents main menu
// - updates client/host logic
public class NetworkingManager : MonoBehaviour
{
    public float RealtimeLogInterval = 0.2f;

    [SerializeField] private AICarSyncSystem _aiCarSystem;
    public ExperimentDefinition[] Experiments;

    public bool hideGui = false;
    public bool RunTrialSequenceAutomatically;
    public bool recordVideos = false;

    public Trial[] trials;

    private NetworkSystem _netSystem;
    private LevelManager _levelManager;
    private PlayerSystem _playerSystem;
    private WorldLogger _logger;
    private WorldLogger _fixedLogger;
    private LogConverter _logConverter;

    private static int CurrentTrialIndex;

    public static NetworkingManager Instance { get; private set; }
    public PlayerSystem PlayerSystem => _playerSystem;


    private void Awake()
    {
        if (Instance == null)
        {
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


    private void NextTrial()
    {
        CurrentTrialIndex++;
        Destroy(gameObject);

        if (CurrentTrialIndex < trials.Length)
        {
            (_netSystem as Host)?.Shutdown();
            SceneManager.LoadScene(0);
        }
        else
        {
            #if UNITY_EDITOR
            EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }


    private void Update()
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

        _netSystem?.Update();
    }


    private void FixedUpdate()
    {
        _netSystem?.FixedUpdate();
    }


    private void OnGUI()
    {
        if (hideGui)
        {
            if (_netSystem == null)
            {
                if (RunTrialSequenceAutomatically)
                {
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trials[CurrentTrialIndex]);
                }
            }
            else
            {
                _netSystem.OnGUI(RunTrialSequenceAutomatically);
            }
        }
        else
        {
            if (_netSystem == null)
            {
                if (RunTrialSequenceAutomatically || GUILayout.Button("Start host"))
                {
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trials[CurrentTrialIndex]);
                }

                if (!RunTrialSequenceAutomatically && GUILayout.Button("Start client"))
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
        if (recordVideos)
        {
            StartCoroutine(RecordAndRunNextTrial());
        }
        #endif
    }


    [Serializable]
    public class ExperimentParameter
    {
        public string name;
        public string value;
    }


    [Serializable]
    public class Trial
    {
        public int experimentIndex;
        public int roleIndex;
        public PlayerSystem.InputMode InputMode;
        public ExperimentParameter[] experimentParameters;
        public float recordingStartTime;
        public float recordingDuration;
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