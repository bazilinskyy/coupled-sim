using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CoupledSim.Tests
{
    /// <summary>Traffic lights on city maps: a signalised crossroads from OpenStreetMap gets a head per approach in two phases.</summary>
    public class TrafficSignalTests
    {
        static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        GameObject _root;
        [TearDown] public void TearDown() { if (_root != null) UnityEngine.Object.DestroyImmediate(_root); }

        // a crossroads 0.001° (about 70-110 m) per arm, the centre node tagged as signals (and a signalised crossing on the east arm)
        static string CrossroadsXml(string centreTags) =>
            "<osm><bounds minlat=\"51.439\" minlon=\"5.469\" maxlat=\"51.441\" maxlon=\"5.471\"/>" +
            $"<node id=\"1\" lat=\"51.44\" lon=\"5.47\">{centreTags}</node>" +
            "<node id=\"2\" lat=\"51.441\" lon=\"5.47\"/><node id=\"3\" lat=\"51.439\" lon=\"5.47\"/>" +
            "<node id=\"4\" lat=\"51.44\" lon=\"5.471\"/><node id=\"5\" lat=\"51.44\" lon=\"5.469\"/>" +
            "<node id=\"6\" lat=\"51.44\" lon=\"5.4703\"><tag k=\"highway\" v=\"crossing\"/><tag k=\"crossing\" v=\"traffic_signals\"/></node>" +
            "<way id=\"10\"><nd ref=\"2\"/><nd ref=\"1\"/><nd ref=\"3\"/><tag k=\"highway\" v=\"secondary\"/></way>" +
            "<way id=\"11\"><nd ref=\"5\"/><nd ref=\"1\"/><nd ref=\"6\"/><nd ref=\"4\"/><tag k=\"highway\" v=\"residential\"/></way></osm>";

        object Build(string xml)
        {
            var infoType = TypeNamed("MapInfo");
            object info = Activator.CreateInstance(infoType);
            void Set(string f, object v) => infoType.GetField(f).SetValue(info, v);
            Set("Name", "SignalTest"); Set("City", "SignalTest"); Set("Country", "Netherlands"); Set("Latitude", 51.44); Set("Longitude", 5.47);
            var dataType = TypeNamed("CoupledSim.Maps.OsmData") ?? AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).First(t => t.Name == "OsmData");
            var data = dataType.GetMethod("Parse").Invoke(null, new object[] { xml });
            var assets = TypeNamed("MapAssets").GetProperty("Instance").GetValue(null);
            var gen = Activator.CreateInstance(TypeNamed("OsmMapGenerator"), info, data, assets);
            _root = new GameObject("SignalTest");
            gen.GetType().GetMethod("Build").Invoke(gen, new object[] { _root.transform });
            return gen;
        }

        static int Field(object o, string name) => (int)o.GetType().GetField(name).GetValue(o);

        [Test]
        public void SignalisedCrossroadsHasAHeadPerApproachInTwoPhases()
        {
            var gen = Build(CrossroadsXml("<tag k=\"highway\" v=\"traffic_signals\"/>"));
            Assert.AreEqual(1, Field(gen, "SignalJunctions"), "one controller: the crossing is within 40 m");
            Assert.AreEqual(4, Field(gen, "SignalHeads"));
            Assert.AreEqual(2, Field(gen, "PedestrianSignalHeads"), "both kerbs of the crossing");
            var sections = _root.GetComponentsInChildren(TypeNamed("CarSection"));
            Assert.AreEqual(2, sections.Length);
            foreach (var s in sections)
                Assert.AreEqual(2, ((Array)s.GetType().GetField("carStreetLights").GetValue(s)).Length, s.name + ": the two opposite approaches");
            // every head shines towards its traffic: along the road, at the junction
            foreach (var h in _root.GetComponentsInChildren(TypeNamed("CarTrafficLight")))
            {
                var p = h.transform.position; var right = h.transform.right;
                Assert.Less(Mathf.Abs(right.y), 0.01f);
                Assert.Greater(Vector3.Dot(right, new Vector3(p.x, 0, p.z).normalized), 0.5f, h.name + " faces away from the junction");
            }
        }

        [Test]
        public void SignalBeforeAJunctionServesOnlyTheTrafficTowardsIt()
        {
            // the signal 20 m north of the junction, on the north arm: only southbound traffic
            var xml = CrossroadsXml("").Replace("<node id=\"2\" lat=\"51.441\" lon=\"5.47\"/>",
                "<node id=\"2\" lat=\"51.441\" lon=\"5.47\"/><node id=\"7\" lat=\"51.44018\" lon=\"5.47\"><tag k=\"highway\" v=\"traffic_signals\"/></node>")
                .Replace("<nd ref=\"2\"/><nd ref=\"1\"/>", "<nd ref=\"2\"/><nd ref=\"7\"/><nd ref=\"1\"/>");
            var gen = Build(xml);
            Assert.AreEqual(1, Field(gen, "SignalHeads"));
            var h = _root.GetComponentInChildren(TypeNamed("CarTrafficLight"));
            Assert.Greater(h.transform.right.z, 0.9f, "shines north, at the southbound traffic");
            // the signalised crossing of the side road (east arm): its pedestrians walk beside the southbound traffic, with
            // its green, so their heads (which face along the main road, towards its drivers) do not show red to them
            Assert.AreEqual(2, PedestrianLightsIn("Pedestrians with phase A"), "the side road's crossing walks with the main road's green");
            Assert.AreEqual(0, PedestrianLightsIn("Pedestrians with phase B"));
        }

        [Test]
        public void PedestriansCrossingTheSignalledRoadWaitForItsRed()
        {
            // a signalised crossing on the north arm, 20 m from the junction (its pedestrians cross the main road)
            var xml = CrossroadsXml("<tag k=\"highway\" v=\"traffic_signals\"/>")
                .Replace("<node id=\"6\" lat=\"51.44\" lon=\"5.4703\">", "<node id=\"6\" lat=\"51.44018\" lon=\"5.47\">")
                .Replace("<nd ref=\"2\"/><nd ref=\"1\"/>", "<nd ref=\"2\"/><nd ref=\"6\"/><nd ref=\"1\"/>")
                .Replace("<nd ref=\"1\"/><nd ref=\"6\"/><nd ref=\"4\"/>", "<nd ref=\"1\"/><nd ref=\"4\"/>");
            Build(xml);
            // the main road (secondary, north-south) is phase A: its crossing walks in phase B, and its heads face across it
            Assert.AreEqual(2, PedestrianLightsIn("Pedestrians with phase B"));
            Assert.AreEqual(0, PedestrianLightsIn("Pedestrians with phase A"));
            foreach (var h in _root.GetComponentsInChildren(TypeNamed("PedestrianTrafficLight")))
            {
                Assert.Greater(Mathf.Abs(h.transform.right.x), 0.95f, h.name + " shines across the road, at the other kerb");
                Assert.AreEqual(2, h.GetComponentsInChildren<MeshRenderer>().Count(r => r.name.EndsWith("figure")), h.name + ": a standing and a walking figure");
            }
            // the lenses are lit towards the front only (not seen from the side by the drivers on the road they cross)
            foreach (var r in _root.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "UpLight" || r.name == "MiddleLight" || r.name == "DownLight"))
                Assert.AreEqual(2, r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, r.transform.parent.name + "/" + r.name + ": one face");
        }

        [Test]
        public void StopLineBeforeACrossingOnTheApproach()
        {
            // a signalised junction with a crossing 6 m north of it, on the stop line's place (5.5 m: half the secondary
            // road's 7 m and 2 m): the southbound traffic waits before its stripes (2 m either side), at 8.5 m
            var xml = CrossroadsXml("<tag k=\"highway\" v=\"traffic_signals\"/>")
                .Replace("<node id=\"6\" lat=\"51.44\" lon=\"5.4703\">", "<node id=\"6\" lat=\"51.4400539\" lon=\"5.47\">")
                .Replace("<nd ref=\"2\"/><nd ref=\"1\"/>", "<nd ref=\"2\"/><nd ref=\"6\"/><nd ref=\"1\"/>")
                .Replace("<nd ref=\"1\"/><nd ref=\"6\"/><nd ref=\"4\"/>", "<nd ref=\"1\"/><nd ref=\"4\"/>");
            Build(xml);
            var southbound = _root.GetComponentsInChildren(TypeNamed("CarTrafficLight")).Single(h => h.transform.right.z > 0.9f);
            Assert.That(southbound.transform.position.z, Is.InRange(8.2f, 9f), "the head over the stop line, 2.5 m before the crossing's node");
            var northbound = _root.GetComponentsInChildren(TypeNamed("CarTrafficLight")).Single(h => h.transform.right.z < -0.9f);
            Assert.That(northbound.transform.position.z, Is.InRange(-6f, -5f), "no crossing on the south arm: the stop line where it was");
        }

        int PedestrianLightsIn(string section)
        {
            foreach (var s in _root.GetComponentsInChildren(TypeNamed("PedestrianSection")))
                if (s.name == section) return ((Array)s.GetType().GetField("pedestrianLights").GetValue(s)).Length;
            return 0;
        }
    }
}
