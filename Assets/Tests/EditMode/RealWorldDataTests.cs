using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CoupledSim.Tests
{
    /// <summary>
    /// Real-world data on the city maps: Dutch sign codes (NDW) to drawn signs, historical weather (Open-Meteo) to the
    /// simulator's weather, and the terrain's sampling and bridges.
    /// </summary>
    public class RealWorldDataTests
    {
        static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        [TestCase("A1", "30", "SpeedLimit", 30)]
        [TestCase("A1", "", "SpeedLimit", 50)]
        [TestCase("A2", "30", "EndSpeedLimit", 30)]
        [TestCase("B6", "", "GiveWay", 0)]
        [TestCase("C2", "", "NoEntry", 0)]
        [TestCase("G11", "", "CyclePath", 0)]
        [TestCase("G12a", "", "MopedCyclePath", 0)]
        [TestCase("E4", "", "Parking", 0)]
        [TestCase("D2", "LO", "KeepLeftDown", 0)]
        [TestCase("D2", "RO", "KeepRightDown", 0)]
        [TestCase("L8", "", "DeadEnd", 0)]
        public void DutchSignCodes(string code, string text, string kind, int limit)
        {
            var gen = TypeNamed("OsmMapGenerator");
            var args = new object[] { code, text, null, null };
            Assert.IsTrue((bool)gen.GetMethod("RvvKind").Invoke(null, args), code);
            Assert.AreEqual(kind, args[2].ToString());
            Assert.AreEqual(limit, (int)args[3]);
        }

        [Test]
        public void UnknownSignCodesAreLeftOut()
        {
            var args = new object[] { "Z99", "", null, null };
            Assert.IsFalse((bool)TypeNamed("OsmMapGenerator").GetMethod("RvvKind").Invoke(null, args));
        }

        static string WeatherOf(int code, float precipitation, float cloud, float visibility, out float rain, out float fog)
        {
            var rw = TypeNamed("RealWeather");
            var hourType = rw.GetNestedType("Hour");
            var h = Activator.CreateInstance(hourType);
            hourType.GetField("Code").SetValue(h, code);
            hourType.GetField("Precipitation").SetValue(h, precipitation);
            hourType.GetField("Cloud").SetValue(h, cloud);
            hourType.GetField("Visibility").SetValue(h, visibility);
            var args = new object[] { h, 0f, 0f };
            var w = rw.GetMethod("WeatherOf").Invoke(null, args).ToString();
            rain = (float)args[1]; fog = (float)args[2];
            return w;
        }

        [Test]
        public void HistoricalWeatherBecomesTheSimulatorsWeather()
        {
            Assert.AreEqual("Clear", WeatherOf(0, 0f, 5f, 30000f, out _, out _));
            Assert.AreEqual("PartlyCloudy", WeatherOf(2, 0f, 45f, 30000f, out _, out _));
            Assert.AreEqual("Overcast", WeatherOf(3, 0f, 100f, 30000f, out _, out _));
            Assert.AreEqual("LightRain", WeatherOf(61, 0.6f, 100f, 20000f, out var rain, out _));
            Assert.That(rain, Is.InRange(0.15f, 0.5f));
            // a heavy shower by the amount, whatever the code said
            Assert.AreEqual("HeavyRain", WeatherOf(61, 4f, 100f, 8000f, out rain, out _));
            Assert.AreEqual(1f, rain, 0.01f);
            // fog from the visibility: 5% contrast left at that distance
            Assert.AreEqual("Fog", WeatherOf(3, 0f, 100f, 400f, out _, out var fog));
            Assert.AreEqual(3f / 400f, fog, 1e-5f);
            Assert.AreEqual("Fog", WeatherOf(45, 0f, 100f, -1f, out _, out _));
        }

        static object Hour(int code, float snowfall, float depth)
        {
            var hourType = TypeNamed("RealWeather").GetNestedType("Hour");
            var h = Activator.CreateInstance(hourType);
            hourType.GetField("Code").SetValue(h, code);
            hourType.GetField("Cloud").SetValue(h, 100f);
            hourType.GetField("Snowfall").SetValue(h, snowfall);
            hourType.GetField("SnowDepth").SetValue(h, depth);
            return h;
        }

        [Test]
        public void SnowFallsAndCoversTheGround()
        {
            var rw = TypeNamed("RealWeather");
            string W(object h) { var a = new object[] { h, 0f, 0f }; return rw.GetMethod("WeatherOf").Invoke(null, a).ToString(); }
            float Cover(object h) => (float)rw.GetMethod("SnowCoverOf").Invoke(null, new[] { h });
            Assert.AreEqual("LightSnow", W(Hour(71, 0.3f, 0.02f)));
            Assert.AreEqual("HeavySnow", W(Hour(73, 1.5f, 0.1f)), "by the amount");
            Assert.AreEqual("HeavySnow", W(Hour(75, 0.2f, -1f)), "by the code");
            Assert.AreEqual(0.4f, Cover(Hour(71, 0.3f, 0.02f)), 1e-4f, "2 cm of 5");
            Assert.AreEqual(1f, Cover(Hour(3, 0f, 0.2f)), 1e-4f, "old snow, no snowfall");
            Assert.AreEqual(0.5f, Cover(Hour(71, 0.3f, -1f)), 1e-4f, "snowing, depth unknown");
            Assert.AreEqual(0f, Cover(Hour(61, 0f, -1f)));
        }

        [Test]
        public void TreesLeanMoreInStrongerWind()
        {
            var lean = TypeNamed("TreeSway").GetMethod("Lean");
            float L(float v) => (float)lean.Invoke(null, new object[] { v });
            Assert.Less(L(3f), 0.2f);
            Assert.AreEqual(1.2f, L(10f), 0.01f);
            Assert.AreEqual(6f, L(40f), 0.01f, "at most 6°");
        }

        [Test]
        public void OpenMeteoAnswersAreRead()
        {
            const string json = "{\"latitude\":52.0,\"hourly\":{\"time\":[\"2025-06-01T00:00\",\"2025-06-01T01:00\"]," +
                                "\"temperature_2m\":[11.5,12.0],\"precipitation\":[0.0,1.2],\"cloud_cover\":[20,95]," +
                                "\"weather_code\":[1,63],\"wind_speed_10m\":[10.0,14.5],\"visibility\":[null,6000.0]," +
                                "\"snowfall\":[0.0,0.7],\"snow_depth\":[0.03,null],\"wind_direction_10m\":[200,315]}}";
            var rw = TypeNamed("RealWeather");
            var h = rw.GetMethod("Parse").Invoke(null, new object[] { json, 1 });
            var t = h.GetType();
            Assert.AreEqual(63, t.GetField("Code").GetValue(h));
            Assert.AreEqual(1.2f, (float)t.GetField("Precipitation").GetValue(h), 1e-4f);
            Assert.AreEqual(95f, (float)t.GetField("Cloud").GetValue(h), 1e-4f);
            Assert.AreEqual(6000f, (float)t.GetField("Visibility").GetValue(h), 1e-4f);
            var h0 = rw.GetMethod("Parse").Invoke(null, new object[] { json, 0 });
            Assert.AreEqual(-1f, (float)t.GetField("Visibility").GetValue(h0), 1e-4f, "missing visibility");
            Assert.AreEqual(0.7f, (float)t.GetField("Snowfall").GetValue(h), 1e-4f);
            Assert.AreEqual(-1f, (float)t.GetField("SnowDepth").GetValue(h), 1e-4f, "missing snow depth");
            Assert.AreEqual(0.03f, (float)t.GetField("SnowDepth").GetValue(h0), 1e-4f);
            Assert.AreEqual(315f, (float)t.GetField("WindDirection").GetValue(h), 1e-4f);
        }

        static object Terrain(float[] h, int n, float step)
        {
            var type = TypeNamed("TerrainHeight");
            var t = Activator.CreateInstance(type, true);
            void Set(string f, object v) => type.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(t, v);
            Set("_x0", 0f); Set("_z0", 0f); Set("_step", step); Set("_nx", n); Set("_nz", n); Set("_h", h);
            return t;
        }

        [Test]
        public void TerrainIsSampledBetweenItsPoints()
        {
            // a plane rising 1 m per metre to the east: height = x
            int n = 5; float step = 10f;
            var h = new float[n * n];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) h[j * n + i] = i * step;
            var t = Terrain(h, n, step);
            var at = t.GetType().GetMethod("At");
            Assert.AreEqual(15f, (float)at.Invoke(t, new object[] { 15f, 22f }), 1e-3f);
            Assert.AreEqual(0f, (float)at.Invoke(t, new object[] { -50f, 0f }), 1e-3f, "beyond the grid: its edge");
        }

        [Test]
        public void BridgesRunStraightOverValleys()
        {
            // a valley 20 m deep in the middle of the grid; a bridge from x=0 to x=40 at z=20 stays at the ends' height
            int n = 5; float step = 10f;
            var h = new float[n * n];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) h[j * n + i] = i == 2 ? -20f : 0f;
            var t = Terrain(h, n, step);
            var type = t.GetType();
            var lift = type.GetMethod("Lift", new[] { typeof(float), typeof(float), typeof(int) });
            float Lift(float x, float z, int layer) => (float)lift.Invoke(t, new object[] { x, z, layer });
            Assert.AreEqual(-20f, Lift(20f, 20f, 0), 1e-3f, "no bridge: the valley");
            type.GetMethod("AddBridge").Invoke(t, new object[] { new System.Collections.Generic.List<Vector2> { new Vector2(0f, 20f), new Vector2(40f, 20f) }, 8f, 0 });
            Assert.AreEqual(0f, Lift(20f, 20f, 0), 1e-3f, "on the bridge: straight between its ends");
            Assert.AreEqual(-20f, Lift(20f, 40f, 0), 1e-3f, "beside it: the valley");
        }

        [Test]
        public void ElevatedBridgesLiftOnlyThemselves()
        {
            // an overpass (layer 1) over a road in the valley: the overpass's own deck is straight, the road under it stays down
            int n = 5; float step = 10f;
            var h = new float[n * n];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) h[j * n + i] = i == 2 ? -20f : 0f;
            var t = Terrain(h, n, step);
            var type = t.GetType();
            var lift = type.GetMethod("Lift", new[] { typeof(float), typeof(float), typeof(int) });
            type.GetMethod("AddBridge").Invoke(t, new object[] { new System.Collections.Generic.List<Vector2> { new Vector2(0f, 20f), new Vector2(40f, 20f) }, 8f, 1 });
            Assert.AreEqual(0f, (float)lift.Invoke(t, new object[] { 20f, 20f, 1 }), 1e-3f, "the overpass");
            Assert.AreEqual(-20f, (float)lift.Invoke(t, new object[] { 20f, 20f, 0 }), 1e-3f, "the road under it");
            // a second deck over the first (layer 2), from x=20 down to the valley's middle: each layer its own deck
            type.GetMethod("AddBridge").Invoke(t, new object[] { new System.Collections.Generic.List<Vector2> { new Vector2(20f, 0f), new Vector2(20f, 40f) }, 8f, 2 });
            Assert.AreEqual(0f, (float)lift.Invoke(t, new object[] { 20f, 20f, 1 }), 1e-3f, "layer 1 keeps its own deck under layer 2");
            Assert.AreEqual(-20f, (float)lift.Invoke(t, new object[] { 20f, 20f, 2 }), 1e-3f, "layer 2 follows its own ends (both in the valley)");
        }

        // a building of the city models (Bag3dBuildings.Building): vertices, wall and roof triangles
        static object Building(Vector3[] v, int[] walls, int[] roofs, byte roofKind)
        {
            var t = TypeNamed("Bag3dBuildings+Building");
            var b = Activator.CreateInstance(t);
            t.GetField("V").SetValue(b, v); t.GetField("Walls").SetValue(b, walls); t.GetField("Roofs").SetValue(b, roofs);
            t.GetField("RoofKind").SetValue(b, roofKind);
            TypeNamed("Bag3dBuildings").GetMethod("SlopingFacades").Invoke(null, new[] { b });
            return b;
        }

        static int Count(object b, string field) => ((int[])b.GetType().GetField(field).GetValue(b)).Length / 3;

        [Test]
        public void TowersSlopingFacadeIsAWall()
        {
            // a 150 m tower whose face slopes back from 20 m up (labelled a roof by the city model), with a flat top
            var v = new[] { new Vector3(0, 20, 0), new Vector3(30, 20, 0), new Vector3(30, 150, 20), new Vector3(0, 150, 20),
                            new Vector3(0, 150, 30), new Vector3(30, 150, 30) };
            var b = Building(v, new int[0], new[] { 0, 2, 1, 0, 3, 2, 3, 4, 5, 3, 5, 2 }, 1);
            Assert.AreEqual(2, Count(b, "Walls"), "the sloping face becomes a wall");
            Assert.AreEqual(2, Count(b, "Roofs"), "the flat top stays a roof");
            Assert.AreEqual(0, (byte)b.GetType().GetField("RoofKind").GetValue(b), "no tiled roof on the tower");
        }

        [Test]
        public void SpireStaysARoof()
        {
            // a church tower 108 m high: the spire's steep faces start at 60 m (more than a third of its height)
            var v = new[] { new Vector3(0, 60, 0), new Vector3(8, 60, 0), new Vector3(4, 108, 4) };
            var b = Building(v, new int[0], new[] { 0, 2, 1 }, 1);
            Assert.AreEqual(0, Count(b, "Walls"));
            Assert.AreEqual(1, Count(b, "Roofs"));
        }

        [Test]
        public void LowBuildingsKeepSteepRoofs()
        {
            // a house with a steep roof: under 100 m, nothing changes
            var v = new[] { new Vector3(0, 6, 0), new Vector3(8, 6, 0), new Vector3(4, 12, 1) };
            var b = Building(v, new int[0], new[] { 0, 2, 1 }, 1);
            Assert.AreEqual(1, Count(b, "Roofs"));
        }
    }
}
