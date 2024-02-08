using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityStandardAssets.Utility;


//spawns, initializes and manages avatar at runtime
public class PlayerSystem : MonoBehaviour
{
    public enum ControlMode
    {
        Driver,
        Passenger,
        HostAI
    }


    public enum InputMode
    {
        Flat,
        VR,
        Suite,
        None
    }


    public enum VehicleType
    {
        MDV,
        AV
    }


    public InputMode PlayerInputMode;
    [SerializeField] private PlayerAvatar _AvatarPrefab;
    [SerializeField] private PlayerAvatar[] _AvatarPrefabDriver;


    // [NonSerialized]
    public PlayerAvatar LocalPlayer;

    // Avatars contains both Drivers and Pedestrians (in arbitrary order)
    //[NonSerialized]
    public List<PlayerAvatar> Avatars = new();
    //[NonSerialized]
    public List<PlayerAvatar> Cars = new();
    //[NonSerialized]
    public List<PlayerAvatar> Pedestrians = new();
    //[NonSerialized]
    public List<PlayerAvatar> Passengers = new();

    private HMIManager _hmiManager;

    private readonly List<AvatarPose> _poses = new();

    private readonly PlayerAvatar[] Player2Avatar = new PlayerAvatar[UNetConfig.MaxPlayers];
    public PlayerAvatar PedestrianPrefab => _AvatarPrefab;


    public PlayerAvatar GetAvatar(int player)
    {
        return Player2Avatar[player];
    }


    private void Awake()
    {
        _hmiManager = FindObjectOfType<HMIManager>();
    }


    public void ActivatePlayerAICar()
    {
        var aiCar = LocalPlayer.GetComponent<AICar>();
        var tracker = LocalPlayer.GetComponent<WaypointProgressTracker>();
        Assert.IsNotNull(tracker);
        aiCar.enabled = true;
        tracker.enabled = true;

        foreach (var waypoint in tracker.Circuit.Waypoints)
        {
            var speedSettings = waypoint.GetComponent<SpeedSettings>();

            if (speedSettings != null)
            {
                speedSettings.targetAICar = aiCar;
            }
        }
    }


    private PlayerAvatar GetAvatarPrefab(SpawnPointType type, int carIdx)
    {
        switch (type)
        {
            case SpawnPointType.PlayerControlledPedestrian:
                return _AvatarPrefab;
            case SpawnPointType.PlayerControlingCar:
            case SpawnPointType.PlayerInAIControlledCar:
                return _AvatarPrefabDriver[carIdx];
            default:
                Assert.IsFalse(true, $"Invalid SpawnPointType: {type}");

                return null;
        }
    }


    public void SpawnLocalPlayer(SpawnPoint spawnPoint, int player, ExperimentRoleDefinition role)
    {
        var isPassenger = spawnPoint.Type == SpawnPointType.PlayerInAIControlledCar;
        LocalPlayer = SpawnAvatar(spawnPoint, GetAvatarPrefab(spawnPoint.Type, role.carIdx), player, role);
        LocalPlayer.Initialize(false, PlayerInputMode, isPassenger ? ControlMode.Passenger : ControlMode.Driver, spawnPoint.VehicleType, spawnPoint.CameraIndex);

        if (isPassenger)
        {
            var waypointFollow = LocalPlayer.GetComponent<WaypointProgressTracker>();
            Assert.IsNotNull(waypointFollow);
            waypointFollow.Init(role.AutonomousPath);
            LocalPlayer.gameObject.layer = LayerMask.NameToLayer(role.AutonomousIsYielding ? "Yielding" : "Car");

            var hmiControl = LocalPlayer.GetComponent<ClientHMIController>();
            hmiControl.Init(_hmiManager);
        }
    }


    public void SpawnRemotePlayer(SpawnPoint spawnPoint, int player, ExperimentRoleDefinition role)
    {
        var remotePlayer = SpawnAvatar(spawnPoint, GetAvatarPrefab(spawnPoint.Type, role.carIdx), player, role);
        remotePlayer.Initialize(true, InputMode.None, ControlMode.HostAI, spawnPoint.VehicleType);
    }


    public List<PlayerAvatar> GetAvatarsOfType(AvatarType type)
    {
        switch (type)
        {
            case AvatarType.Pedestrian: return Pedestrians;
            case AvatarType.Driver: return Cars;
            default:
                Assert.IsFalse(true, $"No avatar collection for type {type}");

                return null;
        }
    }


    private PlayerAvatar SpawnAvatar(SpawnPoint spawnPoint, PlayerAvatar prefab, int player, ExperimentRoleDefinition role)
    {
        var avatar = Instantiate(prefab);
        avatar.transform.position = spawnPoint.position;
        avatar.transform.rotation = spawnPoint.rotation;
        var cameraSetup = spawnPoint.Point.GetComponent<CameraSetup>();

        if (cameraSetup != null)
        {
            foreach (var cam in avatar.GetComponentsInChildren<Camera>())
            {
                cam.fieldOfView = cameraSetup.fieldOfView;
                cam.transform.localRotation = Quaternion.Euler(cameraSetup.rotation);
            }
        }

        Avatars.Add(avatar);
        GetAvatarsOfType(avatar.Type).Add(avatar);
        Player2Avatar[player] = avatar;

        if (role.HoodHMI != null)
        {
            _hmiManager.AddHMI(avatar.HMISlots.Spawn(HMISlot.Hood, role.HoodHMI));
        }

        if (role.TopHMI != null)
        {
            _hmiManager.AddHMI(avatar.HMISlots.Spawn(HMISlot.Top, role.TopHMI));
        }

        if (role.WindshieldHMI != null)
        {
            _hmiManager.AddHMI(avatar.HMISlots.Spawn(HMISlot.Windshield, role.WindshieldHMI));
        }

        return avatar;
    }


    public List<AvatarPose> GatherPoses()
    {
        _poses.Clear();

        foreach (var avatar in Avatars)
        {
            _poses.Add(avatar.GetPose());
        }

        return _poses;
    }


    public void ApplyPoses(List<AvatarPose> poses)
    {
        for (var i = 0; i < Avatars.Count; i++)
        {
            var avatar = Avatars[i];

            if (avatar != LocalPlayer)
            {
                Avatars[i].ApplyPose(poses[i]);
            }
        }
    }


    //displays controler selection GUI
    public void SelectModeGUI()
    {
        GUILayout.Label($"Mode: {PlayerInputMode}");

        if (GUILayout.Button("Suite mode"))
        {
            PlayerInputMode = InputMode.Suite;
        }

        if (GUILayout.Button("HMD mode"))
        {
            PlayerInputMode = InputMode.VR;
        }

        if (GUILayout.Button("Keyboard mode"))
        {
            PlayerInputMode = InputMode.Flat;
        }
    }


    public void SelectMode(InputMode inputMode)
    {
        PlayerInputMode = inputMode;
    }


    private void OnDisable()
    {
        Avatars = null;
        Cars = null;
        Pedestrians = null;
        Passengers = null;
    }
}