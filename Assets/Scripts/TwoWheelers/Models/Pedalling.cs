using System;

namespace CoupledSim.TwoWheelers
{
    /// <summary>How a rider pedals a bicycle: its gears and the cadence the rider keeps.</summary>
    [Serializable]
    public struct PedallingParameters
    {
        /// <summary>Distance (m) the bicycle goes per crank revolution in each gear, lowest first (wheel circumference
        /// times chainring over sprocket).</summary>
        public float[] Developments;
        /// <summary>Cadence (rpm) at which the rider shifts up, and down.</summary>
        public float ShiftUpCadence, ShiftDownCadence;
        /// <summary>Shortest time (s) between two shifts.</summary>
        public float ShiftInterval;
        /// <summary>Below this speed (m/s), at a hard effort, the rider stands on the pedals (setting off).</summary>
        public float StandBelowSpeed;
        /// <summary>Crank angle (deg) the rider brings the right pedal to when stopping, to push off from: forward and up.</summary>
        public float ReadyAngle;
        /// <summary>Lean (rad) beyond which the rider stops pedalling and keeps the inside pedal up, clear of the road.</summary>
        public float PedalStrikeLean;

        /// <summary>A Dutch city bike with a 7-speed hub (developments 2.6-6.2 m; about 60-85 rpm from 10 to 25 km/h), or a
        /// road bike with 2x11 gears (50/34 x 11-28: 2.5-9.6 m; about 80-100 rpm from 25 to 45 km/h).</summary>
        public static PedallingParameters Preset(TwoWheelerType type)
        {
            if (type == TwoWheelerType.RaceBike)
                return new PedallingParameters
                {
                    Developments = new[] { 2.55f, 2.95f, 3.4f, 3.9f, 4.4f, 4.9f, 5.45f, 6.0f, 6.65f, 7.4f, 8.3f, 9.6f },
                    ShiftUpCadence = 100f, ShiftDownCadence = 78f, ShiftInterval = 0.5f,
                    StandBelowSpeed = 5f, ReadyAngle = 50f, PedalStrikeLean = 0.42f,
                };
            return new PedallingParameters
            {
                Developments = new[] { 2.6f, 3.1f, 3.6f, 4.15f, 4.75f, 5.4f, 6.2f },
                ShiftUpCadence = 85f, ShiftDownCadence = 58f, ShiftInterval = 0.8f,
                StandBelowSpeed = 3.2f, ReadyAngle = 55f, PedalStrikeLean = 0.36f,
            };
        }
    }

    /// <summary>What a rider's legs do: crank angle, gear, cadence, foot down when stopped, out of the saddle.</summary>
    [Serializable]
    public struct PedallingState
    {
        /// <summary>Crank angle (deg, 0..360): 0 = right crank straight up, increasing as the crank turns forward.</summary>
        public float CrankAngle;
        public int Gear;
        /// <summary>Cadence (rpm); 0 when not pedalling (freewheeling, stopped).</summary>
        public float Cadence;
        public bool Pedalling;
        /// <summary>0 = both feet on the pedals, 1 = the left foot (right in left-hand traffic) on the ground.</summary>
        public float FootDown;
        /// <summary>0 = seated, 1 = standing on the pedals.</summary>
        public float Standing;
        /// <summary>Effort (0..1), smoothed: how hard the rider pushes.</summary>
        public float Effort;
        public float StoppedFor, SinceShift;
    }

    /// <summary>
    /// The rider's legs on a bicycle, from its speed and the rider's effort: the cranks turn with the rear wheel through
    /// the gear the rider has chosen (shifting to keep a comfortable cadence: 60-85 rpm on a city bike, 80-100 on a road
    /// bike), stop when the rider freewheels (coasting or braking) and come level, stay with the inside pedal up when
    /// leaning far into a bend, and come to the push-off position when stopping; stopped, the rider puts a foot down;
    /// setting off with a hard effort at low speed, the rider stands on the pedals.
    /// </summary>
    public static class Pedalling
    {
        public static float Cadence(in PedallingParameters p, int gear, float speed)
            => p.Developments == null || p.Developments.Length == 0 ? 0f : Math.Max(0f, speed) / p.Developments[Math.Clamp(gear, 0, p.Developments.Length - 1)] * 60f;

        /// <summary>The gear a rider would be in at this speed (for a start at speed): the one nearest the middle of the shift band.</summary>
        public static int GearFor(in PedallingParameters p, float speed)
        {
            if (p.Developments == null) return 0;
            float want = (p.ShiftUpCadence + p.ShiftDownCadence) / 2f;
            int best = 0; float bestErr = float.MaxValue;
            for (int g = 0; g < p.Developments.Length; g++)
            {
                float e = Math.Abs(Cadence(p, g, speed) - want);
                if (e < bestErr) { bestErr = e; best = g; }
            }
            return best;
        }

        static float Wrap(float a) { a %= 360f; return a < 0f ? a + 360f : a; }
        static float Delta(float from, float to) { float d = Wrap(to - from); return d > 180f ? d - 360f : d; }
        static float Toward(float v, float target, float rate) => v < target ? Math.Min(target, v + rate) : Math.Max(target, v - rate);

        /// <summary>Advances the legs by dt seconds. effort: the drive input (0..1); brake: the brake input; lean: the
        /// bicycle's lean (rad, positive right); leftHand: left-hand traffic (the right foot goes down).</summary>
        public static PedallingState Step(PedallingState s, in PedallingParameters p, float speed, float effort, float brake, float lean, float dt)
        {
            if (dt <= 0f) return s;
            speed = Math.Max(0f, speed);
            effort = Math.Clamp(effort, 0f, 1f);
            bool wants = effort > 0.04f && brake < 0.1f;
            s.Effort += (effort - s.Effort) * (1f - (float)Math.Exp(-dt / 0.3f));
            s.SinceShift += dt;

            // stopped: after a moment the foot goes down; wanting to go: it comes back up onto the pedal
            bool stopped = speed < 0.25f && !wants;
            s.StoppedFor = stopped ? s.StoppedFor + dt : 0f;
            s.FootDown = Toward(s.FootDown, s.StoppedFor > 0.25f ? 1f : 0f, dt / (s.StoppedFor > 0.25f ? 0.5f : 0.4f));

            // gears: shift to keep the cadence in the band (while pedalling, or down while slowing to a stop)
            int n = p.Developments != null ? p.Developments.Length : 0;
            if (n > 0)
            {
                s.Gear = Math.Clamp(s.Gear, 0, n - 1);
                float c = Cadence(p, s.Gear, speed);
                if (s.SinceShift >= p.ShiftInterval)
                {
                    if (c > p.ShiftUpCadence && s.Gear < n - 1 && wants) { s.Gear++; s.SinceShift = 0f; }
                    else if (c < p.ShiftDownCadence && s.Gear > 0 && (wants || speed < 3f)) { s.Gear--; s.SinceShift = 0f; }
                }
            }

            // leaning far into a bend: no pedalling, the inside pedal up (right pedal up = 0°, left up = 180°)
            bool strike = Math.Abs(lean) > p.PedalStrikeLean && speed > 1f;
            s.Pedalling = wants && !strike && s.FootDown < 0.5f && n > 0;
            if (s.Pedalling)
            {
                s.Cadence = Cadence(p, s.Gear, Math.Max(speed, 0.3f));
                s.CrankAngle = Wrap(s.CrankAngle + s.Cadence * 6f * dt);   // (rpm x 360°/60 s)
            }
            else
            {
                s.Cadence = 0f;
                float target;
                if (strike) target = lean > 0f ? 0f : 180f;
                else if (s.StoppedFor > 0f || (speed < 1.5f && !wants)) target = p.ReadyAngle;   // (stopping: the push-off position)
                else target = Math.Abs(Delta(s.CrankAngle, 90f)) < Math.Abs(Delta(s.CrankAngle, 270f)) ? 90f : 270f;   // coasting: level
                // (the freewheel lets the rider turn the cranks either way: the shorter way, at most 200°/s, easing in)
                float d = Delta(s.CrankAngle, target);
                float stepDeg = Math.Sign(d) * Math.Min(Math.Abs(d), Math.Max(Math.Abs(d) * (1f - (float)Math.Exp(-dt / 0.25f)), Math.Min(Math.Abs(d), 30f * dt)));
                stepDeg = Math.Clamp(stepDeg, -200f * dt, 200f * dt);
                s.CrankAngle = Wrap(s.CrankAngle + stepDeg);
            }

            // standing on the pedals: a hard effort at low speed (setting off), seated otherwise
            float stand = s.Pedalling ? Math.Clamp((s.Effort - 0.55f) / 0.3f, 0f, 1f) * Math.Clamp((p.StandBelowSpeed - speed) / 1.5f, 0f, 1f) : 0f;
            s.Standing = Toward(s.Standing, stand, dt / 0.35f);
            return s;
        }
    }
}
