using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CoupledSim.Maps;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    public class MapAreaTests
    {
        // a flat projection for the tests: 1e-3 degrees = 100 m
        static Vector2 Local(double lat, double lon) => new Vector2((float)(lon * 1e5), (float)(lat * 1e5));

        [Test]
        public void BoxAndCorridor()
        {
            var a = MapArea.Parse("# test\nbox -0.002 -0.002 0.002 0.002\ncorridor 100 0 0 0 0.02 0.01 0.02  # east, then north\n", Local);
            Assert.AreEqual(1, a.CorridorCount);
            Assert.AreEqual(3000f, a.CorridorLength, 1f);
            Assert.IsTrue(a.Contains(new Vector2(0, 0)));
            Assert.IsTrue(a.Contains(new Vector2(1500, 40)), "in the corridor");
            Assert.IsFalse(a.Contains(new Vector2(1500, 80)), "beside the corridor");
            Assert.IsTrue(a.Contains(new Vector2(1500, 80), 40f), "within the margin");
            Assert.IsTrue(a.Contains(new Vector2(2000, 900)), "the corridor's second leg");
            Assert.IsFalse(a.Contains(new Vector2(1000, 900)));
            Assert.AreEqual(50f, a.Depth(new Vector2(1500, 0)), 0.01f);
            Assert.AreEqual(-30f, a.Depth(new Vector2(1500, 80)), 0.01f);
            Assert.IsTrue(a.AtEdge(new Vector2(2000, 1045), 40f), "past the corridor's end");
            Assert.IsFalse(a.AtEdge(new Vector2(2000, 1000), 40f), "its end point is in its middle");
            Assert.IsFalse(a.AtEdge(new Vector2(1000, 0), 40f));
            Assert.AreEqual(-200f, a.MinX, 0.01f);
            Assert.AreEqual(2050f, a.MaxX, 0.01f);
            Assert.AreEqual(1050f, a.MaxZ, 0.01f);
            Assert.IsTrue(a.Touches(new Vector2(1500, 60), 10f));
            Assert.IsFalse(a.Touches(new Vector2(1500, 300), 10f));
        }

        [Test]
        public void EindhovenCorridorFollowsTheA270()
        {
            var proj = new GeoProjection(51.441, 5.4775);
            Vector2 L(double la, double lo) { proj.ToLocal(la, lo, out var x, out var z); return new Vector2((float)x, (float)z); }
            var a = MapArea.Parse(System.IO.File.ReadAllText("Assets/StreamingAssets/Maps/Eindhoven.area.txt"), L);
            Assert.Greater(a.CorridorLength, 10000f);
            // the station, the A270 eastbound past its start, the N270 at Helmond and the Automotive Campus are in it
            foreach (var (la, lo) in new[] { (51.4433, 5.4797), (51.4603, 5.5592), (51.4712, 5.6079), (51.4741, 5.6235) })
                Assert.IsTrue(a.Contains(L(la, lo)), $"{la}, {lo} in the area ({a.Depth(L(la, lo)):F0} m)");
            // fields south of the N270 at Helmond are not
            Assert.IsFalse(a.Contains(new Vector2(9024, 3119)), $"depth {a.Depth(new Vector2(9024, 3119)):F0} m");
        }

        [Test]
        public void RoutesEndAtTheAreasEdge()
        {
            // a long road through the corridor: routes run its whole length, from one end of the area to the other
            var area = MapArea.Parse("box -0.002 -0.002 0.002 0.002\ncorridor 100 0 0 0 0.03\n", Local);
            var road = new MapRoutes.RoadLine { Line = new List<Vector3>(), Width = 7f };
            for (int x = -200; x <= 3100; x += 20) road.Line.Add(new Vector3(x, 0, 0));
            var routes = MapRoutes.Build(new[] { road }, 1500f, false, depth: p => area.Depth(new Vector2(p.X, p.Z)));
            Assert.AreEqual(2, routes.Count, "both directions");
            foreach (var r in routes)
            {
                float len = 0f; for (int i = 1; i < r.Count; i++) len += Vector3.Distance(r[i - 1], r[i]);
                Assert.Greater(len, 3000f, "the whole road");
            }
            // without the area, a 1500 m square map would end the routes long before
            var square = MapRoutes.Build(new[] { road }, 1500f, false);
            Assert.IsTrue(square.All(r => Vector3.Distance(r[0], r[r.Count - 1]) < 3200f));
        }
    }
}
