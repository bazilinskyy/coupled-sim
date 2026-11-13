using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CoupledSim.Maps;
using UnityEngine;

/// <summary>
/// Real 3D buildings of Dutch maps from 3D BAG (LoD 2.2: walls and roof shapes from laser scans;
/// © 3D BAG by tudelft3d and 3DGI, CC BY 4.0), prepared by Tools/bag3d_buildings.py into
/// StreamingAssets/Maps/{map}.buildings.bin.gz. Vertices are converted from the Dutch grid to the
/// scene (the same projection as the OpenStreetMap data, so they line up with the roads).
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
            if (new string(r.ReadChars(4)) != "B3D1") { Debug.LogWarning("Bag3dBuildings: unknown file " + path); return null; }
            double ox = r.ReadDouble(), oy = r.ReadDouble();
            int n = r.ReadInt32();
            for (int b = 0; b < n; b++)
            {
                var bld = new Building { RoofKind = r.ReadByte(), Year = r.ReadInt16() };
                int nv = r.ReadInt32();
                bld.V = new Vector3[nv];
                for (int i = 0; i < nv; i++)
                {
                    double dx = r.ReadSingle(), dy = r.ReadSingle(); float h = r.ReadSingle();
                    RdProjection.ToWgs(ox + dx, oy + dy, out var lat, out var lon);
                    proj.ToLocal(lat, lon, out var x, out var z);
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
