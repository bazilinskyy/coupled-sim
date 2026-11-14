using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// The weather that really was at the experiment's place at a date and hour, from Open-Meteo's historical weather
/// (weather data by Open-Meteo.com, CC BY 4.0; no key): trial parameter <c>realWeather=2025-06-01T14:00</c> (local time
/// of the map) or <c>-realWeather 2025-06-01T14:00</c>. It sets the weather (from the WMO weather code and the
/// precipitation), the rain's strength, fog from the visibility, the time of day and the day of the year (the sun).
/// Answers are cached in the simulator's data folder (Weather/), so a trial can be repeated offline.
/// </summary>
public static class RealWeather
{
    public sealed class Hour
    {
        public float Temperature, Precipitation, Cloud, Wind, Visibility = -1f;
        public int Code;
    }

    /// <summary>Sets the controller from the weather at that place and local time; false (and a warning) if unknown.</summary>
    public static bool Apply(EnvironmentController env, string when)
    {
        if (!DateTime.TryParse(when.Replace('T', ' '), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
        {
            Debug.LogWarning($"realWeather '{when}': use a date and time such as 2025-06-01T14:00");
            return false;
        }
        var h = Fetch(env.Latitude, env.Longitude, t);
        if (h == null) { Debug.LogWarning($"realWeather {t:yyyy-MM-dd HH:mm}: no weather data (offline and not cached?)"); return false; }
        env.TimeOfDay = t.Hour + t.Minute / 60f;
        env.DayOfYear = t.DayOfYear;
        env.Weather = WeatherOf(h, out float rain, out float fog);
        env.RainOverride = rain;
        env.ExtraFog = fog;
        Debug.Log($"Real weather {t:yyyy-MM-dd HH:mm} at {env.Latitude:F3}, {env.Longitude:F3}: {h.Temperature:F1} °C, cloud cover {h.Cloud:F0}%, " +
                  $"precipitation {h.Precipitation:F1} mm/h, wind {h.Wind:F0} km/h, WMO code {h.Code}" + (h.Visibility >= 0 ? $", visibility {h.Visibility:F0} m" : "") +
                  $" -> {env.Weather} (weather data by Open-Meteo.com, CC BY 4.0)");
        return true;
    }

    /// <summary>The simulator's weather for an hour of observations; rain 0..1 (or -1: the weather's own), fog density (0: none extra).</summary>
    public static Weather WeatherOf(Hour h, out float rain, out float fog)
    {
        rain = -1f; fog = 0f;
        var w = Weather.Clear;
        int c = h.Code;
        if (c == 45 || c == 48) w = Weather.Fog;
        else if (c >= 95) w = Weather.HeavyRain;
        else if (c == 65 || c == 82 || c == 67) w = Weather.HeavyRain;
        else if ((c >= 51 && c <= 64) || c == 66 || c == 80 || c == 81) w = Weather.LightRain;
        else if (c >= 71 && c <= 86) w = Weather.Overcast;           // snow: not drawn, at least grey
        else if (h.Cloud >= 70f || c == 3) w = Weather.Overcast;
        else if (h.Cloud >= 25f || c == 2) w = Weather.PartlyCloudy;
        // the measured amount decides between light and heavy rain
        if (h.Precipitation >= 2.5f && (w == Weather.LightRain || w == Weather.Overcast)) w = Weather.HeavyRain;
        else if (h.Precipitation >= 0.1f && w < Weather.LightRain) w = Weather.LightRain;
        if (w == Weather.LightRain || w == Weather.HeavyRain) rain = Mathf.Clamp(0.15f + h.Precipitation / 4f, 0.15f, 1f);
        // poor visibility as fog: 5% contrast left at the visibility distance (exponential fog: density = 3 / visibility)
        if (h.Visibility > 0f && h.Visibility < 1500f) { if (w < Weather.LightRain) w = Weather.Fog; fog = 3f / Mathf.Max(50f, h.Visibility); }
        return w;
    }

    static string CacheFolder => Path.Combine(Application.persistentDataPath, "Weather");

    /// <summary>The observations of that hour (local time at the place), from the cache or Open-Meteo.</summary>
    public static Hour Fetch(float lat, float lon, DateTime t)
    {
        var key = string.Format(CultureInfo.InvariantCulture, "{0:F3}_{1:F3}_{2:yyyy-MM-dd}", lat, lon, t);
        var file = Path.Combine(CacheFolder, key + ".json");
        string json = null;
        try { if (File.Exists(file)) json = File.ReadAllText(file); } catch { }
        if (json == null)
        {
            string day = t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string q = string.Format(CultureInfo.InvariantCulture,
                "latitude={0:F4}&longitude={1:F4}&start_date={2}&end_date={2}&hourly=temperature_2m,precipitation,cloud_cover,weather_code,wind_speed_10m,visibility&timezone=auto", lat, lon, day);
            // the archive (ERA5) is days behind and has no visibility; the historical forecasts have both for recent years
            bool recent = (DateTime.UtcNow - t).TotalDays < 7;
            json = Get((recent ? "https://api.open-meteo.com/v1/forecast?" : "https://historical-forecast-api.open-meteo.com/v1/forecast?") + q)
                   ?? Get("https://archive-api.open-meteo.com/v1/archive?" + q.Replace(",visibility", ""));
            if (json == null) return null;
            try { Directory.CreateDirectory(CacheFolder); File.WriteAllText(file, json); } catch { }
        }
        return Parse(json, t.Hour);
    }

    static string Get(string url)
    {
        try
        {
            using (var wc = new TimedWebClient())
            {
                wc.Headers.Add("User-Agent", "coupled-sim");
                var s = wc.DownloadString(url);
                return s.Contains("\"hourly\"") ? s : null;
            }
        }
        catch (Exception e) { Debug.LogWarning("Open-Meteo: " + e.Message); return null; }
    }

    sealed class TimedWebClient : System.Net.WebClient
    {
        protected override System.Net.WebRequest GetWebRequest(Uri address)
        {
            var r = base.GetWebRequest(address);
            if (r != null) r.Timeout = 8000;
            return r;
        }
    }

    // the hourly arrays of Open-Meteo's answer at an hour (0..23)
    public static Hour Parse(string json, int hour)
    {
        float At(string name)
        {
            var m = Regex.Match(json, "\"" + name + "\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (!m.Success) return float.NaN;
            var parts = m.Groups[1].Value.Split(',');
            if (hour < 0 || hour >= parts.Length) return float.NaN;
            var v = parts[hour].Trim();
            return v == "null" ? float.NaN : float.Parse(v, CultureInfo.InvariantCulture);
        }
        float temp = At("temperature_2m"), prec = At("precipitation"), cloud = At("cloud_cover"), code = At("weather_code"), wind = At("wind_speed_10m"), vis = At("visibility");
        if (float.IsNaN(code) && float.IsNaN(cloud)) return null;
        return new Hour
        {
            Temperature = float.IsNaN(temp) ? 0f : temp, Precipitation = float.IsNaN(prec) ? 0f : prec, Cloud = float.IsNaN(cloud) ? 0f : cloud,
            Code = float.IsNaN(code) ? 0 : (int)code, Wind = float.IsNaN(wind) ? 0f : wind, Visibility = float.IsNaN(vis) ? -1f : vis,
        };
    }
}
