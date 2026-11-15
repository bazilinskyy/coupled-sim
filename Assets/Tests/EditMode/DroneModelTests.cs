using System;
using System.Collections.Generic;
using System.Numerics;
using CoupledSim.Drones;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    /// <summary>The quadrotor dynamics and flight controller, Dryden turbulence, flocking and mission guidance.</summary>
    public class DroneModelTests
    {
        static Quadrotor Hovering(QuadrotorParameters p, Vector3 at)
        {
            var q = new Quadrotor(p, QuadrotorState.AtRest(Vector3.Zero, 0f));
            q.Command = new DroneCommand { Mode = DroneMode.Position, Position = at };
            for (int i = 0; i < 1000; i++) q.Step(0.02f);   // 20 s
            return q;
        }

        static float Tilt(QuadrotorState s) => (float)Math.Acos(Math.Min(1f, s.Up.Z));

        [Test]
        public void PresetsMatchTheirClass()
        {
            var c = QuadrotorParameters.Consumer;
            Assert.That(c.HoverRotorSpeed, Is.InRange(550f, 650f), "hover rotor speed (rad/s)");
            Assert.That(c.ThrustToWeight, Is.InRange(2.8f, 3.6f));
            var ind = QuadrotorParameters.Industrial;
            Assert.That(ind.HoverRotorSpeed, Is.InRange(320f, 380f));
            Assert.That(ind.ThrustToWeight, Is.InRange(3f, 3.8f));
        }

        [TestCase("Consumer")]
        [TestCase("Industrial")]
        public void TakesOffAndHoldsAPosition(string preset)
        {
            var q = Hovering(QuadrotorParameters.Preset(preset), new Vector3(0, 0, 10));
            Assert.Less((q.State.Position - new Vector3(0, 0, 10)).Length(), 0.3f);
            Assert.Less(q.State.Velocity.Length(), 0.2f);
            Assert.IsFalse(q.State.OnGround);
        }

        [Test]
        public void FliesToATargetWithinItsEnvelope()
        {
            var p = QuadrotorParameters.Consumer;
            var q = Hovering(p, new Vector3(0, 0, 20));
            q.Command = new DroneCommand { Mode = DroneMode.Position, Position = new Vector3(80, 0, 20) };
            float maxSpeed = 0f, maxTilt = 0f;
            for (int i = 0; i < 1250; i++)   // 25 s
            {
                q.Step(0.02f);
                maxSpeed = Math.Max(maxSpeed, new Vector2(q.State.Velocity.X, q.State.Velocity.Y).Length());
                maxTilt = Math.Max(maxTilt, Tilt(q.State));
            }
            Assert.Less((q.State.Position - new Vector3(80, 0, 20)).Length(), 0.5f, "there");
            Assert.LessOrEqual(maxSpeed, p.MaxSpeed + 0.5f, "speed limit");
            Assert.LessOrEqual(maxTilt, p.MaxTilt + 0.08f, "tilt limit");
            Assert.Greater(maxSpeed, 10f, "and not dawdling");
        }

        [Test]
        public void FollowsAVelocityAndAYawRate()
        {
            var q = Hovering(QuadrotorParameters.Consumer, new Vector3(0, 0, 15));
            q.Command = new DroneCommand { Mode = DroneMode.Velocity, Velocity = new Vector3(0, 8, 0), UseYawRate = true, YawRate = 0.5f };
            for (int i = 0; i < 300; i++) q.Step(0.02f);   // 6 s
            Assert.AreEqual(8f, q.State.Velocity.Y, 0.3f);
            Assert.AreEqual(0f, q.State.Velocity.X, 0.3f);
            // 6 s at 0.5 rad/s: 3 rad, minus the start-up
            Assert.That(Math.Abs(Quadrotor.Wrap(q.State.Yaw - 3f)), Is.LessThan(0.3f));
        }

        [Test]
        public void HoldsItsPositionInWind()
        {
            var q = Hovering(QuadrotorParameters.Consumer, new Vector3(0, 0, 30));
            q.Wind = new Vector3(8, 0, 0);
            for (int i = 0; i < 1500; i++) q.Step(0.02f);   // 30 s
            Assert.Less((q.State.Position - new Vector3(0, 0, 30)).Length(), 0.6f);
            Assert.Greater(Tilt(q.State), 0.05f, "leaning into the wind");
        }

        [Test]
        public void FallsWithTheRotorsOff()
        {
            var q = Hovering(QuadrotorParameters.Consumer, new Vector3(0, 0, 50));
            q.Command = new DroneCommand { Mode = DroneMode.Off };
            for (int i = 0; i < 50; i++) q.Step(0.02f);   // 1 s
            Assert.Less(q.State.Velocity.Z, -6f, "about g, less the drag and the spinning down");
            for (int i = 0; i < 500; i++) q.Step(0.02f);
            Assert.IsTrue(q.State.OnGround);
            Assert.AreEqual(0f, q.State.Position.Z, 1e-3f);
        }

        [Test]
        public void DrydenGustsHaveTheirStandardDeviation()
        {
            DrydenTurbulence.Intensities(50f, 10f, out var sh, out var sv, out var lh, out var lv);
            Assert.AreEqual(1f, sv, 1e-4f, "0.1 W20");
            Assert.Greater(sh, sv, "stronger horizontally near the ground");
            Assert.Greater(lh, lv);
            var t = new DrydenTurbulence(1);
            double s2 = 0; int n = 60000;
            for (int i = 0; i < n; i++) { var g = t.Step(0.05f, 50f, 10f, 10f); s2 += g.Z * g.Z; }
            Assert.AreEqual(1.0, Math.Sqrt(s2 / n), 0.1);
        }

        [Test]
        public void BrakingCurveIsContinuous()
        {
            float a = 4f, p = 3f;
            float r0 = a / (p * p);   // where the linear part ends
            Assert.AreEqual(Flocking.Braking(r0 - 1e-4f, a, p), Flocking.Braking(r0 + 1e-4f, a, p), 1e-2f);
            Assert.AreEqual(0f, Flocking.Braking(-1f, a, p));
            Assert.AreEqual((float)Math.Sqrt(2 * a * 20 - a * a / (p * p)), Flocking.Braking(20f, a, p), 1e-4f);
        }

        [Test]
        public void FlockMembersRepelAndAlign()
        {
            var k = FlockingParameters.Default;
            var pos = new List<Vector3> { Vector3.Zero, new Vector3(5, 0, 0) };
            var vel = new List<Vector3> { Vector3.Zero, Vector3.Zero };
            var v0 = Flocking.Velocity(0, pos, vel, null, k);
            Assert.Less(v0.X, 0f, "pushed away from a drone 5 m off");
            // far apart, one fast: the other is pulled towards its velocity (friction above the braking curve)
            pos = new List<Vector3> { Vector3.Zero, new Vector3(0, 20, 0) };
            vel = new List<Vector3> { Vector3.Zero, new Vector3(9, 0, 0) };
            Assert.Greater(Flocking.Velocity(0, pos, vel, null, k).X, 0f);
            // towards the migration point at the flock speed
            pos = new List<Vector3> { Vector3.Zero }; vel = new List<Vector3> { Vector3.Zero };
            Assert.AreEqual(k.FlockSpeed, Flocking.Velocity(0, pos, vel, new Vector3(0, 100, 0), k).Y, 1e-3f);
        }

        [Test]
        public void FliesAMissionAndStopsAtItsEnd()
        {
            var q = new Quadrotor(QuadrotorParameters.Industrial, QuadrotorState.AtRest(Vector3.Zero, 0f));
            var g = new WaypointGuidance(new[] { new Vector3(0, 0, 30), new Vector3(150, 0, 30), new Vector3(150, 100, 30), new Vector3(150, 100, 0.5f) }) { CruiseSpeed = 12f };
            for (int i = 0; i < 4500 && !g.Done; i++)   // up to 90 s
            {
                g.Step(q.State.Position, 0.02f, out var target, out var vel);
                q.Command = new DroneCommand { Mode = DroneMode.Position, Position = target, Velocity = vel, Yaw = 0f };
                q.Step(0.02f);
            }
            Assert.IsTrue(g.Done, $"mission done (at {q.State.Position})");
            Assert.Less((q.State.Position - new Vector3(150, 100, 0.5f)).Length(), 2f);
        }
    }
}
