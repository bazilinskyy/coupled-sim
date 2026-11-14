using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoupledSim.Vehicles
{
    /// <summary>
    /// What a wheel drives on: tyre grip relative to dry asphalt, the extra grip lost when wet, and
    /// how rough it is (vertical jolts that grow with speed). Found from the name of the collider
    /// under the wheel (the city maps name their meshes "Roads", "Roads bricks", "Roads cobblestones",
    /// "Roads gravel", "Green areas", "Pavements", ...) or of its physics material; anything unknown
    /// counts as asphalt. <c>-roadSurface name</c> on the command line uses one surface everywhere
    /// (e.g. for scenes whose roads are not named).
    /// </summary>
    public readonly struct RoadSurface
    {
        public readonly string Name;
        /// <summary>Peak tyre grip relative to dry asphalt.</summary>
        public readonly float Grip;
        /// <summary>Factor on the grip when fully wet, on top of the car's own wet grip (smooth stones get slippery).</summary>
        public readonly float WetFactor;
        /// <summary>Roughness: vertical acceleration of each wheel at 50 km/h, in m/s² (0 smooth).</summary>
        public readonly float Roughness;
        /// <summary>Main wavelength of the unevenness in metres (stone size): sets how fast the jolts come.</summary>
        public readonly float Wavelength;
        /// <summary>Tyre-road noise relative to asphalt, dB (setts about +8, brick paving +4).</summary>
        public readonly float NoiseDb;

        public RoadSurface(string name, float grip, float wetFactor, float roughness, float wavelength, float noiseDb)
        {
            Name = name; Grip = grip; WetFactor = wetFactor; Roughness = roughness; Wavelength = wavelength; NoiseDb = noiseDb;
        }

        /// <summary>Grip with the car's wet grip (0.4..1) at a wetness of 0..1.</summary>
        public float GripAt(float wetness, float carWetGrip) => Grip * Mathf.Lerp(1f, carWetGrip * WetFactor, Mathf.Clamp01(wetness));

        public override string ToString() => Name;
    }

    public static class RoadSurfaces
    {
        // grip values: typical tyre-road friction coefficients relative to dry asphalt (about 0.9-1.0):
        // concrete paving and bricks a little lower, setts lower and much worse when wet, gravel and
        // grass far lower (loose surface). Roughness tuned so cobblestones shake the car clearly at
        // 30 km/h and bricks only lightly.
        public static readonly RoadSurface Asphalt = new RoadSurface("asphalt", 1.0f, 1.0f, 0f, 1f, 0f);
        public static readonly RoadSurface Bricks = new RoadSurface("bricks", 0.95f, 0.9f, 1.2f, 0.25f, 4f);
        public static readonly RoadSurface Cobblestones = new RoadSurface("cobblestones", 0.85f, 0.75f, 4f, 0.15f, 8f);
        public static readonly RoadSurface Gravel = new RoadSurface("gravel", 0.6f, 0.95f, 2f, 0.4f, 4f);
        public static readonly RoadSurface Grass = new RoadSurface("grass", 0.45f, 0.7f, 2f, 0.8f, -4f);
        public static readonly RoadSurface Paving = new RoadSurface("paving", 0.9f, 0.9f, 0.6f, 0.3f, 2f);
        public static readonly RoadSurface Unpaved = new RoadSurface("unpaved ground", 0.75f, 0.85f, 1.5f, 0.6f, 2f);

        public static readonly RoadSurface[] All = { Asphalt, Bricks, Cobblestones, Gravel, Grass, Paving, Unpaved };

        /// <summary>The surface for a collider or material name (case does not matter).</summary>
        public static RoadSurface FromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return Asphalt;
            var n = name.ToLowerInvariant();
            if (n.Contains("cobble") || n.Contains("sett")) return Cobblestones;
            if (n.Contains("brick") || n.Contains("klinker") || n.Contains("paving_stones")) return Bricks;
            if (n.Contains("gravel") || n.Contains("dirt") || n.Contains("compacted")) return Gravel;
            if (n.Contains("grass") || n.Contains("green") || n.Contains("lawn") || n.Contains("meadow")) return Grass;
            if (n.Contains("pavement") || n.Contains("sidewalk") || n.Contains("kerb") || n.Contains("curb") || n.Contains("square")) return Paving;
            if (n.Contains("aerial photo") || n.Contains("unpaved")) return Unpaved;
            return Asphalt;
        }

        static bool _overrideRead;
        static RoadSurface? _override;

        /// <summary><c>-roadSurface name</c>: one surface everywhere (null when not given).</summary>
        public static RoadSurface? Override
        {
            get
            {
                if (_overrideRead) return _override;
                _overrideRead = true;
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-roadSurface");
                if (i >= 0 && i + 1 < args.Length) _override = FromName(args[i + 1]);
                return _override;
            }
            set { _overrideRead = true; _override = value; }
        }

        static readonly RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>The surface under a vehicle (the first collider below it that is not part of it).</summary>
        public static RoadSurface Under(Transform vehicle)
        {
            if (Override.HasValue) return Override.Value;
            int n = Physics.RaycastNonAlloc(vehicle.position + Vector3.up * 1f, Vector3.down, _hits, 3f, ~0, QueryTriggerInteraction.Ignore);
            Collider best = null; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (_hits[i].distance < bestD && !_hits[i].collider.transform.IsChildOf(vehicle)) { best = _hits[i].collider; bestD = _hits[i].distance; }
            return Of(best);
        }

        static readonly Dictionary<Collider, RoadSurface> _cache = new Dictionary<Collider, RoadSurface>();

        /// <summary>The surface of a collider: its object's name, else its physics material's.</summary>
        public static RoadSurface Of(Collider c)
        {
            if (Override.HasValue) return Override.Value;
            if (c == null) return Asphalt;
            if (_cache.TryGetValue(c, out var s)) return s;
            s = FromName(c.gameObject.name);
            if (s.Name == Asphalt.Name && c.sharedMaterial != null) s = FromName(c.sharedMaterial.name);
            if (_cache.Count > 4096) _cache.Clear();
            return _cache[c] = s;
        }
    }
}
