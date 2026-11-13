using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws traffic sign pictures into textures (shapes and a small digit/letter font).</summary>
public static class SignPainter
{
    const int N = 256;
    static readonly Color Red = new Color(0.78f, 0.06f, 0.08f), White = new Color(0.96f, 0.96f, 0.95f), Black = new Color(0.08f, 0.08f, 0.08f),
        Blue = new Color(0.02f, 0.32f, 0.66f), Yellow = new Color(0.98f, 0.76f, 0.05f), Grey = new Color(0.58f, 0.59f, 0.6f);

    // signed distances in units of the half-size (-1..1 square), negative inside
    static float Circle(Vector2 p, float r) => p.magnitude - r;
    static float Box(Vector2 p, Vector2 b) { var d = new Vector2(Mathf.Abs(p.x) - b.x, Mathf.Abs(p.y) - b.y); return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f); }
    static float Polygon(Vector2 p, int n, float r, float rot)
    {
        // regular polygon with circumradius r; rot = direction of an edge's outward normal
        // (-π/2: a flat edge at the bottom, so a triangle points up)
        float a = Mathf.Atan2(p.y, p.x) - rot, seg = 2f * Mathf.PI / n;
        a = Mathf.Repeat(a + seg / 2f, seg) - seg / 2f;
        return p.magnitude * Mathf.Cos(a) - r * Mathf.Cos(seg / 2f);
    }
    static float Segment(Vector2 p, Vector2 a, Vector2 b, float w)
    {
        var pa = p - a; var ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude - w;
    }

    // 5x7 glyphs
    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['0'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
        ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
        ['3'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
        ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" },
        ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
        ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
    };

    // distance to text centred at c with glyph cell height h
    static float Text(Vector2 p, string s, Vector2 c, float h)
    {
        float cell = h / 7f, gw = cell * 5f, gap = cell * 1.2f;
        float total = s.Length * gw + (s.Length - 1) * gap;
        float best = 1e9f;
        for (int k = 0; k < s.Length; k++)
        {
            if (!Glyphs.TryGetValue(s[k], out var g)) continue;
            float x0 = c.x - total / 2f + k * (gw + gap);
            float y0 = c.y + h / 2f;
            // rounded strokes: each lit cell is a small rounded box
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 5; col++)
                    if (g[row][col] == '1')
                    {
                        var cc = new Vector2(x0 + (col + 0.5f) * cell, y0 - (row + 0.5f) * cell);
                        // neighbouring cells overlap, so strokes join; slightly rounded corners
                        best = Mathf.Min(best, Box(p - cc, new Vector2(cell * 0.42f, cell * 0.42f)) - cell * 0.12f);
                    }
        }
        return best;
    }

    static float Person(Vector2 p, Vector2 c, float s)
    {
        // a walking person, about 2s tall
        float d = Circle(p - (c + new Vector2(0.05f, 0.78f) * s), 0.16f * s);
        d = Mathf.Min(d, Segment(p, c + new Vector2(0.02f, 0.55f) * s, c + new Vector2(-0.05f, 0.0f) * s, 0.1f * s));
        d = Mathf.Min(d, Segment(p, c + new Vector2(-0.05f, 0.0f) * s, c + new Vector2(-0.35f, -0.85f) * s, 0.09f * s));
        d = Mathf.Min(d, Segment(p, c + new Vector2(-0.05f, 0.0f) * s, c + new Vector2(0.3f, -0.5f) * s, 0.09f * s));
        d = Mathf.Min(d, Segment(p, c + new Vector2(0.3f, -0.5f) * s, c + new Vector2(0.35f, -0.88f) * s, 0.08f * s));
        d = Mathf.Min(d, Segment(p, c + new Vector2(0.02f, 0.5f) * s, c + new Vector2(-0.35f, 0.15f) * s, 0.07f * s));
        d = Mathf.Min(d, Segment(p, c + new Vector2(0.02f, 0.5f) * s, c + new Vector2(0.38f, 0.22f) * s, 0.07f * s));
        return d;
    }

    struct Layer { public Func<Vector2, float> D; public Color C; }

    public static Texture2D Paint(TrafficSignKind kind, int limit, bool back)
    {
        var layers = new List<Layer>();
        void L(Func<Vector2, float> d, Color c) => layers.Add(new Layer { D = d, C = c });
        Func<Vector2, float> outline;
        switch (kind)
        {
            case TrafficSignKind.Stop:
                outline = p => Polygon(p, 8, 0.98f, 0f);
                L(outline, White);
                L(p => Polygon(p, 8, 0.9f, 0f), Red);
                L(p => Text(p, "STOP", Vector2.zero, 0.42f), White);
                break;
            case TrafficSignKind.GiveWay:
                // pointing down: flat edge at the top
                outline = p => Polygon(p - new Vector2(0, 0.22f), 3, 1.0f, Mathf.PI / 2f);
                L(outline, Red);
                L(p => Polygon(p - new Vector2(0, 0.22f), 3, 0.66f, Mathf.PI / 2f), White);
                break;
            case TrafficSignKind.SpeedLimit:
                outline = p => Circle(p, 0.98f);
                L(outline, Red);
                L(p => Circle(p, 0.78f), White);
                L(p => Text(p, Mathf.Clamp(limit, 0, 999).ToString(), Vector2.zero, limit >= 100 ? 0.5f : 0.62f), Black);
                break;
            case TrafficSignKind.PedestrianCrossing:
                outline = p => Box(p, new Vector2(0.9f, 0.9f)) - 0.08f;
                L(outline, White);
                L(p => Box(p, new Vector2(0.86f, 0.86f)) - 0.04f, Blue);
                L(p => Polygon(p + new Vector2(0, 0.12f), 3, 0.8f, -Mathf.PI / 2f), White);
                L(p => Mathf.Max(Box(p + new Vector2(0, 0.42f), new Vector2(0.5f, 0.1f)), -Mathf.Repeat(p.x + 0.05f, 0.2f) + 0.1f), Black);
                L(p => Person(p, new Vector2(0f, -0.02f), 0.33f), Black);
                break;
            case TrafficSignKind.NoEntry:
                outline = p => Circle(p, 0.98f);
                L(outline, Red);
                L(p => Box(p, new Vector2(0.7f, 0.17f)), White);
                break;
            case TrafficSignKind.OneWay:
                outline = p => Box(p, new Vector2(0.97f, 0.97f)) - 0.02f;
                L(outline, White);
                L(p => Box(p, new Vector2(0.92f, 0.92f)), Blue);
                // one-way road: arrow pointing ahead
                L(p => Mathf.Min(Box(p + new Vector2(0f, 0.2f), new Vector2(0.16f, 0.5f)), Polygon(p - new Vector2(0f, 0.38f), 3, 0.42f, -Mathf.PI / 2f)), White);
                break;
            case TrafficSignKind.PriorityRoad:
                // a diamond: corners at the top and bottom
                outline = p => Polygon(p, 4, 0.98f, Mathf.PI / 4f);
                L(outline, White);
                L(p => Polygon(p, 4, 0.8f, Mathf.PI / 4f), Black);
                L(p => Polygon(p, 4, 0.74f, Mathf.PI / 4f), White);
                L(p => Polygon(p, 4, 0.58f, Mathf.PI / 4f), Yellow);
                break;
            default: // Children: warning triangle with two people
                // warning triangle pointing up
                outline = p => Polygon(p + new Vector2(0, 0.22f), 3, 1.0f, -Mathf.PI / 2f);
                L(outline, Red);
                L(p => Polygon(p + new Vector2(0, 0.22f), 3, 0.78f, -Mathf.PI / 2f), White);
                L(p => Mathf.Min(Person(p, new Vector2(-0.16f, -0.18f), 0.3f), Person(p, new Vector2(0.18f, -0.26f), 0.24f)), Black);
                break;
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = (back ? "back " : "") + kind + " " + limit, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[N * N];
        float aa = 2.5f / N;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
                float inside = Mathf.Clamp01(0.5f - outline(p) / aa);
                Color c = back ? Grey : Color.clear;
                if (!back)
                    foreach (var l in layers)
                    {
                        float a = Mathf.Clamp01(0.5f - l.D(p) / aa);
                        if (a > 0f) c = Color.Lerp(c, l.C, a);
                    }
                c.a = inside;
                px[y * N + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true, false);
        return tex;
    }
}
