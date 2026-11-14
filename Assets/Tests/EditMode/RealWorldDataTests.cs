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

        [Test]
        public void OpenMeteoAnswersAreRead()
        {
            const string json = "{\"latitude\":52.0,\"hourly\":{\"time\":[\"2025-06-01T00:00\",\"2025-06-01T01:00\"]," +
                                "\"temperature_2m\":[11.5,12.0],\"precipitation\":[0.0,1.2],\"cloud_cover\":[20,95]," +
                                "\"weather_code\":[1,63],\"wind_speed_10m\":[10.0,14.5],\"visibility\":[null,6000.0]}}";
            var rw = TypeNamed("RealWeather");
            var h = rw.GetMethod("Parse").Invoke(null, new object[] { json, 1 });
            var t = h.GetType();
            Assert.AreEqual(63, t.GetField("Code").GetValue(h));
            Assert.AreEqual(1.2f, (float)t.GetField("Precipitation").GetValue(h), 1e-4f);
            Assert.AreEqual(95f, (float)t.GetField("Cloud").GetValue(h), 1e-4f);
            Assert.AreEqual(6000f, (float)t.GetField("Visibility").GetValue(h), 1e-4f);
            var h0 = rw.GetMethod("Parse").Invoke(null, new object[] { json, 0 });
            Assert.AreEqual(-1f, (float)t.GetField("Visibility").GetValue(h0), 1e-4f, "missing visibility");
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
            Assert.AreEqual(-20f, (float)type.GetMethod("Lift").Invoke(t, new object[] { 20f, 20f }), 1e-3f, "no bridge: the valley");
            type.GetMethod("AddBridge").Invoke(t, new object[] { new System.Collections.Generic.List<Vector2> { new Vector2(0f, 20f), new Vector2(40f, 20f) }, 8f });
            Assert.AreEqual(0f, (float)type.GetMethod("Lift").Invoke(t, new object[] { 20f, 20f }), 1e-3f, "on the bridge: straight between its ends");
            Assert.AreEqual(-20f, (float)type.GetMethod("Lift").Invoke(t, new object[] { 20f, 40f }), 1e-3f, "beside it: the valley");
        }
    }
}
