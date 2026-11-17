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

    public enum VehicleType
    {
        MDV,
        AV
    }

    public InputMode PlayerInputMode;
    [SerializeField]
    PlayerAvatar _AvatarPrefab;
    [SerializeField]
    PlayerAvatar[] _AvatarPrefabDriver;
    [SerializeField]
    [Tooltip("Bicycle, e-scooter and moped avatars for PlayerRidingTwoWheeler roles.")]
    PlayerAvatar[] _AvatarPrefabTwoWheeler;
    [SerializeField]
    [Tooltip("Drones for PlayerFlyingDrone roles.")]
    PlayerAvatar[] _AvatarPrefabDrone;


    [NonSerialized]
    public PlayerAvatar LocalPlayer;
    public PlayerAvatar PedestrianPrefab => _AvatarPrefab;
    public PlayerAvatar[] DriverPrefabs => _AvatarPrefabDriver;
    public PlayerAvatar[] TwoWheelerPrefabs => _AvatarPrefabTwoWheeler;
    public PlayerAvatar[] DronePrefabs => _AvatarPrefabDrone;

    // Avatars contains both Drivers and Pedestrians (in arbitrary order)
    [NonSerialized]
    public List<PlayerAvatar> Avatars = new List<PlayerAvatar>();
    [NonSerialized]
    public List<PlayerAvatar> Cars = new List<PlayerAvatar>();
    [NonSerialized]
    public List<PlayerAvatar> Pedestrians = new List<PlayerAvatar>();
    [NonSerialized]
    public List<PlayerAvatar> Passengers = new List<PlayerAvatar>();
    [NonSerialized]
    public List<PlayerAvatar> Drones = new List<PlayerAvatar>();

    PlayerAvatar[] Player2Avatar = new PlayerAvatar[UNetConfig.MaxPlayers];
    public PlayerAvatar GetAvatar(int player) => Player2Avatar[player];

    HMIManager _hmiManager;
    void Awake()
    {
        _hmiManager = FindAnyObjectByType<HMIManager>();
    }

    public void ActivatePlayerAICar()
    {
        var aiCar = LocalPlayer.GetComponent<AICar>();
        var tracker = LocalPlayer.GetComponent<WaypointProgressTracker>();
        Assert.IsNotNull(tracker);
        aiCar.enabled = true;
        tracker.enabled = true;
        // automation modes and take-over requests (trial parameters automation, avSpeed, avPath, takeOverAt)
        if (!LocalPlayer.TryGetComponent<AutomatedDriving>(out var automation)) automation = LocalPlayer.gameObject.AddComponent<AutomatedDriving>();
        automation.Activate();
        foreach(var waypoint in tracker.Circuit.Waypoints)
        {
            var speedSettings = waypoint.GetComponent<SpeedSettings>();
            if (speedSettings != null)
            {
                speedSettings.targetAICar = aiCar;
            }
        }
    }

    // -driverCar / -passengerCar Name: the vehicle of the roles that drive or ride in a car, by prefab
    // name (e.g. ToyotaCorolla for DrivableToyotaCorolla; give the same option on every PC)
    PlayerAvatar VehicleByName(string option)
    {
        var args = CommandLine.Args;
        int i = System.Array.IndexOf(args, option);
        if (i < 0 || i + 1 >= args.Length) return null;
        var name = args[i + 1];
        foreach (var p in _AvatarPrefabDriver)
            if (p != null && (string.Equals(p.name, name, System.StringComparison.OrdinalIgnoreCase) || string.Equals(p.name, "Drivable" + name, System.StringComparison.OrdinalIgnoreCase)))
                return p;
        Debug.LogWarning($"{option} {name}: no such vehicle; available: {string.Join(", ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(_AvatarPrefabDriver, x => x != null), x => x.name))}");
        return null;
    }

    PlayerAvatar GetAvatarPrefab(SpawnPointType type, int carIdx, int twoWheelerIdx = 0, int droneIdx = 0)
    {
        switch (type)
        {
            case SpawnPointType.PlayerFlyingDrone:
                return _AvatarPrefabDrone[droneIdx];
            case SpawnPointType.PlayerRidingTwoWheeler:
                return _AvatarPrefabTwoWheeler[twoWheelerIdx];
            case SpawnPointType.PlayerControlledPedestrian:
                return _AvatarPrefab;
            case SpawnPointType.PlayerControlingCar:
            case SpawnPointType.PlayerInAIControlledCar:
                return VehicleByName(type == SpawnPointType.PlayerControlingCar ? "-driverCar" : "-passengerCar") ?? _AvatarPrefabDriver[carIdx];
            default:
                Assert.IsFalse(true, $"Invalid SpawnPointType: {type}");
                return null;
        }
    }

    public void SpawnLocalPlayer(SpawnPoint spawnPoint, int player, ExperimentRoleDefinition role)
    {
        // headset roles: start OpenXR now (flat-screen PCs never need a VR runtime)
        if (PlayerInputMode == InputMode.VR || PlayerInputMode == InputMode.Suite) XRSession.Start();
        bool isPassenger = spawnPoint.Type == SpawnPointType.PlayerInAIControlledCar;
        LocalPlayer = SpawnAvatar(spawnPoint, GetAvatarPrefab(spawnPoint.Type, role.carIdx, role.twoWheelerIdx, role.droneIdx), player, role);
        LocalPlayer.Initialize(false, PlayerInputMode, isPassenger ? ControlMode.Passenger : ControlMode.Driver, spawnPoint.VehicleType, spawnPoint.CameraIndex);
        var response = ResponseInput.WithCommandLine(role.Response);
        if (response.Button || response.Slider) LocalPlayer.gameObject.AddComponent<ResponseInput>().Init(response, role.Name);
        Soundscape.Ensure();
        VideoExport.Ensure();
        if (!(NetworkingManager.Instance != null && NetworkingManager.Instance.NetSystem is Host)) MapTraffic.EnsureLocalAutopilot();
        OpenScenario.Ensure();
        SceneDescriber.EnsureLoggerFromCommandLine();
        if (spawnPoint.Type != SpawnPointType.PlayerControlledPedestrian && spawnPoint.Type != SpawnPointType.PlayerFlyingDrone) MotionOutput.AddIfAsked(LocalPlayer.gameObject);
        var args = CommandLine.Args;
        if (Array.IndexOf(args, "-gaze") >= 0 || Array.IndexOf(args, "-gazeTrigger") >= 0) NetworkGaze.Ensure();
        if (Array.IndexOf(args, "-companions") >= 0) PedestrianGroup.EnsureFromCommandLine();
        // in-car displays for the participant's own car (driver or AV passenger)
        if (spawnPoint.Type == SpawnPointType.PlayerControlingCar || spawnPoint.Type == SpawnPointType.PlayerInAIControlledCar)
        {
            var hud = Arg("-hud") ?? role.HudDesign;
            var centre = Arg("-centreScreen") ?? role.CentreScreenDesign;
            if (!string.IsNullOrEmpty(hud)) InCarDisplay.Add(LocalPlayer, hud, InCarDisplay.Kind.Hud);
            if (!string.IsNullOrEmpty(centre)) InCarDisplay.Add(LocalPlayer, centre, InCarDisplay.Kind.CentreScreen);
            // live instrument cluster on cars with a dashboard (default "Cluster"; "none" keeps the car's own)
            var cluster = Arg("-cluster") ?? role.ClusterDesign;
            if (string.IsNullOrEmpty(cluster)) cluster = "Cluster";
            if (!string.Equals(cluster, "none", StringComparison.OrdinalIgnoreCase) && LocalPlayer.GetComponentInChildren<RealisticVehicleDynamics>(true) != null)
                InCarDisplay.Add(LocalPlayer, cluster, InCarDisplay.Kind.Cluster);
            var messages = Arg("-inCarMessages") ?? role.InCarMessages;
            if (!string.IsNullOrEmpty(messages)) InCarDisplay.LoadSchedule(messages);
            if (spawnPoint.Type == SpawnPointType.PlayerControlingCar && LocalPlayer.GetComponentInChildren<CarBlinkers>(true) != null)
                LocalPlayer.gameObject.AddComponent<ParticipantBlinkers>();
        }
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
        var remotePlayer = SpawnAvatar(spawnPoint, GetAvatarPrefab(spawnPoint.Type, role.carIdx, role.twoWheelerIdx, role.droneIdx), player, role);
        remotePlayer.Initialize(true, InputMode.None, ControlMode.HostAI, spawnPoint.VehicleType);
    }

    public List<PlayerAvatar> GetAvatarsOfType(AvatarType type)
    {
        switch (type)
        {
            case AvatarType.Pedestrian: return Pedestrians;
            case AvatarType.Driver: return Cars;
            case AvatarType.Drone: return Drones;
            default: Assert.IsFalse(true, $"No avatar collection for type {type}"); return null;
        }
    }

    PlayerAvatar SpawnAvatar(SpawnPoint spawnPoint, PlayerAvatar prefab, int player, ExperimentRoleDefinition role)
    {
        var avatar = GameObject.Instantiate(prefab);
        if ((spawnPoint.Type == SpawnPointType.PlayerControlingCar || spawnPoint.Type == SpawnPointType.PlayerInAIControlledCar) && RightHandDrive.Wanted(role.Steering)) RightHandDrive.Apply(avatar.gameObject);
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
        SeatedPassenger.RoleNames[player] = role.Name;
        // -seatRole "Role:Car role:Seat" (give it to every agent) seats a role without editing the experiment
        var seatArg = Arg("-seatRole");
        if (seatArg != null)
        {
            var parts = seatArg.Split(':');
            if (parts.Length >= 2 && parts[0] == role.Name)
            {
                role.RideInCarOf = parts[1];
                if (parts.Length > 2 && Enum.TryParse<CarSeat>(parts[2], true, out var seat)) role.Seat = seat;
            }
        }
        if (!string.IsNullOrEmpty(role.RideInCarOf))
        {
            var seated = avatar.gameObject.AddComponent<SeatedPassenger>();
            seated.CarRole = role.RideInCarOf;
            seated.Seat = role.Seat;
        }
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
        var design = EhmiDesignFor(role, spawnPoint.Type, out var placement);
        if (design != null) _hmiManager.AddHMI(SpawnDesignedEhmi(avatar, design, placement));
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

    /// <summary>The designed eHMIs in StreamingAssets/eHMI (names, sorted: the same list on every agent).</summary>
    public static string[] AvailableEhmiDesigns()
    {
        var dir = System.IO.Path.Combine(Application.streamingAssetsPath, "eHMI");
        if (!System.IO.Directory.Exists(dir)) return new string[0];
        var names = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(System.IO.Directory.GetFiles(dir, "*.json"), f => System.IO.Path.GetFileNameWithoutExtension(f)));
        Array.Sort(names, StringComparer.Ordinal);
        return names;
    }

    /// <summary>The trial's designed eHMI for AVs (trial parameter ehmiDesign=Name[@Placement], sent by the host to every agent); null: the roles' own.</summary>
    public static string TrialEhmiDesign;
    public static EhmiPlacement? TrialEhmiPlacement;

    /// <summary>Parses "Name[@Placement]" into the trial's eHMI (for the host; clients get indices from StartGameMsg).</summary>
    public static void SetTrialEhmi(string value)
    {
        TrialEhmiDesign = null; TrialEhmiPlacement = null;
        if (string.IsNullOrWhiteSpace(value) || value.Trim().ToLowerInvariant() == "none") return;
        var parts = value.Trim().Split('@');
        TrialEhmiDesign = parts[0];
        if (parts.Length > 1 && Enum.TryParse<EhmiPlacement>(parts[1], true, out var pl)) TrialEhmiPlacement = pl;
    }

    /// <summary>The role's designed eHMI, the trial's (ehmiDesign), or the command line's (-ehmiDesign Name[@Hood|Top|Windshield|Bumper|Road]) for AV roles.</summary>
    static string EhmiDesignFor(ExperimentRoleDefinition role, SpawnPointType type, out EhmiPlacement placement)
    {
        placement = role.EhmiDesignPlacement;
        var design = string.IsNullOrEmpty(role.EhmiDesign) ? null : role.EhmiDesign;
        if (type == SpawnPointType.PlayerInAIControlledCar && TrialEhmiDesign != null) { design = TrialEhmiDesign; if (TrialEhmiPlacement.HasValue) placement = TrialEhmiPlacement.Value; }
        var args = CommandLine.Args;
        if (type == SpawnPointType.PlayerInAIControlledCar)
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-ehmiDesign")
                {
                    var parts = args[i + 1].Split('@');
                    design = parts[0];
                    if (parts.Length > 1 && Enum.TryParse<EhmiPlacement>(parts[1], true, out var pl)) placement = pl;
                }
        return design;
    }

    /// <summary>A DesignedEhmi on a car: at one of its eHMI anchors, on the front bumper or flat on the road ahead.</summary>
    public static DesignedEhmi SpawnDesignedEhmi(PlayerAvatar avatar, string design, EhmiPlacement placement)
    {
        var go = new GameObject("Designed eHMI " + design);
        Debug.Log($"Designed eHMI {design} ({placement}) on {avatar.name}");
        var root = avatar.transform;
        Transform anchor = placement == EhmiPlacement.Hood ? avatar.HMISlots.Hood : placement == EhmiPlacement.Top ? avatar.HMISlots.Top
            : placement == EhmiPlacement.Windshield ? avatar.HMISlots.Windshield : null;
        if (anchor != null)
        {
            go.transform.SetParent(anchor, false);
        }
        else
        {
            // the car's front from its solid colliders (renderers include light beams), in its own axes
            float front = 0f, width = 1.6f;
            foreach (var col in avatar.GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger || col is WheelCollider) continue;
                var b = col.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var p = root.InverseTransformPoint(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z));
                    front = Mathf.Max(front, p.z); width = Mathf.Max(width, Mathf.Abs(p.x) * 2f);
                }
            }
            if (front <= 0f) front = 2f;
            Debug.Log($"Designed eHMI {design}: {placement}, car front {front:F2} m ahead of its origin");
            go.transform.SetParent(root, false);
            if (placement == EhmiPlacement.Bumper)
            {
                go.transform.localPosition = new Vector3(0f, 0.45f, front + 0.02f);
            }
            else
            {
                // projected on the road 1.8-4.2 m ahead, facing up, its top pointing away from the car
                var ahead = root.TransformPoint(new Vector3(0f, 0f, front + 3f));
                float ground = Physics.Raycast(ahead + Vector3.up * 3f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : ahead.y;
                go.transform.localPosition = new Vector3(0f, root.InverseTransformPoint(new Vector3(ahead.x, ground + 0.03f, ahead.z)).y, front + 3f);
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }
        }
        go.layer = avatar.gameObject.layer;
        var ehmi = go.AddComponent<DesignedEhmi>();
        ehmi.Design = design;
        return ehmi;
    }

    static string Arg(string name)
    {
        var args = CommandLine.Args;
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        return null;
    }
}
