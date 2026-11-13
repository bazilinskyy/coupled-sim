#!/usr/bin/env python3
"""Real 3D buildings for Dutch city maps from 3D BAG (https://3dbag.nl, CC BY 4.0).

Downloads the LoD 2.2 building models (walls and real roof shapes, from laser scans) for a map's
area, triangulates them and writes Assets/StreamingAssets/Maps/<Map>.buildings.bin.gz, which the
map builder uses instead of extruding OpenStreetMap footprints.

Usage (from the repository): python3 Tools/bag3d_buildings.py Delft
The area is the <bounds> of Assets/StreamingAssets/Maps/<Map>.osm.gz. Only the Netherlands.

File format (little endian, gzip): "B3D1", origin RD x,y (float64), building count (int32);
per building: roof kind (uint8: 0 flat, 1 slanted, 2 multiple), construction year (int16),
vertex count (int32), vertices (dx, dy from the origin in RD metres, height above ground in m;
float32 x3), wall triangle count (int32), indices (int32 x3), roof triangle count, indices.
"""
import gzip, json, math, os, re, struct, sys, time, urllib.request

# ---- WGS84 <-> RD New (EPSG:28992), polynomials of Schreutelkamp & Strang van Hees (~0.25 m)
X0, Y0, PHI0, LAM0 = 155000.0, 463000.0, 52.15517440, 5.38720621
RP = [(0,1,190094.945),(1,1,-11832.228),(2,1,-114.221),(0,3,-32.391),(1,0,-0.705),(3,1,-2.340),(1,3,-0.608),(0,2,-0.008),(2,3,0.148)]
SP = [(1,0,309056.544),(0,2,3638.893),(2,0,73.077),(1,2,-157.984),(3,0,59.788),(0,1,0.433),(2,2,-6.439),(1,1,-0.032),(0,4,0.092),(1,4,-0.054)]

def wgs_to_rd(lat, lon):
    dp, dl = 0.36 * (lat - PHI0), 0.36 * (lon - LAM0)
    return (X0 + sum(c * dp**p * dl**q for p, q, c in RP), Y0 + sum(c * dp**p * dl**q for p, q, c in SP))

def fetch(bbox):
    url = "https://api.3dbag.nl/collections/pand/items?bbox=%.0f,%.0f,%.0f,%.0f&limit=100" % bbox
    out = []
    while url:
        for attempt in range(4):
            try:
                with urllib.request.urlopen(url, timeout=90) as r:
                    page = json.loads(r.read()); break
            except Exception as e:
                if attempt == 3: raise
                time.sleep(3 * (attempt + 1))
        out.append(page)
        url = next((l["href"] for l in page.get("links", []) if l.get("rel") == "next"), None)
    return out

# ---- triangulation of planar polygons (ear clipping in the polygon's dominant plane)
def normal(pts):
    nx = ny = nz = 0.0
    for i in range(len(pts)):
        x1, y1, z1 = pts[i]; x2, y2, z2 = pts[(i + 1) % len(pts)]
        nx += (y1 - y2) * (z1 + z2); ny += (z1 - z2) * (x1 + x2); nz += (x1 - x2) * (y1 + y2)
    return nx, ny, nz

def ear_clip(pts2):
    n = len(pts2)
    if n < 3: return []
    area = sum(pts2[i][0] * pts2[(i+1) % n][1] - pts2[(i+1) % n][0] * pts2[i][1] for i in range(n))
    idx = list(range(n)) if area > 0 else list(range(n))[::-1]
    tris = []
    def inside(p, a, b, c):
        def s(p1, p2, p3): return (p1[0]-p3[0])*(p2[1]-p3[1]) - (p2[0]-p3[0])*(p1[1]-p3[1])
        d1, d2, d3 = s(p, a, b), s(p, b, c), s(p, c, a)
        return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))
    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        for k in range(len(idx)):
            i0, i1, i2 = idx[k-1], idx[k], idx[(k+1) % len(idx)]
            a, b, c = pts2[i0], pts2[i1], pts2[i2]
            if (b[0]-a[0])*(c[1]-a[1]) - (b[1]-a[1])*(c[0]-a[0]) <= 1e-12: continue
            if any(inside(pts2[j], a, b, c) for j in idx if j not in (i0, i1, i2)): continue
            tris.append((i0, i1, i2)); del idx[k]; break
        else:
            break  # degenerate: fan the rest
    for k in range(1, len(idx) - 1): tris.append((idx[0], idx[k], idx[k+1]))
    return tris

def triangulate(pts):
    nx, ny, nz = normal(pts)
    ax = max(range(3), key=lambda i: abs((nx, ny, nz)[i]))
    keep = [i for i in range(3) if i != ax]
    flip = (nx, ny, nz)[ax] < 0
    p2 = [(p[keep[0]], p[keep[1]]) for p in pts]
    tris = ear_clip(p2)
    # keep the winding of the original ring (outward normal)
    if (ax == 1) ^ flip: tris = [(a, c, b) for a, b, c in tris]
    return tris

def main(name):
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    osm = os.path.join(root, "Assets/StreamingAssets/Maps/%s.osm.gz" % name)
    head = gzip.open(osm).read(4000).decode("utf-8", "ignore")
    b = re.search(r'<bounds minlat="([\d.]+)" minlon="([\d.]+)" maxlat="([\d.]+)" maxlon="([\d.]+)"', head)
    if not b: sys.exit("no <bounds> in " + osm)
    s, w, n, e = map(float, b.groups())
    x0, y0 = wgs_to_rd(s, w); x1, y1 = wgs_to_rd(n, e)
    x0, y0, x1, y1 = min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)
    ox, oy = (x0 + x1) / 2, (y0 + y1) / 2
    tiles = 4
    seen = set(); buildings = []
    for i in range(tiles):
        for j in range(tiles):
            bb = (x0 + (x1-x0)*i/tiles, y0 + (y1-y0)*j/tiles, x0 + (x1-x0)*(i+1)/tiles, y0 + (y1-y0)*(j+1)/tiles)
            for page in fetch(bb):
                tr = page["metadata"]["transform"]; sc, tl = tr["scale"], tr["translate"]
                for f in page["features"]:
                    if f["id"] in seen: continue
                    seen.add(f["id"])
                    V = [(v[0]*sc[0]+tl[0], v[1]*sc[1]+tl[1], v[2]*sc[2]+tl[2]) for v in f["vertices"]]
                    bld = f["CityObjects"].get(f["id"], {})
                    att = bld.get("attributes", {})
                    ground = att.get("b3_h_maaiveld") or 0.0
                    kind = {"horizontal": 0, "slanted": 1, "multiple horizontal": 2}.get(att.get("b3_dak_type"), 1)
                    year = int(att.get("oorspronkelijkbouwjaar") or 0)
                    verts, walls, roofs, vmap = [], [], [], {}
                    for cid in bld.get("children", []):
                        part = f["CityObjects"].get(cid, {})
                        geoms = [g for g in part.get("geometry", []) if str(g.get("lod")) == "2.2"]
                        if not geoms: continue
                        g = geoms[0]
                        sem = g.get("semantics", {}); stypes = [x.get("type") for x in sem.get("surfaces", [])]
                        vals = sem.get("values", [[]])
                        for si, shell in enumerate(g["boundaries"]):
                            for fi, surf in enumerate(shell):
                                t = None
                                try: t = stypes[vals[si][fi]]
                                except Exception: pass
                                if t == "GroundSurface": continue
                                ring = surf[0]
                                if len(ring) < 3: continue
                                pts = [V[k] for k in ring]
                                local = []
                                for k in ring:
                                    if k not in vmap:
                                        vmap[k] = len(verts)
                                        x, y, z = V[k]
                                        verts.append((x - ox, y - oy, z - ground))
                                    local.append(vmap[k])
                                tris = triangulate(pts)
                                dest = roofs if t == "RoofSurface" else walls
                                dest.extend((local[a], local[b2], local[c]) for a, b2, c in tris)
                    if verts and (walls or roofs):
                        buildings.append((kind, year, verts, walls, roofs))
            print("tile %d,%d: %d buildings" % (i, j, len(buildings)), flush=True)
    out = os.path.join(root, "Assets/StreamingAssets/Maps/%s.buildings.bin.gz" % name)
    with gzip.open(out, "wb") as fo:
        fo.write(b"B3D1"); fo.write(struct.pack("<ddi", ox, oy, len(buildings)))
        for kind, year, verts, walls, roofs in buildings:
            fo.write(struct.pack("<Bhi", kind, max(-32768, min(32767, year)), len(verts)))
            fo.write(struct.pack("<%df" % (3*len(verts)), *[c for v in verts for c in v]))
            for tl in (walls, roofs):
                fo.write(struct.pack("<i", len(tl)))
                fo.write(struct.pack("<%di" % (3*len(tl)), *[i for t in tl for i in t]))
    print("%s: %d buildings, %d triangles, %.1f MB -> %s" % (name, len(buildings), sum(len(b[3])+len(b[4]) for b in buildings), os.path.getsize(out)/1e6, out))

if __name__ == "__main__":
    for m in sys.argv[1:] or ["Eindhoven", "Delft"]:
        main(m)
