"""
Downloads an open aerial photo of a city map and saves it, cut to the map's square and in the
map's own projection, as Assets/StreamingAssets/Maps/{map}.aerial.jpg (with {map}.aerial.json).
The map builder (OsmMapGenerator) then uses it as the ground under roads and pavements and on the
roofs, so car parks, yards, squares, roof colours, solar panels and dormers look as they really are.

Sources (open data, no account or key needed; the README credits them):
- the Netherlands: PDOK Luchtfoto (Beeldmateriaal Nederland), CC BY 4.0;
- Belgium (Flanders): Digitaal Vlaanderen orthophoto (Modellicentie Gratis Hergebruik);
- Spain: PNOA, IGN/CNIG, CC BY 4.0;
- Japan: GSI seamless photo (地理院タイル, Geospatial Information Authority of Japan);
- New York State: NYS ITS orthoimagery (public domain);
- the United States: USGS National Map imagery (public domain);
- New South Wales: NSW Spatial Services imagery, CC BY 4.0;
- New Zealand (Wellington, Auckland): LINZ aerial imagery on AWS (cloud-optimised GeoTIFFs, read by HTTP range), CC BY 4.0;
  needs tifffile and imagecodecs (WebP tiles).

Usage (needs Pillow and numpy):  python Tools/aerial_photo.py Delft [--size 4096]
Maps without an open source are listed with --list.
"""
import argparse, gzip, io, json, math, os, re, sys, urllib.request
from concurrent.futures import ThreadPoolExecutor

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from citygml_buildings import to_local  # the same projection as GeoProjection in the simulator

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
MAPS = os.path.join(ROOT, "Assets", "StreamingAssets", "Maps")
UA = {"User-Agent": "coupled-sim aerial_photo tool (https://github.com/bazilinskyy/coupled-sim)"}

SOURCES = {
    "pdok": dict(url="https://service.pdok.nl/hwh/luchtfotorgb/wmts/v1_0/Actueel_orthoHR/EPSG:3857/{z}/{x}/{y}.jpeg", zoom=19,
                 credit="Aerial photo: PDOK Luchtfoto, Beeldmateriaal Nederland (CC BY 4.0)"),
    "flanders": dict(wms="https://geo.api.vlaanderen.be/omw/wms?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=OMWRGB24VL&STYLES=&CRS=EPSG:3857&BBOX={bbox}&WIDTH=256&HEIGHT=256&FORMAT=image/jpeg", zoom=19,
                     credit="Aerial photo: Digitaal Vlaanderen, orthophoto 2024 (Modellicentie Gratis Hergebruik)"),
    "pnoa": dict(url="https://tms-pnoa-ma.idee.es/1.0.0/pnoa-ma/{z}/{x}/{ty}.jpeg", zoom=19,
                 credit="Aerial photo: PNOA, IGN/CNIG Spain (CC BY 4.0)"),
    "gsi": dict(url="https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg", zoom=18,
                credit="Aerial photo: GSI Japan, seamless photo tiles (地理院タイル)"),
    "nys": dict(wms="https://orthos.its.ny.gov/arcgis/rest/services/wms/Latest/MapServer/export?bbox={bbox}&bboxSR=3857&imageSR=3857&size=256,256&format=jpg&f=image", zoom=19,
                credit="Aerial photo: NYS ITS Geospatial Services, latest orthoimagery (public domain)"),
    "usgs": dict(url="https://basemap.nationalmap.gov/arcgis/rest/services/USGSImageryOnly/MapServer/tile/{z}/{y}/{x}", zoom=16,
                 credit="Aerial photo: USGS The National Map (public domain)"),
    "nsw": dict(url="https://maps.six.nsw.gov.au/arcgis/rest/services/public/NSW_Imagery/MapServer/tile/{z}/{y}/{x}", zoom=19,
                credit="Aerial photo: NSW Spatial Services (CC BY 4.0)"),
}
# cloud-optimised GeoTIFF collections (STAC) in NZTM2000
LINZ_SOURCES = {
    "Wellington": ("wellington/wellington_2025_0.075m/rgb/2193/", "Aerial photo: Toitū Te Whenua LINZ, Wellington 0.075 m 2025 (CC BY 4.0)"),
    "Auckland": ("auckland/auckland_2024_0.075m/rgb/2193/", "Aerial photo: Toitū Te Whenua LINZ, Auckland 0.075 m 2024 (CC BY 4.0)"),
}
for _m, (_p, _c) in LINZ_SOURCES.items(): SOURCES["linz-" + _m] = dict(cog=_p, credit=_c)
MAP_SOURCE = {"Wellington": "linz-Wellington", "Auckland": "linz-Auckland", "Delft": "pdok", "Eindhoven": "pdok", "Ghent": "flanders", "Barcelona": "pnoa", "Tokyo": "gsi",
              "NewYork": "nys", "Boulder": "usgs", "Sydney": "nsw"}


def bounds(name):
    head = gzip.open(os.path.join(MAPS, name + ".osm.gz")).read(4000).decode("utf-8", "ignore")
    b = re.search(r'<bounds minlat="(-?[\d.]+)" minlon="(-?[\d.]+)" maxlat="(-?[\d.]+)" maxlon="(-?[\d.]+)"', head)
    if not b: sys.exit("no <bounds> in the map")
    return tuple(map(float, b.groups()))


def centre(name):
    """The map's centre in MapCatalog.cs (the origin of GeoProjection)."""
    src = open(os.path.join(ROOT, "Assets", "Scripts", "Maps", "MapCatalog.cs"), encoding="utf-8").read()
    m = re.search(r'Name = "%s".*?Latitude = (-?[\d.]+), Longitude = (-?[\d.]+)' % re.escape(name), src)
    if not m: sys.exit("map %s is not in MapCatalog.cs" % name)
    return float(m.group(1)), float(m.group(2))


def merc(lat, lon, z):
    n = 2 ** z * 256
    return (lon + 180) / 360 * n, (1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * n


def fetch_tile(src, z, x, y, cache):
    path = os.path.join(cache, "%d_%d_%d.img" % (z, x, y))
    if os.path.exists(path):
        return path
    if "wms" in src:
        r = 20037508.342789244; s = 2 * r / 2 ** z
        url = src["wms"].format(bbox="%.3f,%.3f,%.3f,%.3f" % (-r + x * s, r - (y + 1) * s, -r + (x + 1) * s, r - y * s))
    else:
        url = src["url"].format(z=z, x=x, y=y, ty=2 ** z - 1 - y)
    for attempt in range(4):
        try:
            data = urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=30).read()
            Image.open(io.BytesIO(data)).verify()
            open(path, "wb").write(data)
            return path
        except Exception as e:
            err = e
    print("  tile %d/%d/%d failed: %s" % (z, x, y, err))
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("map", nargs="?")
    ap.add_argument("--size", type=int, default=4096, help="pixels per side of the saved photo")
    ap.add_argument("--zoom", type=int, help="tile zoom level (default: the source's best)")
    ap.add_argument("--list", action="store_true")
    a = ap.parse_args()
    if a.list or not a.map:
        for m, s in MAP_SOURCE.items(): print("%-10s %s" % (m, SOURCES[s]["credit"]))
        return
    if a.map not in MAP_SOURCE: sys.exit("no open aerial photo source for %s (see --list)" % a.map)
    src = SOURCES[MAP_SOURCE[a.map]]
    z = a.zoom or src.get("zoom", 0)
    s_, w_, n_, e_ = bounds(a.map)
    lat0, lon0 = centre(a.map)
    # the square the map builder covers: half size = the farthest corner coordinate (OsmMapGenerator.HalfSize)
    c0 = to_local(s_, w_, lat0, lon0); c1 = to_local(n_, e_, lat0, lon0)
    # plus the 40 m of ground the builder lays around the map, and a margin
    h = max(abs(c0[0]), abs(c0[1]), abs(c1[0]), abs(c1[1])) + 60
    # local metres -> (lat, lon): a quadratic fitted to the forward projection (error far below a pixel)
    pad = 0.035   # about 3 km: the fit also covers the surroundings photo
    lats = np.linspace(s_ - pad, n_ + pad, 15); lons = np.linspace(w_ - pad, e_ + pad, 15)
    LA, LO = np.meshgrid(lats, lons); LA = LA.ravel(); LO = LO.ravel()
    XZ = np.array([to_local(la, lo, lat0, lon0) for la, lo in zip(LA, LO)])
    def basis(x, zz): return np.stack([np.ones_like(x), x, zz, x * x, x * zz, zz * zz], -1)
    B = basis(XZ[:, 0], XZ[:, 1])
    clat = np.linalg.lstsq(B, LA, rcond=None)[0]; clon = np.linalg.lstsq(B, LO, rcond=None)[0]
    cache = os.path.join(os.path.expanduser("~"), ".cache", "coupled-sim-aerial", MAP_SOURCE[a.map])
    os.makedirs(cache, exist_ok=True)
    dst = os.path.join(MAPS, a.map + ".aerial.jpg")
    far_h = 3000.0
    if "cog" in src:
        cog_cut(src, h, a.size, basis, clat, clon, cache, dst, a.map)
        cog_cut(src, far_h, 2048, basis, clat, clon, cache, os.path.join(MAPS, a.map + ".aerial-far.jpg"), a.map)
    else:
        cut(src, z, h, a.size, basis, clat, clon, cache, dst, a.map)
        # the surroundings, for distant views: 6 km square, coarser
        cut(src, max(12, z - 4), far_h, 2048, basis, clat, clon, cache, os.path.join(MAPS, a.map + ".aerial-far.jpg"), a.map)
    json.dump({"halfSize": h, "metresPerPixel": 2 * h / a.size, "zoom": z, "farHalfSize": far_h, "source": MAP_SOURCE[a.map], "credit": src["credit"]},
              open(os.path.join(MAPS, a.map + ".aerial.json"), "w"), indent=1)


def cut(src, z, h, n, basis, clat, clon, cache, dst, name):
    """Saves the square of 2h metres around the map centre, n × n pixels, from tiles at zoom z."""
    corners = [(sx * h, sz * h) for sx in (-1, 1) for sz in (-1, 1)]
    cb = basis(np.array([c[0] for c in corners]), np.array([c[1] for c in corners]))
    px = [merc(la, lo, z) for la, lo in zip(cb @ clat, cb @ clon)]
    tx0, tx1 = int(min(p[0] for p in px) // 256), int(max(p[0] for p in px) // 256)
    ty0, ty1 = int(min(p[1] for p in px) // 256), int(max(p[1] for p in px) // 256)
    tiles = [(x, y) for x in range(tx0, tx1 + 1) for y in range(ty0, ty1 + 1)]
    print("%s: %.0f m square, %d tiles at zoom %d" % (name, 2 * h, len(tiles), z))
    with ThreadPoolExecutor(6) as ex:
        paths = list(ex.map(lambda t: fetch_tile(src, z, t[0], t[1], cache), tiles))
    sizes = [os.path.getsize(p) for p in paths if p]
    if len(sizes) > 8 and max(sizes.count(x) for x in set(sizes)) > len(sizes) / 2:
        sys.exit("most tiles are the same image (the service may have moved or wants a key); nothing saved")
    mosaic = Image.new("RGB", ((tx1 - tx0 + 1) * 256, (ty1 - ty0 + 1) * 256), (90, 90, 90))
    for (x, y), p in zip(tiles, paths):
        if p: mosaic.paste(Image.open(p).convert("RGB").resize((256, 256)), ((x - tx0) * 256, (y - ty0) * 256))
    M = np.asarray(mosaic).astype(np.float32)
    # output pixel (row 0 = north) -> local (x, z) -> (lat, lon) -> mosaic pixel, bilinear
    u = (np.arange(n) + 0.5) / n * 2 * h - h
    X, Zl = np.meshgrid(u, u[::-1])
    Bo = basis(X.ravel(), Zl.ravel())
    la = Bo @ clat; lo = Bo @ clon
    N = 2 ** z * 256
    mx = (lo + 180) / 360 * N - tx0 * 256 - 0.5
    my = (1 - np.arcsinh(np.tan(np.radians(la))) / np.pi) / 2 * N - ty0 * 256 - 0.5
    x0 = np.clip(np.floor(mx).astype(int), 0, M.shape[1] - 2); y0 = np.clip(np.floor(my).astype(int), 0, M.shape[0] - 2)
    fx = np.clip(mx - x0, 0, 1)[:, None]; fy = np.clip(my - y0, 0, 1)[:, None]
    out = (M[y0, x0] * (1 - fx) * (1 - fy) + M[y0, x0 + 1] * fx * (1 - fy) + M[y0 + 1, x0] * (1 - fx) * fy + M[y0 + 1, x0 + 1] * fx * fy)
    Image.fromarray(out.reshape(n, n, 3).clip(0, 255).astype(np.uint8)).save(dst, quality=85, optimize=True)
    print("  %.2f m per pixel, %.1f MB -> %s" % (2 * h / n, os.path.getsize(dst) / 1e6, os.path.relpath(dst, ROOT)))


# ------------------------------------------------------------------ cloud-optimised GeoTIFFs in NZTM2000 (LINZ)

def nztm(lat, lon, lon0=173.0, k0=0.9996, fe=1600000, fn=10000000, a=6378137.0, f=1 / 298.257222101):
    e2 = f * (2 - f); ep2 = e2 / (1 - e2); phi = np.radians(lat); lam = np.radians(lon - lon0)
    N = a / np.sqrt(1 - e2 * np.sin(phi) ** 2); T = np.tan(phi) ** 2; C = ep2 * np.cos(phi) ** 2; A = lam * np.cos(phi)
    M = a * ((1 - e2 / 4 - 3 * e2 ** 2 / 64 - 5 * e2 ** 3 / 256) * phi - (3 * e2 / 8 + 3 * e2 ** 2 / 32 + 45 * e2 ** 3 / 1024) * np.sin(2 * phi)
             + (15 * e2 ** 2 / 256 + 45 * e2 ** 3 / 1024) * np.sin(4 * phi) - (35 * e2 ** 3 / 3072) * np.sin(6 * phi))
    x = fe + k0 * N * (A + (1 - T + C) * A ** 3 / 6 + (5 - 18 * T + T * T + 72 * C - 58 * ep2) * A ** 5 / 120)
    y = fn + k0 * (M + N * np.tan(phi) * (A * A / 2 + (5 - T + 9 * C + 4 * C * C) * A ** 4 / 24 + (61 - 58 * T + T * T + 600 * C - 330 * ep2) * A ** 6 / 720))
    return x, y


def http(url, rng=None):
    h = dict(UA)
    if rng: h["Range"] = "bytes=%d-%d" % rng
    for attempt in range(4):
        try: return urllib.request.urlopen(urllib.request.Request(url, headers=h), timeout=60).read()
        except Exception as e: err = e
    raise err


class RangeFile:
    def __init__(s, u):
        s.u = u; s.p = 0
        s.n = int(urllib.request.urlopen(urllib.request.Request(u, method="HEAD", headers=UA), timeout=60).headers["Content-Length"])
    def seek(s, o, w=0): s.p = o if w == 0 else (s.p + o if w == 1 else s.n + o); return s.p
    def tell(s): return s.p
    BLOCK = 1 << 16   # small reads (TIFF directories) come from 64 kB blocks fetched once; large ones straight
    def read(s, k=-1):
        if k < 0: k = s.n - s.p
        if k <= 0: return b""
        if k >= s.BLOCK:
            a, b = s.p, s.p + k - 1
            d = http(s.u, (a, b)); s.p += len(d); return d
        out = b""
        while k > 0 and s.p < s.n:
            bi = s.p // s.BLOCK
            if not hasattr(s, "cache"): s.cache = {}
            if bi not in s.cache:
                a = bi * s.BLOCK; b = min(s.n, a + s.BLOCK) - 1
                s.cache[bi] = http(s.u, (a, b))
            blk = s.cache[bi]; o = s.p - bi * s.BLOCK
            piece = blk[o:o + k]; out += piece; s.p += len(piece); k -= len(piece)
            if not piece: break
        return out
    def readinto(s, b): d = s.read(len(b)); b[:len(d)] = d; return len(d)
    def seekable(s): return True


def cog_items(prefix, cache):
    """(WGS84 bbox, GeoTIFF url) of every item of a LINZ STAC collection (cached)."""
    path = os.path.join(cache, "items.json")
    if os.path.exists(path): return json.load(open(path))
    base = "https://nz-imagery.s3.ap-southeast-2.amazonaws.com/" + prefix
    col = json.loads(http(base + "collection.json"))
    hrefs = [l["href"] for l in col["links"] if l["rel"] == "item"]
    def item(h):
        it = json.loads(http(base + h.lstrip("./")))
        a = [v["href"] for v in it["assets"].values() if v["href"].endswith((".tif", ".tiff"))][0]
        return [it["bbox"], a if a.startswith("http") else base + a.lstrip("./")]
    with ThreadPoolExecutor(16) as ex: items = list(ex.map(item, hrefs))
    json.dump(items, open(path, "w"))
    return items


def cog_cut(src, h, n, basis, clat, clon, cache, dst, name):
    """Saves the square of 2h metres around the map centre, n x n pixels, from the collection's GeoTIFFs at the overview
    level nearest the needed resolution (only the 512 px tiles under the square are read)."""
    import tifffile
    items = cog_items(src["cog"], cache)
    u = (np.arange(n) + 0.5) / n * 2 * h - h
    X, Zl = np.meshgrid(u, u[::-1])
    Bo = basis(X.ravel(), Zl.ravel()); la = Bo @ clat; lo = Bo @ clon
    E, N = nztm(la, lo)
    out = np.full((n * n, 3), 90.0)
    need = 2 * h / n   # metres per output pixel
    hits = [it for it in items if it[0][0] < lo.max() and it[0][2] > lo.min() and it[0][1] < la.max() and it[0][3] > la.min()]
    print("%s: %.0f m square, %d GeoTIFFs, %.2f m per pixel" % (name, 2 * h, len(hits), need))
    def one(it):
        rf = RangeFile(it[1]); res = []
        with tifffile.TiffFile(rf) as tf:
            p0 = tf.pages[0]; sx = p0.tags["ModelPixelScaleTag"].value[0]; tp = p0.tags["ModelTiepointTag"].value; x0, y0 = tp[3], tp[4]
            levels = [p0] + [p for p in tf.pages[1:] if p.imagewidth < p0.imagewidth]
            pg = levels[0]
            for p in levels:   # the coarsest level still at least as fine as needed
                if sx * p0.imagewidth / p.imagewidth <= need * 1.01: pg = p
            k = sx * p0.imagewidth / pg.imagewidth
            inside = np.where((E >= x0) & (E < x0 + p0.imagewidth * sx) & (N <= y0) & (N > y0 - p0.imagelength * sx))[0]
            if len(inside) == 0: return res
            col = ((E[inside] - x0) / k).astype(int).clip(0, pg.imagewidth - 1); row = ((y0 - N[inside]) / k).astype(int).clip(0, pg.imagelength - 1)
            tw, th = pg.tilewidth, pg.tilelength; ntx = (pg.imagewidth + tw - 1) // tw
            ti = (row // th) * ntx + col // tw
            for t in np.unique(ti):
                rf.seek(pg.dataoffsets[t]); raw = rf.read(pg.databytecounts[t])
                tile = np.asarray(pg.decode(raw, int(t))[0]).reshape(th, tw, -1)[..., :3].astype(np.float32)
                m = ti == t
                res.append((inside[m], tile[row[m] % th, col[m] % tw]))
        return res
    with ThreadPoolExecutor(6) as ex:
        for res in ex.map(one, hits):
            for idx, px in res: out[idx] = px
    Image.fromarray(out.reshape(n, n, 3).clip(0, 255).astype(np.uint8)).save(dst, quality=85, optimize=True)
    print("  %.1f MB -> %s" % (os.path.getsize(dst) / 1e6, os.path.relpath(dst, ROOT)))


if __name__ == "__main__":
    main()
