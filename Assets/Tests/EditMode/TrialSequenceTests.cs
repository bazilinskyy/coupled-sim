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
        public void FirstStudyTrialFile()
        {
            // the tutorial's file (docs/first-study.md): practice first, then the three eHMI blocks in a balanced order
            var t = TrialTable.ParseCsv(System.IO.File.ReadAllText("Assets/StreamingAssets/Trials/first-study.csv"));
            Assert.AreEqual(TrialOrder.Latin, t.Order);
            Assert.AreEqual(7, t.Rows.Count);
            CollectionAssert.IsSubsetOf(Enumerable.Range(0, 7).Select(i => t.Get(i, "ehmiDesign")).Distinct().ToList(), new[] { "none", "LightBarCyan@Bumper", "TextWalk@Windshield" });
            var firstBlocks = new HashSet<string>();
            for (int participant = 1; participant <= 6; participant++)
            {
                var order = t.OrderFor(participant, TrialOrder.Latin);
                Assert.AreEqual(0, order[0], "practice first");
                var blocks = order.Skip(1).Select(i => t.Get(i, "block")).ToList();
                Assert.AreEqual(blocks[0], blocks[1]); Assert.AreEqual(blocks[2], blocks[3]); Assert.AreEqual(blocks[4], blocks[5]);
                firstBlocks.Add(blocks[0]);
            }
            Assert.AreEqual(3, firstBlocks.Count, "each eHMI comes first for some participants");
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

        [Test]
        public void LogConverterWritesAlignedHeadersAndHandlesZeroDtAndEmptyLogs()
        {
            var converterType = System.Type.GetType("LogConverter, Assembly-CSharp", true);
            var converter = System.Activator.CreateInstance(converterType, new object[] { new[] { "Head" } });
            var translate = converterType.GetMethod("TranslateBinaryLogToCsv");

            string tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "coupled_sim_test_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tmpDir);
            try
            {
                // 1) Empty 0-frame log does not throw
                string emptyBin = System.IO.Path.Combine(tmpDir, "empty.binLog");
                string emptyCsv = System.IO.Path.Combine(tmpDir, "empty.csv");
                using (var bw = new System.IO.BinaryWriter(System.IO.File.Create(emptyBin)))
                {
                    bw.Write(System.DateTime.UtcNow.ToBinary());
                    bw.Write(-1); // LocalDriver
                    bw.Write(0);  // numPersistentDrivers
                    bw.Write(0);  // numPedestrians
                    bw.Write(0);  // numPOIs
                    bw.Write(0);  // numCarLights
                    bw.Write(0);  // numPedestrianLights
                }
                translate.Invoke(converter, new object[] { emptyBin, emptyCsv, new[] { "Head" }, "WorldRoot", UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity });
                Assert.IsTrue(System.IO.File.Exists(emptyCsv));

                // 2) Log with 1 local driver, 1 AI car, 2 pedestrians, and duplicate timestamp (dt = 0)
                string fullBin = System.IO.Path.Combine(tmpDir, "full.binLog");
                string fullCsv = System.IO.Path.Combine(tmpDir, "full.csv");
                using (var bw = new System.IO.BinaryWriter(System.IO.File.Create(fullBin)))
                {
                    bw.Write(System.DateTime.UtcNow.ToBinary());
                    bw.Write(0); // LocalDriver = 0
                    bw.Write(1); // numPersistentDrivers = 1
                    bw.Write(2); // numPedestrians = 2
                    bw.Write(0); // POIs
                    bw.Write(0); // CarLights
                    bw.Write(0); // PedLights

                    bw.Write(1); // LogFrameType.AICarSpawn -> index 1 is AI car
                    for (int f = 0; f < 3; f++)
                    {
                        float ts = f == 2 ? 0.1f : f * 0.1f; // frame 1 and 2 have identical timestamp (dt = 0)
                        bw.Write(0); // LogFrameType.PositionsUpdate
                        bw.Write(ts);
                        bw.Write(0.01f); // ping

                        // Driver 0 (LocalDriver, rotated 90 deg around Y, moving along +X at 10 m/s -> local +Z!)
                        bw.Write(f * 1f); bw.Write(0f); bw.Write(0f); // pos
                        bw.Write(0f); bw.Write(90f); bw.Write(0f);    // euler rot
                        bw.Write(0); bw.Write(false); bw.Write(false);
                        bw.Write(10f); bw.Write(0f); bw.Write(0f);    // rbVel

                        // Driver 1 (AICar)
                        bw.Write(0f); bw.Write(0f); bw.Write(f * 1f);
                        bw.Write(0f); bw.Write(0f); bw.Write(0f);
                        bw.Write(0); bw.Write(false); bw.Write(false);
                        bw.Write(0f); bw.Write(0f); bw.Write(10f);    // rbVel
                        bw.Write(36f);                                // aicar_speed
                        bw.Write(false); bw.Write(false); bw.Write(false); bw.Write(false);

                        // 2 Pedestrians (Root + 1 bone "Head" = 2 transforms each)
                        for (int p = 0; p < 2; p++)
                        {
                            bw.Write(2); // 2 positions
                            for (int b = 0; b < 2; b++) { bw.Write(0f); bw.Write(0f); bw.Write(0f); }
                            bw.Write(2); // 2 rotations
                            for (int b = 0; b < 2; b++) { bw.Write(0f); bw.Write(0f); bw.Write(0f); }
                            bw.Write(0); bw.Write(false); bw.Write(false);
                        }
                    }
                }
                translate.Invoke(converter, new object[] { fullBin, fullCsv, new[] { "Head" }, "WorldRoot", UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity });
                var lines = System.IO.File.ReadAllLines(fullCsv);
                // Header rows start after Root, Start Time, and 4 POIs (3 custom + WorldRoot) -> index 6, 7, 8; data rows 9, 10, 11
                int expectedCols = 2 + 27 + 32 + 2 * 12; // 85 columns
                Assert.AreEqual(expectedCols, lines[6].Split(';').Length, "row 1 entity header columns");
                Assert.AreEqual(expectedCols, lines[7].Split(';').Length, "row 2 bone header columns");
                Assert.AreEqual(expectedCols, lines[8].Split(';').Length, "row 3 field header columns");
                Assert.AreEqual(expectedCols, lines[9].Split(';').Length, "data row 0 columns");
                Assert.AreEqual(expectedCols, lines[10].Split(';').Length, "data row 1 columns");
                Assert.AreEqual(expectedCols, lines[11].Split(';').Length, "data row 2 (dt=0) columns");
                Assert.IsFalse(lines[11].Contains("NaN") || lines[11].Contains("Infinity"), "dt=0 must not emit NaN/Infinity");

                // Verify local velocity on frame 1 (lines[10]): Driver 0 faces +X (yaw 90°) and moved +1m along +X in 0.1s (36 km/h) -> vel_local_z (col 13) ~ 36, vel_local_smooth_z (col 16) ~ 36
                var row1 = lines[10].Split(';');
                Assert.AreEqual(36f, float.Parse(row1[13], System.Globalization.CultureInfo.InvariantCulture), 0.1f, "vel_local_z");
                Assert.AreEqual(36f, float.Parse(row1[16], System.Globalization.CultureInfo.InvariantCulture), 0.1f, "vel_local_smooth_z");

                // 3) Re-running converter on empty log clears previous aiCarIndexes cleanly
                translate.Invoke(converter, new object[] { emptyBin, emptyCsv, new[] { "Head" }, "WorldRoot", UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity });
            }
            finally
            {
                if (System.IO.Directory.Exists(tmpDir)) System.IO.Directory.Delete(tmpDir, true);
            }
        }
    }
}
