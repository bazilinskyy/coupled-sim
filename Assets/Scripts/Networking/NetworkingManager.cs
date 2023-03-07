using UnityEngine;

[System.Serializable]
public struct InstantStartHostParameters
{
    public bool SkipSelectionScreen;
    public int SelectedExperiment;
    public int SelectedRole;
    public PlayerSystem.InputMode InputMode;
}

//logic entry point 
// - presents main menu
// - updates client/host logic
public class NetworkingManager : MonoBehaviour
{
    public static NetworkingManager Instance
    {
        get;
        private set;
    }

    NetworkSystem _netSystem;
    LevelManager _levelManager;
    PlayerSystem _playerSystem;
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
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
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

    public InstantStartHostParameters InstantStartParams;

    void OnGUI()
    {
        if (hideGui)
        {
            return;
        }
        if (_netSystem == null)
        {
            if (InstantStartParams.SkipSelectionScreen || GUILayout.Button("Start Host"))
            {
                _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, InstantStartParams);
            }
            if (!InstantStartParams.SkipSelectionScreen && GUILayout.Button("Start Client"))
            {
                _netSystem = new Client(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger);
            }

            _logConverter.OnGUI();
        }
        else
        {
            _netSystem.OnGUI();
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
}
