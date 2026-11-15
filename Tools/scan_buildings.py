#!/usr/bin/env python3
"""
Real 3D buildings for a map from open laser scans, where no open 3D city model exists in a usable format:
- the buildings' footprints (OpenStreetMap; for Ghent the Flemish large-scale base map, GRB "GBG", Digitaal Vlaanderen);
- the height of every point of every roof: the surface model minus the terrain model of a laser scan.
Up to four roof planes are fitted to each building's scan (RANSAC); the roof is the lowest of them (gables, hips,
pyramids, flat), the nearest one where a flat roof has several levels, or flat at the measured height. Written as
Assets/StreamingAssets/Maps/{map}.buildings.bin.gz ("B3DL", the format of Tools/citygml_buildings.py), which the
simulator uses instead of the OpenStreetMap buildings.

Sources (open, no key):
  Ghent       GRB footprints; DHMV II DSM and DTM 1 m (Digitaal Vlaanderen, Modellicentie Gratis Hergebruik)
  Wellington  OSM footprints; LINZ 1 m lidar DSM and DEM (Toitū Te Whenua LINZ, CC BY 4.0)
  Auckland    OSM footprints; LINZ 1 m lidar DSM and DEM (Toitū Te Whenua LINZ, CC BY 4.0)
  London      OSM footprints; Environment Agency LIDAR composite first-return DSM and DTM 1 m (Open Government Licence v3)

  python3 Tools/scan_buildings.py Wellington

Needs numpy, Pillow, SciPy (and tifffile + imagecodecs for the LINZ scans).
"""
import gzip, json, math, os, struct, sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from citygml_buildings import to_local
from scipy.spatial import Delaunay
import terrain_height as th

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
MAPS = os.path.join(ROOT, "Assets", "StreamingAssets", "Maps")
GRB = ("https://geo.api.vlaanderen.be/GRB/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=GRB:GBG"
       "&outputFormat=application/json&srsName=EPSG:4326&count={count}&startIndex={start}&bbox={s},{w},{n},{e},urn:ogc:def:crs:EPSG::4326")
DEM = ("https://geo.api.vlaanderen.be/el-{kind}/wcs?service=WCS&version=1.0.0&request=GetCoverage&coverage=EL.GridCoverage.{KIND}"
       "&format=GeoTIFF&crs=EPSG:4326&bbox={w},{s},{e},{n}&width={px}&height={py}")
STEP = 1.0   # metres between roof points


def grb_footprints(bbox):
    s, w, n, e = bbox
    feats, start = [], 0
    while True:
        page = json.loads(th.get(GRB.format(count=5000, start=start, s=s, w=w, n=n, e=e)))["features"]
        feats += page
        if len(page) < 5000: break
        start += 5000
    return feats


def osm_footprints(name):
    """The map's OpenStreetMap buildings as GeoJSON-like polygons (closed ways and multipolygon relations)."""
    import xml.etree.ElementTree as ET
    root = ET.fromstring(gzip.open(os.path.join(MAPS, name + ".osm.gz")).read())
    nodes = {n.get("id"): (float(n.get("lon")), float(n.get("lat"))) for n in root.iter("node")}
    ways = {}
    for w in root.iter("way"):
        ways[w.get("id")] = ([nd.get("ref") for nd in w.iter("nd")], {t.get("k"): t.get("v") for t in w.iter("tag")})
    def coords(ids): return [nodes[i] for i in ids if i in nodes]
    def join(parts):
        """Rings from way pieces joined end to end."""
        rings, parts = [], [p[:] for p in parts if len(p) > 1]
        while parts:
            r = parts.pop(0)
            while r[0] != r[-1]:
                for k, p in enumerate(parts):
                    if p[0] == r[-1]: r += p[1:]; parts.pop(k); break
                    if p[-1] == r[-1]: r += p[::-1][1:]; parts.pop(k); break
                else: break
            if r[0] == r[-1] and len(r) >= 4: rings.append(r)
        return rings
    feats, in_rel = [], set()
    for rel in root.iter("relation"):
        tags = {t.get("k"): t.get("v") for t in rel.iter("tag")}
        if tags.get("type") != "multipolygon" or "building" not in tags: continue
        outer = [ways[m.get("ref")][0] for m in rel.iter("member") if m.get("type") == "way" and m.get("role") == "outer" and m.get("ref") in ways]
        inner = [ways[m.get("ref")][0] for m in rel.iter("member") if m.get("type") == "way" and m.get("role") == "inner" and m.get("ref") in ways]
        for m in rel.iter("member"): in_rel.add(m.get("ref"))
        holes = [coords(r) for r in join(inner)]
        for r in join(outer): feats.append({"geometry": {"type": "Polygon", "coordinates": [coords(r)] + holes}})
    for wid, (ids, tags) in ways.items():
        if "building" in tags and len(ids) >= 4 and ids[0] == ids[-1] and wid not in in_rel:
            feats.append({"geometry": {"type": "Polygon", "coordinates": [coords(ids)]}})
    return feats


def rasters(kind, bbox):
    s, w, n, e = bbox
    lats = np.array([s, n]); lons = np.array([w, e])
    out = []
    for (bw, bs, be, bn) in th.chunks(lats, lons, 400 / 111320.0, pad=0.0005):
        px = int((be - bw) * 111320 * math.cos(math.radians((bs + bn) / 2)) / 0.5); py = int((bn - bs) * 111320 / 0.5)
        out.append(th.tiff_array(th.get(DEM.format(kind=kind, KIND=kind.upper(), w=bw, s=bs, e=be, n=bn, px=px, py=py))))
    return out


def inside_mask(poly, X, Y):
    """Points (X, Y) inside the polygon (even-odd rule)."""
    c = np.zeros(X.shape, bool)
    for i in range(len(poly)):
        (x1, y1), (x2, y2) = poly[i], poly[i - 1]
        cond = (y1 > Y) != (y2 > Y)
        with np.errstate(divide="ignore", invalid="ignore"):
            c ^= cond & (X < x1 + (Y - y1) * (x2 - x1) / (y2 - y1))
    return c


def ring_local(ring, lat0, lon0, ccw):
    ring = ring[:-1] if ring[0] == ring[-1] else ring
    P = [to_local(la, lo, lat0, lon0) for lo, la in ring]
    area = sum(P[i - 1][0] * P[i][1] - P[i][0] * P[i - 1][1] for i in range(len(P))) / 2
    if (area < 0) == ccw: P = P[::-1]
    return P, abs(area)


def edge_distance(P, X, Y):
    """Distance of points to the polygon's edges."""
    d = np.full(X.shape, np.inf)
    for i in range(len(P)):
        (x1, y1), (x2, y2) = P[i - 1], P[i]
        ex, ey = x2 - x1, y2 - y1; l2 = max(1e-9, ex * ex + ey * ey)
        t = np.clip(((X - x1) * ex + (Y - y1) * ey) / l2, 0, 1)
        d = np.minimum(d, np.hypot(X - x1 - t * ex, Y - y1 - t * ey))
    return d


def fit_planes(x, y, h, rng, tol=0.3):
    """Up to four roof planes (h = a x + b y + c) found one after another by RANSAC, each carrying at least 12% of the scan."""
    A = np.stack([x, y, np.ones_like(x)], -1)
    planes, left, need = [], np.ones(len(h), bool), max(10, int(0.12 * len(h)))
    for _ in range(4):
        idx = np.where(left)[0]
        if len(idx) < need: break
        best = None
        for _ in range(200):
            s3 = rng.choice(idx, 3, replace=False)
            try: p = np.linalg.solve(A[s3], h[s3])
            except np.linalg.LinAlgError: continue
            if math.hypot(p[0], p[1]) > 2.0: continue          # steeper than 63°: not a roof
            inl = idx[np.abs(A[idx] @ p - h[idx]) < tol]
            if best is None or len(inl) > len(best): best = inl
        if best is None or len(best) < need: break
        planes.append(np.linalg.lstsq(A[best], h[best], rcond=None)[0]); left[best] = False
    return planes


def building(rings, height, rng):
    """(roof kind, year, vertices, walls, roofs) of one footprint, or None."""
    lat0, lon0 = building.centre
    P, area = ring_local(rings[0], lat0, lon0, True)               # outside to the right of each edge
    if area < 12: return None
    holes = [ring_local(r, lat0, lon0, False)[0] for r in rings[1:]]  # courtyards: clockwise, so their outside (the yard) is to the right too
    holes = [hp for hp in holes if len(hp) >= 3]
    def inside(X, Y):
        m = inside_mask(P, X, Y)
        for hp in holes: m &= ~inside_mask(hp, X, Y)
        return m
    xs = [p[0] for p in P]; ys = [p[1] for p in P]
    # the scan 0.6 m and more inside the walls (their edges are soft in a 1 m model), every 0.5 m
    fx, fy = np.meshgrid(np.arange(min(xs), max(xs), 0.5), np.arange(min(ys), max(ys), 0.5))
    m = inside(fx, fy)
    dist = edge_distance(P, fx, fy)
    for hp in holes: dist = np.minimum(dist, edge_distance(hp, fx, fy))
    m &= dist > 0.6
    if m.sum() < 8: m = inside(fx, fy)
    fx, fy = fx[m], fy[m]
    if len(fx) < 6: return None
    fh = height(fx, fy)
    ok = ~np.isnan(fh)
    if ok.mean() < 0.7: return None
    fx, fy, fh = fx[ok], fy[ok], fh[ok]
    med = float(np.median(fh))
    if not (2.0 < med < 120): return None
    hi = float(np.percentile(fh, 98))
    planes = fit_planes(fx, fy, fh, rng)
    # the roof: the lowest of its planes (gables, hips, pyramids, flat), else the plane nearest the scan at each
    # point (roofs of several levels), else flat at the median height
    model = None
    if planes:
        Afit = np.stack([fx, fy, np.ones_like(fx)], -1)
        pred = np.stack([Afit @ p for p in planes])
        flat = all(math.hypot(p[0], p[1]) < 0.12 for p in planes)
        err_min = np.median(np.abs(pred.min(0) - fh))
        if err_min < 0.4: model = "min"
        elif flat and np.median(np.abs(pred[np.argmin(np.abs(pred - fh), 0), np.arange(len(fh))] - fh)) < 0.4: model = "nearest"   # flat roofs at several levels
        elif err_min < 0.9: model = "min"
    def roof_at(X, Y):
        if model is None: return np.full(X.shape, med)
        pr = np.stack([p[0] * X + p[1] * Y + p[2] for p in planes])
        if model == "min": v = pr.min(0)
        else:
            sc = height(X, Y); sc = np.where(np.isnan(sc), med, sc)
            v = pr[np.argmin(np.abs(pr - sc), 0), np.arange(X.size)]
        return np.clip(v, 2.0, hi + 0.5)
    # roof vertices: every ring's edge each metre, and a 1 m grid inside
    ringsP = [P] + holes
    bnd, ring_of = [], []
    for ri, R in enumerate(ringsP):
        for i in range(len(R)):
            (x1, y1), (x2, y2) = R[i], R[(i + 1) % len(R)]
            k = max(1, int(math.hypot(x2 - x1, y2 - y1) / STEP))
            bnd += [(x1 + (x2 - x1) * t / k, y1 + (y2 - y1) * t / k) for t in range(k)]
            ring_of += [ri] * k
    bnd = np.array(bnd); ring_of = np.array(ring_of)
    gx, gy = np.meshgrid(np.arange(min(xs) + STEP / 2, max(xs), STEP), np.arange(min(ys) + STEP / 2, max(ys), STEP))
    gm = inside(gx, gy)
    gd = edge_distance(P, gx, gy)
    for hp in holes: gd = np.minimum(gd, edge_distance(hp, gx, gy))
    gm &= gd > 0.3
    grid = np.stack([gx[gm], gy[gm]], -1)
    pts = np.concatenate([bnd, grid]) if len(grid) else bnd
    hs = roof_at(pts[:, 0], pts[:, 1])
    T = Delaunay(pts).simplices
    A3 = pts[T]
    test = np.concatenate([A3.mean(1), (A3[:, 0] + A3[:, 1]) / 2, (A3[:, 1] + A3[:, 2]) / 2, (A3[:, 2] + A3[:, 0]) / 2])
    T = T[inside(test[:, 0], test[:, 1]).reshape(4, -1).all(0)]   # only triangles inside the footprint
    if len(T) == 0: return None
    A3 = pts[T]
    cw = (A3[:, 1, 0] - A3[:, 0, 0]) * (A3[:, 2, 1] - A3[:, 0, 1]) - (A3[:, 1, 1] - A3[:, 0, 1]) * (A3[:, 2, 0] - A3[:, 0, 0]) < 0
    T[cw] = T[cw][:, [0, 2, 1]]   # counter-clockwise from above
    nb, nr = len(bnd), len(pts)
    verts = [(float(x), float(y), float(h)) for (x, y), h in zip(pts, hs)]
    verts += [(float(x), float(y), 0.0) for x, y in bnd]          # the wall feet
    walls = []
    for i in range(nb):
        j = i + 1
        if j >= nb or ring_of[j] != ring_of[i]: j = int(np.where(ring_of == ring_of[i])[0][0])   # back to the ring's start
        walls += [(nr + i, nr + j, j), (nr + i, j, i)]   # counter-clockwise seen from outside
    V = np.array(verts)[T]
    nrm = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0])
    sl = np.linalg.norm(nrm[:, :2], axis=1) / np.maximum(1e-6, np.abs(nrm[:, 2])); ar = np.linalg.norm(nrm, axis=1) / 2
    sloped = ar[(sl > 0.27) & (sl < 3)].sum() > 0.5 * ar.sum()
    building.models[model or "flat"] = building.models.get(model or "flat", 0) + 1
    return (1 if sloped else 0, 0, verts, walls, T.tolist())
building.models = {}


FLANDERS = lambda kind: th.service_sampler(lambda w, s, e, n, px, py: DEM.format(kind=kind, KIND=kind.upper(), w=w, s=s, e=e, n=n, px=px, py=py), 0.5, 400)
EA_DSM = ("https://environment.data.gov.uk/spatialdata/lidar-composite-digital-surface-model-first-return-dsm-1m/wcs?service=WCS&version=2.0.1"
          "&request=GetCoverage&coverageId=df4e3ec3-315e-48aa-aaaf-b5ae74d7b2bb__Lidar_Composite_Elevation_FZ_DSM_1m&format=image/tiff"
          "&subset=Long({w},{e})&subset=Lat({s},{n})&subsettingCrs=http://www.opengis.net/def/crs/EPSG/0/4326&outputCrs=http://www.opengis.net/def/crs/EPSG/0/4326")
SOURCES = {
    "Ghent": dict(footprints="grb", dsm=lambda: FLANDERS("dsm"), dtm=lambda: FLANDERS("dtm")),
    "Wellington": dict(footprints="osm", dsm=lambda: th.linz_prefix_sampler("wellington/wellington-city_2025/dsm_1m/2193/"),
                       dtm=lambda: th.linz_prefix_sampler(th.LINZ["Wellington"])),
    "London": dict(footprints="osm", dsm=lambda: th.service_sampler(th.url(EA_DSM), 1.0, 400, workers=1), dtm=lambda: th.service_sampler(th.url(th.EA), 1.0, 400, workers=1)),
    "Auckland": dict(footprints="osm", dsm=lambda: th.linz_prefix_sampler("auckland/auckland-part-1_2024/dsm_1m/2193/"),
                     dtm=lambda: th.linz_prefix_sampler(th.LINZ["Auckland"])),
}


def main():
    name = sys.argv[1] if len(sys.argv) > 1 else "Ghent"
    if name not in SOURCES: sys.exit("no laser scan source for %s (%s)" % (name, ", ".join(SOURCES)))
    src = SOURCES[name]
    lat0, lon0 = th.centre(name)
    s, w, n, e = th.bounds(name)
    bbox = (s - 0.001, w - 0.0015, n + 0.001, e + 0.0015)
    feats = grb_footprints(bbox) if src["footprints"] == "grb" else osm_footprints(name)
    print("%s: %d footprints (%s)" % (name, len(feats), src["footprints"].upper()))
    from scipy.ndimage import map_coordinates
    # heights above the ground on one grid over the map, 0.5 m (local metres <-> geographic as Tools/terrain_height.py)
    xa, ya = to_local(bbox[0], bbox[1], lat0, lon0); xb, yb = to_local(bbox[2], bbox[3], lat0, lon0)
    res = 0.5
    gxs = np.arange(xa, xb, res); gys = np.arange(ya, yb, res)
    GX, GY = np.meshgrid(gxs, gys)
    GLA = lat0 + GY / 111320.0; GLO = lon0 + GX / (111320.0 * math.cos(math.radians(lat0)))
    NDSM = src["dsm"]()(GLA, GLO) - src["dtm"]()(GLA, GLO)
    del GX, GY, GLA, GLO
    print("  height grid %d x %d" % NDSM.shape[::-1])
    def height(x, y):
        return map_coordinates(NDSM, [(np.asarray(y) - ya) / res, (np.asarray(x) - xa) / res], order=1, cval=np.nan)
    rng = np.random.default_rng(1)
    building.centre = (lat0, lon0)
    buildings = []
    for f in feats:
        g = f["geometry"]
        polys = [g["coordinates"]] if g["type"] == "Polygon" else g["coordinates"]
        for rings in polys:
            b = building(rings, height, rng)
            if b: buildings.append(b)
    out = os.path.join(MAPS, "%s.buildings.bin.gz" % name)
    with gzip.open(out, "wb") as fo:
        fo.write(b"B3DL"); fo.write(struct.pack("<ddi", lat0, lon0, len(buildings)))
        for kind, year, verts, walls, roofs in buildings:
            fo.write(struct.pack("<Bhi", kind, year, len(verts)))
            fo.write(struct.pack("<%df" % (3 * len(verts)), *[c for v in verts for c in v]))
            for tl in (walls, roofs):
                fo.write(struct.pack("<i", len(tl)))
                fo.write(struct.pack("<%di" % (3 * len(tl)), *[i for t in tl for i in t]))
    print("%s: %d buildings (%d with sloping roofs; roofs %s), %.1f MB -> %s" % (name, len(buildings), sum(b[0] for b in buildings),
          building.models, os.path.getsize(out) / 1e6, os.path.relpath(out, ROOT)))


if __name__ == "__main__":
    main()
