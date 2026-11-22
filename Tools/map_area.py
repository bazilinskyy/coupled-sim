#!/usr/bin/env python3
"""
The area of a city map, when it is more than the square of its OpenStreetMap file: the centre's box plus corridors
along a route (a highway stretch, a ring road), each a strip of a given width. The simulator builds the map only
inside the area (Assets/Scripts/Maps/Core/MapArea.cs), and the data tools (city_data.py, bag3d_buildings.py,
terrain_height.py) fetch it in tiles that cover the area. A map without an area file is its square, as before.

  # a corridor along the driving route between two places, 300 m wide, added to the map's box; then its OSM data
  python3 Tools/map_area.py Eindhoven --route 51.4410,5.4775 51.4740,5.6235 --width 300
  python3 Tools/map_area.py Eindhoven --osm

The route comes from the public OSRM router (router.project-osrm.org, OpenStreetMap data); --via adds places it must
pass. --osm adds the corridors' OpenStreetMap data (OSM API, in tiles of 500 m) to the map's file. The file Assets/StreamingAssets/Maps/{map}.area.txt (plain text, # comments):
  box south west north east
  corridor width_m lat lon lat lon ...
"""
import argparse, gzip, json, math, os, re, sys, time, urllib.parse, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAPS = os.path.join(ROOT, "Assets", "StreamingAssets", "Maps")
UA = {"User-Agent": "coupled-sim-map-area/1.0 (open-source driving simulator)"}
OVERPASS = ["https://overpass-api.de/api/interpreter", "https://overpass.kumi.systems/api/interpreter"]


def path(name): return os.path.join(MAPS, name + ".area.txt")


def osm_bounds(name):
    head = gzip.open(os.path.join(MAPS, name + ".osm.gz")).read(4000).decode("utf-8", "ignore")
    b = re.search(r'<bounds minlat="(-?[\d.]+)" minlon="(-?[\d.]+)" maxlat="(-?[\d.]+)" maxlon="(-?[\d.]+)"', head)
    return tuple(map(float, b.groups()))


def read(name):
    """[("box", (s, w, n, e)), ("corridor", width_m, [(lat, lon), ...])], or None (the map is its square)."""
    p = path(name)
    if not os.path.exists(p): return None
    parts = []
    for line in open(p, encoding="utf-8"):
        f = line.split("#")[0].split()
        if not f: continue
        if f[0] == "box": parts.append(("box", tuple(map(float, f[1:5]))))
        elif f[0] == "corridor":
            v = list(map(float, f[2:]))
            parts.append(("corridor", float(f[1]), list(zip(v[0::2], v[1::2]))))
    return parts


def union_bbox(parts, margin_m=0.0):
    s = w = math.inf; n = e = -math.inf
    for p in parts:
        pts = [(p[1][0], p[1][1]), (p[1][2], p[1][3])] if p[0] == "box" else p[2]
        pad = 0.0 if p[0] == "box" else p[1] / 2
        for lat, lon in pts:
            dl = (pad + margin_m) / 111320.0; dlo = dl / math.cos(math.radians(lat))
            s, n, w, e = min(s, lat - dl), max(n, lat + dl), min(w, lon - dlo), max(e, lon + dlo)
    return s, w, n, e


def _xy(lat, lon, lat0):
    return lon * 111320.0 * math.cos(math.radians(lat0)), lat * 111320.0


def _seg_dist(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx - ax, by - ay; l2 = dx * dx + dy * dy
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / l2))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def distance_inside(parts, lat, lon):
    """How far (m) a point is inside the area (negative: outside by that much)."""
    best = -math.inf
    for p in parts:
        if p[0] == "box":
            s, w, n, e = p[1]
            lat0 = (s + n) / 2
            x, y = _xy(lat, lon, lat0); x0, y0 = _xy(s, w, lat0); x1, y1 = _xy(n, e, lat0)
            best = max(best, min(x - x0, x1 - x, y - y0, y1 - y))
        else:
            lat0 = p[2][0][0]
            q = _xy(lat, lon, lat0); pts = [_xy(a, b, lat0) for a, b in p[2]]
            d = min(_seg_dist(q, pts[i], pts[i + 1]) for i in range(len(pts) - 1))
            best = max(best, p[1] / 2 - d)
    return best


def inside(parts, lat, lon, margin_m=0.0):
    return parts is None or distance_inside(parts, lat, lon) >= -margin_m


def tiles(parts, size_m=500.0, margin_m=0.0):
    """Boxes (s, w, n, e) of about size_m that together cover the area (and margin_m around it)."""
    s, w, n, e = union_bbox(parts, margin_m)
    lat0 = (s + n) / 2
    dlat = size_m / 111320.0; dlon = dlat / math.cos(math.radians(lat0))
    out = []
    lat = s
    while lat < n:
        lon = w
        while lon < e:
            c = (lat + dlat / 2, lon + dlon / 2)
            if distance_inside(parts, *c) > -(size_m * 0.75 + margin_m): out.append((lat, lon, min(n, lat + dlat), min(e, lon + dlon)))
            lon += dlon
        lat += dlat
    return out


def get(url, data=None):
    for k in range(5):
        try:
            req = urllib.request.Request(url, data=data, headers=UA)
            with urllib.request.urlopen(req, timeout=600) as r: return r.read()
        except Exception as ex:
            if k == 4: raise
            print("  retry (%s)" % ex); time.sleep(10 * (k + 1))


def simplify(pts, tol_m, lat0):
    if len(pts) < 3: return pts
    xy = [_xy(a, b, lat0) for a, b in pts]
    keep = [False] * len(pts); keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        i, j = stack.pop()
        k, dmax = 0, 0.0
        for m in range(i + 1, j):
            d = _seg_dist(xy[m], xy[i], xy[j])
            if d > dmax: k, dmax = m, d
        if dmax > tol_m: keep[k] = True; stack += [(i, k), (k, j)]
    return [p for p, kk in zip(pts, keep) if kk]


def route(points):
    coords = ";".join("%f,%f" % (lon, lat) for lat, lon in points)
    d = json.loads(get("https://router.project-osrm.org/route/v1/driving/%s?overview=full&geometries=geojson" % coords))
    r = d["routes"][0]
    print("route: %.1f km" % (r["distance"] / 1000))
    return [(lat, lon) for lon, lat in r["geometry"]["coordinates"]]


def write(name, parts):
    with open(path(name), "w", encoding="utf-8") as f:
        f.write("# The area of the %s map (Tools/map_area.py): the simulator builds the map only inside it.\n" % name)
        f.write("# box south west north east | corridor width_m lat lon lat lon ...  (route: OSRM, OpenStreetMap data)\n")
        for p in parts:
            if p[0] == "box": f.write("box %.7f %.7f %.7f %.7f\n" % p[1])
            else: f.write("corridor %.0f %s\n" % (p[1], " ".join("%.6f %.6f" % q for q in p[2])))
    print("wrote", os.path.relpath(path(name), ROOT))


def _overpass(q):
    for url in OVERPASS:
        for k in range(3):
            try:
                data = get(url, urllib.parse.urlencode({"data": q}).encode()).decode("utf-8")
                if "<node " not in data: raise RuntimeError("no data in the answer" + (": " + data[-300:] if "<remark>" in data else ""))
                if "<remark>" in data: raise RuntimeError(re.search(r"<remark>(.*?)</remark>", data, re.S).group(1).strip()[:200])
                return data
            except Exception as ex:
                print("  %s: %s" % (url, ex)); time.sleep(15 * (k + 1))
    sys.exit("no Overpass server answered")


def fetch_osm(name, tile_m=500.0):
    """The OpenStreetMap ways (and their nodes) that reach into the area's corridors, from the OSM API's map call in
    tiles of 500 m along them (as the maps' own files), merged into the map's file: elements already in it stay as they
    are, new ones are added, the bounds grow to the whole area. Relations are left out (route and boundary relations
    would bring whole countries; the map draws its multipolygons from their ways' tags where it can)."""
    import xml.etree.ElementTree as ET
    parts = read(name)
    if not parts: sys.exit("no area file: make one with --route first")
    # (every part: the corridors and the boxes, e.g. a campus added to a city centre; elements already in the file stay)
    corridors = list(parts)
    src = os.path.join(MAPS, name + ".osm.gz")
    text = gzip.open(src).read().decode("utf-8")
    have = set(re.findall(r'<(node|way|relation) id="(\d+)"', text))
    nodes, ways = {}, {}
    ts = tiles(corridors, tile_m, 60.0)
    for k, (s_, w_, n_, e_) in enumerate(ts):
        data = get("https://api.openstreetmap.org/api/0.6/map?bbox=%.6f,%.6f,%.6f,%.6f" % (w_, s_, e_, n_))
        root = ET.fromstring(data)
        for el in root:
            if el.tag == "node": nodes[el.get("id")] = el
            elif el.tag == "way": ways[el.get("id")] = el
        print("  tile %d/%d: %d nodes, %d ways so far" % (k + 1, len(ts), len(nodes), len(ways)), flush=True)
        time.sleep(1.0)   # (gently: the OSM API's usage policy)
    # ways with a node in the corridors (60 m margin), and the nodes they use
    keep_ways, need = [], set()
    for wid, w in ways.items():
        refs = [nd.get("ref") for nd in w.findall("nd")]
        if any(r in nodes and inside(corridors, float(nodes[r].get("lat")), float(nodes[r].get("lon")), 60.0) for r in refs[::max(1, len(refs) // 20)] + refs[-1:]):
            keep_ways.append(w); need.update(refs)
    new_nodes = [nodes[i] for i in need if i in nodes and ("node", i) not in have]
    new_ways = [w for w in keep_ways if ("way", w.get("id")) not in have]
    s, w, n, e = union_bbox(parts, 50.0)
    body = re.sub(r'\s*<bounds[^>]*/>', "", text.split("</osm>")[0])
    head_end = body.index(">", body.index("<osm")) + 1
    out = body[:head_end] + '\n <bounds minlat="%.7f" minlon="%.7f" maxlat="%.7f" maxlon="%.7f"/>' % (s, w, n, e) + body[head_end:]
    # (the simulator reads nodes before the ways that use them: new nodes go before the first way)
    i = out.find("<way ")
    block = "".join(" " + ET.tostring(x, encoding="unicode").strip() + "\n" for x in new_nodes)
    out = (out[:i] + block + " " + out[i:]) if i >= 0 else out + "\n" + block
    out = out.rstrip() + "\n" + "".join(" " + ET.tostring(x, encoding="unicode").strip() + "\n" for x in new_ways) + "</osm>\n"
    with gzip.open(src, "wt", encoding="utf-8") as f: f.write(out)
    print("%s: %d new nodes and %d new ways from the area's parts, bounds %.4f %.4f %.4f %.4f, %.1f MB" % (
        name, len(new_nodes), len(new_ways), s, w, n, e, os.path.getsize(src) / 1e6))


def main():
    a = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    a.add_argument("map")
    a.add_argument("--route", nargs=2, metavar="LAT,LON", help="a corridor along the driving route from one place to another")
    a.add_argument("--via", nargs="*", default=[], metavar="LAT,LON")
    a.add_argument("--width", type=float, default=300.0)
    a.add_argument("--osm", action="store_true", help="download the corridors' OpenStreetMap data into the map")
    a.add_argument("--show", action="store_true")
    a = a.parse_args()
    if a.route:
        pts = [tuple(map(float, x.split(","))) for x in [a.route[0]] + a.via + [a.route[1]]]
        line = simplify(route(pts), 3.0, pts[0][0])
        parts = read(a.map) or [("box", osm_bounds(a.map))]
        parts.append(("corridor", a.width, line))
        write(a.map, parts)
    if a.osm: fetch_osm(a.map)
    if a.show or not (a.route or a.osm):
        parts = read(a.map)
        if not parts: print("%s: no area file (the map is its square)" % a.map); return
        for p in parts: print(p[0], p[1] if p[0] == "box" else "%.0f m wide, %d points" % (p[1], len(p[2])))
        print("bounds %.5f %.5f %.5f %.5f, %d tiles of 500 m" % (union_bbox(parts) + (len(tiles(parts)),)))


if __name__ == "__main__":
    main()
