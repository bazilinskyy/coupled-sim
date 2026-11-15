using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    /// <summary>Lanes per direction from OpenStreetMap's lane tags (OsmMapGenerator.LaneCounts).</summary>
    public class LaneMarkingTests
    {
        static (int fwd, int bwd) Lanes(string tags, float width)
        {
            var xml = "<osm><node id=\"1\" lat=\"51.44\" lon=\"5.47\"/><node id=\"2\" lat=\"51.441\" lon=\"5.47\"/><way id=\"10\"><nd ref=\"1\"/><nd ref=\"2\"/>" +
                      string.Concat(tags.Split(';').Where(t => t.Length > 0).Select(t => { var kv = t.Split('='); return $"<tag k=\"{kv[0]}\" v=\"{kv[1]}\"/>"; })) +
                      "<tag k=\"highway\" v=\"secondary\"/></way></osm>";
            var data = CoupledSim.Maps.OsmData.Parse(xml);
            var gen = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("OsmMapGenerator")).First(t => t != null);
            var args = new object[] { data.Ways[0], width, 0, 0 };
            gen.GetMethod("LaneCounts", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            return ((int)args[2], (int)args[3]);
        }

        [TestCase("", 7f, 1, 1)]
        [TestCase("lanes=4", 13f, 2, 2)]
        [TestCase("lanes=3;lanes:forward=2", 10f, 2, 1)]
        [TestCase("lanes=3;lanes:backward=2", 10f, 1, 2)]
        [TestCase("lanes=3;oneway=yes", 10f, 3, 0)]
        [TestCase("lanes=2;oneway=-1", 7f, 0, 2)]
        [TestCase("oneway=yes", 6.6f, 2, 0)]
        public void LanesPerDirection(string tags, float width, int fwd, int bwd)
        {
            var (f, b) = Lanes(tags, width);
            Assert.AreEqual(fwd, f, "forward");
            Assert.AreEqual(bwd, b, "backward");
        }
    }
}
