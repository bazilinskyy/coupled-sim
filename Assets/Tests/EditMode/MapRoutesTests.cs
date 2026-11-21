using System.Collections.Generic;
using System;
using System.Numerics;
using CoupledSim.Maps;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    /// <summary>Lane routes across a city map for the built-in traffic (MapRoutes).</summary>
    public class MapRoutesTests
    {
        static MapRoutes.RoadLine Road(float x0, float z0, float x1, float z1, bool oneWay = false)
        {
            var l = new List<Vector3>();
            for (int i = 0; i <= 10; i++) l.Add(Vector3.Lerp(new Vector3(x0, 0, z0), new Vector3(x1, 0, z1), i / 10f));
            return new MapRoutes.RoadLine { Line = l, Width = 8f, OneWay = oneWay };
        }

        [Test]
        public void CrossroadsGiveStraightRoutesOnTheRight()
        {
            // two roads crossing in the middle of a 400 m map
            var roads = new[] { Road(-200, 0, 200, 0), Road(0, -200, 0, 200) };
            var routes = MapRoutes.Build(roads, 200f, false);
            Assert.AreEqual(4, routes.Count, "each road both ways");
            foreach (var r in routes)
            {
                Assert.Greater(MapRoutes.Length(r), 380f);
                var a = r[0]; var b = r[r.Count - 1]; var dir = Vector3.Normalize(b - a);
                var right = Vector3.Cross(Vector3.UnitY, dir);
                Assert.AreEqual(2f, Vector3.Dot(a, right), 0.3f, "a quarter of the road's width to the right");
            }
        }

        [Test]
        public void LeftHandTrafficKeepsLeftAndOneWayRoadsOneWay()
        {
            var routes = MapRoutes.Build(new[] { Road(-200, 0, 200, 0) }, 200f, true);
            Assert.AreEqual(2, routes.Count);
            foreach (var r in routes) { var dir = Vector3.Normalize(r[r.Count - 1] - r[0]); Assert.AreEqual(-2f, Vector3.Dot(r[0], Vector3.Cross(Vector3.UnitY, dir)), 0.3f); }
            var one = MapRoutes.Build(new[] { Road(-200, 0, 200, 0, oneWay: true) }, 200f, false);
            Assert.AreEqual(1, one.Count);
            Assert.Greater(one[0][one[0].Count - 1].X, one[0][0].X, "along the way");
        }

        [Test]
        public void LanesKeepClearOfStreetParking()
        {
            // parked cars (1.8 m wide) stand ParkDepth into the carriageway; a car in the lane (1.8 m wide) neither touches
            // them nor the oncoming car in the other lane
            foreach (float width in new[] { 5.5f, 6.5f, 7f, 9f })
            {
                var road = Road(-200, 0, 200, 0); road.Width = width; road.ParkLeft = road.ParkRight = true;
                float depth = MapRoutes.ParkDepth(width, false, 2), parkedInner = width / 2f + 0.2f - depth;
                foreach (var r in MapRoutes.Build(new[] { road }, 200f, false))
                {
                    var dir = Vector3.Normalize(r[r.Count - 1] - r[0]);
                    float off = Vector3.Dot(r[r.Count / 2], Vector3.Cross(Vector3.UnitY, dir));
                    Assert.GreaterOrEqual(off, 0.9f - 0.01f, $"{width} m: clear of the oncoming lane");
                    Assert.LessOrEqual(off + 0.9f, parkedInner + 0.01f, $"{width} m: clear of the parked cars (depth {depth})");
                }
            }
            Assert.AreEqual(2.1f, MapRoutes.ParkDepth(9f, false, 2), "wide enough: on the road");
            Assert.AreEqual(0.2f, MapRoutes.ParkDepth(5.5f, false, 2), "narrow: on the kerb");
        }

        [Test]
        public void TJunctionTurns()
        {
            // a road from the south edge ends at a road running east-west: routes turn into it
            var routes = MapRoutes.Build(new[] { Road(-200, 50, 200, 50), Road(0, -200, 0, 50) }, 200f, false);
            Assert.IsTrue(routes.Exists(r => r[0].Z < -150f && Math.Abs(r[r.Count - 1].X) > 150f), "from the south, turning at the T");
        }

        /// <summary>Radius (m) of the circle through three points (infinite on a straight line).</summary>
        static float Radius(Vector3 a, Vector3 b, Vector3 c)
        {
            float ab = Vector3.Distance(a, b), bc = Vector3.Distance(b, c), ca = Vector3.Distance(c, a);
            float cross = Math.Abs((b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X));
            return cross < 1e-6f ? float.PositiveInfinity : ab * bc * ca / (2f * cross);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TurnsAreDrivableAndKeepToTheirSide(bool leftHand)
        {
            // a corner: a road from the south edge turns east at (0, 50) (8 m wide; lanes 2 m from the centre lines). The
            // routes round it (from the south to the east, and back) bend no tighter than a car can turn (4.7 m), keep to
            // their side of the road they leave until the junction, and are on their side of the road they turn into once
            // past it (a wide turn swung out into its oncoming lane first)
            var routes = MapRoutes.Build(new[] { Road(0, -200, 0, 50), Road(0, 50, 200, 50) }, 200f, leftHand);
            Assert.AreEqual(2, routes.Count, "round the corner both ways");
            float side = leftHand ? -1f : 1f;   // the driver's side: right
            foreach (var r in routes)
            {
                bool north = r[0].Z < -150f;   // from the south, turning east; else from the east, turning south
                string what = north ? "south to east" : "east to south";
                for (int i = 1; i + 1 < r.Count; i++)
                    Assert.Greater(Radius(r[i - 1], r[i], r[i + 1]), 4.7f, $"{what}: bend at {r[i]}");
                foreach (var p in r)
                {
                    // on the north-south road (south of the junction's edge): northbound east of its centre line (in
                    // right-hand traffic), southbound west of it
                    float ns = (north ? 1f : -1f) * side * p.X;
                    // on the east-west road (east of the junction's edge): eastbound south of its centre line, westbound north
                    float ew = (north ? -1f : 1f) * side * (p.Z - 50f);
                    if (p.Z < 46f) Assert.Greater(ns, 0.5f, $"{what}: north-south road at {p}");
                    if (p.Z < 35f) Assert.AreEqual(2f, ns, 0.3f, $"{what}: in the lane at {p}");
                    if (p.X > 4f) Assert.Greater(ew, 0.5f, $"{what}: east-west road at {p}");
                    if (p.X > 15f) Assert.AreEqual(2f, ew, 0.3f, $"{what}: in the lane at {p}");
                }
            }
        }

        [Test]
        public void NarrowStreetsKeepTheCarsOnTheRoad()
        {
            // a two-way street 4 m wide: each lane close enough to the middle that a car (1.8 m) stays on the road, and the
            // two lanes 1.9 m apart at least, so two cars pass
            var road = Road(-200, 0, 200, 0); road.Width = 4f;
            var routes = MapRoutes.Build(new[] { road }, 200f, false);
            Assert.AreEqual(2, routes.Count);
            foreach (var r in routes)
            {
                var dir = Vector3.Normalize(r[r.Count - 1] - r[0]);
                float off = Vector3.Dot(r[r.Count / 2], Vector3.Cross(Vector3.UnitY, dir));
                Assert.LessOrEqual(off + 0.9f, 2f + 0.01f, "on the road");
                Assert.GreaterOrEqual(off, 0.95f - 0.01f, "clear of the oncoming lane");
            }
        }

        [Test]
        public void SmoothingKeepsStraightLanesAndEnds()
        {
            var line = new List<Vector3>();
            for (int i = 0; i <= 20; i++) line.Add(new Vector3(i * 7.3f, 0f, 3f));
            var s = MapRoutes.Smooth(line);
            Assert.AreEqual(0f, Vector3.Distance(line[0], s[0]), 1e-3f);
            Assert.AreEqual(0f, Vector3.Distance(line[line.Count - 1], s[s.Count - 1]), 1e-3f);
            foreach (var p in s) Assert.AreEqual(3f, p.Z, 1e-3f);
            for (int i = 1; i + 1 < s.Count; i++) Assert.AreEqual(MapRoutes.LaneSpacing, Vector3.Distance(s[i - 1], s[i]), 0.01f);
        }
    }
}
