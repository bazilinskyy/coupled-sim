using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CoupledSim.Maps;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    public class MapGeometryTests
    {
        [Test]
        public void TriangulatesConcavePolygon()
        {
            // L-shaped building, area 3
            var poly = new List<Vector2> { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 1), new Vector2(1, 1), new Vector2(1, 2), new Vector2(0, 2) };
            var tris = Geometry2D.Triangulate(poly);
            Assert.AreEqual(12, tris.Count, "4 triangles");
            float area = 0;
            for (int i = 0; i < tris.Count; i += 3)
                area += Math.Abs(Geometry2D.Cross(poly[tris[i + 1]] - poly[tris[i]], poly[tris[i + 2]] - poly[tris[i]])) / 2;
            Assert.AreEqual(3f, area, 1e-4);
        }

        [Test]
        public void TriangulatesClockwiseInput()
        {
            var poly = new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            Assert.AreEqual(6, Geometry2D.Triangulate(poly).Count);
        }

        [Test]
        public void StripHasTheRequestedWidth()
        {
            var line = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10) };
            var l = new List<Vector2>(); var r = new List<Vector2>();
            Geometry2D.Strip(line, 4f, 0f, l, r);
            Assert.AreEqual(4f, Vector2.Distance(l[0], r[0]), 1e-4);
            Assert.AreEqual(4f, Vector2.Distance(l[2], r[2]), 1e-4);
            // right of an eastward line is south (-y)
            Assert.Less(r[0].Y, l[0].Y);
        }

        [Test]
        public void RemainingCutsOutJunctions()
        {
            var parts = Geometry2D.Remaining(100f, new[] { (0f, 5f), (50f, 6f), (100f, 5f) });
            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual((5f, 44f), parts[0]);
            Assert.AreEqual((56f, 95f), parts[1]);
        }

        [Test]
        public void SubLineFollowsTheLine()
        {
            var line = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10) };
            var sub = Geometry2D.SubLine(line, 5f, 15f);
            Assert.AreEqual(new Vector2(5, 0), sub.First());
            Assert.AreEqual(new Vector2(10, 5), sub.Last());
            Assert.AreEqual(10f, Geometry2D.Length(sub), 1e-4);
        }

        [Test]
        public void ProjectionMatchesSumoNetconvert()
        {
            // netconvert with +proj=tmerc +lat_0=51.441 +lon_0=5.4775 put these OSM nodes at:
            var p = new GeoProjection(51.441, 5.4775);
            p.ToLocal(51.4370852, 5.4742606, out var x, out var z);
            Assert.AreEqual(-225.26, x, 0.02);
            Assert.AreEqual(-435.54, z, 0.02);
            p.ToLocal(51.4379731, 5.4743008, out x, out z);
            Assert.AreEqual(-222.46, x, 0.02);
            Assert.AreEqual(-336.76, z, 0.02);
        }

        [Test]
        public void ParsesOsmXml()
        {
            var xml = "<osm><node id=\"1\" lat=\"51.44\" lon=\"5.47\"><tag k=\"highway\" v=\"crossing\"/></node>" +
                      "<node id=\"2\" lat=\"51.45\" lon=\"5.48\"/><way id=\"10\"><nd ref=\"1\"/><nd ref=\"2\"/><tag k=\"highway\" v=\"residential\"/></way></osm>";
            var d = OsmData.Parse(xml);
            Assert.AreEqual(2, d.Nodes.Count);
            Assert.AreEqual("crossing", d.Nodes[1].Tag("highway"));
            Assert.AreEqual(new long[] { 1, 2 }, d.Ways[0].NodeIds.ToArray());
            Assert.AreEqual(51.44, d.MinLat, 1e-9);
        }


        [Test]
        public void ProjectionRoundTrip()
        {
            var proj = new GeoProjection(51.4483, 5.4908);
            foreach (var (x, z) in new[] { (0.0, 0.0), (850.0, -420.0), (-1500.0, 2200.0) })
            {
                proj.ToGeo(x, z, out var lat, out var lon);
                proj.ToLocal(lat, lon, out var x2, out var z2);
                Assert.AreEqual(x, x2, 1e-3);
                Assert.AreEqual(z, z2, 1e-3);
            }
        }

        [Test]
        public void RdProjectionMatchesReferenceAndRoundTrips()
        {
            // reference from the same polynomials (Tools/bag3d_buildings.py)
            RdProjection.FromWgs(52.0, 5.0, out var x, out var y);
            Assert.AreEqual(128409.900, x, 0.01);
            Assert.AreEqual(445806.276, y, 0.01);
            // Delft Markt: back and forth within a few centimetres
            RdProjection.FromWgs(52.0116, 4.3586, out var dx, out var dy);
            RdProjection.ToWgs(dx, dy, out var lat, out var lon);
            Assert.AreEqual(52.0116, lat, 1e-6);
            Assert.AreEqual(4.3586, lon, 1e-6);
        }
    }
}
