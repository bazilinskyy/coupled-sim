using System;
using System.Numerics;

namespace CoupledSim.Drones
{
    /// <summary>
    /// Physical parameters of a quadrotor (X configuration). Rotor thrust and reaction torque grow with the square of the
    /// rotor speed (T = kF ω², Q = kM ω²), the motors follow their commands with a first-order lag, and the airframe has
    /// linear and quadratic drag: the standard multirotor model (e.g. Mahony, Kumar &amp; Corke 2012, IEEE Robotics &amp;
    /// Automation Magazine 19(3); Beard &amp; McLain, Small Unmanned Aircraft). Presets are for typical airframes, with the
    /// coefficients chosen so that hover rotor speed, thrust-to-weight ratio and top speed match published specifications
    /// of that class.
    /// </summary>
    [Serializable]
    public struct QuadrotorParameters
    {
        public string Name;
        /// <summary>Take-off mass (kg).</summary>
        public float Mass;
        /// <summary>Distance from the centre to each rotor axis (m).</summary>
        public float ArmLength;
        /// <summary>Moments of inertia about the body x (forward), y (left) and z (up) axes (kg m²).</summary>
        public float Ixx, Iyy, Izz;
        /// <summary>Thrust coefficient kF (N per (rad/s)²) and reaction torque coefficient kM (N m per (rad/s)²).</summary>
        public float ThrustCoefficient, TorqueCoefficient;
        /// <summary>Motor time constant (s).</summary>
        public float MotorTimeConstant;
        /// <summary>Rotor speed range (rad/s).</summary>
        public float MinRotorSpeed, MaxRotorSpeed;
        /// <summary>Airframe drag: linear (N per m/s) and quadratic (N per (m/s)²), and rotational damping (N m per rad/s).</summary>
        public float LinearDrag, QuadraticDrag, RotationalDrag;
        /// <summary>Rotor radius (m) and blades per rotor (for the sound).</summary>
        public float RotorRadius;
        public int Blades;
        /// <summary>Flight envelope the controller keeps to: tilt (rad), horizontal speed, climb and descent rates (m/s), yaw rate (rad/s).</summary>
        public float MaxTilt, MaxSpeed, MaxClimb, MaxDescent, MaxYawRate;

        public float HoverRotorSpeed => (float)Math.Sqrt(Mass * Quadrotor.G / (4f * ThrustCoefficient));
        public float ThrustToWeight => 4f * ThrustCoefficient * MaxRotorSpeed * MaxRotorSpeed / (Mass * Quadrotor.G);

        /// <summary>A folding consumer camera drone (about 0.9 kg, 0.38 m diagonal, 9-inch props): hovers at about
        /// 600 rad/s, thrust-to-weight about 3, up to 15 m/s (21 in sport mode), 6 m/s climb, 5 m/s descent.</summary>
        public static QuadrotorParameters Consumer => new QuadrotorParameters
        {
            Name = "Consumer", Mass = 0.9f, ArmLength = 0.19f, Ixx = 0.005f, Iyy = 0.005f, Izz = 0.009f,
            ThrustCoefficient = 6.13e-6f, TorqueCoefficient = 1.0e-7f, MotorTimeConstant = 0.03f,
            MinRotorSpeed = 150f, MaxRotorSpeed = 1100f, LinearDrag = 0.12f, QuadraticDrag = 0.014f, RotationalDrag = 0.002f,
            RotorRadius = 0.12f, Blades = 2, MaxTilt = 0.61f, MaxSpeed = 15f, MaxClimb = 6f, MaxDescent = 5f, MaxYawRate = 2.0f,
        };

        /// <summary>An industrial inspection or delivery quadrotor (about 6.3 kg, 0.9 m diagonal, 21-inch props): hovers at
        /// about 350 rad/s, thrust-to-weight about 3.4, up to 17 m/s, 6 m/s climb, 5 m/s descent.</summary>
        public static QuadrotorParameters Industrial => new QuadrotorParameters
        {
            Name = "Industrial", Mass = 6.3f, ArmLength = 0.45f, Ixx = 0.17f, Iyy = 0.17f, Izz = 0.30f,
            ThrustCoefficient = 1.26e-4f, TorqueCoefficient = 2.5e-6f, MotorTimeConstant = 0.06f,
            MinRotorSpeed = 90f, MaxRotorSpeed = 650f, LinearDrag = 0.6f, QuadraticDrag = 0.11f, RotationalDrag = 0.03f,
            RotorRadius = 0.27f, Blades = 2, MaxTilt = 0.52f, MaxSpeed = 17f, MaxClimb = 6f, MaxDescent = 5f, MaxYawRate = 1.5f,
        };

        public static QuadrotorParameters Preset(string name) =>
            string.Equals(name, "Industrial", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Delivery", StringComparison.OrdinalIgnoreCase) ? Industrial : Consumer;
    }

    /// <summary>
    /// State of a quadrotor in a right-handed world frame (x east, y north, z up) with a body frame x forward, y left,
    /// z up (the thrust axis).
    /// </summary>
    public struct QuadrotorState
    {
        public Vector3 Position, Velocity;
        /// <summary>Body to world.</summary>
        public Quaternion Attitude;
        /// <summary>Body rates (rad/s) about the body axes.</summary>
        public Vector3 Rates;
        /// <summary>Rotor speeds (rad/s): front left, rear left, rear right, front right.</summary>
        public Vector4 Rotors;
        public bool OnGround;

        public static QuadrotorState AtRest(Vector3 position, float yaw) => new QuadrotorState
        {
            Position = position, Attitude = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw), OnGround = true,
        };

        /// <summary>Heading: the angle of the body's forward axis in the horizontal plane, from east towards north (rad).</summary>
        public float Yaw
        {
            get { var f = Vector3.Transform(Vector3.UnitX, Attitude); return (float)Math.Atan2(f.Y, f.X); }
        }
        public Vector3 Up => Vector3.Transform(Vector3.UnitZ, Attitude);
        public Vector3 Forward => Vector3.Transform(Vector3.UnitX, Attitude);
    }

    public enum DroneMode
    {
        /// <summary>Fly to a position (with a velocity feed-forward); holds it when there.</summary>
        Position,
        /// <summary>Fly a world velocity (the pilot's sticks in position mode, like consumer drones' "GPS" mode).</summary>
        Velocity,
        /// <summary>Tilt and climb rate from the sticks, drifting with the wind ("ATTI" mode).</summary>
        Attitude,
        /// <summary>Rotors off.</summary>
        Off,
    }

    public struct DroneCommand
    {
        public DroneMode Mode;
        public Vector3 Position, Velocity;
        /// <summary>Heading to hold (rad), or the yaw rate (rad/s) when <see cref="UseYawRate"/>.</summary>
        public float Yaw, YawRate;
        public bool UseYawRate;
        /// <summary>Attitude mode: roll and pitch (rad; pitch positive nose down = forward), climb rate (m/s).</summary>
        public float Roll, Pitch, Climb;
    }

    /// <summary>
    /// A quadrotor's rigid-body dynamics with a cascaded flight controller of the kind used by PX4 and ArduPilot:
    /// position (P) → velocity (PI) → thrust vector and attitude (geometric mapping as in Lee, Leok &amp; McClamroch 2010,
    /// IEEE CDC) → body rates (P on the attitude error) → torques (rate P with gyroscopic compensation) → rotor speeds
    /// (the X-configuration mixer). Steps at 500 Hz inside <see cref="Step"/>.
    /// </summary>
    public sealed class Quadrotor
    {
        public const float G = 9.81f;
        const float SubStep = 0.002f;

        public QuadrotorParameters Parameters;
        public QuadrotorState State;
        public DroneCommand Command;
        /// <summary>Wind (m/s, world) including gusts.</summary>
        public Vector3 Wind;
        /// <summary>Height of the ground under the drone (world z).</summary>
        public float GroundHeight;

        // gains (per second): position and velocity loops, attitude (P to rates) and rates (to angular acceleration)
        public float PositionGainXY = 1.0f, PositionGainZ = 1.2f, VelocityGainXY = 2.2f, VelocityGainZ = 4f, VelocityIntegralXY = 1.0f, VelocityIntegralZ = 1.5f;
        public float AttitudeGain = 7f, YawGain = 3f, RateGain = 25f, YawRateGain = 10f;

        Vector3 _velIntegral, _lastVelError;
        float _yawTarget;
        readonly float[,] _mixInverse;
        static readonly float[] Angles = { (float)(Math.PI / 4), (float)(3 * Math.PI / 4), (float)(5 * Math.PI / 4), (float)(7 * Math.PI / 4) };
        static readonly float[] Spin = { 1f, -1f, 1f, -1f };   // reaction torque sign about +z per rotor

        public Quadrotor(QuadrotorParameters p, QuadrotorState s)
        {
            Parameters = p; State = s; _yawTarget = s.Yaw;
            Command = new DroneCommand { Mode = DroneMode.Off, Yaw = s.Yaw, Position = s.Position };
            _mixInverse = Invert(MixMatrix(p));
        }

        /// <summary>[T, τx, τy, τz] = M · [ω1², ω2², ω3², ω4²] for the X configuration.</summary>
        public static float[,] MixMatrix(QuadrotorParameters p)
        {
            var m = new float[4, 4];
            for (int i = 0; i < 4; i++)
            {
                m[0, i] = p.ThrustCoefficient;
                m[1, i] = p.ThrustCoefficient * p.ArmLength * (float)Math.Sin(Angles[i]);
                m[2, i] = -p.ThrustCoefficient * p.ArmLength * (float)Math.Cos(Angles[i]);
                m[3, i] = Spin[i] * p.TorqueCoefficient;
            }
            return m;
        }

        /// <summary>Advances by dt seconds (several 2 ms steps).</summary>
        public void Step(float dt)
        {
            int n = Math.Max(1, (int)Math.Ceiling(dt / SubStep));
            float h = dt / n;
            for (int i = 0; i < n; i++) StepOnce(h);
        }

        void StepOnce(float dt)
        {
            var p = Parameters;
            var cmd = Control(dt);
            // motors: first-order lag towards the commanded speeds
            float k = 1f - (float)Math.Exp(-dt / Math.Max(1e-4f, p.MotorTimeConstant));
            State.Rotors += (cmd - State.Rotors) * k;
            var w2 = State.Rotors * State.Rotors;
            float thrust = p.ThrustCoefficient * (w2.X + w2.Y + w2.Z + w2.W);
            // translation
            var air = State.Velocity - Wind;
            var drag = -p.LinearDrag * air - p.QuadraticDrag * air.Length() * air;
            var force = State.Up * thrust + new Vector3(0, 0, -p.Mass * G) + drag;
            var acc = force / p.Mass;
            // the ground: resting on it until the thrust lifts the drone
            if (State.OnGround)
            {
                if (acc.Z <= 0f) { State.Velocity = Vector3.Zero; State.Rates = Vector3.Zero; LevelOnGround(); return; }
                State.OnGround = false;
            }
            State.Velocity += acc * dt;
            State.Position += State.Velocity * dt;
            if (State.Position.Z <= GroundHeight)
            {
                State.Position.Z = GroundHeight;
                if (State.Velocity.Z < 0f) State.Velocity = Vector3.Zero;
                if (thrust < p.Mass * G) { State.OnGround = true; State.Rates = Vector3.Zero; LevelOnGround(); return; }
            }
            // rotation: Euler's equations with the rotor torques
            var torque = new Vector3(
                p.ThrustCoefficient * p.ArmLength * Dot4(w2, Sin4()),
                -p.ThrustCoefficient * p.ArmLength * Dot4(w2, Cos4()),
                p.TorqueCoefficient * (w2.X - w2.Y + w2.Z - w2.W)) - p.RotationalDrag * State.Rates;
            var I = new Vector3(p.Ixx, p.Iyy, p.Izz);
            var w = State.Rates;
            var gyro = Vector3.Cross(w, I * w);
            State.Rates += (torque - gyro) / I * dt;
            var dq = Quaternion.Multiply(State.Attitude, new Quaternion(State.Rates, 0f));
            State.Attitude = Quaternion.Normalize(new Quaternion(State.Attitude.X + 0.5f * dq.X * dt, State.Attitude.Y + 0.5f * dq.Y * dt,
                                                                 State.Attitude.Z + 0.5f * dq.Z * dt, State.Attitude.W + 0.5f * dq.W * dt));
        }

        void LevelOnGround()
        {
            State.Position.Z = Math.Max(State.Position.Z, GroundHeight);
            State.Attitude = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, State.Yaw);
            _velIntegral = Vector3.Zero;
        }

        static Vector4 Sin4() => new Vector4((float)Math.Sin(Angles[0]), (float)Math.Sin(Angles[1]), (float)Math.Sin(Angles[2]), (float)Math.Sin(Angles[3]));
        static Vector4 Cos4() => new Vector4((float)Math.Cos(Angles[0]), (float)Math.Cos(Angles[1]), (float)Math.Cos(Angles[2]), (float)Math.Cos(Angles[3]));
        static float Dot4(Vector4 a, Vector4 b) => Vector4.Dot(a, b);

        /// <summary>The flight controller: rotor speed commands for the current command and state.</summary>
        Vector4 Control(float dt)
        {
            var p = Parameters;
            var c = Command;
            if (c.Mode == DroneMode.Off) return Vector4.Zero;
            if (c.UseYawRate) _yawTarget = Wrap(_yawTarget + Clamp(c.YawRate, -p.MaxYawRate, p.MaxYawRate) * dt);
            else _yawTarget = c.Yaw;
            Vector3 accCmd;
            float maxAccXY = G * (float)Math.Tan(p.MaxTilt);
            if (c.Mode == DroneMode.Attitude)
            {
                // tilt from the sticks, climb rate held by the thrust
                float vz = Clamp(c.Climb, -p.MaxDescent, p.MaxClimb);
                float az = VelocityGainZ * (vz - State.Velocity.Z);
                float yaw = State.Yaw;
                var fwd = new Vector3((float)Math.Cos(yaw), (float)Math.Sin(yaw), 0); var left = new Vector3(-fwd.Y, fwd.X, 0);
                float pitch = Clamp(c.Pitch, -p.MaxTilt, p.MaxTilt), roll = Clamp(c.Roll, -p.MaxTilt, p.MaxTilt);
                accCmd = (fwd * (float)Math.Tan(pitch) - left * (float)Math.Tan(roll)) * (G + az) + new Vector3(0, 0, az);
            }
            else
            {
                // position → velocity (limited to the envelope) → acceleration (PI)
                Vector3 vCmd = c.Velocity;
                if (c.Mode == DroneMode.Position)
                {
                    var e = c.Position - State.Position;
                    vCmd += new Vector3(e.X * PositionGainXY, e.Y * PositionGainXY, e.Z * PositionGainZ);
                }
                var vxy = new Vector2(vCmd.X, vCmd.Y);
                if (vxy.Length() > p.MaxSpeed) vxy = vxy / vxy.Length() * p.MaxSpeed;
                vCmd = new Vector3(vxy.X, vxy.Y, Clamp(vCmd.Z, -p.MaxDescent, p.MaxClimb));
                var ev = vCmd - State.Velocity;
                _lastVelError = ev;
                if (!State.OnGround) _velIntegral += ev * dt;
                _velIntegral = new Vector3(Clamp(_velIntegral.X, -8f, 8f), Clamp(_velIntegral.Y, -8f, 8f), Clamp(_velIntegral.Z, -4f, 4f));
                accCmd = new Vector3(VelocityGainXY * ev.X + VelocityIntegralXY * _velIntegral.X,
                                     VelocityGainXY * ev.Y + VelocityIntegralXY * _velIntegral.Y,
                                     VelocityGainZ * ev.Z + VelocityIntegralZ * _velIntegral.Z);
            }
            // tilt limit: the horizontal acceleration the thrust can give (and no integrating while it limits: anti-windup)
            var axy = new Vector2(accCmd.X, accCmd.Y);
            if (axy.Length() > maxAccXY)
            {
                axy = axy / axy.Length() * maxAccXY; accCmd = new Vector3(axy.X, axy.Y, accCmd.Z);
                if (c.Mode != DroneMode.Attitude) { _velIntegral -= new Vector3(_lastVelError.X, _lastVelError.Y, 0) * dt; }
            }
            // the thrust vector: gravity compensated, the drag seen by the controller's model left to the integrator
            var f = p.Mass * (accCmd + new Vector3(0, 0, G));
            if (f.Z < 0.1f * p.Mass * G) f.Z = 0.1f * p.Mass * G;
            var b3 = Vector3.Normalize(f);
            float thrust = Vector3.Dot(f, State.Up);   // what the current attitude makes of it
            // desired attitude: thrust axis b3, heading from the yaw target
            var b1c = new Vector3((float)Math.Cos(_yawTarget), (float)Math.Sin(_yawTarget), 0);
            var b2 = Vector3.Normalize(Vector3.Cross(b3, b1c));
            var b1 = Vector3.Cross(b2, b3);
            // attitude error e_R = 1/2 vee(Rdᵀ R - Rᵀ Rd) with R = [r1 r2 r3], Rd = [b1 b2 b3]
            var r1 = Vector3.Transform(Vector3.UnitX, State.Attitude); var r2 = Vector3.Transform(Vector3.UnitY, State.Attitude); var r3 = State.Up;
            var eR = 0.5f * new Vector3(Vector3.Dot(b3, r2) - Vector3.Dot(b2, r3), Vector3.Dot(b1, r3) - Vector3.Dot(b3, r1), Vector3.Dot(b2, r1) - Vector3.Dot(b1, r2));
            var ratesCmd = new Vector3(-AttitudeGain * eR.X, -AttitudeGain * eR.Y, -YawGain * eR.Z);
            var I = new Vector3(p.Ixx, p.Iyy, p.Izz);
            var gains = new Vector3(RateGain, RateGain, YawRateGain);
            var torque = I * (gains * (ratesCmd - State.Rates)) + Vector3.Cross(State.Rates, I * State.Rates);
            // mixer: rotor speeds² from thrust and torques, within the rotors' range
            var u = new[] { Math.Max(0f, thrust), torque.X, torque.Y, torque.Z };
            float lo = p.MinRotorSpeed * p.MinRotorSpeed, hi = p.MaxRotorSpeed * p.MaxRotorSpeed;
            var w = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float s = 0f;
                for (int j = 0; j < 4; j++) s += _mixInverse[i, j] * u[j];
                w[i] = (float)Math.Sqrt(Clamp(s, lo, hi));
            }
            return new Vector4(w[0], w[1], w[2], w[3]);
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Wrap(float a) { while (a > Math.PI) a -= 2f * (float)Math.PI; while (a < -Math.PI) a += 2f * (float)Math.PI; return a; }

        static float[,] Invert(float[,] m)
        {
            int n = m.GetLength(0);
            var a = new double[n, 2 * n];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) a[i, j] = m[i, j]; a[i, n + i] = 1; }
            for (int c = 0; c < n; c++)
            {
                int piv = c;
                for (int r = c + 1; r < n; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[piv, c])) piv = r;
                for (int j = 0; j < 2 * n; j++) { var t = a[c, j]; a[c, j] = a[piv, j]; a[piv, j] = t; }
                double d = a[c, c];
                for (int j = 0; j < 2 * n; j++) a[c, j] /= d;
                for (int r = 0; r < n; r++)
                {
                    if (r == c) continue;
                    double f = a[r, c];
                    for (int j = 0; j < 2 * n; j++) a[r, j] -= f * a[c, j];
                }
            }
            var inv = new float[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = (float)a[i, n + j];
            return inv;
        }

        /// <summary>Blade-passing frequency (Hz) of the rotors at their mean speed: the fundamental of a multirotor's tonal noise.</summary>
        public float BladePassingFrequency => Parameters.Blades * (State.Rotors.X + State.Rotors.Y + State.Rotors.Z + State.Rotors.W) / 4f / (2f * (float)Math.PI);
    }

    /// <summary>
    /// Atmospheric turbulence by the Dryden model's low-altitude form (MIL-F-8785C): gust components as first-order
    /// (Gauss-Markov) processes with standard deviations σw = 0.1 W20, σu = σv = σw / (0.177 + 0.000823 h)^0.4 and length
    /// scales Lw = h, Lu = Lv = h / (0.177 + 0.000823 h)^1.2 (h in feet, here converted), driven by the airspeed. W20 is the
    /// wind speed at 20 ft (6 m).
    /// </summary>
    public sealed class DrydenTurbulence
    {
        Vector3 _gust;
        readonly Random _rng;
        public DrydenTurbulence(int seed) { _rng = new Random(seed); }

        public static void Intensities(float heightM, float w20, out float sigmaH, out float sigmaV, out float lengthH, out float lengthV)
        {
            float hft = Math.Max(10f, heightM / 0.3048f);
            float k = 0.177f + 0.000823f * hft;
            sigmaV = 0.1f * w20;
            sigmaH = sigmaV / (float)Math.Pow(k, 0.4);
            lengthV = hft * 0.3048f;
            lengthH = hft / (float)Math.Pow(k, 1.2) * 0.3048f;
        }

        /// <summary>The gust (m/s, world: x, y horizontal, z up) after dt seconds.</summary>
        public Vector3 Step(float dt, float heightM, float w20, float airspeed)
        {
            Intensities(heightM, w20, out var sh, out var sv, out var lh, out var lv);
            float v = Math.Max(1f, airspeed);
            float Ou(float x, float sigma, float L)
            {
                float a = (float)Math.Exp(-v * dt / L);
                return a * x + sigma * (float)Math.Sqrt(1 - a * a) * Normal();
            }
            _gust = new Vector3(Ou(_gust.X, sh, lh), Ou(_gust.Y, sh, lh), Ou(_gust.Z, sv, lv));
            return _gust;
        }

        float Normal()
        {
            double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }
}
