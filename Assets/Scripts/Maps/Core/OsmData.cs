using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace CoupledSim.Maps
{
    /// <summary>An OpenStreetMap node.</summary>
    public sealed class OsmNode
    {
        public long Id;
        public double Lat, Lon;
        public Dictionary<string, string> Tags = new Dictionary<string, string>();
        public string Tag(string k) => Tags.TryGetValue(k, out var v) ? v : null;
    }

    /// <summary>An OpenStreetMap way (road, building outline, area).</summary>
    public sealed class OsmWay
    {
        public long Id;
        public List<long> NodeIds = new List<long>();
        public Dictionary<string, string> Tags = new Dictionary<string, string>();
        public string Tag(string k) => Tags.TryGetValue(k, out var v) ? v : null;
        public bool IsClosed => NodeIds.Count > 3 && NodeIds[0] == NodeIds[NodeIds.Count - 1];
    }

    /// <summary>OSM XML (as returned by the Overpass API or exported from openstreetmap.org).</summary>
    public sealed class OsmData
    {
        public readonly Dictionary<long, OsmNode> Nodes = new Dictionary<long, OsmNode>();
        public readonly List<OsmWay> Ways = new List<OsmWay>();
        public double MinLat = double.MaxValue, MinLon = double.MaxValue, MaxLat = double.MinValue, MaxLon = double.MinValue;

        public static OsmData Parse(string xml)
        {
            var data = new OsmData();
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var root = doc.DocumentElement;
            foreach (XmlNode n in root.ChildNodes)
            {
                if (n.NodeType != XmlNodeType.Element) continue;
                switch (n.Name)
                {
                    case "bounds":
                        data.MinLat = D(n, "minlat"); data.MinLon = D(n, "minlon");
                        data.MaxLat = D(n, "maxlat"); data.MaxLon = D(n, "maxlon");
                        break;
                    case "node":
                    {
                        var node = new OsmNode { Id = L(n, "id"), Lat = D(n, "lat"), Lon = D(n, "lon") };
                        ReadTags(n, node.Tags);
                        data.Nodes[node.Id] = node;
                        break;
                    }
                    case "way":
                    {
                        var way = new OsmWay { Id = L(n, "id") };
                        foreach (XmlNode c in n.ChildNodes)
                            if (c.Name == "nd") way.NodeIds.Add(L(c, "ref"));
                        ReadTags(n, way.Tags);
                        data.Ways.Add(way);
                        break;
                    }
                }
            }
            if (data.MinLat == double.MaxValue)
            {
                foreach (var node in data.Nodes.Values)
                {
                    data.MinLat = Math.Min(data.MinLat, node.Lat); data.MaxLat = Math.Max(data.MaxLat, node.Lat);
                    data.MinLon = Math.Min(data.MinLon, node.Lon); data.MaxLon = Math.Max(data.MaxLon, node.Lon);
                }
            }
            return data;
        }

        static void ReadTags(XmlNode n, Dictionary<string, string> tags)
        {
            foreach (XmlNode c in n.ChildNodes)
                if (c.Name == "tag") tags[c.Attributes["k"].Value] = c.Attributes["v"].Value;
        }

        static double D(XmlNode n, string a) => double.Parse(n.Attributes[a].Value, CultureInfo.InvariantCulture);
        static long L(XmlNode n, string a) => long.Parse(n.Attributes[a].Value, CultureInfo.InvariantCulture);

        /// <summary>Overpass API query for everything the map builder uses inside a bounding box.</summary>
        public static string OverpassQuery(double south, double west, double north, double east)
        {
            string bbox = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", south, west, north, east);
            return "[out:xml][timeout:90];(" +
                   $"way[\"highway\"]({bbox});way[\"building\"]({bbox});way[\"landuse\"]({bbox});way[\"leisure\"]({bbox});" +
                   $"way[\"natural\"]({bbox});way[\"amenity\"=\"parking\"]({bbox});way[\"waterway\"]({bbox});way[\"railway\"]({bbox});" +
                   $"node[\"highway\"]({bbox});node[\"natural\"=\"tree\"]({bbox});" +
                   ");(._;>;);out body;";
        }
    }

    /// <summary>
    /// Local metric coordinates around an origin: transverse Mercator (WGS84) centred on the origin,
    /// x east, z north (Unity), in metres. The same projection string is given to SUMO's netconvert,
    /// so SUMO networks built from the same OSM data line up with the scene.
    /// </summary>
    public readonly struct GeoProjection
    {
        public readonly double Lat0, Lon0;
        const double A = 6378137.0, F = 1 / 298.257223563;

        public GeoProjection(double lat0, double lon0) { Lat0 = lat0; Lon0 = lon0; }

        public string Proj4 => string.Format(CultureInfo.InvariantCulture,
            "+proj=tmerc +lat_0={0} +lon_0={1} +k=1 +x_0=0 +y_0=0 +ellps=WGS84 +units=m +no_defs", Lat0, Lon0);

        /// <summary>Latitude/longitude (degrees) to x (east), z (north) in metres.</summary>
        public void ToLocal(double lat, double lon, out double x, out double z)
        {
            // Transverse Mercator (Snyder 1987, series to 4th order), accurate to mm over a few km
            double e2 = F * (2 - F), ep2 = e2 / (1 - e2);
            double phi = lat * Math.PI / 180, lam = (lon - Lon0) * Math.PI / 180;
            double N = A / Math.Sqrt(1 - e2 * Math.Sin(phi) * Math.Sin(phi));
            double T = Math.Tan(phi) * Math.Tan(phi), C = ep2 * Math.Cos(phi) * Math.Cos(phi);
            double Ac = lam * Math.Cos(phi);
            double M = Meridian(phi, e2), M0 = Meridian(Lat0 * Math.PI / 180, e2);
            x = N * (Ac + (1 - T + C) * Math.Pow(Ac, 3) / 6 + (5 - 18 * T + T * T + 72 * C - 58 * ep2) * Math.Pow(Ac, 5) / 120);
            z = M - M0 + N * Math.Tan(phi) * (Ac * Ac / 2 + (5 - T + 9 * C + 4 * C * C) * Math.Pow(Ac, 4) / 24
                                               + (61 - 58 * T + T * T + 600 * C - 330 * ep2) * Math.Pow(Ac, 6) / 720);
        }

        /// <summary>x (east), z (north) in metres back to latitude/longitude (degrees), by Newton iteration on <see cref="ToLocal"/>.</summary>
        public void ToGeo(double x, double z, out double lat, out double lon)
        {
            lat = Lat0 + z / 111320.0;
            lon = Lon0 + x / (111320.0 * Math.Cos(Lat0 * Math.PI / 180));
            for (int i = 0; i < 8; i++)
            {
                ToLocal(lat, lon, out var x0, out var z0);
                double ex = x - x0, ez = z - z0;
                if (Math.Abs(ex) < 1e-5 && Math.Abs(ez) < 1e-5) break;
                const double h = 1e-6;
                ToLocal(lat + h, lon, out var xa, out var za);
                ToLocal(lat, lon + h, out var xb, out var zb);
                double a = (xa - x0) / h, b = (xb - x0) / h, c = (za - z0) / h, d = (zb - z0) / h;
                double det = a * d - b * c;
                if (Math.Abs(det) < 1e-12) break;
                lat += (d * ex - b * ez) / det;
                lon += (-c * ex + a * ez) / det;
            }
        }

        static double Meridian(double phi, double e2)
        {
            double e4 = e2 * e2, e6 = e4 * e2;
            return A * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * phi
                        - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * phi)
                        + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * phi)
                        - (35 * e6 / 3072) * Math.Sin(6 * phi));
        }
    }
}
