using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityStandardAssets.Utility;

//spawns, initializes and manages avatar at runtime
public class PlayerSystem : MonoBehaviour
{
    public enum InputMode
    {
        Flat,
        VR,
        Suite,
        None
    }

    public enum ControlMode
    {
        Driver,
        Passenger,
        HostAI
    }

    public InputMode PlayerInputMode;
    [SerializeField]
    PlayerAvatar _AvatarPrefab;
    [SerializeField]
    PlayerAvatar[] _AvatarPrefabDriver;


    [NonSerialized]
    public PlayerAvatar LocalPlayer;
    public PlayerAvatar PedestrianPrefab => _AvatarPrefab;

    // Avatars contains both Drivers and Pedestrians (in arbitrary order)
    [NonSerialized]
    public List<PlayerAvatar> Avatars = new List<PlayerAvatar>();
    [NonSerialized]
    public List<PlayerAvatar> Cars = new List<PlayerAvatar>();
    [NonSerialized]
    public List<PlayerAvatar> Pedestrians = new List<PlayerAvatar>();
    [NonSerialized]
    public List<PlayerAvatar> Passengers = new List<PlayerAvatar>();

    PlayerAvatar[] Player2Avatar = new PlayerAvatar[UNetConfig.MaxPlayers];
    public PlayerAvatar GetAvatar(int player) => Player2Avatar[player];

    HMIManager _hmiManager;
    void Awake()
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
    }

    PlayerAvatar GetAvatarPrefab(SpawnPointType type, int carIdx)
    {
        switch (type)
        {
            case SpawnPointType.Pedestrian:
                return _AvatarPrefab;
            case SpawnPointType.Driver:
            case SpawnPointType.Passenger:
                return _AvatarPrefabDriver[carIdx];
            default:
                Assert.IsFalse(true, $"Invalid SpawnPointType: {type}");
                return null;
        }
    }

    public void SpawnLocalPlayer(SpawnPoint spawnPoint, int player, ExperimentRoleDefinition role)
    {
        bool isPassenger = spawnPoint.Type == SpawnPointType.Passenger;
        LocalPlayer = SpawnAvatar(spawnPoint, GetAvatarPrefab(spawnPoint.Type, role.carIdx), player, role);
        LocalPlayer.Initialize(false, PlayerInputMode, isPassenger ? ControlMode.Passenger : ControlMode.Driver);
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
        remotePlayer.Initialize(true, InputMode.None, ControlMode.HostAI);
    }

    public List<PlayerAvatar> GetAvatarsOfType(AvatarType type)
    {
        switch (type)
        {
            case AvatarType.Pedestrian: return Pedestrians;
            case AvatarType.Driver: return Cars;
            default: Assert.IsFalse(true, $"No avatar collection for type {type}"); return null;
        }
    }

    PlayerAvatar SpawnAvatar(SpawnPoint spawnPoint, PlayerAvatar prefab, int player, ExperimentRoleDefinition role)
    {
        var avatar = GameObject.Instantiate(prefab);
        avatar.transform.position = spawnPoint.position;
        avatar.transform.rotation = spawnPoint.rotation;
        var cameraSetup = spawnPoint.Point.GetComponent<CameraSetup>();
        if (cameraSetup != null)
        {
            var cam = avatar.GetComponentInChildren<Camera>();
            cam.fieldOfView = cameraSetup.fieldOfView;
            cam.transform.localRotation = Quaternion.Euler(cameraSetup.rotation);
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

    List<AvatarPose> _poses = new List<AvatarPose>();
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
        for (int i = 0; i < Avatars.Count; i++)
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
            if (GUILayout.Button("Oculus mode"))
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
}
