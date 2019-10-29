using UnityEngine;

public class NetworkingManager : MonoBehaviour
{
    NetworkSystem _netSystem;
    LevelManager _levelManager;
    PlayerSystem _playerSystem;
    WorldLogger _logger;
    LogConverter _logConverter;

    [SerializeField]
    AICarSyncSystem _aiCarSystem;
    public ExperimentDefinition[] Experiments;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _playerSystem = GetComponent<PlayerSystem>();
        _levelManager = new LevelManager(_playerSystem, Experiments);
        _logger = new WorldLogger(_playerSystem);
        _logConverter = new LogConverter(_playerSystem.PedestrianPrefab);
    }

    void Update()
    {
        if (_netSystem != null)
        {
            _netSystem.Update();
        }
    }

    void OnGUI()
    {
        if (_netSystem == null)
        {
            if (GUILayout.Button("Start Host"))
            {
                _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger);
            }
            if (GUILayout.Button("Start Client"))
            {
                _netSystem = new Client(_levelManager, _playerSystem, _aiCarSystem, _logger);
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
        _logger.EndLog();
    }
}
