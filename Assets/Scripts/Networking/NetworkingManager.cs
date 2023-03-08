using UnityEngine;

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
    public class Trail
    {
        public int experimentIndex;
        public int roleIndex;
        public PlayerSystem.InputMode InputMode;
        public ExperimentParameter [] experimentParameters;
    }

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
    public bool RunTrailSequenceAutomatically;
    public int CurrentTrailIndex;

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

    public Trail[] trails;

    void OnGUI()
    {
        if (hideGui)
        {
            if (_netSystem == null)
            {
                if (RunTrailSequenceAutomatically)
                {
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trails[CurrentTrailIndex]);
                }
            } else
            {
                _netSystem.OnGUI(RunTrailSequenceAutomatically);
            }
        }
        else
        {
            if (_netSystem == null)
            {
                if (RunTrailSequenceAutomatically || GUILayout.Button("Start Host"))
                {
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, trails[CurrentTrailIndex]);
                }
                if (!RunTrailSequenceAutomatically && GUILayout.Button("Start Client"))
                {
                    _netSystem = new Client(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger);
                }

                _logConverter.OnGUI();
            }
            else
            {
                _netSystem.OnGUI(RunTrailSequenceAutomatically);
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
}
