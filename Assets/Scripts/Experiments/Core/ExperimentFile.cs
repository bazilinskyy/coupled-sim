using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CoupledSim.Experiments
{
    /// <summary>
    /// An experiment as one YAML file (as in Coupled CARLA): the place, time, weather, traffic, sound and participants,
    /// and a list of trials that each override any of them. It becomes a <see cref="TrialTable"/> (one row per trial,
    /// the same columns as a CSV trial file), so counterbalancing, the session file and the trial preview work as for
    /// CSV files. Example: StreamingAssets/Trials/example.yaml.
    ///
    /// Sections and keys (all optional):
    ///   name                         the experiment's name (kept in the configuration written next to the logs)
    ///   experiment                   the registered experiment used as the world and roles (default CityMap with a
    ///                                map, else Base)
    ///   map                          a city map name (Delft), {lat, lon, radius_m} or {bbox: [south, west, north, east]}
    ///   simulation                   time, weather, fog, rain, snow, snow_cover, wind, wind_speed, wind_direction,
    ///                                time_speed, day_of_year, latitude, real_weather; duration_s
    ///   traffic                      cars_per_km (map traffic), autopilot_kmh, animals, drones, drone_swarm
    ///   sound                        vehicle_sounds, electric, ambience_db, cabin_music, cabin_music_db, auditory_ehmi, avas
    ///   parameters                   further experiment parameters as they are (your own IExperimentModifier's)
    ///   participants                 the host's participant first: role, input (keyboard, wheel, gamepad, vr, suit,
    ///                                autopilot), vehicle, autopilot (km/h) and any command-line option of that agent
    ///                                (hud: HudSpeed is -hud HudSpeed, car_spec: van is -carSpec van, a true value a bare
    ///                                flag); the other participants are the clients' roles, in the order they connect
    ///   order                        latin (default), random or none
    ///   duration_s                   seconds after which a trial ends by itself (also in simulation)
    ///   trials                       the trials in order; each may give block, notes, recording {start_s, duration_s},
    ///                                duration_s and any of the sections above, merged over the file's own (participants
    ///                                by position)
    /// </summary>
    public static class ExperimentFile
    {
        /// <summary>The column with the trial's merged configuration (YAML), written next to its logs.</summary>
        public const string ConfigColumn = "_config";
        /// <summary>The column with the host participant's command-line options for the trial.</summary>
        public const string ArgsColumn = "args";

        static readonly string[] Sections = { "simulation", "traffic", "sound", "parameters" };
        static readonly HashSet<string> TopKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "name", "experiment", "map", "simulation", "traffic", "sound", "parameters", "participants", "order", "duration_s", "trials", "description" };
        static readonly HashSet<string> TrialKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "experiment", "map", "simulation", "traffic", "sound", "parameters", "participants", "duration_s", "block", "notes", "name", "recording" };

        /// <summary>Reads an experiment file; problems that do not stop it (unknown keys) go to <paramref name="warnings"/>.</summary>
        public static TrialTable Read(string yaml, List<string> warnings = null)
        {
            warnings = warnings ?? new List<string>();
            var root = MiniYaml.Parse(yaml) as Dictionary<string, object>;
            if (root == null) throw new FormatException("an experiment file is a YAML mapping (name:, map:, participants:, trials:, ...)");
            foreach (var k in root.Keys) if (!TopKeys.Contains(k)) warnings.Add($"unknown key '{k}' (known: {string.Join(", ", TopKeys)})");

            var table = new TrialTable();
            var order = Str(root, "order");
            if (order != null)
            {
                if (TrialTable.TryParseOrder(order, out var o)) table.Order = o;
                else warnings.Add($"order '{order}' is not latin, random or none");
            }
            var defaults = new Dictionary<string, object>(root);
            defaults.Remove("trials"); defaults.Remove("order");
            var trials = root.TryGetValue("trials", out var tv) ? tv as List<object> : null;
            if (tv != null && trials == null) throw new FormatException("trials: is a list (- {duration_s: 60}, ...)");
            if (trials == null || trials.Count == 0) trials = new List<object> { new Dictionary<string, object>() };

            foreach (var c in new[] { "experiment", "role", "input", "block", "recordingStart", "recordingDuration", "notes" }) table.Columns.Add(c);
            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i] as Dictionary<string, object> ?? (trials[i] == null ? new Dictionary<string, object>() : null);
                if (t == null) throw new FormatException($"trial {i + 1} is not a mapping ({{block: A, simulation: {{weather: Fog}}}})");
                foreach (var k in t.Keys) if (!TrialKeys.Contains(k)) warnings.Add($"trial {i + 1}: unknown key '{k}'");
                var merged = Merge(defaults, t);
                table.Rows.Add(Row(merged, table, i + 1, warnings));
            }
            return table;
        }

        static Dictionary<string, string> Row(Dictionary<string, object> m, TrialTable table, int n, List<string> warnings)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Param(string name, string value)
            {
                if (value == null) return;
                if (!table.Columns.Contains(name, StringComparer.OrdinalIgnoreCase)) table.Columns.Add(name);
                row[name] = value;
            }
            var map = m.TryGetValue("map", out var mv) ? MapValue(mv, n) : null;
            row["experiment"] = Str(m, "experiment") ?? (map != null ? "CityMap" : "Base");
            Param("map", map);
            row["block"] = Str(m, "block") ?? "";
            row["notes"] = Str(m, "notes") ?? "";
            if (m.TryGetValue("recording", out var rec) && rec is Dictionary<string, object> r)
            {
                row["recordingStart"] = Str(r, "start_s") ?? Str(r, "start") ?? "";
                row["recordingDuration"] = Str(r, "duration_s") ?? Str(r, "duration") ?? "";
            }
            foreach (var s in Sections)
            {
                if (!m.TryGetValue(s, out var sv) || sv == null) continue;
                if (!(sv is Dictionary<string, object> sec)) { warnings.Add($"trial {n}: {s}: is a mapping"); continue; }
                foreach (var kv in sec)
                {
                    if (kv.Value is Dictionary<string, object> || kv.Value is List<object>) { warnings.Add($"trial {n}: {s}.{kv.Key} is not a single value"); continue; }
                    if (s == "simulation" && Key(kv.Key) == "durations") { Param("duration", Str(kv.Value)); continue; }
                    Param(s == "parameters" ? kv.Key : ParameterName(s, kv.Key), Str(kv.Value));
                }
            }
            if (Str(m, "duration_s") != null) Param("duration", Str(m, "duration_s"));

            // participants: the first is this host's (role, input, its options), the others the clients' roles
            if (m.TryGetValue("participants", out var pv) && pv != null)
            {
                var ps = pv as List<object>;
                if (ps == null) warnings.Add($"trial {n}: participants: is a list (- role: Driver)");
                else
                {
                    var host = ps.Count > 0 ? ps[0] as Dictionary<string, object> : null;
                    if (host != null)
                    {
                        row["role"] = Str(host, "role") ?? "";
                        var args = new List<string>();
                        foreach (var kv in host)
                        {
                            var k = Key(kv.Key);
                            var v = Str(kv.Value);
                            if (k == "role" || k == "name" || k == "notes") continue;
                            if (k == "input")
                            {
                                switch ((v ?? "").ToLowerInvariant())
                                {
                                    case "": case "keyboard": case "wheel": case "gamepad": case "flat": row["input"] = "Flat"; break;
                                    case "vr": case "hmd": row["input"] = "VR"; break;
                                    case "suit": case "suite": case "xsens": row["input"] = "Suite"; break;
                                    case "autopilot": row["input"] = "Flat"; if (!host.ContainsKey("autopilot")) args.Add("-autopilot"); break;
                                    default: warnings.Add($"trial {n}: input '{v}' is not keyboard, wheel, gamepad, vr, suit or autopilot"); break;
                                }
                                continue;
                            }
                            if (k == "options") { if (v != null) args.Add(v); continue; }
                            var flag = k == "vehicle" ? "-driverCar" : "-" + Camel(kv.Key);
                            if (v == null || v.Equals("false", StringComparison.OrdinalIgnoreCase) || v == "no") continue;
                            if (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "yes") args.Add(flag);
                            else args.Add(flag + " " + (v.Contains(" ") || v.Contains(";") ? "\"" + v + "\"" : v));
                        }
                        if (args.Count > 0) Param(ArgsColumn, string.Join(" ", args));
                    }
                    else if (ps.Count > 0) warnings.Add($"trial {n}: a participant is a mapping (- role: Driver)");
                    var clients = ps.Skip(1).Select(p => p is Dictionary<string, object> d ? Str(d, "role") : Str(p)).ToList();
                    if (clients.Count > 0)
                    {
                        if (clients.Any(c => string.IsNullOrEmpty(c))) warnings.Add($"trial {n}: every participant needs a role");
                        Param("clientRoles", string.Join(",", clients.Where(c => !string.IsNullOrEmpty(c))));
                    }
                }
            }
            var config = new Dictionary<string, object>(m);
            if (!config.ContainsKey("experiment")) config["experiment"] = row["experiment"];
            row[ConfigColumn] = MiniYaml.Write(config);
            if (!table.Columns.Contains(ConfigColumn)) table.Columns.Add(ConfigColumn);
            return row;
        }

        /// <summary>A section's key as the experiment parameter that components read (names compared without case).</summary>
        static string ParameterName(string section, string key)
        {
            var k = Key(key);
            if (section == "traffic")
            {
                if (k == "carsperkm" || k == "cars" || k == "maptraffic") return "mapTraffic";
                if (k == "autopilotkmh") return "autopilot";
            }
            return k;
        }

        /// <summary>lower case without underscores, dashes or spaces: wind_direction -> winddirection.</summary>
        static string Key(string key) => new string(key.Where(c => c != '_' && c != '-' && c != ' ').ToArray()).ToLowerInvariant();

        /// <summary>snake_case to camelCase: car_spec -> carSpec (keys already in camelCase stay).</summary>
        static string Camel(string key)
        {
            var parts = key.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1) return key.TrimStart('-');
            var sb = new StringBuilder(parts[0]);
            foreach (var p in parts.Skip(1)) sb.Append(char.ToUpperInvariant(p[0])).Append(p.Substring(1));
            return sb.ToString();
        }

        static string MapValue(object v, int n)
        {
            if (v == null) return null;
            if (v is string s) return s;
            if (v is List<object> l && l.Count >= 2) return string.Join(",", l.Select(Str));
            if (v is Dictionary<string, object> d)
            {
                var ci = CultureInfo.InvariantCulture;
                if (d.TryGetValue("bbox", out var bv) && bv is List<object> b && b.Count == 4)
                {
                    double south = Num(b[0]), west = Num(b[1]), north = Num(b[2]), east = Num(b[3]);
                    double lat = (south + north) / 2, lon = (west + east) / 2;
                    double half = Math.Max((north - south) / 2 * 111320.0, (east - west) / 2 * 111320.0 * Math.Cos(lat * Math.PI / 180));
                    return string.Format(ci, "{0:F6},{1:F6},{2:F0}", lat, lon, half);
                }
                if (d.ContainsKey("lat") && d.ContainsKey("lon"))
                {
                    var r = Str(d, "radius_m") ?? Str(d, "radius");
                    return string.Format(ci, "{0},{1}", Str(d, "lat"), Str(d, "lon")) + (r != null ? "," + r : "");
                }
                if (d.ContainsKey("name")) return Str(d, "name");
            }
            throw new FormatException($"trial {n}: map is a name (Delft), {{lat, lon, radius_m}} or {{bbox: [south, west, north, east]}}");
        }

        static double Num(object o) => double.Parse(Str(o), NumberStyles.Float, CultureInfo.InvariantCulture);

        static string Str(object o) => o == null ? null : o is string s ? s : o is List<object> l ? string.Join(",", l.Select(Str)) : Convert.ToString(o, CultureInfo.InvariantCulture);

        static string Str(Dictionary<string, object> d, string key)
        {
            foreach (var kv in d) if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return Str(kv.Value);
            return null;
        }

        /// <summary>The file's settings with a trial's on top: mappings merged key by key, participants by position,
        /// other values replaced.</summary>
        public static Dictionary<string, object> Merge(Dictionary<string, object> under, Dictionary<string, object> over)
        {
            var r = new Dictionary<string, object>(under);
            foreach (var kv in over)
            {
                r.TryGetValue(kv.Key, out var old);
                if (old is Dictionary<string, object> a && kv.Value is Dictionary<string, object> b) r[kv.Key] = Merge(a, b);
                else if (string.Equals(kv.Key, "participants", StringComparison.OrdinalIgnoreCase) && old is List<object> la && kv.Value is List<object> lb)
                {
                    var l = new List<object>();
                    for (int i = 0; i < Math.Max(la.Count, lb.Count); i++)
                    {
                        var x = i < la.Count ? la[i] : null; var y = i < lb.Count ? lb[i] : null;
                        l.Add(x is Dictionary<string, object> dx && y is Dictionary<string, object> dy ? Merge(dx, dy) : y ?? x);
                    }
                    r[kv.Key] = l;
                }
                else r[kv.Key] = kv.Value;
            }
            return r;
        }
    }
}
