using System.Collections;
using NUnit.Framework;

// InCarDisplay is in Assembly-CSharp, which test assemblies cannot reference: used by reflection
public class InCarMessageTests
{
    static IList Parse(string csv, out string error)
    {
        var t = System.Type.GetType("InCarDisplay, Assembly-CSharp", true);
        var args = new object[] { csv, null };
        var list = (IList)t.GetMethod("ParseScheduleText").Invoke(null, args);
        error = (string)args[1];
        return list;
    }

    static object F(object m, string field) => m.GetType().GetField(field).GetValue(m);

    [Test]
    public void ReadsTimesDistancesAndStates()
    {
        var list = Parse("at;seconds;display;text;color;atDistance;onState\n2;4;hud;Hello;#FFFFFF;;\n;5;centre;\"Turn right; now\";;150;\n;3;all;Engaged;#00E5FF;;AUTOMATED\n", out var error);
        Assert.IsNull(error);
        Assert.AreEqual(3, list.Count);
        Assert.AreEqual(2f, (float)F(list[0], "at"));
        Assert.AreEqual("hud", F(list[0], "display"));
        Assert.AreEqual("Turn right; now", F(list[1], "text"));
        Assert.AreEqual(150f, (float)F(list[1], "atDistance"));
        Assert.AreEqual(-1f, (float)F(list[1], "at"));
        Assert.AreEqual("AUTOMATED", F(list[2], "onState"));
    }

    [Test]
    public void CommaSeparatedAndCommentsWork()
    {
        var list = Parse("text,at,seconds\n# a comment\nHi,1.5,2\n", out var error);
        Assert.IsNull(error);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(1.5f, (float)F(list[0], "at"));
    }

    [Test]
    public void NeedsATextColumn()
    {
        Parse("at;seconds\n1;2\n", out var error);
        Assert.IsNotNull(error);
    }
}
