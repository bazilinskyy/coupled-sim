using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CoupledSim.Maps;
using UnityEngine;

/// <summary>
/// Real 3D buildings of a map, in StreamingAssets/Maps/{map}.buildings.bin.gz:
/// - Dutch maps from 3D BAG (LoD 2.2: walls and roof shapes from laser scans; © 3D BAG by tudelft3d
///   and 3DGI, CC BY 4.0), made by Tools/bag3d_buildings.py ("B3D1", vertices in the Dutch grid);
/// - maps with an open CityGML city model (Tokyo: Project PLATEAU, MLIT; New York: NYC 3-D Building
///   Model; Montreal: Ville de Montreal; LoD2 or LoD1), made by Tools/citygml_buildings.py ("B3DL",
///   vertices in local metres around the map centre).
/// Vertices are converted to the scene (the same projection as the OpenStreetMap data, so they
/// line up with the roads).
/// </summary>
public static class Bag3dBuildings
{
    public sealed class Building
    {
        public byte RoofKind;     // 0 flat, 1 slanted, 2 several flat levels
        public short Year;        // construction year (0 = unknown)
        public Vector3[] V;       // scene coordinates; y = height above the ground
        public int[] Walls, Roofs;
    }

    public static string PathFor(string map) => Path.Combine(Application.streamingAssetsPath, "Maps", map + ".buildings.bin.gz");

    public static List<Building> Load(string map, GeoProjection proj)
    {
        var path = PathFor(map);
        if (!File.Exists(path)) return null;
        var list = new List<Building>();
        using (var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
        using (var r = new BinaryReader(new BufferedStream(gz)))
        {
            var magic = new string(r.ReadChars(4));
            if (magic != "B3D1" && magic != "B3DL") { Debug.LogWarning("Bag3dBuildings: unknown file " + path); return null; }
            bool rd = magic == "B3D1";
            double ox = r.ReadDouble(), oy = r.ReadDouble();   // RD origin, or the local projection's centre (lat, lon)
            var local = new GeoProjection(ox, oy);
            bool same = !rd && System.Math.Abs(ox - proj.Lat0) < 1e-9 && System.Math.Abs(oy - proj.Lon0) < 1e-9;
            int n = r.ReadInt32();
            for (int b = 0; b < n; b++)
            {
                var bld = new Building { RoofKind = r.ReadByte(), Year = r.ReadInt16() };
                int nv = r.ReadInt32();
                bld.V = new Vector3[nv];
                for (int i = 0; i < nv; i++)
                {
                    double dx = r.ReadSingle(), dy = r.ReadSingle(); float h = r.ReadSingle();
                    double x = dx, z = dy, lat, lon;
                    if (!same)
                    {
                        if (rd) RdProjection.ToWgs(ox + dx, oy + dy, out lat, out lon);
                        else local.ToGeo(dx, dy, out lat, out lon);
                        proj.ToLocal(lat, lon, out x, out z);
                    }
                    bld.V[i] = new Vector3((float)x, h, (float)z);
                }
                bld.Walls = ReadInts(r);
                bld.Roofs = ReadInts(r);
                list.Add(bld);
            }
        }
        return list;
    }

    static int[] ReadInts(BinaryReader r)
    {
        int n = r.ReadInt32() * 3;
        var a = new int[n];
        for (int i = 0; i < n; i++) a[i] = r.ReadInt32();
        return a;
    }
}
