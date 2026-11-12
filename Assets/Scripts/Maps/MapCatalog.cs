using System;
using System.Collections.Generic;

/// <summary>A real-world map: an area of a city built from OpenStreetMap data at load time.</summary>
[Serializable]
public struct MapInfo
{
    public string Name;
    public string City;
    public string Country;
    public string Continent;
    /// <summary>Population of the city (approximate, city proper), to choose between small and large cities.</summary>
    public int Population;
    /// <summary>Centre of the map (degrees); the scene origin.</summary>
    public double Latitude, Longitude;
    /// <summary>Hours ahead of UTC in June (for the sun position at a given clock time).</summary>
    public float UtcOffsetJune;
    public bool LeftHandTraffic;
    public string Area;

    public override string ToString() => $"{City} ({Country})";
}

/// <summary>
/// The maps that can be chosen for experiments, like CARLA's towns: the hand-built town
/// (MainScene) plus city centres from OpenStreetMap on every continent, from small towns to
/// megacities. Map data © OpenStreetMap contributors (ODbL), in StreamingAssets/Maps/{Name}.osm.gz.
/// </summary>
public static class MapCatalog
{
    public static readonly MapInfo[] Maps =
    {
        // the Netherlands
        new MapInfo { Name = "Eindhoven", City = "Eindhoven", Country = "Netherlands", Continent = "Europe", Population = 240000, Latitude = 51.441, Longitude = 5.4775, UtcOffsetJune = 2, Area = "city centre: station, 18 Septemberplein, Markt" },
        new MapInfo { Name = "Delft", City = "Delft", Country = "Netherlands", Continent = "Europe", Population = 105000, Latitude = 52.01125, Longitude = 4.359, UtcOffsetJune = 2, Area = "historic centre: Markt, canals" },
        // Europe
        new MapInfo { Name = "London", City = "London", Country = "United Kingdom", Continent = "Europe", Population = 8900000, Latitude = 51.5114, Longitude = -0.1235, UtcOffsetJune = 1, LeftHandTraffic = true, Area = "Covent Garden, Strand" },
        new MapInfo { Name = "Barcelona", City = "Barcelona", Country = "Spain", Continent = "Europe", Population = 1600000, Latitude = 41.3870, Longitude = 2.1700, UtcOffsetJune = 2, Area = "Plaça de Catalunya, Eixample" },
        new MapInfo { Name = "Ghent", City = "Ghent", Country = "Belgium", Continent = "Europe", Population = 265000, Latitude = 51.0543, Longitude = 3.7210, UtcOffsetJune = 2, Area = "historic centre" },
        // Asia
        new MapInfo { Name = "Tokyo", City = "Tokyo", Country = "Japan", Continent = "Asia", Population = 14000000, Latitude = 35.6595, Longitude = 139.7005, UtcOffsetJune = 9, LeftHandTraffic = true, Area = "Shibuya crossing" },
        new MapInfo { Name = "HoChiMinhCity", City = "Ho Chi Minh City", Country = "Vietnam", Continent = "Asia", Population = 9000000, Latitude = 10.7730, Longitude = 106.7000, UtcOffsetJune = 7, Area = "District 1" },
        new MapInfo { Name = "LuangPrabang", City = "Luang Prabang", Country = "Laos", Continent = "Asia", Population = 56000, Latitude = 19.8890, Longitude = 102.1360, UtcOffsetJune = 7, Area = "old town" },
        // Africa
        new MapInfo { Name = "Cairo", City = "Cairo", Country = "Egypt", Continent = "Africa", Population = 10000000, Latitude = 30.0444, Longitude = 31.2357, UtcOffsetJune = 3, Area = "Tahrir Square, downtown" },
        new MapInfo { Name = "Nairobi", City = "Nairobi", Country = "Kenya", Continent = "Africa", Population = 4400000, Latitude = -1.2864, Longitude = 36.8172, UtcOffsetJune = 3, LeftHandTraffic = true, Area = "central business district" },
        new MapInfo { Name = "Stellenbosch", City = "Stellenbosch", Country = "South Africa", Continent = "Africa", Population = 175000, Latitude = -33.9360, Longitude = 18.8610, UtcOffsetJune = 2, LeftHandTraffic = true, Area = "town centre" },
        // North America
        new MapInfo { Name = "NewYork", City = "New York", Country = "United States", Continent = "North America", Population = 8300000, Latitude = 40.7580, Longitude = -73.9855, UtcOffsetJune = -4, Area = "Midtown Manhattan, Times Square" },
        new MapInfo { Name = "Montreal", City = "Montreal", Country = "Canada", Continent = "North America", Population = 1800000, Latitude = 45.5017, Longitude = -73.5673, UtcOffsetJune = -4, Area = "downtown" },
        new MapInfo { Name = "Boulder", City = "Boulder", Country = "United States", Continent = "North America", Population = 105000, Latitude = 40.0176, Longitude = -105.2797, UtcOffsetJune = -6, Area = "Pearl Street" },
        // South America
        new MapInfo { Name = "SaoPaulo", City = "São Paulo", Country = "Brazil", Continent = "South America", Population = 11500000, Latitude = -23.5614, Longitude = -46.6559, UtcOffsetJune = -3, Area = "Avenida Paulista" },
        new MapInfo { Name = "Montevideo", City = "Montevideo", Country = "Uruguay", Continent = "South America", Population = 1300000, Latitude = -34.9060, Longitude = -56.1990, UtcOffsetJune = -3, Area = "Centro" },
        new MapInfo { Name = "Cusco", City = "Cusco", Country = "Peru", Continent = "South America", Population = 430000, Latitude = -13.5170, Longitude = -71.9785, UtcOffsetJune = -5, Area = "Plaza de Armas" },
        // Oceania
        new MapInfo { Name = "Sydney", City = "Sydney", Country = "Australia", Continent = "Oceania", Population = 5300000, Latitude = -33.8700, Longitude = 151.2070, UtcOffsetJune = 10, LeftHandTraffic = true, Area = "central business district" },
        new MapInfo { Name = "Auckland", City = "Auckland", Country = "New Zealand", Continent = "Oceania", Population = 1700000, Latitude = -36.8485, Longitude = 174.7633, UtcOffsetJune = 12, LeftHandTraffic = true, Area = "city centre" },
        new MapInfo { Name = "Wellington", City = "Wellington", Country = "New Zealand", Continent = "Oceania", Population = 215000, Latitude = -41.2865, Longitude = 174.7762, UtcOffsetJune = 12, LeftHandTraffic = true, Area = "city centre" },
    };

    public static int IndexOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        for (int i = 0; i < Maps.Length; i++)
            if (string.Equals(Maps[i].Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(Maps[i].City, name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    public static bool TryGet(string name, out MapInfo map)
    {
        int i = IndexOf(name);
        map = i >= 0 ? Maps[i] : default;
        return i >= 0;
    }

    public static IEnumerable<string> Names()
    {
        foreach (var m in Maps) yield return m.Name;
    }
}
