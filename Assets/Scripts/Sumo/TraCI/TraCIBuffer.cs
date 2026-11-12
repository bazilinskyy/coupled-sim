using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace CoupledSim.TraCI
{
    /// <summary>Big-endian writer for TraCI messages.</summary>
    public sealed class TraCIWriter
    {
        readonly List<byte> _bytes = new List<byte>(64);
        public int Length => _bytes.Count;
        public byte[] ToArray() => _bytes.ToArray();

        public TraCIWriter UByte(byte v) { _bytes.Add(v); return this; }
        public TraCIWriter Int(int v)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(b, v);
            for (int i = 0; i < 4; i++) _bytes.Add(b[i]);
            return this;
        }
        public TraCIWriter Double(double v)
        {
            Span<byte> b = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(b, BitConverter.DoubleToInt64Bits(v));
            for (int i = 0; i < 8; i++) _bytes.Add(b[i]);
            return this;
        }
        public TraCIWriter String(string s)
        {
            var b = Encoding.UTF8.GetBytes(s ?? "");
            Int(b.Length);
            _bytes.AddRange(b);
            return this;
        }
        public TraCIWriter StringList(IList<string> list)
        {
            Int(list.Count);
            foreach (var s in list) String(s);
            return this;
        }
        public TraCIWriter Bytes(byte[] b) { _bytes.AddRange(b); return this; }

        // Typed values: type byte followed by the value
        public TraCIWriter TypedInt(int v) => UByte(TraCIConstants.TYPE_INTEGER).Int(v);
        public TraCIWriter TypedDouble(double v) => UByte(TraCIConstants.TYPE_DOUBLE).Double(v);
        public TraCIWriter TypedString(string v) => UByte(TraCIConstants.TYPE_STRING).String(v);
        public TraCIWriter TypedByte(sbyte v) => UByte(TraCIConstants.TYPE_BYTE).UByte(unchecked((byte)v));
        public TraCIWriter TypedUByte(byte v) => UByte(TraCIConstants.TYPE_UBYTE).UByte(v);
        public TraCIWriter TypedStringList(IList<string> v) => UByte(TraCIConstants.TYPE_STRINGLIST).StringList(v);
        public TraCIWriter Compound(int count) => UByte(TraCIConstants.TYPE_COMPOUND).Int(count);
        public TraCIWriter TypedColor(byte r, byte g, byte b, byte a) =>
            UByte(TraCIConstants.TYPE_COLOR).UByte(r).UByte(g).UByte(b).UByte(a);
    }

    /// <summary>Big-endian reader for TraCI responses.</summary>
    public sealed class TraCIReader
    {
        readonly byte[] _data;
        int _pos;
        public TraCIReader(byte[] data, int start = 0) { _data = data; _pos = start; }
        public int Position { get => _pos; set => _pos = value; }
        public bool AtEnd => _pos >= _data.Length;
        public int Remaining => _data.Length - _pos;

        void Need(int n)
        {
            if (_pos + n > _data.Length)
                throw new TraCIException($"TraCI response truncated: need {n} bytes at {_pos}, have {_data.Length - _pos}");
        }
        public byte UByte() { Need(1); return _data[_pos++]; }
        public sbyte Byte() => unchecked((sbyte)UByte());
        public int Int()
        {
            Need(4);
            var v = BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(_data, _pos, 4));
            _pos += 4;
            return v;
        }
        public double Double()
        {
            Need(8);
            var v = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(_data, _pos, 8)));
            _pos += 8;
            return v;
        }
        public string String()
        {
            int n = Int();
            Need(n);
            var s = Encoding.UTF8.GetString(_data, _pos, n);
            _pos += n;
            return s;
        }
        public List<string> StringList()
        {
            int n = Int();
            var list = new List<string>(n);
            for (int i = 0; i < n; i++) list.Add(String());
            return list;
        }
        /// <summary>Length prefix of a command: one byte, or 0 followed by an int.</summary>
        public int CommandLength()
        {
            int len = UByte();
            if (len == 0) return Int() - 5; // the 1+4 header bytes count in the extended length
            return len - 1;
        }

        /// <summary>Reads a typed value (type byte first) into a boxed .NET value.</summary>
        public object Typed()
        {
            byte type = UByte();
            switch (type)
            {
                case TraCIConstants.TYPE_UBYTE: return UByte();
                case TraCIConstants.TYPE_BYTE: return Byte();
                case TraCIConstants.TYPE_INTEGER: return Int();
                case TraCIConstants.TYPE_DOUBLE: return Double();
                case TraCIConstants.TYPE_STRING: return String();
                case TraCIConstants.TYPE_STRINGLIST: return StringList();
                case TraCIConstants.POSITION_2D:
                case TraCIConstants.POSITION_LON_LAT:
                    return new TraCIPosition(Double(), Double(), 0);
                case TraCIConstants.POSITION_3D:
                case TraCIConstants.POSITION_LON_LAT_ALT:
                    return new TraCIPosition(Double(), Double(), Double());
                case TraCIConstants.POSITION_ROADMAP:
                    return new TraCIRoadPosition(String(), Double(), UByte());
                case TraCIConstants.TYPE_BOUNDINGBOX:
                    return new[] { Double(), Double(), Double(), Double() };
                case TraCIConstants.TYPE_POLYGON:
                {
                    int n = UByte();
                    if (n == 0) n = Int();
                    var pts = new TraCIPosition[n];
                    for (int i = 0; i < n; i++) pts[i] = new TraCIPosition(Double(), Double(), 0);
                    return pts;
                }
                case TraCIConstants.TYPE_COLOR:
                    return new TraCIColor(UByte(), UByte(), UByte(), UByte());
                case TraCIConstants.TYPE_DOUBLELIST:
                {
                    int n = Int();
                    var d = new double[n];
                    for (int i = 0; i < n; i++) d[i] = Double();
                    return d;
                }
                case TraCIConstants.TYPE_COMPOUND:
                {
                    int n = Int();
                    var items = new object[n];
                    for (int i = 0; i < n; i++) items[i] = Typed();
                    return items;
                }
                default:
                    throw new TraCIException($"Unknown TraCI data type 0x{type:x2}");
            }
        }
    }

    public readonly struct TraCIPosition
    {
        public readonly double X, Y, Z;
        public TraCIPosition(double x, double y, double z) { X = x; Y = y; Z = z; }
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    public readonly struct TraCIRoadPosition
    {
        public readonly string EdgeId;
        public readonly double Pos;
        public readonly byte LaneIndex;
        public TraCIRoadPosition(string edge, double pos, byte lane) { EdgeId = edge; Pos = pos; LaneIndex = lane; }
    }

    public readonly struct TraCIColor
    {
        public readonly byte R, G, B, A;
        public TraCIColor(byte r, byte g, byte b, byte a) { R = r; G = g; B = b; A = a; }
    }

    public class TraCIException : Exception
    {
        public TraCIException(string msg) : base(msg) { }
    }
}
