using System.Collections.Generic;
using System.Linq;
using CoupledSim.Experiments;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    public class ExperimentFileTests
    {
        [Test]
        public void YamlBlocksFlowQuotesAndComments()
        {
            var y = (Dictionary<string, object>)MiniYaml.Parse(
                "# comment\nname: test   # trailing\ntime: \"13:00\"\nurl: http://x.org/a#b\nlist:\n  - a\n  - 'b # not a comment'\n  - {k: 1, s: [1, 2]}\n" +
                "items:\n- role: Driver\n  input: wheel\n- role: Pedestrian\nempty:\nnested:\n  deep:\n    v: ~\n");
            Assert.AreEqual("test", y["name"]);
            Assert.AreEqual("13:00", y["time"]);
            Assert.AreEqual("http://x.org/a#b", y["url"]);
            var list = (List<object>)y["list"];
            Assert.AreEqual("b # not a comment", list[1]);
            var flow = (Dictionary<string, object>)list[2];
            Assert.AreEqual("1", flow["k"]);
            CollectionAssert.AreEqual(new object[] { "1", "2" }, (List<object>)flow["s"]);
            var items = (List<object>)y["items"];
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("wheel", ((Dictionary<string, object>)items[0])["input"]);
            Assert.IsNull(y["empty"]);
            Assert.IsNull(((Dictionary<string, object>)((Dictionary<string, object>)y["nested"])["deep"])["v"]);
        }

        [Test]
        public void YamlErrorsNameTheLine()
        {
            var e = Assert.Throws<MiniYaml.YamlException>(() => MiniYaml.Parse("a: 1\nb: [1, 2\n"));
            StringAssert.Contains("line 2", e.Message);
            Assert.Throws<MiniYaml.YamlException>(() => MiniYaml.Parse("a: 1\na: 2\n"));
            Assert.Throws<MiniYaml.YamlException>(() => MiniYaml.Parse("a:\n  b: 1\n c: 2\n"));
        }

        [Test]
        public void WrittenYamlReadsBack()
        {
            var tree = MiniYaml.Parse("name: x\ntrials:\n  - {block: A, simulation: {time: \"13:00\", weather: Fog}}\n  - notes: 'a: b, c'\nlist: [1, two]\n");
            var again = MiniYaml.Parse(MiniYaml.Write(tree));
            Assert.AreEqual(MiniYaml.Write(tree), MiniYaml.Write(again));
            var t0 = (Dictionary<string, object>)((List<object>)((Dictionary<string, object>)again)["trials"])[0];
            Assert.AreEqual("13:00", ((Dictionary<string, object>)t0["simulation"])["time"]);
            Assert.AreEqual("a: b, c", ((Dictionary<string, object>)((List<object>)((Dictionary<string, object>)again)["trials"])[1])["notes"]);
        }

        [Test]
        public void ExperimentBecomesTrialRows()
        {
            var warnings = new List<string>();
            var t = ExperimentFile.Read(@"
name: study
map: {bbox: [52.00, 4.34, 52.02, 4.38]}
simulation: {time: '13:00', weather: Clear, wind_direction: 270}
traffic: {cars_per_km: 15, autopilot_kmh: 30, drone_swarm: 4}
parameters: {MyParam: 7}
participants:
  - role: Driver
    input: autopilot
    vehicle: ToyotaCorolla
    car_spec: van
    magic_formula_tyres: true
    companions: 'follow:-1.2,0;cross:1.4,0.4,3'
  - role: Pedestrian
  - Cyclist
order: random
duration_s: 90
trials:
  - {notes: practice}
  - {block: A, simulation: {weather: Fog}, recording: {start_s: 5, duration_s: 20}}
  - block: B
    participants: [{input: vr, vehicle: LandRoverDefender}]
    surprise: 1
", warnings);
            Assert.AreEqual(TrialOrder.Random, t.Order);
            Assert.AreEqual(3, t.Rows.Count);
            Assert.AreEqual("CityMap", t.Get(0, "experiment"));
            StringAssert.StartsWith("52.010000,4.360000,", t.Get(0, "map"));
            Assert.AreEqual(1370, int.Parse(t.Get(0, "map").Split(',')[2]), 2, "half the box's width at 52 degrees");
            Assert.AreEqual("Clear", t.Get(0, "weather"));
            Assert.AreEqual("Fog", t.Get(1, "weather"));
            Assert.AreEqual("13:00", t.Get(1, "time"), "the file's settings stay under a trial's");
            Assert.AreEqual("270", t.Get(0, "winddirection"));
            Assert.AreEqual("15", t.Get(0, "mapTraffic"));
            Assert.AreEqual("30", t.Get(0, "autopilot"));
            Assert.AreEqual("4", t.Get(0, "droneswarm"));
            Assert.AreEqual("7", t.Get(0, "MyParam"));
            Assert.AreEqual("90", t.Get(2, "duration"));
            Assert.AreEqual("5", t.Get(1, "recordingStart"));
            Assert.AreEqual("Driver", t.Get(0, "role"));
            Assert.AreEqual("Flat", t.Get(0, "input"));
            Assert.AreEqual("VR", t.Get(2, "input"));
            Assert.AreEqual("Pedestrian,Cyclist", t.Get(0, "clientRoles"));
            var args = CommandLineSplit(t.Get(0, ExperimentFile.ArgsColumn));
            CollectionAssert.IsSubsetOf(new[] { "-autopilot", "-driverCar", "ToyotaCorolla", "-carSpec", "van", "-magicFormulaTyres", "-companions", "follow:-1.2,0;cross:1.4,0.4,3" }, args);
            var args2 = CommandLineSplit(t.Get(2, ExperimentFile.ArgsColumn));
            CollectionAssert.Contains(args2, "LandRoverDefender");
            CollectionAssert.DoesNotContain(args2, "ToyotaCorolla");
            CollectionAssert.Contains(args2, "-carSpec", "participants merge by position");
            Assert.IsTrue(warnings.Any(w => w.Contains("surprise")), string.Join("; ", warnings));
            // the merged configuration of a trial reads back as YAML
            var config = (Dictionary<string, object>)MiniYaml.Parse(t.Get(1, ExperimentFile.ConfigColumn));
            Assert.AreEqual("Fog", ((Dictionary<string, object>)config["simulation"])["weather"]);
            Assert.AreEqual("A", config["block"]);
        }

        [Test]
        public void ExampleFileAndOrders()
        {
            var warnings = new List<string>();
            var t = ExperimentFile.Read(System.IO.File.ReadAllText("Assets/StreamingAssets/Trials/example.yaml"), warnings);
            CollectionAssert.IsEmpty(warnings);
            Assert.AreEqual(4, t.Rows.Count);
            Assert.AreEqual("Delft", t.Get(0, "map"));
            Assert.AreEqual("Pedestrian", t.Get(0, "clientRoles"));
            Assert.AreEqual("60", t.Get(0, "duration"));
            Assert.AreEqual("120", t.Get(1, "duration"));
            Assert.AreEqual("22:30", t.Get(3, "time"));
            StringAssert.Contains("LandRoverDefender", t.Get(3, ExperimentFile.ArgsColumn));
            StringAssert.Contains("-hud HudSpeed", t.Get(3, ExperimentFile.ArgsColumn));
            for (int p = 1; p <= 3; p++)
            {
                var order = t.OrderFor(p, t.Order ?? TrialOrder.Latin);
                Assert.AreEqual(0, order[0], "practice first");
                CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, order.Skip(1).ToList());
            }
        }

        [Test]
        public void MapForms()
        {
            Assert.AreEqual("Ghent", ExperimentFile.Read("map: Ghent").Get(0, "map"));
            Assert.AreEqual("51.44,5.48,300", ExperimentFile.Read("map: {lat: 51.44, lon: 5.48, radius_m: 300}").Get(0, "map"));
            Assert.AreEqual("Base", ExperimentFile.Read("simulation: {weather: Fog}").Get(0, "experiment"));
            Assert.AreEqual("Traffic", ExperimentFile.Read("experiment: Traffic\nmap: Delft").Get(0, "experiment"));
        }

        // as CommandLine.Split (in the game assembly): spaces separate, "double quotes" group
        static List<string> CommandLineSplit(string s)
        {
            var parts = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false, any = false;
            foreach (var c in s ?? "")
            {
                if (c == '"') { q = !q; any = true; continue; }
                if (c == ' ' && !q) { if (any) parts.Add(cur.ToString()); cur.Clear(); any = false; continue; }
                cur.Append(c); any = true;
            }
            if (any) parts.Add(cur.ToString());
            return parts;
        }
    }
}
