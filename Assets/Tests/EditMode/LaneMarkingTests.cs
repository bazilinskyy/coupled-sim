using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    /// <summary>Lanes per direction from OpenStreetMap's lane tags (OsmMapGenerator.LaneCounts).</summary>
    public class LaneMarkingTests
    {
        static object Way(string tags)
        {
            var xml = "<osm><node id=\"1\" lat=\"51.44\" lon=\"5.47\"/><node id=\"2\" lat=\"51.441\" lon=\"5.47\"/><way id=\"10\"><nd ref=\"1\"/><nd ref=\"2\"/>" +
                      string.Concat(tags.Split(';').Where(t => t.Length > 0).Select(t => { var kv = t.Split('='); return $"<tag k=\"{kv[0]}\" v=\"{kv[1]}\"/>"; })) +
                      "<tag k=\"highway\" v=\"secondary\"/></way></osm>";
            return CoupledSim.Maps.OsmData.Parse(xml).Ways[0];
        }

        static Type Gen => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("OsmMapGenerator")).First(t => t != null);

        static (int fwd, int bwd) Lanes(string tags, float width)
        {
            var args = new object[] { Way(tags), width, 0, 0 };
            Gen.GetMethod("LaneCounts", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            return ((int)args[2], (int)args[3]);
        }

        static (bool right, bool left) Sides(string method, string tags, bool lht)
        {
            var args = new object[] { Way(tags), lht, false, false };
            Gen.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            return ((bool)args[2], (bool)args[3]);
        }

        [TestCase("", 7f, 1, 1)]
        [TestCase("lanes=4", 13f, 2, 2)]
        [TestCase("lanes=3;lanes:forward=2", 10f, 2, 1)]
        [TestCase("lanes=3;lanes:backward=2", 10f, 1, 2)]
        [TestCase("lanes=3;oneway=yes", 10f, 3, 0)]
        [TestCase("lanes=2;oneway=-1", 7f, 0, 2)]
        [TestCase("oneway=yes", 6.6f, 2, 0)]
        [TestCase("oneway=yes", 5.5f, 1, 0)]
        public void LanesPerDirection(string tags, float width, int fwd, int bwd)
        {
            var (f, b) = Lanes(tags, width);
            Assert.AreEqual(fwd, f, "forward");
            Assert.AreEqual(bwd, b, "backward");
        }

        // cycle and bus lanes: on the side of the kerb of a one-way road's traffic (right in right-hand traffic, left in
        // left-hand traffic, the other way round for oneway=-1), per-lane bus tags from the driver's left
        [TestCase("CycleLanes", "cycleway=lane;oneway=yes", false, true, false)]
        [TestCase("CycleLanes", "cycleway=lane;oneway=yes", true, false, true)]
        [TestCase("CycleLanes", "cycleway=lane;oneway=-1", false, false, true)]
        [TestCase("CycleLanes", "cycleway=opposite_lane;oneway=yes", false, false, true)]
        [TestCase("CycleLanes", "cycleway=lane", true, true, true)]
        [TestCase("CycleLanes", "cycleway:right=lane;oneway=yes", true, true, false)]
        [TestCase("BusLanes", "busway=lane;oneway=yes", true, false, true)]
        [TestCase("BusLanes", "lanes=4;bus:lanes:forward=designated|yes", true, false, true)]
        [TestCase("BusLanes", "lanes=4;bus:lanes:forward=yes|designated", false, true, false)]
        [TestCase("BusLanes", "lanes=4;bus:lanes:backward=yes|designated", false, false, true)]
        [TestCase("BusLanes", "lanes=4;bus:lanes:backward=designated|yes", true, true, false)]
        [TestCase("BusLanes", "lanes=4;bus:lanes=designated|yes|yes|designated", false, true, true)]
        [TestCase("BusLanes", "lanes=2;oneway=-1;bus:lanes=designated|yes", false, true, false)]
        public void LaneSides(string method, string tags, bool lht, bool right, bool left)
        {
            var (r, l) = Sides(method, tags, lht);
            Assert.AreEqual(right, r, "right");
            Assert.AreEqual(left, l, "left");
        }

        [TestCase("Acer palmatum", "deciduous")]
        [TestCase("Phoenix canariensis", "palm")]
        [TestCase("Cabbage palm", "palm")]
        [TestCase("Pinus nigra", "conifer")]
        [TestCase("Platanus x hispanica", "deciduous")]
        public void TreeKinds(string species, string kind)
        {
            Assert.AreEqual(kind, Gen.GetMethod("TreeKind").Invoke(null, new object[] { species, null }));
        }
    }
}
