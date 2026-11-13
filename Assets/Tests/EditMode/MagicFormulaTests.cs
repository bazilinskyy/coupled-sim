using NUnit.Framework;
using UnityEngine;

// MagicFormula is in Assembly-CSharp, which test assemblies cannot reference: used by reflection
public class MagicFormulaTests
{
    static object Formula(float b, float c, float d, float e)
    {
        var t = System.Type.GetType("CoupledSim.Vehicles.MagicFormula, Assembly-CSharp", true);
        var f = System.Activator.CreateInstance(t);
        t.GetField("B").SetValue(f, b); t.GetField("C").SetValue(f, c); t.GetField("D").SetValue(f, d); t.GetField("E").SetValue(f, e);
        return f;
    }

    [Test]
    public void PeakMatchesTheFormula()
    {
        var f = Formula(10f, 1.9f, 1.0f, 0.97f);
        var t = f.GetType();
        float y0 = (float)t.GetMethod("Evaluate").Invoke(f, new object[] { 0f });
        Assert.AreEqual(0f, y0, 1e-5f, "no force without slip");
        var args = new object[] { 1f, 0f, 0f, 0f, 0f };
        t.GetMethod("Fit").Invoke(f, args);
        float peakSlip = (float)args[1], peak = (float)args[2], slide = (float)args[4];
        Assert.AreEqual(1.0f, peak, 0.01f, "peak = D");
        Assert.That(peakSlip, Is.InRange(0.05f, 0.3f), "longitudinal peak at 5-30% slip");
        Assert.That(slide, Is.LessThan(peak), "sliding friction below the peak");
    }

    [Test]
    public void FrictionCurveScalesWithGrip()
    {
        var f = Formula(8f, 1.3f, 0.9f, -1.6f);
        var c = (WheelFrictionCurve)f.GetType().GetMethod("ToWheelFriction").Invoke(f, new object[] { new WheelFrictionCurve(), 0.5f, 0.7f });
        Assert.AreEqual(0.7f, c.stiffness, 1e-5f);
        Assert.AreEqual(0.9f, c.extremumValue, 0.01f);
        Assert.That(c.asymptoteSlip, Is.GreaterThan(c.extremumSlip));
    }
}
