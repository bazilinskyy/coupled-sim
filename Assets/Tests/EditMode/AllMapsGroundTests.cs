using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CoupledSim.Tests
{
    /// <summary>
    /// Builds every city map of the catalogue on its terrain and checks that what stands in it stands on the ground:
    /// trees, street lamps, signs, street furniture and parked vehicles neither float above the surface under them nor
    /// sink into it, signal heads hang at their height over the road, and the paths of traffic run on the road. Catches
    /// mistakes in the terrain, the real-world data or the placement before they reach an experiment or a screenshot.
    /// About 20-40 s per map; in the category "AllMaps" (-testCategory AllMaps runs only these).
    /// </summary>
    [Category("AllMaps")]
    public class AllMapsGroundTests
    {
        static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        static IEnumerable<string> Maps()
        {
            var cat = TypeNamed("MapCatalog");
            var maps = (Array)cat.GetField("Maps").GetValue(null);
            foreach (var m in maps) yield return (string)m.GetType().GetField("Name").GetValue(m);
        }

        GameObject _root;
        [TearDown] public void TearDown() { if (_root != null) UnityEngine.Object.DestroyImmediate(_root); }

        /// <summary>Most a placed object may be off the ground (m): above it, and into it (foundations, kerbs, roots).</summary>
        const float MaxFloat = 0.45f, MaxSink = 0.7f;
        /// <summary>The share of objects that may be off (OpenStreetMap's odd corners: objects on bridges or in tunnels' mouths).</summary>
        const float MaxShareOff = 0.02f;

        // (building a map takes up to 3 minutes: Eindhoven's took 156-187 s, near NUnit's default limit of 180 s)
        [TestCaseSource(nameof(Maps)), Timeout(360000)]
        public void EverythingStandsOnTheGround(string map)
        {
            var catType = TypeNamed("MapCatalog");
            var args = new object[] { map, null };
            Assert.IsTrue((bool)catType.GetMethod("TryGet").Invoke(null, args), map);
            var info = args[1];
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, "Maps", map + ".osm.gz");
            string xml;
            using (var gz = new System.IO.Compression.GZipStream(System.IO.File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress))
            using (var rd = new System.IO.StreamReader(gz)) xml = rd.ReadToEnd();
            var dataType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }).First(t => t.Name == "OsmData");
            var data = dataType.GetMethod("Parse").Invoke(null, new object[] { xml });
            var assets = TypeNamed("MapAssets").GetProperty("Instance").GetValue(null);
            var gen = Activator.CreateInstance(TypeNamed("OsmMapGenerator"), info, data, assets);
            _root = new GameObject(map);
            // (without the far city: no colliders, nothing on the map's ground, and Eindhoven's build ran over the timeout)
            var noFar = gen.GetType().GetField("NoFarCity");
            var wasNoFar = noFar != null && (bool)noFar.GetValue(null);
            noFar?.SetValue(null, true);
            try { gen.GetType().GetMethod("Build").Invoke(gen, new object[] { _root.transform }); }
            finally { noFar?.SetValue(null, wasNoFar); }
            var terrain = gen.GetType().GetProperty("Terrain").GetValue(gen);
            if (terrain != null) terrain.GetType().GetMethod("Apply").Invoke(terrain, new object[] { _root.transform, gen });
            Physics.SyncTransforms();

            var emitted = (System.Collections.IEnumerable)gen.GetType().GetProperty("EmittedMeshes").GetValue(gen);
            var emittedSet = new HashSet<GameObject>(emitted.Cast<GameObject>());
            var surfaces = new HashSet<Collider>();
            foreach (GameObject go in emittedSet) if (go != null && go.TryGetComponent<Collider>(out var c)) surfaces.Add(c);
            var carSignal = TypeNamed("CarTrafficLight");
            var pedSignal = TypeNamed("PedestrianTrafficLight");

            // the ground under a point: the surface (road, pavement, ground) nearest below or a little above it
            string hitName = "";
            bool Ground(Vector3 p, out float y)
            {
                y = 0f;
                float best = float.MaxValue;
                hitName = "";
                foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!surfaces.Contains(h.collider)) continue;
                    var n = h.collider.name;
                    if (n.StartsWith("Buildings") || n.StartsWith("Roofs") || n == "Cornices") continue;
                    float d = Mathf.Abs(h.point.y - p.y);
                    if (d < best) { best = d; y = h.point.y; hitName = n; }
                }
                return best < float.MaxValue;
            }

            int objects = 0, off = 0, signals = 0, badSignals = 0;
            var worst = new List<string>();
            void Visit(Transform t)
            {
                if (emittedSet.Contains(t.gameObject)) return;
                if (t != _root.transform && t.GetComponents<Component>().Length > 1)
                {
                    if (carSignal != null && t.GetComponent(carSignal) != null)
                    {
                        // a signal head over the road, 5 m up
                        signals++;
                        if (!Ground(t.position - Vector3.up * 5f, out var gy) || Mathf.Abs(t.position.y - gy - 5f) > 0.8f) { badSignals++; if (worst.Count < 12) worst.Add($"{t.name} at {t.position}: {t.position.y - gy:F1} m over the ground"); }
                        return;
                    }
                    if (pedSignal != null && t.GetComponent(pedSignal) != null)
                    {
                        // a pedestrian light at eye height on its pole (which goes on into the ground)
                        signals++;
                        if (!Ground(t.position - Vector3.up * 2.6f, out var gy) || Mathf.Abs(t.position.y - gy - 2.6f) > 0.6f) { badSignals++; if (worst.Count < 12) worst.Add($"{t.name} at {t.position}: {t.position.y - gy:F1} m over the ground ({hitName})"); }
                        return;
                    }
                    var rs = t.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) return;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (b.size.y > 40f) return;   // not a street object
                    var foot = new Vector3(t.position.x, b.min.y, t.position.z);
                    if (!Ground(foot, out var g)) return;   // over water or beyond the map's surfaces
                    objects++;
                    float dy = foot.y - g;
                    if (dy > MaxFloat || dy < -MaxSink)
                    {
                        off++;
                        if (worst.Count < 12) worst.Add($"{t.name} at {t.position}: {(dy > 0 ? "floats" : "sinks")} {Mathf.Abs(dy):F2} m over {hitName}");
                    }
                    return;
                }
                for (int i = 0; i < t.childCount; i++) Visit(t.GetChild(i));
            }
            Visit(_root.transform);

            // the traffic's paths on the roads
            var roads = (System.Collections.IList)gen.GetType().GetField("Roads").GetValue(gen);
            float half = (float)gen.GetType().GetField("HalfSize").GetValue(gen);
            int points = 0, offRoad = 0;
            foreach (var road in roads)
            {
                var line = (List<Vector3>)road.GetType().GetField("Line").GetValue(road);
                foreach (var p in line)
                {
                    // inside the map (long roads run on beyond it)
                    if (Mathf.Abs(p.x) > half || Mathf.Abs(p.z) > half || !Ground(p, out var g)) continue;
                    points++;
                    if (Mathf.Abs(p.y - g) > 0.5f) { offRoad++; if (worst.Count < 24) worst.Add($"road point {p}: {p.y - g:F2} m from {hitName}"); }
                }
            }
            Debug.Log($"{map}: {objects} objects, {off} off the ground; {signals} signal heads, {badSignals} not at their height; {points} road points, {offRoad} off the road" +
                      (worst.Count > 0 ? "\n  " + string.Join("\n  ", worst) : ""));
            Assert.Greater(objects, 50, "the map has street objects");
            Assert.LessOrEqual(off, MaxShareOff * objects, $"{map}: {off} of {objects} objects off the ground:\n  " + string.Join("\n  ", worst));
            Assert.LessOrEqual(badSignals, Math.Max(1, signals / 20), $"{map}: {badSignals} of {signals} signal heads not 5 m over the ground");
            Assert.LessOrEqual(offRoad, MaxShareOff * Math.Max(1, points), $"{map}: {offRoad} of {points} road points off the road");
        }
    }
}
