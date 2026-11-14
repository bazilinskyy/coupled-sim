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
- New South Wales: NSW Spatial Services imagery, CC BY 4.0.

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
MAP_SOURCE = {"Delft": "pdok", "Eindhoven": "pdok", "Ghent": "flanders", "Barcelona": "pnoa", "Tokyo": "gsi",
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
    z = a.zoom or src["zoom"]
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
    cut(src, z, h, a.size, basis, clat, clon, cache, dst, a.map)
    # the surroundings, for distant views: 6 km square, coarser
    far_h = 3000.0
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


if __name__ == "__main__":
    main()
