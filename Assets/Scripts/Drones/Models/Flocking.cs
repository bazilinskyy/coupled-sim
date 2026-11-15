using System;
using System.Collections.Generic;
using System.Numerics;

namespace CoupledSim.Drones
{
    /// <summary>
    /// Desired velocities of drones flying as a flock, by the model of Vásárhelyi et al. (2018), "Optimized flocking of
    /// autonomous drones in confined environments", Science Robotics 3(20): self-propulsion at a preferred speed
    /// (here towards a migration point), short-range repulsion that grows linearly as drones come closer than a set
    /// distance, and velocity alignment as "viscous friction" that only acts above a speed difference allowed by a
    /// braking curve D(r) (a constant deceleration with a linear final part), so that drones can brake in time;
    /// obstacles act through "shill" agents that the caller places at their surfaces. Parameters below are of the
    /// paper's form, set for a 6 m/s flock with drones about 15 m apart; tune them for other speeds and spacings.
    /// </summary>
    [Serializable]
    public struct FlockingParameters
    {
        public float FlockSpeed;
        public float RepulsionRadius, RepulsionGain;
        public float FrictionRadius, FrictionGain, FrictionMinSpeed, FrictionAcceleration, FrictionSlope;
        public float ShillSpeed, ShillRadius, ShillAcceleration, ShillSlope;
        public float MaxSpeed;

        public static FlockingParameters Default => new FlockingParameters
        {
            FlockSpeed = 6f, RepulsionRadius = 15f, RepulsionGain = 0.3f,
            FrictionRadius = 30f, FrictionGain = 0.08f, FrictionMinSpeed = 0.6f, FrictionAcceleration = 4f, FrictionSlope = 3f,
            ShillSpeed = 8f, ShillRadius = 3f, ShillAcceleration = 3f, ShillSlope = 3.5f, MaxSpeed = 10f,
        };
    }

    public static class Flocking
    {
        /// <summary>The braking curve D(r, a, p): the highest speed from which one can stop within r with deceleration a,
        /// with a linear part (slope p) near zero.</summary>
        public static float Braking(float r, float a, float p)
        {
            if (r <= 0f) return 0f;
            if (r * p > 0f && r * p < a / p) return r * p;
            return (float)Math.Sqrt(Math.Max(0f, 2f * a * r - a * a / (p * p)));
        }

        /// <summary>
        /// The desired velocity of drone <paramref name="i"/> given all drones' positions and velocities, its migration
        /// point (null: keep its heading) and shill agents (positions on obstacles with a velocity pointing out of them).
        /// </summary>
        public static Vector3 Velocity(int i, IList<Vector3> pos, IList<Vector3> vel, Vector3? goal, FlockingParameters k,
                                       IList<(Vector3 pos, Vector3 vel)> shills = null)
        {
            var pi = pos[i]; var vi = vel[i];
            // self-propulsion
            Vector3 dir = goal.HasValue ? goal.Value - pi : vi;
            if (goal.HasValue && dir.Length() < 0.5f) dir = Vector3.Zero;
            var v = dir.Length() > 1e-3f ? Vector3.Normalize(dir) * Math.Min(k.FlockSpeed, goal.HasValue ? dir.Length() * 0.5f : k.FlockSpeed) : Vector3.Zero;
            for (int j = 0; j < pos.Count; j++)
            {
                if (j == i) continue;
                var d = pi - pos[j]; float r = d.Length();
                if (r < 1e-3f) continue;
                // repulsion: linear below the repulsion radius
                if (r < k.RepulsionRadius) v += k.RepulsionGain * (k.RepulsionRadius - r) * d / r;
                // alignment: friction above the speed difference the braking curve allows at this distance
                var dv = vel[j] - vi; float vij = dv.Length();
                float allowed = Math.Max(k.FrictionMinSpeed, Braking(r - k.FrictionRadius, k.FrictionAcceleration, k.FrictionSlope));
                if (vij > allowed) v += k.FrictionGain * (vij - allowed) * dv / vij;
            }
            if (shills != null)
                foreach (var (sp, sv) in shills)
                {
                    var d = pi - sp; float r = d.Length();
                    var dv = sv * k.ShillSpeed / Math.Max(1e-3f, sv.Length()) - vi; float vis = dv.Length();
                    float allowed = Braking(r - k.ShillRadius, k.ShillAcceleration, k.ShillSlope);
                    if (vis > allowed && vis > 1e-3f) v += (vis - allowed) * dv / vis;
                }
            if (v.Length() > k.MaxSpeed) v = Vector3.Normalize(v) * k.MaxSpeed;
            return v;
        }
    }

    /// <summary>
    /// A drone's mission as a path of waypoints flown with a smooth speed profile: constant acceleration up to the cruise
    /// speed and down to a stop at the last waypoint (and at sharp corners), as multirotor autopilots do in mission mode.
    /// Gives the controller a position target and a velocity feed-forward.
    /// </summary>
    public sealed class WaypointGuidance
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        public float CruiseSpeed = 10f, ClimbSpeed = 4f, Acceleration = 2.5f, AcceptRadius = 1.5f;
        public int Index;
        public bool Done => Index >= Points.Count;
        float _speed;

        public WaypointGuidance(IEnumerable<Vector3> points) { Points.AddRange(points); }

        /// <summary>Position target and velocity feed-forward for a drone at <paramref name="p"/> after dt.</summary>
        public void Step(Vector3 p, float dt, out Vector3 target, out Vector3 velocity)
        {
            while (Index < Points.Count && (Points[Index] - p).Length() < AcceptRadius) Index++;
            if (Done) { target = Points.Count > 0 ? Points[Points.Count - 1] : p; velocity = Vector3.Zero; _speed = 0f; return; }
            var to = Points[Index] - p; float dist = to.Length();
            // remaining path length to the next stop (the end, or a corner sharper than 60°)
            float remaining = dist;
            for (int k = Index; k + 1 < Points.Count; k++)
            {
                var a = Points[k] - (k == Index ? p : Points[k - 1]); var b = Points[k + 1] - Points[k];
                if (a.Length() > 0.1f && b.Length() > 0.1f && Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)) < 0.5f) break;
                remaining += b.Length();
            }
            var dir = Vector3.Normalize(to);
            // vertical legs fly at the climb speed
            float cruise = Math.Abs(dir.Z) > 0.8f ? ClimbSpeed : CruiseSpeed;
            float vStop = (float)Math.Sqrt(2f * Acceleration * Math.Max(0f, remaining - AcceptRadius * 0.5f));
            float want = Math.Min(cruise, vStop);
            _speed = want > _speed ? Math.Min(want, _speed + Acceleration * dt) : want;
            velocity = dir * _speed;
            // a carrot a little ahead along the leg keeps the position loop from fighting the feed-forward
            target = p + dir * Math.Min(dist, Math.Max(1f, _speed * 0.5f));
        }
    }
}
