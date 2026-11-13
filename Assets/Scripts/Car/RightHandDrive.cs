using UnityEngine;

/// <summary>
/// Right-hand drive (#41): turns the participant's left-hand-drive car into a right-hand-drive one
/// for cities with left-hand traffic. The body and interior model is mirrored (left to right), and
/// the driver, passenger, steering wheel, instrument cluster, mirrors and cameras move to the other
/// side; the dashboard text and the steering wheel stay unmirrored. Driving physics are unchanged.
/// Chosen per role (ExperimentRoleDefinition.Steering); Auto follows the city map. Every agent
/// decides the same, as the role and the map are the same everywhere.
/// </summary>
public static class RightHandDrive
{
    // moved to the other side (position and orientation mirrored), relative to the car's root
    static readonly string[] Mirrored =
    {
        "CarLogic/ModelElements/Dashboard",
        "CarLogic/ModelElements/steering wheel",
        "CarLogic/ModelElements/RearMirrors/LeftMirror",
        "CarLogic/ModelElements/RearMirrors/RightMirror",
        "CarLogic/ModelElements/RearMirrors/MiddleMirror",
        "CarLogic/HeadPosition",
        "CarLogic/HeadTargetAnchor",
        "DriverModel&Rig/Driver",
        "DriverLogic/CameraPositionSet",
        "DriverLogic/EyeContact",
        "PassengerModel/BlueSuitFree01",
        "PassengerLogic/CameraPositionSet",
    };

    // parts of the model with text on them (the instrument cluster's screen): moved, not mirrored
    static readonly string[] Readable = { "Object001" };

    public static bool Wanted(SteeringSide side) =>
        side == SteeringSide.Right ||
        (side == SteeringSide.Auto && OsmMap.Current != null && OsmMap.Current.Info.LeftHandTraffic);

    /// <summary>Makes the car right-hand drive; call right after instantiating it. Returns false when the car does not have the expected layout.</summary>
    public static bool Apply(GameObject car)
    {
        var model = car.transform.Find("CarModel");
        if (model == null || car.transform.Find("CarLogic/ModelElements") == null)
        {
            Debug.LogWarning($"RightHandDrive: {car.name} has no CarModel and CarLogic/ModelElements; it stays left-hand drive.");
            return false;
        }
        // the steering wheel's mesh is part of the model: take it out first, so it moves with its pivot and is not mirrored
        foreach (var sw in car.GetComponentsInChildren<SteeringWheel>(true))
            if (sw.steeringWheel != null && sw.pivot != null) sw.steeringWheel.SetParent(sw.pivot, true);
        var wheel = car.transform.Find("CarLogic/ModelElements/steering wheel");
        float wheelX = wheel != null ? car.transform.InverseTransformPoint(wheel.position).x : 0f;
        var readable = new System.Collections.Generic.List<Transform>();
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
            if (System.Array.IndexOf(Readable, t.name) >= 0) readable.Add(t);
        foreach (var t in readable) t.SetParent(car.transform, true);
        var s = model.localScale;
        model.localScale = new Vector3(-s.x, s.y, s.z);
        foreach (var path in Mirrored)
        {
            var t = car.transform.Find(path);
            if (t != null) Reflect(t);
        }
        // moved across as a whole, so it stays behind the steering wheel, turned and shaped as before
        foreach (var t in readable) t.localPosition += new Vector3(-2f * wheelX, 0f, 0f);
        car.name += " (right-hand drive)";
        return true;
    }

    /// <summary>Mirrors a transform's local position and orientation in the car's left-right plane.</summary>
    public static void Reflect(Transform t)
    {
        var p = t.localPosition;
        t.localPosition = new Vector3(-p.x, p.y, p.z);
        var q = t.localRotation;
        t.localRotation = new Quaternion(q.x, -q.y, -q.z, q.w);
    }
}
