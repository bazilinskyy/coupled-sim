using System;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Assertions;
using UnityEngine.Networking;
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
    public void DeserializeFrom(BinaryReader reader)
    {
        LocalPositions = reader.ReadListVector3();
        LocalRotations = reader.ReadListQuaternion();
        Blinkers = (BlinkerState)reader.ReadInt32();
    }

    public void SerializeTo(BinaryWriter writer)
    {
        writer.Write(LocalPositions);
        writer.Write(LocalRotations);
        writer.Write((int)Blinkers);
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
    Passenger
}

// These are used for more than Players (AI cars have these as well)
public class PlayerAvatar : MonoBehaviour
{
    [Serializable]
    public struct ModeElements
    {
        [Header("Enabled elements")]
        public GameObject[] gameObjects;
        public MonoBehaviour[] monoBehaviours;
        public Collider collider;
    }

    public ModeElements VRModeElements;
    public ModeElements FlatModeElements;
    public ModeElements RemoteModeElements;
    public ModeElements SuiteModeElements;
    [FormerlySerializedAs("LocalModeElements")]
    public ModeElements HostDrivenAIElements;

    //set up an Avatar (disabling and enabling needed components) for different control methods
    public void Initialize(PlayerSystem.Mode mode)
    {
        ModeElements modeElements = default(ModeElements);
        switch (mode)
        {
            case PlayerSystem.Mode.Suite:
                modeElements = SuiteModeElements;
                break;
            case PlayerSystem.Mode.Flat:
                modeElements = FlatModeElements;
                break;
            case PlayerSystem.Mode.VR:
                modeElements = VRModeElements;
                break;
            case PlayerSystem.Mode.Remote:
                modeElements = RemoteModeElements;
                break;
            case PlayerSystem.Mode.HostAI:
                modeElements = HostDrivenAIElements;
                break;
        }
        foreach (var go in modeElements.gameObjects) {
            go.SetActive(true);
        }
        foreach (var mb in modeElements.monoBehaviours)
        {
            mb.enabled = true;
        }
        if (modeElements.collider != null)
        {
            modeElements.collider.enabled = true;
        }
        if (mode == PlayerSystem.Mode.Remote)
        {
            GetComponentInChildren<Rigidbody>().isKinematic = true;
            GetComponentInChildren<Rigidbody>().useGravity = false;
            GetComponentInChildren<Camera>().gameObject.SetActive(false);
            foreach (var wc in GetComponentsInChildren<WheelCollider>())
            {
                wc.enabled = false;
            }
            foreach (var su in GetComponentsInChildren<Suspension>())
            {
                su.enabled = false;
            }
        }
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

    public HMIAnchors HMISlots;
    public AvatarType Type;
    public Transform[] SyncTransforms;
    public CarBlinkers _carBlinkers;
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
    }
}
