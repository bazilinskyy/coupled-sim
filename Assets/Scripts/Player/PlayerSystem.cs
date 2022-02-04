using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityStandardAssets.Utility;

//spawns, initializes and manages avatar at runtime
public class PlayerSystem : MonoBehaviour
{
    public enum Mode
    {
        Flat,
        VR,
        Suite,
        Remote,
        HostAI
    }

    [SerializeField]
    Mode PlayerMode;
    [SerializeField]
    PlayerAvatar _AvatarPrefab;
    [SerializeField]
    PlayerAvatar[] _AvatarPrefabDriver;
    [SerializeField]
    PlayerAvatar[] _AvatarPrefabPassenger;


    [NonSerialized]
    public PlayerAvatar LocalPlayer;
    public PlayerAvatar PedestrianPrefab => _AvatarPrefab;

    // Avatars contains both Drivers and Pedestrians (in arbitrary order)
    [NonSerialized]
    public List<PlayerAvatar> Avatars = new List<PlayerAvatar>();
    [NonSerialized]
    public List<PlayerAvatar> Drivers = new List<PlayerAvatar>();
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
                return _AvatarPrefabDriver[carIdx];
            case SpawnPointType.Passenger:
                return _AvatarPrefabPassenger[carIdx];
            default:
                Assert.IsFalse(true, $"Invalid SpawnPointType: {type}");
                return null;
        }
    }

    public void SpawnLocalPlayer(SpawnPoint spawnPoint, int player, ExperimentRoleDefinition role)
    {
        LocalPlayer = SpawnAvatar(spawnPoint, GetAvatarPrefab(spawnPoint.Type, role.carIdx), player, role);
        LocalPlayer.Initialize(PlayerMode);
        if (spawnPoint.Type == SpawnPointType.Passenger)
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
        remotePlayer.Initialize(Mode.Remote);
    }

    public List<PlayerAvatar> GetAvatarsOfType(AvatarType type)
    {
        switch (type)
        {
            case AvatarType.Pedestrian: return Pedestrians;
            case AvatarType.Driver: return Drivers;
            case AvatarType.Passenger: return Passengers;
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

    public void destroyPlayers()
    {
        destroyObjectsInList(Avatars);
        destroyObjectsInList(Drivers);
        destroyObjectsInList(Pedestrians);
        destroyObjectsInList(Passengers);

        Avatars.Clear();
        Drivers.Clear();
        Pedestrians.Clear();
        Passengers.Clear();
    }

    private void destroyObjectsInList(List<PlayerAvatar> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Destroy(list[i].gameObject);
        }
    }

    //displays controler selection GUI
    public void SelectModeGUI()
    {

        PlayerMode = Mode.Flat;

        //    GUILayout.Label($"Mode: {PlayerMode}");
        //    if (GUILayout.Button("Suite mode"))
        //    {
        //        PlayerMode = Mode.Suite;
        //    }
        //    if (GUILayout.Button("Oculus mode"))
        //    {
        //        PlayerMode = Mode.VR;
        //    }
        //    if (GUILayout.Button("Keyboard mode"))
        //    {
        //        PlayerMode = Mode.Flat;
        //    }
    }
}
