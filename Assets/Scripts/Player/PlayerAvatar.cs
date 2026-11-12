using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Assertions;
using VehicleBehaviour;


public enum HMISlot
{
    None,
    Hood,
    Top,
    Windshield,
}

// stores Transform state for every "bone" of an Avatar
// The pose represents both a pedestrian and car (for simplicity)
// so we have some redundancy
public struct AvatarPose : INetSubMessage
{
    public List<Vector3> LocalPositions;
    public List<Quaternion> LocalRotations;
    public BlinkerState Blinkers;
    public bool FrontLights;
    public bool StopLights;
    public void DeserializeFrom(BinaryReader reader)
    {
        LocalPositions = reader.ReadListVector3();
        LocalRotations = reader.ReadListQuaternion();
        Blinkers = (BlinkerState)reader.ReadInt32();
        FrontLights = reader.ReadBoolean();
        StopLights = reader.ReadBoolean();
    }
    public void DeserializeFrom(ref Unity.Collections.DataStreamReader reader)
    {
        LocalPositions = reader.ReadListVector3();
        LocalRotations = reader.ReadListQuaternion();
        Blinkers = (BlinkerState)reader.ReadInt();
        FrontLights = reader.ReadBoolean();
        StopLights = reader.ReadBoolean();
    }

    public void SerializeTo(BinaryWriter writer)
    {
        writer.Write(LocalPositions);
        writer.Write(LocalRotations);
        writer.Write((int)Blinkers);
        writer.Write(FrontLights);
        writer.Write(StopLights);
    }

    IEnumerable<string> CsvEnumerator()
    {
        for(int i=0; i < LocalPositions.Count; i++)
        {
            var pos = LocalPositions[i];
            var rot = LocalRotations[i];
            yield return $"{pos.x},{pos.y},{pos.z},{rot.x},{rot.y},{rot.z},{rot.w},{(int)Blinkers}";
        }
    }

    IEnumerable<string> CsvEnumeratorNoBlinkers()
    {
        for(int i=0; i < LocalPositions.Count; i++)
        {
            var pos = LocalPositions[i];
            var rot = LocalRotations[i];
            yield return $"{pos.x},{pos.y},{pos.z},{rot.x},{rot.y},{rot.z},{rot.w}";
        }
    }
}

public enum AvatarType
{
    Pedestrian,
    Driver,
}

// These are used for more than Players (AI cars have these as well)
public class PlayerAvatar : MonoBehaviour
{
    [Header("Controls")]
    public ModeElements VRModeElements;
    public ModeElements FlatModeElements;
    public ModeElements SuiteModeElements;

    [Header("Player")]
    [FormerlySerializedAs("LocalModeElements")]
    public ModeElements HostDrivenAIElements;
    public ModeElements PlayerAsDriver;
    public ModeElements PlayerAsPassenger;

    [Header("Driver")]
    public ModeElements AV;
    public ModeElements MDV;

    [Header("Audio")]
    public EngineSoundManager Internal;
    public EngineSoundManager External;

    [Serializable]
    public struct ModeElements
    {
        [Header("Enabled elements")]
        public GameObject[] gameObjects;
        public MonoBehaviour[] monoBehaviours;
        public Collider collider;
        /*
        [Header("Disabled elements")]
        public GameObject[] disabledGameObjects;
        public MonoBehaviour[] disabledMonoBehaviours;
        */
    }



    //set up an Avatar (disabling and enabling needed components) for different control methods
    public void Initialize(bool isRemote, PlayerSystem.InputMode inputMode, PlayerSystem.ControlMode controlMode, PlayerSystem.VehicleType vehicleType, int cameraIndex = -1)
    {
        if (isRemote)
        {
            if (External != null) {
                External.enabled = true;
            }
            GetComponentInChildren<Rigidbody>().isKinematic = true;
            GetComponentInChildren<Rigidbody>().useGravity = false;
            foreach (var wc in GetComponentsInChildren<WheelCollider>())
            {
                wc.enabled = false;
            }
            foreach (var su in GetComponentsInChildren<Suspension>())
            {
                su.enabled = false;
            }
        } 
        else
        {
            if (cameraIndex >= 0) {
                cameras[cameraIndex].gameObject.SetActive(true);
            }

            ModeElements modeElements = default(ModeElements);
            switch (inputMode)
            {
                case PlayerSystem.InputMode.Suite:
                    modeElements = SuiteModeElements;
                    break;
                case PlayerSystem.InputMode.Flat:
                    modeElements = FlatModeElements;
                    break;
                case PlayerSystem.InputMode.VR:
                    modeElements = VRModeElements;
                    break;
                default:
                    break;
            }

            SetupModeElements(modeElements);

            switch (controlMode)
            {
                case PlayerSystem.ControlMode.Driver:
                    if (Internal != null)
                    {
                        Internal.enabled = true;
                    }
                    modeElements = PlayerAsDriver;
                    break;
                case PlayerSystem.ControlMode.HostAI:
                    if (External != null)
                    {
                        External.enabled = true;
                    }
                    modeElements = HostDrivenAIElements;
                    break;
                case PlayerSystem.ControlMode.Passenger:
                    if (Internal != null)
                    {
                        Internal.enabled = true;
                    }
                    modeElements = PlayerAsPassenger;
                    break;
            }

            SetupModeElements(modeElements); 
            
            switch (vehicleType)
            {
                case PlayerSystem.VehicleType.AV:
                    modeElements = AV;
                    break;
                case PlayerSystem.VehicleType.MDV:
                    modeElements = MDV;
                    break;
            }

            SetupModeElements(modeElements);
        }

    }

    private static void SetupModeElements(ModeElements modeElements)
    {
        if (modeElements.gameObjects != null) {
            foreach (var go in modeElements.gameObjects)
            {
                go.SetActive(true);
            }
        }
        if (modeElements.monoBehaviours != null)
        {
            foreach (var mb in modeElements.monoBehaviours)
            {
                mb.enabled = true;
            }
        }
        if (modeElements.collider != null)
        {
            modeElements.collider.enabled = true;
        }
        /*
        if (modeElements.disabledGameObjects != null)
        {
            foreach (var go in modeElements.disabledGameObjects)
            {
                go.SetActive(false);
            }
        }
        if (modeElements.disabledMonoBehaviours != null)
        {
            foreach (var mb in modeElements.disabledMonoBehaviours)
            {
                mb.enabled = false;
            }
        }
        */
    }

    // defines HMI to be spawned on the car
    [Serializable]
    public struct HMIAnchors
    {
        public Transform Hood;
        [NonSerialized]
        public HMI HoodHMI;
        public Transform Top;
        [NonSerialized]
        public HMI TopHMI;
        public Transform Windshield;
        [NonSerialized]
        public HMI WindshieldHMI;

        HMI Spawn(HMI prefab, ref HMI instance, Transform parent)
        {
            if (instance != null)
            {
                GameObject.Destroy(instance);
            }
            instance = GameObject.Instantiate(prefab, parent);
            instance.transform.localPosition = default;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }
        public HMI Spawn(HMISlot slot, HMI prefab)
        {
            switch (slot)
            {
                default:
                case HMISlot.None:
                    Assert.IsFalse(true);
                    return null;
                case HMISlot.Hood:
                    return Spawn(prefab, ref HoodHMI, Hood);
                case HMISlot.Top:
                    return Spawn(prefab, ref TopHMI, Top);
                case HMISlot.Windshield:
                    return Spawn(prefab, ref WindshieldHMI, Windshield);
            }
        }
    }

    [Header("Other")]
    public Camera[] cameras;
    public HMIAnchors HMISlots;
    public AvatarType Type;
    public Transform[] SyncTransforms;
    [SerializeField]
    private CarBlinkers _carBlinkers;
    public GameObject stopLights;
    public GameObject frontLights;
    public CarBlinkers CarBlinkers => _carBlinkers;
    List<Vector3> _pos = new List<Vector3>();
    List<Quaternion> _rot = new List<Quaternion>();

    public AvatarPose GetPose()
    {
        _pos.Clear();
        _rot.Clear();
        _pos.Add(transform.position);
        _rot.Add(transform.rotation);
        for (int i = 0; i < SyncTransforms.Length; i++)
        {
            var trans = SyncTransforms[i];
            _pos.Add(trans.localPosition);
            _rot.Add(trans.localRotation);
        }
        return new AvatarPose
        {
            LocalPositions = _pos,
            LocalRotations = _rot,
            Blinkers = _carBlinkers == null ? BlinkerState.None : _carBlinkers.State,
            FrontLights = frontLights == null ? false : frontLights.activeSelf,
            StopLights = stopLights == null ? false : stopLights.activeSelf,
        };
    }

    public void ApplyPose(AvatarPose pose)
    {
        transform.position = pose.LocalPositions[0];
        transform.rotation = pose.LocalRotations[0];
        for (int i = 0; i < SyncTransforms.Length; i++)
        {
            var trans = SyncTransforms[i];
            trans.localPosition = pose.LocalPositions[i+1];
            trans.localRotation = pose.LocalRotations[i+1];
        }
        if (_carBlinkers != null)
        {
            _carBlinkers.SwitchToState(pose.Blinkers);
        }
        if (frontLights != null)
        {
            frontLights.SetActive(pose.FrontLights);
        }
        if (stopLights != null)
        {
            stopLights.SetActive(pose.StopLights);
        }
    }

    internal void SetBreakLights(bool breaking)
    {
        stopLights.SetActive(breaking);
    }
}
