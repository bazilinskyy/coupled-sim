using System.Collections.Generic;
using CoupledSim.Drones;
using UnityEngine;
using SVec3 = System.Numerics.Vector3;

/// <summary>
/// A quadrotor's look, made from code (no third-party model): a body, four arms in an X, motors, two-blade
/// propellers (with a faint disc when they spin fast), a camera gimbal under the nose and navigation lights (red at
/// the front, green at the back, as on most consumer drones), sized to the airframe's arm length.
/// Also the conversions between Unity's frame (x east, y up, z north) and the model's (x east, y north, z up).
/// </summary>
public static class DroneBody
{
    public static SVec3 ToModel(Vector3 u) => new SVec3(u.x, u.z, u.y);
    public static Vector3 ToUnity(SVec3 m) => new Vector3(m.X, m.Z, m.Y);
    public static Quaternion ToUnity(QuadrotorState s) => Quaternion.LookRotation(ToUnity(s.Forward), ToUnity(s.Up));
    /// <summary>Unity heading (degrees clockwise from north) to the model's yaw (radians counter-clockwise from east).</summary>
    public static float YawFromHeading(float headingDeg) => Mathf.PI / 2f - headingDeg * Mathf.Deg2Rad;

    static Material _body, _dark, _disc, _red, _green, _lens;

    static Material Lit(Color c, float smooth, bool emissive = false)
    {
        var assets = MapAssets.Instance;
        var m = assets != null && assets.LitTemplate != null ? new Material(assets.LitTemplate) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 3f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        return m;
    }

    static void Materials()
    {
        if (_body != null) return;
        _body = Lit(new Color(0.86f, 0.87f, 0.88f), 0.55f);   // light grey, as most consumer drones
        _dark = Lit(new Color(0.08f, 0.08f, 0.09f), 0.35f);
        _red = Lit(new Color(1f, 0.1f, 0.05f), 0.6f, true);
        _green = Lit(new Color(0.1f, 1f, 0.2f), 0.6f, true);
        _lens = Lit(new Color(0.02f, 0.02f, 0.04f), 0.95f);
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        _disc = shader != null ? new Material(shader) : Lit(new Color(0.1f, 0.1f, 0.1f), 0.2f);
        _disc.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.13f, 0.28f));
        _disc.SetFloat("_Surface", 1f); _disc.SetFloat("_Blend", 0f);
        _disc.SetOverrideTag("RenderType", "Transparent");
        _disc.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _disc.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _disc.SetFloat("_ZWrite", 0f);
        _disc.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _disc.renderQueue = 3000;
    }

    static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    /// <summary>Builds the drone under <paramref name="root"/>; returns the four propeller hubs (front left, rear left,
    /// rear right, front right, as in <see cref="Quadrotor"/>) and their discs.</summary>
    public static (Transform[] props, Renderer[] discs) Build(Transform root, QuadrotorParameters p)
    {
        Materials();
        float a = p.ArmLength, s = a / 0.19f;   // 1 for the consumer drone
        var model = new GameObject("Drone model").transform;
        model.SetParent(root, false);
        // a rounded shell (as a consumer drone's moulded body), a little wider at the back
        Part(PrimitiveType.Sphere, "Body", model, new Vector3(0, 0.05f * s, 0), new Vector3(0.1f, 0.055f, 0.22f) * s, _body);
        Part(PrimitiveType.Sphere, "Body rear", model, new Vector3(0, 0.055f * s, -0.045f * s), new Vector3(0.115f, 0.05f, 0.12f) * s, _body);
        Part(PrimitiveType.Cube, "Battery", model, new Vector3(0, 0.085f * s, -0.03f * s), new Vector3(0.07f, 0.025f, 0.12f) * s, _dark);
        // gimbal and camera under the nose
        Part(PrimitiveType.Sphere, "Gimbal", model, new Vector3(0, 0.015f * s, 0.11f * s), Vector3.one * 0.035f * s, _dark);
        Part(PrimitiveType.Cylinder, "Lens", model, new Vector3(0, 0.015f * s, 0.128f * s), new Vector3(0.018f, 0.006f, 0.018f) * s, _lens, Quaternion.Euler(90, 0, 0));
        var props = new Transform[4];
        var discs = new Renderer[4];
        float[] angles = { 45f, 135f, 225f, 315f };
        for (int i = 0; i < 4; i++)
        {
            float th = angles[i] * Mathf.Deg2Rad;
            // model frame (x forward, y left) to Unity's local frame (z forward, x right)
            var tip = new Vector3(-a * Mathf.Sin(th), 0.07f * s, a * Mathf.Cos(th));
            var mid = tip * 0.5f; mid.y = 0.06f * s;
            Part(PrimitiveType.Cube, "Arm " + i, model, mid, new Vector3(0.018f * s, 0.014f * s, a), _dark, Quaternion.LookRotation(new Vector3(tip.x, 0, tip.z)));
            Part(PrimitiveType.Cylinder, "Motor " + i, model, tip, new Vector3(0.028f, 0.012f, 0.028f) * s, _dark);
            var hub = new GameObject("Propeller " + i).transform;
            hub.SetParent(model, false);
            hub.localPosition = tip + new Vector3(0, 0.016f * s, 0);
            Part(PrimitiveType.Cube, "Blades", hub, Vector3.zero, new Vector3(p.RotorRadius * 2f, 0.002f * s, 0.018f * s), _dark, Quaternion.Euler(0, 0, i % 2 == 0 ? 6 : -6));
            discs[i] = Part(PrimitiveType.Cylinder, "Disc", hub, Vector3.zero, new Vector3(p.RotorRadius * 2f, 0.0005f, p.RotorRadius * 2f), _disc).GetComponent<Renderer>();
            discs[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            props[i] = hub;
            // navigation lights under the motors: red at the front, green at the back
            Part(PrimitiveType.Sphere, "Light " + i, model, tip - new Vector3(0, 0.012f * s, 0), Vector3.one * 0.012f * s, i == 0 || i == 3 ? _red : _green);
        }
        // landing legs
        foreach (var x in new[] { -1f, 1f })
            Part(PrimitiveType.Cube, "Leg", model, new Vector3(x * 0.04f * s, 0.02f * s, 0.02f * s), new Vector3(0.008f, 0.04f, 0.13f) * s, _dark);
        return (props, discs);
    }
}
