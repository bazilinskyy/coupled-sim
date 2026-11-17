using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CoupledSim.Experiments
{
    /// <summary>
    /// The part of YAML that experiment files need, without a library: block mappings and sequences (by indentation),
    /// flow collections on one line (<c>[a, b]</c>, <c>{a: 1}</c>), plain, 'single' and "double" quoted scalars and
    /// # comments. Mappings become <see cref="Dictionary{TKey,TValue}"/> (string keys, in file order), sequences
    /// <see cref="List{T}"/> of object, scalars strings (null for an empty value, ~ or null). Not supported: anchors,
    /// tags, block scalars (| and &gt;) and flow collections over several lines.
    /// </summary>
    public static class MiniYaml
    {
        public class YamlException : Exception
        {
            public YamlException(int line, string message) : base($"line {line}: {message}") { }
        }

        struct Line { public int Number, Indent; public string Text; }

        public static object Parse(string text)
        {
            var lines = new List<Line>();
            var raw = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                var l = raw[i];
                if (l.Contains("\t") && l.TrimStart(' ').StartsWith("\t")) throw new YamlException(i + 1, "tabs cannot indent YAML; use spaces");
                var content = StripComment(l).TrimEnd();
                if (content.Trim().Length == 0 || content.Trim() == "---" || content.Trim() == "...") continue;
                int indent = 0;
                while (indent < content.Length && content[indent] == ' ') indent++;
                lines.Add(new Line { Number = i + 1, Indent = indent, Text = content.Substring(indent) });
            }
            if (lines.Count == 0) return null;
            int at = 0;
            var result = ParseBlock(lines, ref at, lines[0].Indent);
            if (at < lines.Count) throw new YamlException(lines[at].Number, "unexpected indentation");
            return result;
        }

        static object ParseBlock(List<Line> lines, ref int at, int indent)
        {
            return IsSeqItem(lines[at].Text) ? ParseSeq(lines, ref at, indent) : (object)ParseMap(lines, ref at, indent);
        }

        static bool IsSeqItem(string t) => t == "-" || t.StartsWith("- ");

        static List<object> ParseSeq(List<Line> lines, ref int at, int indent)
        {
            var list = new List<object>();
            while (at < lines.Count && lines[at].Indent == indent && IsSeqItem(lines[at].Text))
            {
                var line = lines[at];
                var item = line.Text.Length > 1 ? line.Text.Substring(1).TrimStart() : "";
                if (item.Length == 0)
                {
                    at++;
                    list.Add(at < lines.Count && lines[at].Indent > indent ? ParseBlock(lines, ref at, lines[at].Indent) : null);
                    continue;
                }
                if (KeyColon(item) > 0 || IsSeqItem(item))
                {
                    // "- key: value" (and the item's further keys below it), or "- - a": a block that starts on this line
                    int inner = indent + (line.Text.Length - item.Length);
                    lines[at] = new Line { Number = line.Number, Indent = inner, Text = item };
                    list.Add(ParseBlock(lines, ref at, inner));
                    continue;
                }
                list.Add(Inline(item, line.Number));
                at++;
            }
            return list;
        }

        static Dictionary<string, object> ParseMap(List<Line> lines, ref int at, int indent)
        {
            var map = new Dictionary<string, object>();
            while (at < lines.Count && lines[at].Indent == indent && !IsSeqItem(lines[at].Text))
            {
                var line = lines[at];
                int colon = KeyColon(line.Text);
                if (colon <= 0) throw new YamlException(line.Number, $"expected 'key: value', got '{line.Text}'");
                var key = Unquote(line.Text.Substring(0, colon).Trim(), line.Number);
                var rest = line.Text.Substring(colon + 1).Trim();
                if (map.ContainsKey(key)) throw new YamlException(line.Number, $"'{key}' is given twice");
                at++;
                if (rest.Length > 0) map[key] = Inline(rest, line.Number);
                else if (at < lines.Count && lines[at].Indent > indent) map[key] = ParseBlock(lines, ref at, lines[at].Indent);
                else if (at < lines.Count && lines[at].Indent == indent && IsSeqItem(lines[at].Text)) map[key] = ParseSeq(lines, ref at, indent);
                else map[key] = null;
            }
            if (at < lines.Count && lines[at].Indent > indent) throw new YamlException(lines[at].Number, "unexpected indentation");
            return map;
        }

        /// <summary>The position of the colon that ends a mapping key (followed by a space or the line's end), or -1.</summary>
        static int KeyColon(string t)
        {
            if (t.Length == 0 || t[0] == '[' || t[0] == '{') return -1;
            char quote = '\0';
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (quote != '\0') { if (c == quote && !(quote == '"' && i > 0 && t[i - 1] == '\\')) quote = '\0'; continue; }
                if ((c == '"' || c == '\'') && i == 0) { quote = c; continue; }
                if (c == ':' && (i + 1 == t.Length || t[i + 1] == ' ')) return i;
            }
            return -1;
        }

        static string StripComment(string l)
        {
            char quote = '\0';
            for (int i = 0; i < l.Length; i++)
            {
                char c = l[i];
                if (quote != '\0') { if (c == quote && !(quote == '"' && l[i - 1] == '\\')) quote = '\0'; continue; }
                if ((c == '"' || c == '\'') && (i == 0 || " :[{,-".IndexOf(l[i - 1]) >= 0)) { quote = c; continue; }
                if (c == '#' && (i == 0 || l[i - 1] == ' ')) return l.Substring(0, i);
            }
            return l;
        }

        static object Inline(string text, int line)
        {
            text = text.Trim();
            if (text.StartsWith("[") || text.StartsWith("{"))
            {
                int pos = 0;
                var v = Flow(text, ref pos, line);
                SkipSpaces(text, ref pos);
                if (pos != text.Length) throw new YamlException(line, $"unexpected '{text.Substring(pos)}' after a flow collection");
                return v;
            }
            if (text.StartsWith("|") || text.StartsWith(">")) throw new YamlException(line, "block scalars (| and >) are not supported; quote the text on one line");
            if (text.StartsWith("&") || text.StartsWith("*") || text.StartsWith("!")) throw new YamlException(line, "anchors, aliases and tags are not supported");
            return Scalar(text, line);
        }

        static object Scalar(string text, int line)
        {
            if (text.Length >= 2 && (text[0] == '"' || text[0] == '\'')) return Unquote(text, line);
            if (text == "~" || text == "null" || text == "Null" || text == "NULL") return null;
            return text;
        }

        static string Unquote(string text, int line)
        {
            if (text.Length < 2 || (text[0] != '"' && text[0] != '\'')) return text;
            char q = text[0];
            if (text[text.Length - 1] != q) throw new YamlException(line, $"unclosed quote in {text}");
            var inner = text.Substring(1, text.Length - 2);
            if (q == '\'') return inner.Replace("''", "'");
            var sb = new StringBuilder();
            for (int i = 0; i < inner.Length; i++)
            {
                if (inner[i] != '\\' || i + 1 == inner.Length) { sb.Append(inner[i]); continue; }
                char e = inner[++i];
                sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
            }
            return sb.ToString();
        }

        static void SkipSpaces(string t, ref int pos) { while (pos < t.Length && t[pos] == ' ') pos++; }

        static object Flow(string t, ref int pos, int line)
        {
            SkipSpaces(t, ref pos);
            if (pos >= t.Length) throw new YamlException(line, "unfinished flow collection");
            char open = t[pos];
            if (open == '[')
            {
                pos++;
                var list = new List<object>();
                SkipSpaces(t, ref pos);
                if (pos < t.Length && t[pos] == ']') { pos++; return list; }
                while (true)
                {
                    list.Add(FlowValue(t, ref pos, line, "],"));
                    SkipSpaces(t, ref pos);
                    if (pos >= t.Length) throw new YamlException(line, "missing ]");
                    if (t[pos] == ',') { pos++; continue; }
                    if (t[pos] == ']') { pos++; return list; }
                    throw new YamlException(line, $"expected , or ] at '{t.Substring(pos)}'");
                }
            }
            if (open == '{')
            {
                pos++;
                var map = new Dictionary<string, object>();
                SkipSpaces(t, ref pos);
                if (pos < t.Length && t[pos] == '}') { pos++; return map; }
                while (true)
                {
                    SkipSpaces(t, ref pos);
                    int start = pos;
                    if (pos < t.Length && (t[pos] == '"' || t[pos] == '\'')) { char q = t[pos++]; while (pos < t.Length && t[pos] != q) pos++; pos++; }
                    else while (pos < t.Length && !(t[pos] == ':' && (pos + 1 == t.Length || t[pos + 1] == ' ' || t[pos + 1] == ',' || t[pos + 1] == '}')) && t[pos] != ',' && t[pos] != '}') pos++;
                    if (pos > t.Length) throw new YamlException(line, "unclosed quote");
                    var key = Unquote(t.Substring(start, pos - start).Trim(), line);
                    SkipSpaces(t, ref pos);
                    object value = null;
                    if (pos < t.Length && t[pos] == ':') { pos++; value = FlowValue(t, ref pos, line, "},"); }
                    if (map.ContainsKey(key)) throw new YamlException(line, $"'{key}' is given twice");
                    map[key] = value;
                    SkipSpaces(t, ref pos);
                    if (pos >= t.Length) throw new YamlException(line, "missing }");
                    if (t[pos] == ',') { pos++; continue; }
                    if (t[pos] == '}') { pos++; return map; }
                    throw new YamlException(line, $"expected , or }} at '{t.Substring(pos)}'");
                }
            }
            throw new YamlException(line, "expected [ or {");
        }

        static object FlowValue(string t, ref int pos, int line, string ends)
        {
            SkipSpaces(t, ref pos);
            if (pos < t.Length && (t[pos] == '[' || t[pos] == '{')) return Flow(t, ref pos, line);
            int start = pos;
            if (pos < t.Length && (t[pos] == '"' || t[pos] == '\''))
            {
                char q = t[pos++];
                while (pos < t.Length && !(t[pos] == q && !(q == '"' && t[pos - 1] == '\\'))) pos++;
                if (pos >= t.Length) throw new YamlException(line, "unclosed quote");
                pos++;
                return Unquote(t.Substring(start, pos - start), line);
            }
            while (pos < t.Length && ends.IndexOf(t[pos]) < 0) pos++;
            var s = t.Substring(start, pos - start).Trim();
            return s.Length == 0 ? null : Scalar(s, line);
        }

        // ---------------------------------------------------------------- writing (for the merged configuration of a trial)

        /// <summary>Writes a tree of mappings, sequences and scalars as block YAML.</summary>
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0, false);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v, int indent, bool inline)
        {
            var pad = new string(' ', indent);
            if (v is Dictionary<string, object> map)
            {
                if (map.Count == 0) { sb.Append(inline ? " {}\n" : "{}\n"); return; }
                if (inline) sb.Append('\n');
                foreach (var kv in map)
                {
                    sb.Append(pad).Append(QuoteIfNeeded(kv.Key)).Append(':');
                    if (kv.Value is Dictionary<string, object> || kv.Value is List<object>) WriteValue(sb, kv.Value, indent + 2, true);
                    else sb.Append(' ').Append(ScalarText(kv.Value)).Append('\n');
                }
                return;
            }
            if (v is List<object> list)
            {
                if (list.Count == 0) { sb.Append(inline ? " []\n" : "[]\n"); return; }
                if (inline) sb.Append('\n');
                foreach (var item in list)
                {
                    sb.Append(pad).Append("- ");
                    if (item is Dictionary<string, object> m && m.Count > 0)
                    {
                        // the first key on the dash's line, the others below it
                        var inner = new StringBuilder();
                        WriteValue(inner, item, indent + 2, false);
                        sb.Append(inner.ToString().Substring(indent + 2));
                    }
                    else if (item is List<object>) { sb.Append('\n'); WriteValue(sb, item, indent + 2, false); }
                    else sb.Append(ScalarText(item)).Append('\n');
                }
                return;
            }
            sb.Append(inline ? " " : "").Append(ScalarText(v)).Append('\n');
        }

        static string ScalarText(object v) => v == null ? "null" : QuoteIfNeeded(Convert.ToString(v, CultureInfo.InvariantCulture));

        static string QuoteIfNeeded(string s)
        {
            if (s.Length == 0) return "\"\"";
            bool plain = s.Trim() == s && s.IndexOfAny(new[] { '#', '"', '\'', '[', ']', '{', '}', ',', '\n', '\t' }) < 0 && !s.Contains(": ") && !s.EndsWith(":")
                && !s.StartsWith("- ") && s != "-" && "&*!|>%@`~".IndexOf(s[0]) < 0 && s != "null";
            return plain ? s : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
        }
    }
}
