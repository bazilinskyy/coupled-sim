using System.Collections.Generic;
using System.Linq;
using CoupledSim.Experiments;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    public class TrialSequenceTests
    {
        [Test]
        public void CsvWithQuotesSemicolonsAndDirective()
        {
            var t = TrialTable.ParseCsv("# a comment\n#order=latin\nexperiment;role;block;note\nBase;Driver;A;\"x; \"\"y\"\"\"\n\n;;;\nBase;Pedestrian;B;\n");
            Assert.AreEqual(TrialOrder.Latin, t.Order);
            CollectionAssert.AreEqual(new[] { "experiment", "role", "block", "note" }, t.Columns);
            Assert.AreEqual(2, t.Rows.Count);
            Assert.AreEqual("x; \"y\"", t.Get(0, "note"));
            Assert.AreEqual("Pedestrian", t.Get(1, "ROLE"));
            Assert.AreEqual("fallback", t.Get(1, "note", "fallback"));
        }

        [Test]
        public void CsvRoundTrip()
        {
            var t = TrialTable.ParseCsv("experiment,weather,time\nBase,\"Light, rain\",17:30\n");
            var u = TrialTable.ParseCsv(t.ToCsv());
            Assert.AreEqual("Light, rain", u.Get(0, "weather"));
            Assert.AreEqual("17:30", u.Get(0, "time"));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void WilliamsSquareIsBalanced(int n)
        {
            int rows = n % 2 == 0 ? n : 2 * n;
            var pairs = new Dictionary<(int, int), int>();
            var positions = new int[n, n];
            for (int r = 0; r < rows; r++)
            {
                var row = Counterbalance.WilliamsRow(n, r);
                CollectionAssert.AreEquivalent(Enumerable.Range(0, n), row, "each condition once per row");
                for (int j = 0; j < n; j++) positions[row[j], j]++;
                for (int j = 1; j < n; j++)
                {
                    var k = (row[j - 1], row[j]);
                    pairs[k] = pairs.TryGetValue(k, out var c) ? c + 1 : 1;
                }
            }
            int perPosition = rows / n;
            for (int c = 0; c < n; c++)
                for (int j = 0; j < n; j++) Assert.AreEqual(perPosition, positions[c, j], "each condition equally often in each position");
            // every ordered pair of different conditions follows equally often
            Assert.AreEqual(n * (n - 1), pairs.Count);
            Assert.IsTrue(pairs.Values.All(v => v == pairs.Values.First()), "first-order carry-over balanced");
        }

        [Test]
        public void BlocksAreCounterbalancedPracticeStaysFirst()
        {
            var t = TrialTable.ParseCsv("name,block\npractice,\na1,A\na2,A\nb1,B\nc1,C\nd1,D\nend,\n");
            string Names(int participant, TrialOrder o) => string.Join(" ", t.OrderFor(participant, o).Select(i => t.Get(i, "name")));
            Assert.AreEqual("practice a1 a2 b1 c1 d1 end", Names(0, TrialOrder.Latin));
            Assert.AreEqual("practice a1 a2 b1 d1 c1 end", Names(1, TrialOrder.Latin));
            Assert.AreEqual("practice b1 c1 a1 a2 d1 end", Names(2, TrialOrder.Latin));
            Assert.AreEqual(Names(1, TrialOrder.Latin), Names(5, TrialOrder.Latin), "4 blocks: the square repeats every 4 participants");
            var r1 = Names(3, TrialOrder.Random);
            Assert.AreEqual(r1, Names(3, TrialOrder.Random), "random order is repeatable per participant");
            StringAssert.StartsWith("practice ", r1);
            StringAssert.EndsWith(" end", r1);
            Assert.AreEqual("practice a1 a2 b1 c1 d1 end", Names(3, TrialOrder.None));
        }
    }
}
