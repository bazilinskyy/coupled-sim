using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoupledSim.Tests
{
    /// <summary>
    /// Every vehicle prefab (the map traffic's and the participants'): its wheel colliders hold the body where the
    /// model's tyres touch the ground. Traffic vehicles keep their wheel models where the model has them (only the
    /// colliders carry the body: VehicleBehaviour.Suspension is off), so a collider's wheel sitting higher than the
    /// model's sinks the model's tyres into the road by that much.
    /// </summary>
    public class WheelContactTests
    {
        static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        // (not the Smart: its wheel meshes have stray vertices metres away, so their size cannot be read from them; its
        // colliders are as they were)
        public static IEnumerable<string> Prefabs() => Directory.GetFiles("Assets/Prefabs/Cars", "Drivable*.prefab", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/')).Where(p => !p.Contains("SmartCommon")).OrderBy(p => p);

        /// <summary>The lowest point and the radius of a wheel model, in world space (from its meshes' vertices).</summary>
        static (float bottom, float radius) Wheel(GameObject model)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) { float y = m.MultiplyPoint3x4(v).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            }
            foreach (var sk in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sk.sharedMesh == null) continue;
                var m = sk.transform.localToWorldMatrix;
                foreach (var v in sk.sharedMesh.vertices) { float y = m.MultiplyPoint3x4(v).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            }
            return (lo, (hi - lo) / 2f);
        }

        [TestCaseSource(nameof(Prefabs))]
        public void CollidersHoldTheBodyWhereTheTyresTouchTheGround(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var car = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            try
            {
                var suspension = TypeNamed("VehicleBehaviour.Suspension");
                int n = 0;
                foreach (var wc in car.GetComponentsInChildren<WheelCollider>(true))
                {
                    var s = wc.GetComponent(suspension);
                    var model = s != null ? suspension.GetField("wheelModel").GetValue(s) as GameObject : null;
                    if (model == null) continue;
                    var (bottom, radius) = Wheel(model);
                    if (radius <= 0f) continue;   // (a wheel drawn as part of another mesh: a truck's rear tandem)
                    n++;
                    // the collider's wheel at rest (spring target 0.5: half the suspension travel down from its centre)
                    var t = wc.transform;
                    float scale = t.lossyScale.y;
                    float hub = t.TransformPoint(wc.center).y - wc.suspensionDistance * scale * (1f - 0.5f);
                    Assert.AreEqual(0.5f, wc.suspensionSpring.targetPosition, 1e-4f, $"{wc.name}: spring target");
                    Assert.AreEqual(radius, wc.radius * scale, 0.03f, $"{prefab.name} {wc.name}: tyre radius (collider {wc.radius} x {scale})");
                    Assert.AreEqual(bottom, hub - wc.radius * scale, 0.02f, $"{prefab.name} {wc.name}: the collider's tyre bottom at rest against the model's");
                }
                Assert.GreaterOrEqual(n, 2, $"{prefab.name}: wheels with a model");
            }
            finally { UnityEngine.Object.DestroyImmediate(car); }
        }

        /// <summary>The same with the physics: a traffic vehicle (wheel models fixed in the body) dropped on flat ground
        /// settles with its tyres on it, neither sunk nor floating.</summary>
        [TestCase("Assets/Prefabs/Cars/Vehicles/DrivableLandRoverDefender.prefab")]
        [TestCase("Assets/Prefabs/Cars/Vehicles/DrivableGenericSedan.prefab")]
        [TestCase("Assets/Prefabs/Cars/Vehicles/DrivableKenneyVan.prefab")]
        [TestCase("Assets/Prefabs/Cars/Vehicles/DrivableBoxTruck.prefab")]
        public void SettlesWithTheTyresOnTheGround(string path)
        {
            // (in a preview scene of its own, with its own physics: the project's physics settings stay as they are)
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.localScale = new Vector3(60f, 1f, 60f);
                ground.transform.position = new Vector3(0f, -0.5f, 0f);   // its top at y = 0
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
                var car = UnityEngine.Object.Instantiate(prefab, new Vector3(0f, 0.15f, 0f), Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(car, scene);
                var rb = car.GetComponent<Rigidbody>();
                rb.isKinematic = false; rb.useGravity = true;
                var suspension = TypeNamed("VehicleBehaviour.Suspension");
                var models = car.GetComponentsInChildren<WheelCollider>(true).Select(w => w.GetComponent(suspension))
                    .Where(s => s != null).Select(s => suspension.GetField("wheelModel").GetValue(s) as GameObject).Where(m => m != null).ToList();
                var physics = scene.GetPhysicsScene();
                for (int i = 0; i < 200; i++) physics.Simulate(0.02f);
                Assert.Less(rb.linearVelocity.magnitude, 0.05f, "settled");
                int n = 0;
                foreach (var m in models)
                    if (Wheel(m).radius > 0f) { n++; Assert.AreEqual(0f, Wheel(m).bottom, 0.03f, $"{prefab.name} {m.name}: tyre bottom above the ground"); }
                Assert.GreaterOrEqual(n, 2, "wheels measured");
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
