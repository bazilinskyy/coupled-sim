using System;
using NUnit.Framework;
using UnityEngine;

namespace CoupledSim.Tests
{
    /// <summary>
    /// A vehicle standing in a car's path is stopped for by its body, not by its middle (TrafficCar.BodyDist): a pickup
    /// waiting across the autopilot's lane, its nose in the lane and its middle 2.7 m beside the path, was driven into.
    /// TrafficCar is in the game's own assembly (Assembly-CSharp): called by reflection.
    /// </summary>
    public class TrafficObstacleTests
    {
        static float BodyDist(Vector3 p, Vector3 centre, Vector3 forward, float halfLength, float halfWidth) =>
            (float)Type.GetType("TrafficCar, Assembly-CSharp", true)
                .GetMethod("BodyDist", new[] { typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(float), typeof(float) })
                .Invoke(null, new object[] { p, centre, forward, halfLength, halfWidth });

        [Test]
        public void ABodyAcrossThePathIsInTheWay()
        {
            // a car driving along +z on x = 0; a pickup (5.6 m by 2 m) facing +x with its middle 2.7 m to the right
            var centre = new Vector3(2.7f, 0f, 10f);
            const float halfLength = 2.8f, halfWidth = 1f, carHalfWidth = 0.9f;
            // its middle is further from the path than the half widths of both (the old test) ...
            Assert.Greater(Mathf.Abs(centre.x), carHalfWidth + halfWidth);
            // ... but its nose reaches over the path: the car's body (half its width and 5 cm) would touch it at z = 10
            Assert.Less(BodyDist(new Vector3(0f, 0f, 10f), centre, Vector3.right, halfLength, halfWidth), carHalfWidth + 0.05f);
            Assert.AreEqual(0f, BodyDist(new Vector3(0f, 0f, 10f), centre, Vector3.right, halfLength, halfWidth), 1e-4f, "on the path, inside its body");
            // and not before it comes level with it
            Assert.Greater(BodyDist(new Vector3(0f, 0f, 7f), centre, Vector3.right, halfLength, halfWidth), carHalfWidth + 0.05f);
        }

        [Test]
        public void ACarQueuedInTheNarrowestOncomingLaneIsPassed()
        {
            // two-way lanes 1.9 m apart (the narrowest streets): a car standing in the oncoming lane does not block this one
            var oncoming = new Vector3(-1.9f, 0f, 10f);
            Assert.GreaterOrEqual(BodyDist(new Vector3(0f, 0f, 10f), oncoming, Vector3.back, 2.25f, 0.9f), 0.9f + 0.05f);
        }
    }
}
