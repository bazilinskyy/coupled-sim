#!/usr/bin/env python3
"""
Tree crowns from Meta and WRI's global 1 m canopy height map (Tolan et al. 2024, CC BY 4.0), for the trees no
register lists: city registers hold the trees the city manages, so a university campus, gardens, courtyards and
company grounds come out bare (Eindhoven's register has none on the TU/e campus, OSM one). As in coupled-carla
(coupledcarla/canopy.py): the tallest pixel of every 5 m square where the canopy reaches 3 m is a crown, and a tree
stands there when no tree within 6 m is already placed; its height from the canopy, calibrated against Eindhoven's
register (6.0 m + 1.1 x the canopy height, at most 30 m: the map sees urban trees too low).

The map is a set of cloud-optimised GeoTIFFs (BigTIFF, uint8 metres, deflate strips, Web Mercator) on AWS open data,
one per zoom-9 quadkey; only the strips over the map's rows are read (HTTP range requests) and the crop is cached in
Tools/.cache/canopy-<map>.npz (not committed). Needs numpy.

  python3 Tools/city_data.py Eindhoven --layers trees      # the register's trees and the canopy's crowns
"""
import math, os, struct, sys, time, urllib.request, zlib

CHM = "https://dataforgood-fb-data.s3.amazonaws.com/forests/v1/alsgedi_global_v6_float/chm/{qk}.tif"
CREDIT = "Canopy height map, Meta and World Resources Institute (Tolan et al. 2024, CC BY 4.0)"
ZOOM = 9
SEEN_M = 3.0
CALIBRATION = (6.0, 1.1)
MAX_M = 30.0
R = 6378137.0
HALF = math.pi * R
UA = {"User-Agent": "coupled-sim-city-data/1.0 (open-source driving simulator)"}
CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".cache")


def mercator(lat, lon):
    return R * math.radians(lon), R * math.log(math.tan(math.pi / 4 + math.radians(lat) / 2))


def latlon(x, y):
    return math.degrees(2 * math.atan(math.exp(y / R)) - math.pi / 2), math.degrees(x / R)


def quadkey(lat, lon, z=ZOOM):
    x, y = mercator(lat, lon)
    n = 2 ** z
    tx = min(n - 1, int((x + HALF) / (2 * HALF) * n)); ty = min(n - 1, int((HALF - y) / (2 * HALF) * n))
    return "".join(str(((tx >> i) & 1) + 2 * ((ty >> i) & 1)) for i in range(z - 1, -1, -1))


def _get(url, a, b):
    req = urllib.request.Request(url, headers={"Range": "bytes=%d-%d" % (a, b - 1), **UA})
    for attempt in range(4):
        try:
            return urllib.request.urlopen(req, timeout=120).read()
        except Exception:
            if attempt == 3: raise
            time.sleep(2 + 4 * attempt)


TYPES = {1: ("B", 1), 2: ("c", 1), 3: ("H", 2), 4: ("I", 4), 5: ("II", 8), 11: ("f", 4), 12: ("d", 8), 16: ("Q", 8), 17: ("q", 8)}


def _ifd(url):
    """The first image's tags of a (Big)TIFF: {tag: tuple of values}."""
    head = _get(url, 0, 1 << 16)
    bo = "<" if head[:2] == b"II" else ">"
    big = struct.unpack(bo + "H", head[2:4])[0] == 43
    off = struct.unpack(bo + ("Q" if big else "I"), head[8:16] if big else head[4:8])[0]
    def read(a, n):
        nonlocal head
        if a + n > len(head): head += _get(url, len(head), max(a + n, len(head) * 2))
        return head[a:a + n]
    n = struct.unpack(bo + ("Q" if big else "H"), read(off, 8 if big else 2))[0]
    tags, p = {}, off + (8 if big else 2)
    for _ in range(n):
        e = read(p, 20 if big else 12)
        tag, typ = struct.unpack(bo + "HH", e[:4])
        count = struct.unpack(bo + ("Q" if big else "I"), e[4:12] if big else e[4:8])[0]
        fmt, size = TYPES.get(typ, ("B", 1))
        total = size * count
        inline = 8 if big else 4
        data = (e[12:12 + total] if big else e[8:8 + total]) if total <= inline else read(struct.unpack(bo + ("Q" if big else "I"), e[12:20] if big else e[8:12])[0], total)
        if typ == 2: tags[tag] = (data.rstrip(b"\0").decode("latin-1"),)
        elif typ == 5: tags[tag] = tuple(a / b for a, b in zip(*[iter(struct.unpack(bo + "I" * 2 * count, data))] * 2))
        else: tags[tag] = struct.unpack(bo + fmt[-1] * count, data)
        p += 20 if big else 12
    return tags


def _window(url, x0, y0, x1, y1, budget_mb):
    """The canopy heights of one file inside the Mercator box: (array, left, top, res), or None outside it."""
    import numpy as np
    t = _ifd(url)
    width, height = t[256][0], t[257][0]
    res = t[33550][0]; left, top = t[33922][3], t[33922][4]
    rps = t.get(278, (height,))[0]; offsets, counts = t[273], t[279]
    predictor = t.get(317, (1,))[0]
    if 322 in t: raise ValueError("tiled file: not handled")
    c0, c1 = max(0, int((x0 - left) / res)), min(width, int(math.ceil((x1 - left) / res)))
    r0, r1 = max(0, int((top - y1) / res)), min(height, int(math.ceil((top - y0) / res)))
    if c1 <= c0 or r1 <= r0: return None
    s0, s1 = r0 // rps, (r1 - 1) // rps + 1
    mb = sum(counts[s0:s1]) / 1e6
    print("  canopy: %s rows %d-%d (%d strips, %.0f MB)" % (url.rsplit("/", 1)[-1], r0, r1, s1 - s0, mb), flush=True)
    if mb > budget_mb: raise ValueError("%.0f MB to read, over the budget of %.0f MB (--canopy-mb)" % (mb, budget_mb))
    out = np.zeros(((s1 - s0) * rps, c1 - c0), dtype=np.uint8)
    i = s0
    while i < s1:
        j, a = i, offsets[i]
        while j + 1 < s1 and offsets[j + 1] == offsets[j] + counts[j] and offsets[j + 1] - a < 8 << 20: j += 1
        blob = _get(url, a, offsets[j] + counts[j])
        for k in range(i, j + 1):
            o = offsets[k] - a
            rows = min(rps, height - k * rps)
            raw = np.frombuffer(zlib.decompress(blob[o:o + counts[k]]), dtype=np.uint8)[:width * rows].reshape(rows, width)
            if predictor == 2: raw = np.cumsum(raw, axis=1, dtype=np.uint8)
            out[(k - s0) * rps:(k - s0) * rps + rows] = raw[:, c0:c1]
        i = j + 1
    return out[r0 - s0 * rps:r1 - s0 * rps], left + c0 * res, top - r0 * res, res


def grid(name, bbox, budget_mb=400.0, margin_m=30.0):
    """The canopy heights over the box: (array, left, top, res) in Web Mercator, cached per map."""
    import numpy as np
    path = os.path.join(CACHE, "canopy-%s.npz" % name)
    if os.path.exists(path):
        d = np.load(path)
        return d["z"], *map(float, d["geo"])
    s, w, n, e = bbox
    k = 1 / math.cos(math.radians((s + n) / 2))
    x0, y0 = mercator(s, w); x1, y1 = mercator(n, e)
    x0, y0, x1, y1 = x0 - margin_m * k, y0 - margin_m * k, x1 + margin_m * k, y1 + margin_m * k
    parts = []
    for qk in sorted({quadkey(a, b) for a in (s, n) for b in (w, e)}):
        win = _window(CHM.format(qk=qk), x0, y0, x1, y1, budget_mb)
        if win is not None: parts.append(win)
    if not parts: return None
    res = parts[0][3]
    left, top = min(p[1] for p in parts), max(p[2] for p in parts)
    right = max(p[1] + p[0].shape[1] * res for p in parts); bottom = min(p[2] - p[0].shape[0] * res for p in parts)
    z = np.zeros((int(round((top - bottom) / res)), int(round((right - left) / res))), np.uint8)
    for a, l, t, _ in parts:
        r, c = int(round((top - t) / res)), int(round((l - left) / res))
        z[r:r + a.shape[0], c:c + a.shape[1]] = a
    os.makedirs(CACHE, exist_ok=True)
    np.savez_compressed(path, z=z, geo=np.array([left, top, res]))
    return z, left, top, res


def crowns(g, bbox, block_m=5.0, min_m=SEEN_M):
    """[(lat, lon, canopy m)]: the tallest pixel of each block_m square where the canopy reaches min_m."""
    import numpy as np
    z, left, top, res = g
    s, w, n, e = bbox
    k = 1 / math.cos(math.radians((s + n) / 2))
    x0, y0 = mercator(s, w); x1, y1 = mercator(n, e)
    c0, c1 = max(0, int((x0 - left) / res)), min(z.shape[1], int((x1 - left) / res) + 1)
    r0, r1 = max(0, int((top - y1) / res)), min(z.shape[0], int((top - y0) / res) + 1)
    if c1 <= c0 or r1 <= r0: return []
    b = max(1, int(round(block_m * k / res)))
    zz = z[r0:r1, c0:c1]
    H, W = -(-zz.shape[0] // b) * b, -(-zz.shape[1] // b) * b
    pad = np.zeros((H, W), zz.dtype); pad[:zz.shape[0], :zz.shape[1]] = zz
    blocks = pad.reshape(H // b, b, W // b, b).transpose(0, 2, 1, 3).reshape(H // b, W // b, b * b)
    idx = blocks.argmax(axis=2)
    tops = np.take_along_axis(blocks, idx[..., None], axis=2)[..., 0]
    out = []
    for bi, bj in zip(*np.nonzero(tops >= min_m)):
        r = r0 + bi * b + idx[bi, bj] // b; c = c0 + bj * b + idx[bi, bj] % b
        lat, lon = latlon(left + (c + 0.5) * res, top - (r + 0.5) * res)
        if s <= lat <= n and w <= lon <= e: out.append((lat, lon, float(tops[bi, bj])))
    out.sort(key=lambda t: -t[2])
    return out


def height(canopy_m):
    return round(min(MAX_M, CALIBRATION[0] + CALIBRATION[1] * canopy_m), 1)
