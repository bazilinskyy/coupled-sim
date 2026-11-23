#!/usr/bin/env python3
"""The far city of a Dutch map: every building around the detailed area as a simple block (3D BAG LoD 1.3).

A map builds its area (Tools/map_area.py: the centre's box, more boxes, corridors) in full detail: OpenStreetMap
roads, the topography's surfaces, 3D BAG LoD 2.2 buildings, street furniture, traffic. Around it, so that the city
does not end at the area's edge (seen from a drone or a motorway), the far city: 3D BAG's LoD 1.3 building parts
(footprints with the height of their roofs, from laser scans; (c) 3D BAG by tudelft3d and 3DGI, CC BY 4.0), which
the map builder extrudes into blocks with plain facades and the colour of their roof in the surroundings photo.
The same idea as coupled-carla's city twin (detail by distance: LoD 2.2 where people drive, LoD 1.3 further out).

The far region comes from lines in Assets/StreamingAssets/Maps/{map}.area.txt (ignored by the area itself):
  far box south west north east
  far corridor width_m lat lon lat lon ...      (e.g. 2 km either side of a motorway)
Buildings within 40 m of the detailed area are left out (it has them in detail).

  python3 Tools/bag3d_far.py Eindhoven

Writes Assets/StreamingAssets/Maps/{map}.far.bin.gz (little endian, gzip): "BFR1", origin RD x, y (float64), part
count (int32); per part: height of the roof above the ground (float32, m), roof kind (uint8: 0 flat, 1 slanted,
2 several flat levels), vertex count (uint16), the outer ring's vertices (dx, dy from the origin in RD metres,
float32 x2; counter-clockwise, not closed).
"""
import gzip, json, math, os, struct, sys, time, urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import map_area
from bag3d_buildings import wgs_to_rd, rd_inverse

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAPS = os.path.join(ROOT, "Assets", "StreamingAssets", "Maps")
WFS = "https://data.3dbag.nl/api/BAG3D/wfs"
UA = {"User-Agent": "coupled-sim-far-city/1.0 (open-source driving simulator)"}
KINDS = {"horizontal": 0, "slanted": 1, "multiple horizontal": 2}


def far_parts(name):
    """The far region's boxes and corridors from the area file's "far" lines, in map_area's form."""
    parts = []
    for line in open(map_area.path(name), encoding="utf-8"):
        f = line.split("#")[0].split()
        if len(f) < 2 or f[0] != "far": continue
        if f[1] == "box": parts.append(("box", tuple(map(float, f[2:6]))))
        elif f[1] == "corridor":
            v = list(map(float, f[3:]))
            parts.append(("corridor", float(f[2]), list(zip(v[0::2], v[1::2]))))
    return parts


def get(url):
    for attempt in range(6):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180) as r:
                return json.loads(r.read())
        except Exception as e:
            if attempt == 5: raise
            time.sleep(5 * (attempt + 1))


def fetch(x0, y0, x1, y1):
    """The LoD 1.3 parts in an RD box, page by page."""
    out, start, page = [], 0, 5000
    while True:
        url = (f"{WFS}?service=WFS&version=2.0.0&request=GetFeature&typeNames=BAG3D:lod13&outputFormat=application/json"
               f"&propertyName=b3_h_70p,b3_h_maaiveld,b3_dak_type,geom&srsName=EPSG:28992"
               f"&bbox={x0:.0f},{y0:.0f},{x1:.0f},{y1:.0f}&count={page}&startIndex={start}&sortBy=fid")
        d = get(url)
        fs = d.get("features") or []
        out.extend(fs)
        if len(fs) < page: return out
        start += page


def main(name):
    area = map_area.read(name)
    if not area: sys.exit(f"{name} has no area file")
    far = far_parts(name)
    if not far: sys.exit(f"no 'far' lines in {map_area.path(name)}")
    s, w, n, e = map_area.union_bbox(far)
    ax, ay = wgs_to_rd(s, w); bx, by = wgs_to_rd(n, e)
    x0, y0, x1, y1 = min(ax, bx), min(ay, by), max(ax, bx), max(ay, by)
    ox, oy = (x0 + x1) / 2, (y0 + y1) / 2
    tile = 1000.0
    seen, parts, skipped_detail = set(), [], 0
    nx, ny = math.ceil((x1 - x0) / tile), math.ceil((y1 - y0) / tile)
    t0 = time.time()
    for i in range(nx):
        for j in range(ny):
            tx0, ty0 = x0 + i * tile, y0 + j * tile
            # (a tile wholly outside the far region: not asked for)
            corners = [rd_inverse(tx0 + a * tile, ty0 + b * tile) for a in (0, 0.5, 1) for b in (0, 0.5, 1)]
            if not any(map_area.inside(far, la, lo, 800.0) for la, lo in corners): continue
            for f in fetch(tx0, ty0, tx0 + tile, ty0 + tile):
                fid = f.get("id")
                if fid in seen: continue
                seen.add(fid)
                g = f.get("geometry") or {}
                if g.get("type") != "Polygon" or not g.get("coordinates"): continue
                ring = g["coordinates"][0]
                if len(ring) < 4: continue
                if ring[0] == ring[-1]: ring = ring[:-1]
                p = f.get("properties") or {}
                h, ground = p.get("b3_h_70p"), p.get("b3_h_maaiveld")
                if h is None or ground is None or h - ground < 1.5: continue
                cx = sum(q[0] for q in ring) / len(ring); cy = sum(q[1] for q in ring) / len(ring)
                lat, lon = rd_inverse(cx, cy)
                if not map_area.inside(far, lat, lon, 0.0): continue
                if map_area.inside(area, lat, lon, 40.0): skipped_detail += 1; continue
                a2 = sum(ring[k][0] * ring[(k + 1) % len(ring)][1] - ring[(k + 1) % len(ring)][0] * ring[k][1] for k in range(len(ring)))
                if a2 < 0: ring = ring[::-1]
                parts.append((h - ground, KINDS.get(p.get("b3_dak_type"), 0), [(q[0] - ox, q[1] - oy) for q in ring[:65535]]))
            print(f"tile {i},{j} of {nx}x{ny}: {len(parts)} parts ({time.time() - t0:.0f} s)", flush=True)
    out = os.path.join(MAPS, f"{name}.far.bin.gz")
    with gzip.open(out, "wb") as fo:
        fo.write(b"BFR1"); fo.write(struct.pack("<ddi", ox, oy, len(parts)))
        for h, kind, ring in parts:
            fo.write(struct.pack("<fBH", h, kind, len(ring)))
            fo.write(struct.pack("<%df" % (2 * len(ring)), *[c for q in ring for c in q]))
    print(f"{name}: {len(parts)} far building parts ({skipped_detail} in the detailed area left out), {os.path.getsize(out) / 1e6:.1f} MB -> {out}")


if __name__ == "__main__":
    for m in sys.argv[1:] or ["Eindhoven"]:
        main(m)
