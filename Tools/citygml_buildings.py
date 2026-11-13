#!/usr/bin/env python3
"""
Real 3D buildings for city maps from open CityGML city models, like Tools/bag3d_buildings.py
does for the Netherlands:
- Japan: Project PLATEAU (MLIT; https://www.mlit.go.jp/plateau/), e.g. Tokyo;
- New York: NYC 3-D Building Model (NYC Office of Technology and Innovation, NYC Open Data);
- Montreal: Ville de Montreal 3D buildings (LoD2, CC BY 4.0).

    python3 Tools/citygml_buildings.py <Map> <source> [<source> ...] [--member REGEX]

A source is the URL (or local path) of a CityGML zip or .gml file. Zips are read with HTTP range
requests, so only the CityGML files needed are downloaded (cached in ~/.cache/citygml), not the
textures or the rest of the area. In a PLATEAU zip the 1 km mesh tiles covering the map are chosen
automatically; elsewhere every .gml in the zip is read, or only those matching --member.
The coordinate system is read from the file (srsName), or given with --crs when the file has
none: geographic (PLATEAU, EPSG:6697), EPSG:2263 (New York Long Island, US feet) or MTM zone 8
(EPSG:2950/32188, Montreal).

Reads the area of Assets/StreamingAssets/Maps/<Map>.osm.gz (its <bounds>) and writes
Assets/StreamingAssets/Maps/<Map>.buildings.bin.gz with each building inside it: LoD2 roofs and
walls where the model has them, else the LoD1 block.

File format "B3DL": like "B3D1" (see bag3d_buildings.py) but the vertices are already in the
map's local metres (x east, y north, z up from the building's lowest point), in the transverse
Mercator projection around the map centre (lat0, lon0 in the header) that the simulator uses.
Python 3.8+, no extra packages.
"""
import argparse, gzip, io, math, os, re, struct, sys, urllib.request, zipfile
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bag3d_buildings import triangulate, FALLBACKS  # ear clipping that keeps the ring's winding

# some open-data servers refuse Python's default user agent
_opener = urllib.request.build_opener(); _opener.addheaders = [("User-Agent", "Mozilla/5.0 (coupled-sim citygml_buildings)")]
urllib.request.install_opener(_opener)

# --- the simulator's projection (GeoProjection.ToLocal in Assets/Scripts/Maps/Core/OsmData.cs)
A_, F_ = 6378137.0, 1 / 298.257223563
def _meridian(phi, e2, a=A_):
    e4, e6 = e2 * e2, e2 * e2 * e2
    return a * ((1 - e2/4 - 3*e4/64 - 5*e6/256) * phi - (3*e2/8 + 3*e4/32 + 45*e6/1024) * math.sin(2*phi)
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

# --- source coordinate systems -> (lat, lon, height in m). NAD83 is taken as WGS84 (about 1 m apart).
GRS80_F = 1 / 298.257222101
FT_US = 1200 / 3937

def tm_inverse(x, y, lon0, k0, fe, fn, a=A_, f=GRS80_F):
    """Inverse transverse Mercator (Snyder 8-7..8-9), latitude of origin 0."""
    e2 = f * (2 - f); ep2 = e2 / (1 - e2)
    mu = ((y - fn) / k0) / (a * (1 - e2/4 - 3*e2*e2/64 - 5*e2**3/256))
    e1 = (1 - math.sqrt(1 - e2)) / (1 + math.sqrt(1 - e2))
    p1 = (mu + (3*e1/2 - 27*e1**3/32) * math.sin(2*mu) + (21*e1**2/16 - 55*e1**4/32) * math.sin(4*mu)
          + (151*e1**3/96) * math.sin(6*mu) + (1097*e1**4/512) * math.sin(8*mu))
    s, c, t = math.sin(p1), math.cos(p1), math.tan(p1)
    C1, T1 = ep2 * c * c, t * t
    N1 = a / math.sqrt(1 - e2 * s * s); R1 = a * (1 - e2) / (1 - e2 * s * s) ** 1.5
    D = (x - fe) / (N1 * k0)
    lat = p1 - (N1 * t / R1) * (D*D/2 - (5 + 3*T1 + 10*C1 - 4*C1*C1 - 9*ep2) * D**4/24
                                 + (61 + 90*T1 + 298*C1 + 45*T1*T1 - 252*ep2 - 3*C1*C1) * D**6/720)
    lon = math.radians(lon0) + (D - (1 + 2*T1 + C1) * D**3/6 + (5 - 2*C1 + 28*T1 - 3*C1*C1 + 8*ep2 + 24*T1*T1) * D**5/120) / c
    return math.degrees(lat), math.degrees(lon)

def lcc_inverse(x, y, lat1, lat2, lat0, lon0, fe, fn, a=A_, f=GRS80_F):
    """Inverse Lambert conformal conic with two standard parallels (Snyder 15-*)."""
    e2 = f * (2 - f); e = math.sqrt(e2)
    m = lambda p: math.cos(p) / math.sqrt(1 - e2 * math.sin(p) ** 2)
    t = lambda p: math.tan(math.pi/4 - p/2) / ((1 - e * math.sin(p)) / (1 + e * math.sin(p))) ** (e/2)
    p1, p2, p0 = map(math.radians, (lat1, lat2, lat0))
    n = (math.log(m(p1)) - math.log(m(p2))) / (math.log(t(p1)) - math.log(t(p2)))
    F = m(p1) / (n * t(p1) ** n); rho0 = a * F * t(p0) ** n
    dx, dy = x - fe, rho0 - (y - fn)
    rho = math.copysign(math.hypot(dx, dy), n); theta = math.atan2(dx, dy)
    tt = (rho / (a * F)) ** (1 / n)
    phi = math.pi/2 - 2 * math.atan(tt)
    for _ in range(8):
        phi = math.pi/2 - 2 * math.atan(tt * ((1 - e * math.sin(phi)) / (1 + e * math.sin(phi))) ** (e/2))
    return math.degrees(phi), lon0 + math.degrees(theta / n)

def crs_of(srs):
    """(name, function (a, b, c) -> (lat, lon, h)) for a GML srsName."""
    s = srs or ""
    if re.search(r"\b(6697|6668|4326|4979|4269|4612|4937)\b", s):
        return "geographic (lat, lon, h)", lambda a, b, c: (a, b, c)
    if re.search(r"\b2263\b", s):
        return "EPSG:2263 New York Long Island (US ft)", lambda a, b, c: lcc_inverse(
            a * FT_US, b * FT_US, 41 + 2/60, 40 + 40/60, 40 + 10/60, -74.0, 300000.0, 0.0) + (c * FT_US,)
    if re.search(r"\b(2950|32188)\b", s) or re.search(r"MTM.?8|MTM zone 8", s, re.I):
        return "MTM zone 8 (m)", lambda a, b, c: tm_inverse(a, b, -73.5, 0.9999, 304800.0, 0.0) + (c,)
    sys.exit("unknown coordinate system %r: give it with --crs, or add it to crs_of() in %s" % (srs, __file__))

def map_centre(name):
    cat = open(os.path.join(ROOT, "Assets/Scripts/Maps/MapCatalog.cs"), encoding="utf-8").read()
    m = re.search(r'Name = "%s".*?Latitude = ([-\d.]+), Longitude = ([-\d.]+)' % re.escape(name), cat)
    if not m: sys.exit("map %s not in MapCatalog.cs" % name)
    return float(m.group(1)), float(m.group(2))

# --- Japanese standard grid (3rd mesh, ~1 km), for PLATEAU's tiles
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

def fetch(source, meshes, member, cache_root):
    """Local paths of the CityGML files of a source (downloaded once into the cache)."""
    if not source.lower().endswith(".zip"):
        if not re.match(r"https?://", source): return [source]
        local = os.path.join(cache_root, re.sub(r"\W", "_", source.split("/")[-1]))
        if not os.path.exists(local):
            os.makedirs(cache_root, exist_ok=True)
            urllib.request.urlretrieve(source, local + ".part"); os.replace(local + ".part", local)
        return [local]
    cache = os.path.join(cache_root, re.sub(r"\W", "_", source.rstrip("/").split("/")[-1]))
    os.makedirs(cache, exist_ok=True)
    index = os.path.join(cache, "_members.txt")
    if os.path.exists(index):
        names = open(index, encoding="utf-8").read().split("\n")
    else:
        zf = zipfile.ZipFile(HttpFile(source) if re.match(r"https?://", source) else source)
        names = zf.namelist()
        open(index, "w", encoding="utf-8").write("\n".join(names))
    if any("/bldg/" in n for n in names):   # PLATEAU: the 1 km tiles of the map
        wanted = [n for n in names for m in meshes if re.search(r"/bldg/%s_bldg_\d+(_op)?\.gml$" % m, n)]
    else:
        wanted = [n for n in names if n.lower().endswith(".gml") and (not member or re.search(member, n))]
    if not wanted: print("%s: no matching CityGML files" % source); return []
    out, zf = [], None
    for n in wanted:
        local = os.path.join(cache, re.sub(r"[^\w.-]", "_", n))
        if not os.path.exists(local):
            zf = zf or zipfile.ZipFile(HttpFile(source) if re.match(r"https?://", source) else source)
            print("downloading %s from %s" % (n, source.split("/")[-1]), flush=True)
            with zf.open(n) as src, open(local + ".part", "wb") as dst:
                while True:
                    chunk = src.read(1 << 22)
                    if not chunk: break
                    dst.write(chunk)
            os.replace(local + ".part", local)
        out.append(local)
    return out

def local_name(tag): return tag.rsplit("}", 1)[-1]

def rings(elem):
    """Exterior rings (lists of (a, b, c) in the file's coordinates) of the polygons under elem."""
    for ext in elem.iter():
        if local_name(ext.tag) != "exterior": continue
        for lr in ext.iter():
            if local_name(lr.tag) != "LinearRing": continue
            vals = []
            for c in lr:
                if local_name(c.tag) in ("posList", "pos") and c.text: vals.extend(map(float, c.text.split()))
            pts = [tuple(vals[i:i+3]) for i in range(0, len(vals) - 2, 3)]
            if len(pts) > 1 and pts[0] == pts[-1]: pts = pts[:-1]
            if len(pts) >= 3: yield pts

def building_surfaces(bld):
    """(kind, ring) pairs of a building: thematic surfaces (RoofSurface, WallSurface, ...) of the
    highest LoD, else the faces of its solid or multi-surface, kind "?" (classified by normal)."""
    by_lod = {}
    for bounded in bld.iter():
        if local_name(bounded.tag) != "boundedBy": continue
        for surf in bounded:
            kind = local_name(surf.tag)
            for geom in surf:
                m = re.match(r"lod(\d)", local_name(geom.tag))
                if m: by_lod.setdefault(int(m.group(1)), []).extend((kind, r) for r in rings(geom))
    if by_lod: return by_lod[max(by_lod)]
    for lod in ("lod2Solid", "lod2MultiSurface", "lod1Solid", "lod1MultiSurface"):
        out = [("?", r) for g in bld.iter() if local_name(g.tag) == lod for r in rings(g)]
        if out: return out
    return []

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("map"); ap.add_argument("sources", nargs="+")
    ap.add_argument("--member", help="regular expression for the .gml files to read in a zip (default: all)")
    ap.add_argument("--crs", help="coordinate system when the files do not name it, e.g. EPSG:2950 (Montreal)")
    args = ap.parse_args()
    name = args.map
    osm = os.path.join(ROOT, "Assets/StreamingAssets/Maps/%s.osm.gz" % name)
    head = gzip.open(osm).read(4000).decode("utf-8", "ignore")
    b = re.search(r'<bounds minlat="([-\d.]+)" minlon="([-\d.]+)" maxlat="([-\d.]+)" maxlon="([-\d.]+)"', head)
    s, w, n, e = map(float, b.groups())
    lat0, lon0 = map_centre(name)
    meshes = sorted({mesh3(la, lo) for la in (s, (s+n)/2, n) for lo in (w, (w+e)/2, e)})
    cache_root = os.path.expanduser("~/.cache/citygml")
    files = [f for src in args.sources for f in fetch(src, meshes, args.member, cache_root)]
    inside = lambda la, lo: s - 0.0005 <= la <= n + 0.0005 and w - 0.0005 <= lo <= e + 0.0005
    buildings = []
    for path in files:
        with open(path, "rb") as fh: srs = re.search(rb'srsName="([^"]+)"', fh.read(200000))
        crs_name, geo = crs_of(srs.group(1).decode() if srs else args.crs)
        print("%s: %s" % (os.path.basename(path), crs_name), flush=True)
        count = skipped = 0
        ctx = ET.iterparse(path, events=("start", "end"))
        _, root = next(ctx)
        for ev, el in ctx:
            if ev != "end": continue
            tag = local_name(el.tag)
            if tag == "cityObjectMember": root.clear(); continue
            if tag != "Building": continue
            surfaces = building_surfaces(el)
            yr = next((c for c in el if local_name(c.tag) == "yearOfConstruction"), None)
            year = int(yr.text) if yr is not None and yr.text and yr.text.strip().isdigit() else 0
            el.clear()
            if not surfaces: continue
            # inside the map? (one vertex first: cheap; then all)
            la, lo, _ = geo(*surfaces[0][1][0])
            if not (s - 0.003 <= la <= n + 0.003 and w - 0.004 <= lo <= e + 0.004): continue
            geo_pts = {}
            for _, r in surfaces:
                for p in r:
                    if p not in geo_pts: geo_pts[p] = geo(*p)
            if not any(inside(g[0], g[1]) for g in geo_pts.values()): continue
            ground = min(g[2] for g in geo_pts.values())
            verts, vmap, walls, roofs = [], {}, [], []
            roof_area = slanted_area = 0.0
            for kind, ring in surfaces:
                pts = []
                for p in ring:
                    if p not in vmap:
                        la, lo, h = geo_pts[p]
                        x, y = to_local(la, lo, lat0, lon0)
                        vmap[p] = len(verts); verts.append((x, y, h - ground))
                    pts.append(vmap[p])
                P = [verts[i] for i in pts]
                # normal (Newell) in local metres: classifies untyped faces, skips floors on the ground
                nx = sum((P[i][1] - P[i-1][1]) * (P[i][2] + P[i-1][2]) for i in range(len(P)))
                ny = sum((P[i][2] - P[i-1][2]) * (P[i][0] + P[i-1][0]) for i in range(len(P)))
                nz = sum((P[i][0] - P[i-1][0]) * (P[i][1] + P[i-1][1]) for i in range(len(P)))
                ln = math.sqrt(nx*nx + ny*ny + nz*nz)
                if ln < 1e-6: continue
                if kind == "?": kind = "RoofSurface" if nz / ln > 0.7 else "GroundSurface" if nz / ln < -0.7 else "WallSurface"
                if kind in ("GroundSurface", "FloorSurface") and min(p[2] for p in P) < 1.0: continue
                if kind == "ClosureSurface": continue
                if kind == "RoofSurface":
                    roof_area += ln / 2
                    if abs(nz / ln) < 0.97: slanted_area += ln / 2
                tris = triangulate(P)
                (roofs if kind == "RoofSurface" else walls).extend((pts[a], pts[b2], pts[c]) for a, b2, c in tris)
            if walls or roofs:
                # sloped roof (tiles) when most of it slopes; photogrammetric flat roofs have small slopes too
                slanted = slanted_area > 0.5 * roof_area
                buildings.append((1 if slanted else 0, year, verts, walls, roofs)); count += 1
        print("%s: %d buildings in the map" % (os.path.basename(path), count), flush=True)
    out = os.path.join(ROOT, "Assets/StreamingAssets/Maps/%s.buildings.bin.gz" % name)
    with gzip.open(out, "wb") as fo:
        fo.write(b"B3DL"); fo.write(struct.pack("<ddi", lat0, lon0, len(buildings)))
        for kind, year, verts, walls, roofs in buildings:
            fo.write(struct.pack("<Bhi", kind, max(-32768, min(32767, year)), len(verts)))
            fo.write(struct.pack("<%df" % (3*len(verts)), *[c for v in verts for c in v]))
            for tl in (walls, roofs):
                fo.write(struct.pack("<i", len(tl)))
                fo.write(struct.pack("<%di" % (3*len(tl)), *[i for t in tl for i in t]))
    print("%s: %d buildings, %d triangles, %d polygons fanned, %.1f MB -> %s" % (name, len(buildings), sum(len(b[3]) + len(b[4]) for b in buildings), FALLBACKS[0], os.path.getsize(out) / 1e6, out))

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
if __name__ == "__main__":
    main()
