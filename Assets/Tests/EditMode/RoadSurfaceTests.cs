using NUnit.Framework;

// RoadSurfaces is in Assembly-CSharp, which test assemblies cannot reference: used by reflection
public class RoadSurfaceTests
{
    static readonly System.Type Surfaces = System.Type.GetType("CoupledSim.Vehicles.RoadSurfaces, Assembly-CSharp", true);
    static readonly System.Type Surface = System.Type.GetType("CoupledSim.Vehicles.RoadSurface, Assembly-CSharp", true);

    static object FromName(string n) => Surfaces.GetMethod("FromName").Invoke(null, new object[] { n });
    static string Name(object s) => (string)Surface.GetField("Name").GetValue(s);
    static float F(object s, string field) => (float)Surface.GetField(field).GetValue(s);

    [TestCase("Roads", "asphalt")]
    [TestCase("Roads bricks", "bricks")]
    [TestCase("Roads cobblestones", "cobblestones")]
    [TestCase("Roads gravel", "gravel")]
    [TestCase("Green areas", "grass")]
    [TestCase("Pavements", "paving")]
    [TestCase("Kerbs", "paving")]
    [TestCase("Cycle paths", "asphalt")]
    [TestCase("Ground", "asphalt")]
    [TestCase("", "asphalt")]
    public void MapMeshNames(string mesh, string surface) => Assert.AreEqual(surface, Name(FromName(mesh)));

    [Test]
    public void GripOrder()
    {
        // dry: asphalt > bricks > cobblestones > gravel > grass
        string[] order = { "asphalt", "bricks", "cobblestones", "gravel", "grass" };
        for (int i = 1; i < order.Length; i++)
            Assert.Greater(F(FromName(order[i - 1]), "Grip"), F(FromName(order[i]), "Grip"), order[i]);
    }

    [Test]
    public void WetCobblestonesLoseMoreThanWetAsphalt()
    {
        var gripAt = Surface.GetMethod("GripAt");
        float asphaltWet = (float)gripAt.Invoke(FromName("asphalt"), new object[] { 1f, 0.7f });
        float cobbleWet = (float)gripAt.Invoke(FromName("cobblestones"), new object[] { 1f, 0.7f });
        float cobbleDry = (float)gripAt.Invoke(FromName("cobblestones"), new object[] { 0f, 0.7f });
        Assert.AreEqual(0.7f, asphaltWet, 1e-5f);
        Assert.AreEqual(0.85f, cobbleDry, 1e-5f);
        Assert.Less(cobbleWet / cobbleDry, asphaltWet);
    }

    [Test]
    public void AsphaltIsSmoothAndQuietest()
    {
        var asphalt = FromName("asphalt");
        Assert.AreEqual(0f, F(asphalt, "Roughness"));
        Assert.Greater(F(FromName("cobblestones"), "Roughness"), F(FromName("bricks"), "Roughness"));
        Assert.Greater(F(FromName("cobblestones"), "NoiseDb"), F(FromName("bricks"), "NoiseDb"));
    }
}
