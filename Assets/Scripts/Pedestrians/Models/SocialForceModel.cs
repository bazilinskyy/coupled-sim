using System;
using System.Collections.Generic;
using System.Numerics;

namespace CoupledSim.Pedestrians
{
    /// <summary>A walker in the social force model. Positions are on the ground plane (Unity x, z).</summary>
    public struct SocialAgent
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
    }

    /// <summary>A static obstacle line (wall, kerb, parked car side).</summary>
    public struct WallSegment
    {
        public Vector2 A, B;
        public WallSegment(Vector2 a, Vector2 b) { A = a; B = b; }

        public Vector2 ClosestPoint(Vector2 p)
        {
            var ab = B - A;
            float len2 = ab.LengthSquared();
            if (len2 < 1e-9f) return A;
            float t = Math.Clamp(Vector2.Dot(p - A, ab) / len2, 0f, 1f);
            return A + t * ab;
        }
    }

    [Serializable]
    public struct SocialForceParameters
    {
        /// <summary>Relaxation time towards the desired velocity (s).</summary>
        public float Tau;
        /// <summary>Strength (m/s²) and range (m) of the repulsion between pedestrians.</summary>
        public float AgentStrength, AgentRange;
        /// <summary>Look-ahead (s) of the velocity-dependent elliptical repulsion (Johansson et al. 2007); 0 = circular only.</summary>
        public float AnticipationTime;
        /// <summary>Anisotropy: weight of interactions behind the pedestrian (0..1).</summary>
        public float Lambda;
        /// <summary>Strength (m/s²) and range (m) of the repulsion from walls.</summary>
        public float WallStrength, WallRange;
        /// <summary>Ignore neighbours farther than this (m).</summary>
        public float NeighbourRadius;
        /// <summary>Speed cap as a multiple of the desired speed.</summary>
        public float MaxSpeedFactor;

        /// <summary>Values close to Helbing et al. (2000) and Johansson et al. (2007), mass-normalised.</summary>
        public static SocialForceParameters Default => new SocialForceParameters
        {
            Tau = 0.5f,
            AgentStrength = 2.1f,
            AgentRange = 0.3f,
            AnticipationTime = 1.0f,
            Lambda = 0.35f,
            WallStrength = 5f,
            WallRange = 0.1f,
            NeighbourRadius = 5f,
            MaxSpeedFactor = 1.3f,
        };
    }

    /// <summary>
    /// Social force model (Helbing &amp; Molnár 1995; Helbing, Farkas &amp; Vicsek 2000) for local collision
    /// avoidance between pedestrians. The route (waypoints, crossing decision) gives the desired
    /// direction; this model bends the walking velocity around other people and obstacles.
    /// </summary>
    public static class SocialForceModel
    {
        /// <summary>Acceleration of agent `self` (index in agents, -1 if not in the list).</summary>
        public static Vector2 Acceleration(
            in SocialAgent agent, int self, Vector2 desiredDirection, float desiredSpeed,
            IReadOnlyList<SocialAgent> agents, IReadOnlyList<WallSegment> walls, in SocialForceParameters p)
        {
            Vector2 e = desiredDirection.LengthSquared() > 1e-9f ? Vector2.Normalize(desiredDirection) : Vector2.Zero;
            Vector2 force = (desiredSpeed * e - agent.Velocity) / Math.Max(1e-3f, p.Tau);

            float nr2 = p.NeighbourRadius * p.NeighbourRadius;
            if (agents != null)
            {
                for (int j = 0; j < agents.Count; j++)
                {
                    if (j == self) continue;
                    var other = agents[j];
                    Vector2 diff = agent.Position - other.Position;
                    float d2 = diff.LengthSquared();
                    if (d2 > nr2 || d2 < 1e-12f) continue;
                    float d = (float)Math.Sqrt(d2);
                    Vector2 n = diff / d;
                    float rij = agent.Radius + other.Radius;
                    // anisotropy: people react more to what is in front of them
                    float cosPhi = e == Vector2.Zero ? 1f : Vector2.Dot(-n, e);
                    float w = p.Lambda + (1f - p.Lambda) * (1f + cosPhi) * 0.5f;
                    // body/contact repulsion (circular)
                    force += p.AgentStrength * (float)Math.Exp((rij - d) / p.AgentRange) * w * n;
                    // anticipatory repulsion (elliptical specification II): uses where the other
                    // will be relative to us after AnticipationTime, so people sidestep early
                    if (p.AnticipationTime > 0)
                    {
                        Vector2 y = (other.Velocity - agent.Velocity) * p.AnticipationTime;
                        Vector2 dy = diff - y;
                        float ldy = dy.Length();
                        float ly = y.Length();
                        float s = d + ldy;
                        float b = 0.5f * (float)Math.Sqrt(Math.Max(1e-6f, s * s - ly * ly));
                        if (ldy > 1e-6f && b > 1e-4f)
                        {
                            float g = p.AgentStrength * (float)Math.Exp((rij - b) / (p.AgentRange * 3f));
                            Vector2 dir = 0.5f * (n + dy / ldy);
                            force += g * (s / (2f * b)) * w * dir;
                        }
                    }
                }
            }

            if (walls != null)
            {
                for (int k = 0; k < walls.Count; k++)
                {
                    Vector2 c = walls[k].ClosestPoint(agent.Position);
                    Vector2 diff = agent.Position - c;
                    float d = diff.Length();
                    if (d < 1e-6f || d > p.NeighbourRadius) continue;
                    force += p.WallStrength * (float)Math.Exp((agent.Radius - d) / p.WallRange) * (diff / d);
                }
            }
            return force;
        }

        /// <summary>Semi-implicit Euler step with a speed cap. Returns the new velocity.</summary>
        public static Vector2 Integrate(Vector2 velocity, Vector2 acceleration, float desiredSpeed, float dt, in SocialForceParameters p)
        {
            var v = velocity + acceleration * dt;
            float max = desiredSpeed * p.MaxSpeedFactor;
            float s = v.Length();
            if (s > max && s > 1e-6f) v *= max / s;
            return v;
        }
    }
}
