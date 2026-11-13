using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CoupledSim.Experiments
{
    /// <summary>How the blocks of a trial sequence are ordered per participant.</summary>
    public enum TrialOrder
    {
        /// <summary>As in the file.</summary>
        None,
        /// <summary>Blocks in a balanced Latin square (Williams design) row chosen by the participant number.</summary>
        Latin,
        /// <summary>Blocks, and trials within blocks, shuffled with the participant number as seed.</summary>
        Random,
    }

    /// <summary>
    /// A trial sequence as a table: one row per trial with named columns (experiment, role, block,
    /// parameters, ...). Reads CSV (comma, semicolon or tab separated, quoted fields, # comments,
    /// "#order=latin" directive) and orders the rows for a participant (counterbalancing).
    /// </summary>
    public class TrialTable
    {
        public readonly List<string> Columns = new List<string>();
        public readonly List<Dictionary<string, string>> Rows = new List<Dictionary<string, string>>();
        /// <summary>From an "#order=..." line; null when the file does not say.</summary>
        public TrialOrder? Order;

        public string Get(int row, string column, string fallback = "")
        {
            return Rows[row].TryGetValue(column, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;
        }

        public static TrialTable ParseCsv(string text)
        {
            var table = new TrialTable();
            var lines = SplitRecords(text);
            char sep = ',';
            bool header = true;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#"))
                {
                    var d = line.TrimStart('#').Trim().Replace(" ", "");
                    if (d.StartsWith("order=", StringComparison.OrdinalIgnoreCase) && TryParseOrder(d.Substring(6), out var o)) table.Order = o;
                    continue;
                }
                if (header)
                {
                    sep = line.Count(c => c == ';') > line.Count(c => c == ',') ? ';' : line.Contains('\t') && !line.Contains(',') ? '\t' : ',';
                    foreach (var c in SplitFields(line, sep)) table.Columns.Add(c.Trim());
                    header = false;
                    continue;
                }
                var fields = SplitFields(line, sep);
                if (fields.All(f => f.Trim().Length == 0)) continue;
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < table.Columns.Count; i++)
                    row[table.Columns[i]] = i < fields.Count ? fields[i].Trim() : "";
                table.Rows.Add(row);
            }
            return table;
        }

        public static bool TryParseOrder(string s, out TrialOrder order)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "": case "none": case "fixed": order = TrialOrder.None; return true;
                case "latin": case "latinsquare": case "williams": case "counterbalance": order = TrialOrder.Latin; return true;
                case "random": case "shuffle": order = TrialOrder.Random; return true;
                default: order = TrialOrder.None; return false;
            }
        }

        // records separated by newlines outside quotes
        static List<string> SplitRecords(string text)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            foreach (var c in text ?? "")
            {
                if (c == '"') quoted = !quoted;
                if ((c == '\n' || c == '\r') && !quoted)
                {
                    if (sb.Length > 0) result.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        static List<string> SplitFields(string line, char sep)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == sep) { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            fields.Add(sb.ToString());
            return fields;
        }

        public string ToCsv()
        {
            string Quote(string s) => s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            var sb = new StringBuilder();
            if (Order != null) sb.Append("#order=").Append(Order.Value.ToString().ToLowerInvariant()).Append('\n');
            sb.Append(string.Join(",", Columns.Select(Quote))).Append('\n');
            foreach (var row in Rows)
                sb.Append(string.Join(",", Columns.Select(c => Quote(row.TryGetValue(c, out var v) ? v : "")))).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// The order of the rows for a participant (1-based; 0 = as in the file). Rows with an empty
        /// "block" before the first block (e.g. practice) stay first; the other rows without a block
        /// come last; the blocks are ordered as <paramref name="order"/> says, the trials within a
        /// block keep their order (shuffled with <see cref="TrialOrder.Random"/>).
        /// </summary>
        public int[] OrderFor(int participant, TrialOrder order, string blockColumn = "block")
        {
            var lead = new List<int>();
            var tail = new List<int>();
            var blocks = new List<string>();
            var members = new Dictionary<string, List<int>>();
            for (int i = 0; i < Rows.Count; i++)
            {
                var b = Get(i, blockColumn);
                if (b.Length == 0) { (blocks.Count == 0 ? lead : tail).Add(i); continue; }
                if (!members.TryGetValue(b, out var list)) { members[b] = list = new List<int>(); blocks.Add(b); }
                list.Add(i);
            }
            if (participant <= 0 || order == TrialOrder.None) return Enumerable.Range(0, Rows.Count).ToArray();
            var rng = new Random(participant * 7919 + 17);
            int[] blockOrder;
            if (order == TrialOrder.Latin) blockOrder = Counterbalance.WilliamsRow(blocks.Count, participant - 1);
            else blockOrder = Counterbalance.Shuffle(Enumerable.Range(0, blocks.Count).ToArray(), rng);
            var result = new List<int>(lead);
            foreach (var bi in blockOrder)
            {
                var list = members[blocks[bi]].ToArray();
                result.AddRange(order == TrialOrder.Random ? Counterbalance.Shuffle(list, rng) : list);
            }
            result.AddRange(tail);
            return result.ToArray();
        }

        public static int ParseInt(string s, int fallback)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        public static float ParseFloat(string s, float fallback)
        {
            return float.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }
    }

    public static class Counterbalance
    {
        /// <summary>
        /// Row <paramref name="row"/> of a balanced Latin square (Williams, 1949) for n conditions:
        /// every condition once per row and in each position once over n rows; each condition
        /// follows each other one equally often (once over n rows for even n; for odd n the
        /// square is doubled with mirrored rows, 2n rows).
        /// </summary>
        public static int[] WilliamsRow(int n, int row)
        {
            if (n <= 0) return new int[0];
            int rows = n % 2 == 0 ? n : 2 * n;
            row = ((row % rows) + rows) % rows;
            var offsets = new int[n];
            // 0, 1, n-1, 2, n-2, ...
            for (int j = 0, lo = 1, hi = n - 1; j < n; j++)
                offsets[j] = j == 0 ? 0 : j % 2 == 1 ? lo++ : hi--;
            var result = new int[n];
            int start = row % n;
            for (int j = 0; j < n; j++) result[j] = (start + offsets[j]) % n;
            if (row >= n) Array.Reverse(result);
            return result;
        }

        public static T[] Shuffle<T>(T[] items, Random rng)
        {
            var a = (T[])items.Clone();
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = a[i]; a[i] = a[j]; a[j] = t;
            }
            return a;
        }
    }
}
