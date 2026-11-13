using System;
using UnityEngine;

public enum CarSeat { FrontPassenger, RearLeft, RearRight, RearMiddle }

/// <summary>
/// A participant riding in another role's car (ExperimentRoleDefinition.RideInCarOf and Seat), e.g.
/// three participants in one shared AV. The role is a pedestrian role (its avatar carries the camera,
/// headset and logging); this keeps it in the seat on every agent, turns off its walking and
/// collisions, hides its standing body and shows a seated figure (a copy of the car's passenger
/// figure) in the seat instead. Seats are placed from the driver's eye point of the car: front
/// passenger beside it (taken by the AV passenger in AVs), rear seats 0.85 m behind (use a car with
/// rear seats, e.g. the hatchback, carIdx 1).
/// </summary>
public class SeatedPassenger : MonoBehaviour
{
    public string CarRole;
    public CarSeat Seat;
    public float RearOffset = 0.85f, RearHalfWidth = 0.38f;

    PlayerAvatar _car;
    Transform _figure;
    Vector3 _eyeInRoot;      // camera position in this avatar's root, before any tracking
    bool _prepared;

    public static string[] RoleNames = new string[UNetConfig.MaxPlayers];

    void Start()
    {
        foreach (var m in GetComponentsInChildren<KeyboardPlayerMovement>(true)) m.enabled = false;
        foreach (var c in GetComponentsInChildren<CharacterController>(true)) c.enabled = false;
        foreach (var a in GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) a.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (var r in GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) r.enabled = false;
        var cam = GetComponentInChildren<Camera>(true);
        _eyeInRoot = cam != null ? transform.InverseTransformPoint(cam.transform.position) : new Vector3(0f, 1.6f, 0f);
    }

    PlayerAvatar FindCar()
    {
        var ps = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem : null;
        if (ps == null) return null;
        for (int p = 0; p < RoleNames.Length; p++)
        {
            if (RoleNames[p] != CarRole) continue;
            var a = ps.GetAvatar(p);
            if (a != null && a.Type == AvatarType.Driver) return a;
        }
        return null;
    }

    // the car's driver eye point and the seat's, in the car's axes
    bool SeatPoints(out Vector3 driverEye, out Vector3 seatEye)
    {
        driverEye = seatEye = default;
        if (_car == null) return false;
        var car = _car.transform;
        var cam = _car.cameras != null && _car.cameras.Length > 0 && _car.cameras[0] != null ? _car.cameras[0] : _car.GetComponentInChildren<Camera>(true);
        driverEye = cam != null ? car.InverseTransformPoint(cam.transform.position) : new Vector3(-0.37f, 1.15f, 0f);
        switch (Seat)
        {
            case CarSeat.FrontPassenger: seatEye = new Vector3(-driverEye.x, driverEye.y, driverEye.z); break;
            case CarSeat.RearLeft: seatEye = new Vector3(-RearHalfWidth, driverEye.y, driverEye.z - RearOffset); break;
            case CarSeat.RearRight: seatEye = new Vector3(RearHalfWidth, driverEye.y, driverEye.z - RearOffset); break;
            default: seatEye = new Vector3(0f, driverEye.y, driverEye.z - RearOffset); break;
        }
        return true;
    }

    void LateUpdate()
    {
        if (_car == null) { _car = FindCar(); _prepared = false; }
        if (!SeatPoints(out var driverEye, out var seatEye)) return;
        var car = _car.transform;
        if (!_prepared) { AddFigure(driverEye, seatEye); _prepared = true; }
        // the avatar's eye on the seat's eye point, facing forward with the car
        transform.rotation = car.rotation;
        transform.position = car.TransformPoint(seatEye) - transform.rotation * _eyeInRoot;
    }

    // a copy of the car's seated passenger figure (or its driver), moved to this seat
    void AddFigure(Vector3 driverEye, Vector3 seatEye)
    {
        if (_figure != null) Destroy(_figure.gameObject);
        var car = _car.transform;
        // the passenger figure sits in the front passenger seat, the driver figure in the driver's
        Transform src = car.Find("PassengerModel/BlueSuitFree01");
        Vector3 srcEye = new Vector3(-driverEye.x, driverEye.y, driverEye.z);
        if (src == null) { src = car.Find("DriverModel&Rig/Driver"); srcEye = driverEye; }
        if (src == null) return;
        _figure = Instantiate(src.gameObject, src.parent).transform;
        _figure.name = "Seated passenger " + Seat;
        _figure.localRotation = src.localRotation;
        _figure.localScale = src.localScale;
        _figure.position = src.position + car.TransformVector(seatEye - srcEye);
        foreach (var r in _figure.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        _figure.gameObject.SetActive(true);
    }

    void OnDestroy() { if (_figure != null) Destroy(_figure.gameObject); }
}
