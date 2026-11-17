using NUnit.Framework;
using UnityEngine;

// AerialPhoto is in Assembly-CSharp, which test assemblies cannot reference: used by reflection
public class AerialPhotoTests
{
    static Vector2 Uv(float half, Vector3 p)
    {
        var t = System.Type.GetType("AerialPhoto, Assembly-CSharp", true);
        var photo = System.Activator.CreateInstance(t);
        t.GetField("HalfSize").SetValue(photo, half);
        return (Vector2)t.GetMethod("Uv").Invoke(photo, new object[] { p });
    }

    [Test]
    public void CentreIsTheMiddleOfThePhoto() => Assert.That(Vector2.Distance(Uv(500f, Vector3.zero), new Vector2(0.5f, 0.5f)), Is.LessThan(1e-6f));

    [Test]
    public void NorthEastCornerIsTopRight()
    {
        // the photo is saved north up; Unity's LoadImage puts the image's top row at v = 1
        var uv = Uv(500f, new Vector3(500f, 12f, 500f));
        Assert.That(uv.x, Is.EqualTo(1f).Within(1e-6f));
        Assert.That(uv.y, Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void SouthWestCornerIsBottomLeft()
    {
        var uv = Uv(400f, new Vector3(-400f, 0f, -400f));
        Assert.That(uv.x, Is.EqualTo(0f).Within(1e-6f));
        Assert.That(uv.y, Is.EqualTo(0f).Within(1e-6f));
    }

    [Test]
    public void NonSquareSampleReadsCorrectRowAndColumn()
    {
        var t = System.Type.GetType("AerialPhoto, Assembly-CSharp", true);
        var photo = System.Activator.CreateInstance(t);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        t.GetField("HalfSize").SetValue(photo, 100f);
        t.GetField("_smallWidth", flags).SetValue(photo, 4);
        t.GetField("_smallHeight", flags).SetValue(photo, 2);
        var px = new Color32[8];
        px[1 * 4 + 3] = new Color32(250, 40, 10, 255); // top-right (x=3, y=1)
        t.GetField("_small", flags).SetValue(photo, px);
        var c = (Color)t.GetMethod("Sample").Invoke(photo, new object[] { new Vector3(90f, 0f, 90f) });
        Assert.AreEqual(250f / 255f, c.r, 1e-3f);
        Assert.AreEqual(40f / 255f, c.g, 1e-3f);
    }
}
