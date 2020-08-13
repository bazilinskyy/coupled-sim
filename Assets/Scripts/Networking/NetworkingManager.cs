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
    LogConverter _logConverter;
    public SceneSelector _SceneSelector;
    public bool _nextScene;

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
        _logConverter = new LogConverter(_playerSystem.PedestrianPrefab);
        //Debug.LogError("Awake entered");
    }

    void Start()
    {
        _SceneSelector = transform.GetComponentInChildren<SceneSelector>();
    }

    bool hideGui = false;
    void Update()
    {
        _nextScene = PersistentManager.Instance.nextScene;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            hideGui = !hideGui;
        }
        if (_netSystem != null)// && _nextScene == false) //if (_netSystem != null) test // stop updating when going to the next scene
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
        if (_netSystem != null)// && _nextScene == false) //nextScene should be set at true when entering new scene. How come it is set back to false? // if (_netSystem != null)
        {
            _netSystem.FixedUpdate();
        }
    }

    void OnGUI()
    {
        //Debug.LogError($"Netsystem = {_netSystem}");
        if (hideGui)
        {
            return;
        }
        if (_netSystem == null) //if (_netSystem == null)
        {
            //Debug.LogError("NetSystem is null");
            if (GUILayout.Button("Start Host"))
            {
                //Debug.LogError($"netsystem 1 = {_netSystem}");
                if (_netSystem == null)
                {
                    _netSystem = new Host(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger);
                    //Debug.LogError($"netsystem 2 = {_netSystem}");
                    PersistentManager.Instance.nextScene = false;
                }
                else if (_netSystem != null)
                {
                    Debug.LogError("Netsystem already active: Host");
                }
            }
            if (GUILayout.Button("Start Client"))
            {
                _netSystem = new Client(_levelManager, _playerSystem, _aiCarSystem, _logger, _fixedLogger);
            }
            _logConverter.OnGUI();
        }
        else
        {
            //Debug.LogError("NetSystem is NOT null");
            _netSystem.OnGUI();
        }
    }

    private void OnDestroy()
    {
        _logger.EndLog();
        _fixedLogger.EndLog();
    }
}
