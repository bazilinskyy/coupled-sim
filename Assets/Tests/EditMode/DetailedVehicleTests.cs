using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoupledSim.Tests
{
    /// <summary>
    /// The detailed (Sketchfab) vehicles: real sizes, wheels where the model has them, and the rear section of
    /// the articulated bus following like a trailer.
    /// </summary>
    public class DetailedVehicleTests
    {
        static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        // real wheelbases (front axle to the rear axle, or to the middle of a tandem)
        [TestCase("ToyotaCorolla", 2.70f, 0.31f)]       // Corolla E180: 2.70 m, 205/55 R16
        [TestCase("LandRoverDefender", 2.79f, 0.38f)]   // Defender 110: 2794 mm (110 in), 235/85 R16
        [TestCase("DafXF95", 4.48f, 0.53f)]             // XF95 6x2: 3.80 m + half the 1.35 m tandem, 315/80 R22.5
        [TestCase("BoxTruck", 4.1f, 0.40f)]
        [TestCase("ArticulatedBus", 5.1f, 0.48f)]       // front section of an 18 m articulated bus
        public void WheelsSitWhereTheModelHasThem(string name, float wheelbase, float radius)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Cars/Vehicles/Drivable{name}.prefab");
            Assert.IsNotNull(prefab, name);
            var wheels = prefab.GetComponentsInChildren<WheelCollider>(true);
            Assert.AreEqual(4, wheels.Length);
            var local = wheels.Select(w => prefab.transform.InverseTransformPoint(w.transform.position)).ToArray();
            float front = local.Max(p => p.z), rear = local.Min(p => p.z);
            Assert.AreEqual(wheelbase, front - rear, 0.12f, "wheelbase");
            foreach (var w in wheels) Assert.AreEqual(radius, w.radius, 0.03f, "tyre radius");
            // left and right wheels mirror each other
            var fronts = local.Where(p => p.z > (front + rear) / 2f).OrderBy(p => p.x).ToArray();
            Assert.AreEqual(-fronts[0].x, fronts[1].x, 0.08f, "front track centred");
            // each wheel collider turns a wheel of the model
            var suspension = TypeNamed("VehicleBehaviour.Suspension");
            foreach (var w in wheels)
            {
                var s = w.GetComponent(suspension);
                var model = suspension.GetField("wheelModel").GetValue(s) as GameObject;
                Assert.IsNotNull(model, $"{w.name} has no wheel to turn");
            }
        }

        [Test]
        public void InteriorsHaveADriverSeatInside()
        {
            foreach (var name in new[] { "ToyotaCorolla", "LandRoverDefender", "DafXF95", "ArticulatedBus" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Cars/Vehicles/Drivable{name}.prefab");
                var eye = prefab.transform.InverseTransformPoint(prefab.transform.Find("DriverLogic/CameraPositionSet").position);
                var box = prefab.GetComponent<BoxCollider>();
                Assert.IsTrue(new Bounds(box.center, box.size + Vector3.one * 0.1f).Contains(eye), $"{name}: driver's eyes {eye} outside the body");
            }
        }

        [Test]
        public void ArticulatedBusFollowsLikeATrailer()
        {
            var type = TypeNamed("ArticulatedTrailer");
            Assert.IsNotNull(type);
            var body = new GameObject("front section");
            var trailer = new GameObject("rear section");
            try
            {
                const float hitch = 2f, axle = 6f, radius = 15f;
                trailer.transform.SetParent(body.transform, false);
                var art = body.AddComponent(type);
                type.GetField("Trailer").SetValue(art, trailer.transform);
                type.GetField("HitchLocal").SetValue(art, new Vector3(0f, 0f, -hitch));
                type.GetField("AxleDistance").SetValue(art, axle);
                var start = type.GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
                var late = type.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                var angle = type.GetProperty("Angle");
                start.Invoke(art, null);

                // straight ahead: straight behind
                for (int i = 0; i < 200; i++) { body.transform.position += Vector3.forward * 0.1f; late.Invoke(art, null); }
                Assert.AreEqual(0f, (float)angle.GetValue(art), 0.1f);
                Assert.AreEqual(body.transform.position.z - hitch, trailer.transform.position.z, 0.01f);

                // a steady circle (the front section's reference point on radius R): the angle settles where the
                // trailer's axle runs on its own, smaller circle: asin(L / Rh) + atan(c / R), Rh = sqrt(R² + c²)
                var centre = body.transform.position + Vector3.left * radius;
                for (int i = 0; i < 4000; i++)
                {
                    float phi = i * 0.005f;
                    body.transform.SetPositionAndRotation(centre + radius * new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi)),
                        Quaternion.LookRotation(new Vector3(-Mathf.Sin(phi), 0f, Mathf.Cos(phi))));
                    late.Invoke(art, null);
                }
                float rh = Mathf.Sqrt(radius * radius + hitch * hitch);
                float expected = (Mathf.Asin(axle / rh) + Mathf.Atan(hitch / radius)) * Mathf.Rad2Deg;
                Assert.AreEqual(expected, Mathf.Abs((float)angle.GetValue(art)), 1.0f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(trailer);
                UnityEngine.Object.DestroyImmediate(body);
            }
        }
    }
}
