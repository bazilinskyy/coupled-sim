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
            // parked cars take 2.1 m along a kerb, their centre 1 m inside it: a car (1.8 m wide) in the lane must not touch them
            // (a 5.5 m street with cars on both sides leaves 1.7 m: it is driven in its middle)
            var narrow = Road(-200, 0, 200, 0); narrow.Width = 5.5f; narrow.ParkLeft = narrow.ParkRight = true;
            foreach (var r in MapRoutes.Build(new[] { narrow }, 200f, false))
                Assert.AreEqual(0f, Vector3.Dot(r[r.Count / 2], Vector3.Cross(Vector3.UnitY, Vector3.Normalize(r[r.Count - 1] - r[0]))), 0.05f);
            foreach (float width in new[] { 6.5f, 7f, 9f })
            {
                var road = Road(-200, 0, 200, 0); road.Width = width; road.ParkLeft = road.ParkRight = true;
                foreach (var r in MapRoutes.Build(new[] { road }, 200f, false))
                {
                    var dir = Vector3.Normalize(r[r.Count - 1] - r[0]);
                    float off = Vector3.Dot(r[r.Count / 2], Vector3.Cross(Vector3.UnitY, dir));
                    Assert.GreaterOrEqual(off, -0.01f, $"{width} m: on its own side or in the middle");
                    Assert.LessOrEqual(off + 0.9f, width / 2f - 1.0f - 0.9f + 0.01f, $"{width} m: clear of the parked cars");
                }
            }
        }

        [Test]
        public void TJunctionTurns()
        {
            // a road from the south edge ends at a road running east-west: routes turn into it
            var routes = MapRoutes.Build(new[] { Road(-200, 50, 200, 50), Road(0, -200, 0, 50) }, 200f, false);
            Assert.IsTrue(routes.Exists(r => r[0].Z < -150f && Math.Abs(r[r.Count - 1].X) > 150f), "from the south, turning at the T");
        }
    }
}
