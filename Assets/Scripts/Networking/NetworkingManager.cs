using UnityEngine;
using Varjo;
using UnityEngine.SceneManagement;

//logic entry point 
// - presents main menu
// - updates client/host logic
public class NetworkingManager : MonoBehaviour
{
    NetworkSystem _netSystem;
    LevelManager _levelManager;
    PlayerSystem _playerSystem;
    WorldLogger _logger;
    WorldLogger _fixedLogger;
    OrderLogging _orderLogger;
    LogConverter _logConverter;
    public SceneSelector _SceneSelector;
    //public string _participantNr = "enter";
    public string[] participantOptions = { "Duo1", "Duo2", "Duo3", "Duo4", "Duo5", "Duo6", "Duo7", "Duo8", "Duo9", "Duo10", "Duo11", "Duo12", "Duo13", "Duo14", "Duo15", "Duo16", "Duo17", "Duo18", "Duo19", "Duo20" };
    private int participantInt = 0;
    public string[] mappingOptions = {"Baseline", "Mapping1", "Mapping2"};
    private int mappingInt = 0;

    [SerializeField]
    AICarSyncSystem _aiCarSystem;
    public ExperimentDefinition[] Experiments;

    private int firstHost = 0;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _playerSystem = GetComponent<PlayerSystem>();
        _levelManager = new LevelManager(_playerSystem, Experiments);
        _logger = new WorldLogger(_playerSystem, _aiCarSystem);
        _fixedLogger = new WorldLogger(_playerSystem, _aiCarSystem);
        _orderLogger = new OrderLogging();
        _logConverter = new LogConverter(_playerSystem.PedestrianPrefab);
        //_participantNr = "enter";
    }

    void Start()
    {
        _SceneSelector = transform.GetComponentInChildren<SceneSelector>();
        _SceneSelector.GazeEffectOnAV();
        _SceneSelector.VisualizeGaze();
    }

    bool hideGui = false;
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

        if(PersistentManager.Instance.stopLogging == true)
        {
            OnDestroy();
            _playerSystem.destroyPlayers();
            _aiCarSystem.destroyCars();
            PersistentManager.Instance.stopLogging = false;
        }


    }
    void FixedUpdate()
    {
        if (_netSystem != null)
        {
            _netSystem.FixedUpdate();
        }
    }

    void OnGUI()
    {
        if (hideGui)
        {
            return;
        }
        if (_netSystem == null) 
        {
            GUILayout.Label("Participant number:");
            participantInt = GUILayout.SelectionGrid(participantInt, participantOptions, 4);
            PersistentManager.Instance.ParticipantNr = participantInt + 1;
            GUILayout.Label("Mapping:");
            GUILayout.BeginHorizontal("Box");
            mappingInt = GUILayout.SelectionGrid(mappingInt, mappingOptions, 3);
            GUILayout.EndHorizontal();
            PersistentManager.Instance.mapping = mappingInt;
            if (GUILayout.Button("Start Host"))
            {
                if (_netSystem == null)
                {
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger, _orderLogger);
                    PersistentManager.Instance.nextScene = false;
                    if(_SceneSelector.useManualSelection == false) 
                    {
                        PersistentManager.Instance.hostRole = 0; // Host = passenger
                    }
                    else if(_SceneSelector.useManualSelection == true)
                    {
                        PersistentManager.Instance.hostRole = _SceneSelector.manualHostRole;
                    }
                }
                else if (_netSystem != null)
                {
                    Debug.LogError("Netsystem already active: Host");
                }
            }
            if (GUILayout.Button("Start Client"))
            {
                _netSystem = new Client(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger);
                PersistentManager.Instance.clientRole = 1; // client = pedestrian
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
        _fixedLogger.EndLog();
    }
}
