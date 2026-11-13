using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Xml;

namespace CoupledSim.Maps
{
    /// <summary>
    /// A SUMO road network (.net.xml, e.g. made by SUMO's netconvert from an OpenDRIVE .xodr file,
    /// #40) turned into OpenStreetMap-like data, so the city-map builder makes its roads, lane
    /// markings, pavements, cycle lanes, traffic signals and spawn points from it:
    /// - every road (a SUMO edge, joined with the edge in the opposite direction) becomes a way
    ///   from junction to junction along the middle of its lanes, with highway type, lanes, width,
    ///   one-way, speed limit, pavements (pedestrian lanes) and cycle lanes;
    /// - junctions become nodes (traffic_signals where SUMO has traffic lights).
    /// The network is centred on the middle of its area (scene origin).
    /// </summary>
    public static class SumoNetwork
    {
        sealed class Lane { public List<Vector2> Shape; public float Width, Speed; public bool Car, Pedestrian, Bicycle; }
        sealed class Edge { public string Id, From, To, Type; public List<Lane> Lanes = new List<Lane>(); public bool Used; }

        public sealed class Result
        {
            public OsmData Data;
            /// <summary>Network coordinates of the scene origin (subtract from SUMO positions).</summary>
            public double OffsetX, OffsetY;
            /// <summary>Geographic centre when the network has a projection; else NaN.</summary>
            public double Latitude = double.NaN, Longitude = double.NaN;
            public int Roads, Junctions;
        }

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>The network's geographic centre (from its location element), or false when it has none (OpenDRIVE without geo reference).</summary>
        public static bool TryGetCentre(string netXml, out double lat, out double lon)
        {
            lat = lon = double.NaN;
            var doc = new XmlDocument();
            doc.LoadXml(netXml);
            var loc = doc.DocumentElement.SelectSingleNode("location");
            if (loc == null) return false;
            var proj = loc.Attributes["projParameter"]?.Value ?? "!";
            var orig = Numbers(loc.Attributes["origBoundary"]?.Value);
            if (proj == "!" || orig.Length != 4 || Math.Abs(orig[1]) > 90 || Math.Abs(orig[3]) > 90) return false;
            lon = (orig[0] + orig[2]) / 2; lat = (orig[1] + orig[3]) / 2;
            return true;
        }

        public static Result Convert(string netXml, GeoProjection proj)
        {
            var doc = new XmlDocument();
            doc.LoadXml(netXml);
            var root = doc.DocumentElement;
            var r = new Result();
            var loc = root.SelectSingleNode("location");
            var conv = Numbers(loc?.Attributes["convBoundary"]?.Value);
            if (conv.Length == 4) { r.OffsetX = (conv[0] + conv[2]) / 2; r.OffsetY = (conv[1] + conv[3]) / 2; }
            Vector2 P(double x, double y) => new Vector2((float)(x - r.OffsetX), (float)(y - r.OffsetY));

            var junctions = new Dictionary<string, (Vector2 pos, string type)>();
            var edges = new List<Edge>();
            foreach (XmlNode n in root.ChildNodes)
            {
                if (n.NodeType != XmlNodeType.Element) continue;
                if (n.Name == "junction" && n.Attributes["type"]?.Value != "internal")
                    junctions[n.Attributes["id"].Value] = (P(D(n, "x"), D(n, "y")), n.Attributes["type"]?.Value ?? "");
                else if (n.Name == "edge" && n.Attributes["function"] == null && n.Attributes["from"] != null)
                {
                    var e = new Edge { Id = n.Attributes["id"].Value, From = n.Attributes["from"].Value, To = n.Attributes["to"].Value, Type = n.Attributes["type"]?.Value ?? "" };
                    foreach (XmlNode l in n.ChildNodes)
                    {
                        if (l.Name != "lane") continue;
                        var allow = l.Attributes["allow"]?.Value ?? "";
                        var disallow = l.Attributes["disallow"]?.Value ?? "";
                        bool Allows(string c) => allow.Length > 0 ? allow.Split(' ').Contains(c) : !disallow.Split(' ').Contains(c);
                        var shape = new List<Vector2>();
                        foreach (var xy in (l.Attributes["shape"]?.Value ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var c = xy.Split(',');   // x,y or x,y,z
                            if (c.Length >= 2) shape.Add(P(double.Parse(c[0], Ci), double.Parse(c[1], Ci)));
                        }
                        if (shape.Count < 2) continue;
                        bool car = Allows("passenger");
                        e.Lanes.Add(new Lane
                        {
                            Shape = shape,
                            Width = l.Attributes["width"] != null ? (float)D(l, "width") : 3.2f,
                            Speed = l.Attributes["speed"] != null ? (float)D(l, "speed") : 13.9f,
                            Car = car,
                            Pedestrian = !car && Allows("pedestrian") && !Allows("bicycle"),
                            Bicycle = !car && Allows("bicycle"),
                        });
                    }
                    if (e.Lanes.Any(l => l.Car)) edges.Add(e);
                }
            }

            var data = new OsmData();
            r.Data = data;
            long nextId = 1;
            var nodeOf = new Dictionary<string, long>();
            long Node(Vector2 p, Dictionary<string, string> tags = null)
            {
                proj.ToGeo(p.X, p.Y, out var lat, out var lon);
                var node = new OsmNode { Id = nextId++, Lat = lat, Lon = lon };
                if (tags != null) foreach (var kv in tags) node.Tags[kv.Key] = kv.Value;
                data.Nodes[node.Id] = node;
                return node.Id;
            }
            foreach (var kv in junctions)
            {
                var tags = kv.Value.type.Contains("traffic_light") ? new Dictionary<string, string> { ["highway"] = "traffic_signals" } : null;
                nodeOf[kv.Key] = Node(kv.Value.pos, tags);
                r.Junctions++;
            }

            // opposite directions of the same road, matched by their junctions and shape
            var byEnds = edges.GroupBy(e => (e.From, e.To)).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var a in edges)
            {
                if (a.Used) continue;
                a.Used = true;
                Edge b = null;
                if (byEnds.TryGetValue((a.To, a.From), out var opposite))
                    b = opposite.Where(o => !o.Used).OrderBy(o => Distance(Centre(a), Reverse(Centre(o)))).FirstOrDefault();
                if (b != null && Distance(Centre(a), Reverse(Centre(b))) > 15f) b = null;
                if (b != null) b.Used = true;

                var ca = Centre(a);
                var line = b == null ? ca : Average(ca, Reverse(Centre(b)), CarWidth(a), CarWidth(b));
                var tags = new Dictionary<string, string>
                {
                    ["highway"] = Highway(a),
                    ["lanes"] = (a.Lanes.Count(l => l.Car) + (b?.Lanes.Count(l => l.Car) ?? 0)).ToString(Ci),
                    ["width"] = (CarWidth(a) + (b != null ? CarWidth(b) : 0f)).ToString("F1", Ci),
                    ["maxspeed"] = Math.Round(a.Lanes.Where(l => l.Car).Max(l => l.Speed) * 3.6f).ToString(Ci),
                    ["name"] = a.Id,
                };
                if (b == null) tags["oneway"] = "yes";
                // pavements: pedestrian lanes on the right of each direction (index 0 is the rightmost lane)
                bool right = a.Lanes.Any(l => l.Pedestrian), left = b != null ? b.Lanes.Any(l => l.Pedestrian) : false;
                tags["sidewalk"] = right && left ? "both" : right ? "right" : left ? "left" : "no";
                if (a.Lanes.Any(l => l.Bicycle) || (b != null && b.Lanes.Any(l => l.Bicycle))) tags["cycleway"] = "lane";

                var way = new OsmWay { Id = nextId++ };
                foreach (var kv in tags) way.Tags[kv.Key] = kv.Value;
                if (nodeOf.TryGetValue(a.From, out var from)) way.NodeIds.Add(from);
                foreach (var p in line) way.NodeIds.Add(Node(p));
                if (nodeOf.TryGetValue(a.To, out var to)) way.NodeIds.Add(to);
                data.Ways.Add(way);
                r.Roads++;
            }

            foreach (var n in data.Nodes.Values)
            {
                data.MinLat = Math.Min(data.MinLat, n.Lat); data.MaxLat = Math.Max(data.MaxLat, n.Lat);
                data.MinLon = Math.Min(data.MinLon, n.Lon); data.MaxLon = Math.Max(data.MaxLon, n.Lon);
            }
            return r;
        }

        static string Highway(Edge e)
        {
            var t = e.Type.StartsWith("highway.") ? e.Type.Substring(8) : "";
            if (t.Length > 0 && !t.Contains('.')) return t;
            float speed = e.Lanes.Where(l => l.Car).Max(l => l.Speed);
            return speed >= 22f ? "primary" : speed >= 15f ? "secondary" : "residential";
        }

        static float CarWidth(Edge e) => e.Lanes.Where(l => l.Car).Sum(l => l.Width);

        // middle of the car lanes, resampled along the length
        static List<Vector2> Centre(Edge e)
        {
            var lanes = e.Lanes.Where(l => l.Car).ToList();
            int n = Math.Max(2, Math.Min(200, (int)Math.Ceiling(Geometry2D.Length(lanes[0].Shape) / 5f) + 1));
            n = Math.Max(n, lanes.Max(l => l.Shape.Count));
            var result = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                var sum = Vector2.Zero;
                foreach (var l in lanes) sum += At(l.Shape, i / (float)(n - 1));
                result.Add(sum / lanes.Count);
            }
            return result;
        }

        static List<Vector2> Average(List<Vector2> a, List<Vector2> b, float wa, float wb)
        {
            int n = Math.Max(a.Count, b.Count);
            var result = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                result.Add((At(a, t) * wa + At(b, t) * wb) / (wa + wb));
            }
            return result;
        }

        static List<Vector2> Reverse(List<Vector2> l) { var r = new List<Vector2>(l); r.Reverse(); return r; }

        static float Distance(List<Vector2> a, List<Vector2> b) => (Vector2.Distance(a[0], b[0]) + Vector2.Distance(At(a, 0.5f), At(b, 0.5f)) + Vector2.Distance(a[a.Count - 1], b[b.Count - 1])) / 3f;

        // the point at fraction t of a polyline's length
        static Vector2 At(List<Vector2> line, float t)
        {
            float len = Geometry2D.Length(line);
            if (len < 1e-4f) return line[0];
            return Geometry2D.PointAt(line, t * len).point;
        }

        static double D(XmlNode n, string a) => double.Parse(n.Attributes[a].Value, Ci);

        static double[] Numbers(string s) => string.IsNullOrWhiteSpace(s) ? new double[0]
            : s.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, Ci)).ToArray();
    }
}
