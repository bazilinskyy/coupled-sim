#!/usr/bin/env python3
"""
Real 3D buildings for Japanese city maps from Project PLATEAU (Japan's open 3D city models,
MLIT; https://www.mlit.go.jp/plateau/), like Tools/bag3d_buildings.py does for the Netherlands.

    python3 Tools/plateau_buildings.py Tokyo <CityGML zip URL>

Reads the area of Assets/StreamingAssets/Maps/<Map>.osm.gz (its <bounds>), finds the 1 km mesh
tiles that cover it in the ward's CityGML zip (from the dataset page on geospatial.jp, e.g.
"plateau-13113-shibuya-ku-2023"), downloads only those files (HTTP range requests; cached in
~/.cache/plateau) and writes Assets/StreamingAssets/Maps/<Map>.buildings.bin.gz:
LoD2 roofs and walls where PLATEAU has them, else the LoD1 block.

File format "B3DL": like "B3D1" (see bag3d_buildings.py) but the vertices are already in the
map's local metres (x east, y north, z up from the building's lowest point), in the transverse
Mercator projection around the map centre (lat0, lon0 in the header) that the simulator uses.
Python 3.8+, no extra packages.
"""
import gzip, io, math, os, re, struct, sys, urllib.request, zipfile
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bag3d_buildings import triangulate  # ear clipping that keeps the ring's winding

NS = {"gml": "http://www.opengis.net/gml", "bldg": "http://www.opengis.net/citygml/building/2.0"}
G = "{%s}" % NS["gml"]; B = "{%s}" % NS["bldg"]

# --- the simulator's projection (GeoProjection.ToLocal in Assets/Scripts/Maps/Core/OsmData.cs)
A_, F_ = 6378137.0, 1 / 298.257223563
def _meridian(phi, e2):
    e4, e6 = e2 * e2, e2 * e2 * e2
    return A_ * ((1 - e2/4 - 3*e4/64 - 5*e6/256) * phi - (3*e2/8 + 3*e4/32 + 45*e6/1024) * math.sin(2*phi)
                 + (15*e4/256 + 45*e6/1024) * math.sin(4*phi) - (35*e6/3072) * math.sin(6*phi))
def to_local(lat, lon, lat0, lon0):
    e2 = F_ * (2 - F_); ep2 = e2 / (1 - e2)
    phi = math.radians(lat); lam = math.radians(lon - lon0)
    N = A_ / math.sqrt(1 - e2 * math.sin(phi) ** 2)
    T = math.tan(phi) ** 2; C = ep2 * math.cos(phi) ** 2; Ac = lam * math.cos(phi)
    M = _meridian(phi, e2); M0 = _meridian(math.radians(lat0), e2)
    x = N * (Ac + (1 - T + C) * Ac**3 / 6 + (5 - 18*T + T*T + 72*C - 58*ep2) * Ac**5 / 120)
    z = M - M0 + N * math.tan(phi) * (Ac*Ac/2 + (5 - T + 9*C + 4*C*C) * Ac**4 / 24 + (61 - 58*T + T*T + 600*C - 330*ep2) * Ac**6 / 720)
    return x, z

def map_centre(name):
    cat = open(os.path.join(ROOT, "Assets/Scripts/Maps/MapCatalog.cs"), encoding="utf-8").read()
    m = re.search(r'Name = "%s".*?Latitude = ([-\d.]+), Longitude = ([-\d.]+)' % re.escape(name), cat)
    if not m: sys.exit("map %s not in MapCatalog.cs" % name)
    return float(m.group(1)), float(m.group(2))

# --- Japanese standard grid (3rd mesh, ~1 km)
def mesh3(lat, lon):
    a = lat * 1.5; p = int(a); b = lon - 100; u = int(b)
    q = int((a - p) * 8); v = int((b - u) * 8)
    r = int(((a - p) * 8 - q) * 10); w = int(((b - u) * 8 - v) * 10)
    return "%d%d%d%d%d%d" % (p, u, q, v, r, w)

class HttpFile(io.RawIOBase):
    """Seekable read-only file over HTTP range requests, so zipfile reads only what it needs."""
    def __init__(self, url):
        self.url, self.pos = url, 0
        self.size = int(urllib.request.urlopen(urllib.request.Request(url, method="HEAD"), timeout=60).headers["Content-Length"])
    def seekable(self): return True
    def readable(self): return True
    def tell(self): return self.pos
    def seek(self, off, whence=0):
        self.pos = off if whence == 0 else self.pos + off if whence == 1 else self.size + off
        return self.pos
    def readinto(self, b):
        n = min(len(b), self.size - self.pos)
        if n <= 0: return 0
        req = urllib.request.Request(self.url, headers={"Range": "bytes=%d-%d" % (self.pos, self.pos + n - 1)})
        d = urllib.request.urlopen(req, timeout=600).read()
        b[:len(d)] = d; self.pos += len(d); return len(d)

def rings(elem):
    """Exterior rings (lists of (lat, lon, h)) of the gml:Polygons under elem."""
    for pl in elem.iter(G + "posList"):
        vals = list(map(float, pl.text.split()))
        pts = [tuple(vals[i:i+3]) for i in range(0, len(vals) - 2, 3)]
        if len(pts) > 1 and pts[0] == pts[-1]: pts = pts[:-1]
        if len(pts) >= 3: yield pts

def building_surfaces(bld):
    """(kind, ring) pairs of a building: LoD2 roof/wall surfaces, else LoD1 solid faces."""
    out = []
    for bounded in bld.iter(B + "boundedBy"):
        for surf in bounded:
            kind = surf.tag.split("}")[1]          # RoofSurface, WallSurface, GroundSurface, ...
            for ms in surf.iter(B + "lod2MultiSurface"):
                out.extend((kind, r) for r in rings(ms))
    if out: return out, 2
    for solid in bld.iter(B + "lod1Solid"):
        for r in rings(solid):
            out.append(("?", r))
    return out, 1

def main(name, url):
    global ROOT
    osm = os.path.join(ROOT, "Assets/StreamingAssets/Maps/%s.osm.gz" % name)
    head = gzip.open(osm).read(4000).decode("utf-8", "ignore")
    b = re.search(r'<bounds minlat="([\d.]+)" minlon="([\d.]+)" maxlat="([\d.]+)" maxlon="([\d.]+)"', head)
    s, w, n, e = map(float, b.groups())
    lat0, lon0 = map_centre(name)
    meshes = sorted({mesh3(la, lo) for la in (s, (s+n)/2, n) for lo in (w, (w+e)/2, e)})
    cache = os.path.join(os.path.expanduser("~/.cache/plateau"), re.sub(r"\W", "_", url.split("/")[-1]))
    os.makedirs(cache, exist_ok=True)
    zf = None
    buildings = []
    for m in meshes:
        local = os.path.join(cache, m + ".gml")
        if not os.path.exists(local):
            zf = zf or zipfile.ZipFile(HttpFile(url))
            names = [i for i in zf.namelist() if re.search(r"/bldg/%s_bldg_\d+(_op)?\.gml$" % m, i)]
            if not names: print("mesh %s: no buildings in the zip" % m); continue
            print("mesh %s: downloading %s" % (m, names[0]), flush=True)
            with zf.open(names[0]) as src, open(local + ".part", "wb") as dst:
                while True:
                    chunk = src.read(1 << 22)
                    if not chunk: break
                    dst.write(chunk)
            os.replace(local + ".part", local)
        count = 0
        for _, el in ET.iterparse(local, events=("end",)):
            if el.tag != B + "Building": continue
            surfaces, lod = building_surfaces(el)
            yr = el.find(B + "yearOfConstruction")
            year = int(yr.text) if yr is not None and yr.text and yr.text.strip().isdigit() else 0
            el.clear()
            if not surfaces: continue
            # inside the map (any vertex)?
            if not any(s - 0.0005 <= p[0] <= n + 0.0005 and w - 0.0005 <= p[1] <= e + 0.0005 for _, r in surfaces for p in r): continue
            ground = min(p[2] for _, r in surfaces for p in r)
            verts, vmap, walls, roofs = [], {}, [], []
            slanted = False
            for kind, ring in surfaces:
                pts = []
                for la, lo, h in ring:
                    key = (la, lo, h)
                    if key not in vmap:
                        x, y = to_local(la, lo, lat0, lon0)
                        vmap[key] = len(verts); verts.append((x, y, h - ground))
                    pts.append(vmap[key])
                P = [verts[i] for i in pts]
                # normal (Newell) in local metres: classifies LoD1 faces, skips floors on the ground
                nx = sum((P[i][1] - P[i-1][1]) * (P[i][2] + P[i-1][2]) for i in range(len(P)))
                ny = sum((P[i][2] - P[i-1][2]) * (P[i][0] + P[i-1][0]) for i in range(len(P)))
                nz = sum((P[i][0] - P[i-1][0]) * (P[i][1] + P[i-1][1]) for i in range(len(P)))
                ln = math.sqrt(nx*nx + ny*ny + nz*nz) or 1
                if kind == "?": kind = "RoofSurface" if nz / ln > 0.7 else "GroundSurface" if nz / ln < -0.7 else "WallSurface"
                if kind == "GroundSurface" and min(p[2] for p in P) < 1.0: continue
                if kind == "RoofSurface" and abs(nz / ln) < 0.97: slanted = True
                tris = triangulate(P)
                (roofs if kind == "RoofSurface" else walls).extend((pts[a], pts[b2], pts[c]) for a, b2, c in tris)
            if walls or roofs:
                buildings.append((1 if slanted else 0, year, verts, walls, roofs)); count += 1
        print("mesh %s: %d buildings" % (m, count), flush=True)
    out = os.path.join(ROOT, "Assets/StreamingAssets/Maps/%s.buildings.bin.gz" % name)
    with gzip.open(out, "wb") as fo:
        fo.write(b"B3DL"); fo.write(struct.pack("<ddi", lat0, lon0, len(buildings)))
        for kind, year, verts, walls, roofs in buildings:
            fo.write(struct.pack("<Bhi", kind, max(-32768, min(32767, year)), len(verts)))
            fo.write(struct.pack("<%df" % (3*len(verts)), *[c for v in verts for c in v]))
            for tl in (walls, roofs):
                fo.write(struct.pack("<i", len(tl)))
                fo.write(struct.pack("<%di" % (3*len(tl)), *[i for t in tl for i in t]))
    print("%s: %d buildings, %d triangles, %.1f MB -> %s" % (name, len(buildings), sum(len(b[3]) + len(b[4]) for b in buildings), os.path.getsize(out) / 1e6, out))

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
if __name__ == "__main__":
    if len(sys.argv) != 3: sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
