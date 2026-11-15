#!/usr/bin/env python3
"""
The terrain heights of a city map, from open bare-earth elevation models (no buildings or trees in them), no key needed:
  - Eindhoven, Delft: AHN4 (0.5 m, PDOK, CC0)
  - New York, Boulder: USGS 3DEP (1 m lidar, public domain)
  - London: Environment Agency LIDAR Composite DTM (1 m, Open Government Licence v3)
  - Ghent: Digitaal Vlaanderen DHMV II DTM (1 m, Modellicentie Gratis Hergebruik)
  - Barcelona: IGN MDT05 (5 m, CC BY 4.0)
  - Tokyo: GSI elevation tiles DEM1A / DEM5A (1 m / 5 m)
  - Sydney: Geoscience Australia 5 m lidar DEM (CC BY 4.0)
  - Montreal: NRCan HRDEM (1-2 m lidar, Open Government Licence - Canada)
  - Wellington, Auckland: LINZ 1 m lidar DEM (CC BY 4.0); needs `pip install tifffile imagecodecs` (LERC compression)
  - Montevideo: IDE Uruguay 1 m lidar DTM (Licencia de Datos Abiertos de Uruguay)
  - elsewhere: GEDTM30 (30 m global bare-earth model, OpenGeoHub, CC BY 4.0), or with --terrarium the Terrain Tiles
    (Mapzen/Tilezen: SRTM etc.; surface models in cities, so buildings are filtered out).

Writes Assets/StreamingAssets/Maps/{map}.height.bin.gz: a grid in the simulator's local metres around the map
centre, smoothed, relative to the ground at the centre. The simulator lifts the whole map onto it (TerrainHeight).

  python3 Tools/terrain_height.py Wellington
  python3 Tools/terrain_height.py --all

File: "HGT1", lat0, lon0 (float64), x0, z0, step (float32), nx, nz (int32), credit (int32 length + UTF-8),
then nz rows of nx float32 heights (row k at z0 + k*step).
"""
import argparse, gzip, io, json, math, os, re, struct, sys, time, urllib.request, zlib
from concurrent.futures import ThreadPoolExecutor
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAPS = os.path.join(ROOT, "Assets", "StreamingAssets", "Maps")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from citygml_buildings import to_local  # the simulator's projection

UA = {"User-Agent": "coupled-sim-terrain/1.0 (open-source driving simulator)"}
NL = {"Eindhoven", "Delft"}
CREDITS = {
    "usgs": "USGS 3D Elevation Program, 1 m lidar DEM (public domain)",
    "ea": "Environment Agency LIDAR Composite DTM 1 m (2022), © Environment Agency copyright and/or database right, Open Government Licence v3",
    "flanders": "Digitaal Vlaanderen, Digitaal Hoogtemodel Vlaanderen II DTM 1 m (Modellicentie Gratis Hergebruik)",
    "ign": "MDT05 © Instituto Geográfico Nacional (scne.es), CC BY 4.0",
    "gsi": "出典：国土地理院 (GSI elevation tiles DEM1A/DEM5A)",
    "ga": "© Commonwealth of Australia (Geoscience Australia) 2025, 5 m lidar DEM, CC BY 4.0",
    "nrcan": "Natural Resources Canada, HRDEM, Open Government Licence - Canada",
    "linz": "Sourced from Toitū Te Whenua Land Information New Zealand, 1 m lidar DEM, CC BY 4.0",
    "uy": "IDE Uruguay, MDT 1 m (Remesa 01), Licencia de Datos Abiertos de Uruguay",
    "gedtm": "GEDTM30 v1.2 global digital terrain model (OpenGeoHub, Ho et al. 2025), CC BY 4.0",
}
# map -> (source, grid step m, smoothing sigma m)
SOURCE = {"Eindhoven": ("ahn", 2, 6), "Delft": ("ahn", 2, 6), "NewYork": ("usgs", 2, 4), "Boulder": ("usgs", 2, 4),
          "London": ("ea", 2, 4), "Ghent": ("flanders", 2, 4), "Barcelona": ("ign", 2, 8), "Tokyo": ("gsi", 2, 4),
          "Sydney": ("ga", 2, 5), "Montreal": ("nrcan", 2, 4), "Wellington": ("linz", 2, 4), "Auckland": ("linz", 2, 4),
          "Montevideo": ("uy", 2, 4)}
TERRARIUM_CREDIT = ("Terrain Tiles (Mapzen/Tilezen, AWS Open Data): includes SRTM and GMTED2010 courtesy of the U.S. Geological Survey, "
                    "3DEP, EU-DEM (Copernicus, funded by the European Union), Geoscience Australia, Open Government Licence - Canada, "
                    "and Environment Agency (UK) data; see github.com/tilezen/joerd/blob/master/docs/attribution.md")
AHN_CREDIT = "AHN4 digital terrain model (Actueel Hoogtebestand Nederland, via PDOK, CC0)"


def get(url, headers=None, tries=6):
    for k in range(tries):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={**UA, **(headers or {})}), timeout=180) as r:
                return r.read()
        except Exception as e:
            if k == tries - 1: raise
            busy = "429" in str(e)   # rate limited: wait longer
            print("  retry (%s)" % e); time.sleep((20 if busy else 3) * (k + 1))


def bounds(name):
    import gzip as gz
    head = gz.open(os.path.join(MAPS, name + ".osm.gz")).read(4000).decode("utf-8", "ignore")
    b = re.search(r'<bounds minlat="(-?[\d.]+)" minlon="(-?[\d.]+)" maxlat="(-?[\d.]+)" maxlon="(-?[\d.]+)"', head)
    return tuple(map(float, b.groups()))


def centre(name):
    src = open(os.path.join(ROOT, "Assets", "Scripts", "Maps", "MapCatalog.cs"), encoding="utf-8").read()
    m = re.search(r'Name = "%s".*?Latitude = (-?[\d.]+), Longitude = (-?[\d.]+)' % re.escape(name), src)
    return float(m.group(1)), float(m.group(2))


def maps():
    src = open(os.path.join(ROOT, "Assets", "Scripts", "Maps", "MapCatalog.cs"), encoding="utf-8").read()
    return re.findall(r'Name = "([A-Za-z]+)", City', src)


# ------------------------------------------------------------------ WGS84 <-> RD (Schreutelkamp & Strang van Hees)

def wgs_to_rd(lat, lon):
    dphi = 0.36 * (lat - 52.15517440); dlam = 0.36 * (lon - 5.38720621)
    x = (190094.945 * dlam - 11832.228 * dphi * dlam - 114.221 * dphi ** 2 * dlam - 32.391 * dlam ** 3
         - 0.705 * dphi - 2.340 * dphi ** 3 * dlam - 0.608 * dphi * dlam ** 3 + 0.008 * dlam ** 2 + 0.148 * dphi ** 2 * dlam ** 3)
    y = (309056.544 * dphi + 3638.893 * dlam ** 2 + 73.077 * dphi ** 2 - 157.984 * dphi * dlam ** 2 + 59.788 * dphi ** 3
         + 0.433 * dlam - 6.439 * dphi ** 2 * dlam ** 2 - 0.032 * dphi * dlam + 0.092 * dlam ** 4 - 0.054 * dphi * dlam ** 4)
    return 155000 + x, 463000 + y


# ------------------------------------------------------------------ sources: height at (lat, lon) arrays

def fill_gaps(a):
    """NoData (buildings, water in a DTM) filled from the surrounding ground, by repeated neighbour averaging."""
    bad = ~np.isfinite(a) | (a > 1e4) | (a < -1e3)
    a = np.where(bad, np.nan, a)
    for _ in range(400):
        if not np.isnan(a).any(): break
        p = np.pad(a, 1, mode="edge")
        nb = np.stack([p[:-2, 1:-1], p[2:, 1:-1], p[1:-1, :-2], p[1:-1, 2:]])
        m = np.nanmean(np.where(np.isnan(nb), np.nan, nb), axis=0)
        a = np.where(np.isnan(a), m, a)
    return np.nan_to_num(a, nan=float(np.nanmedian(a)) if np.isfinite(a).any() else 0.0)


def ahn_sampler(lats, lons):
    xs, ys = zip(*[wgs_to_rd(la, lo) for la, lo in zip(lats.ravel(), lons.ravel())])
    x0, x1, y0, y1 = min(xs) - 20, max(xs) + 20, min(ys) - 20, max(ys) + 20
    res = 2.0
    tiles, tile = {}, 1000.0
    for tx in np.arange(math.floor(x0 / tile) * tile, x1, tile):
        for ty in np.arange(math.floor(y0 / tile) * tile, y1, tile):
            url = ("https://service.pdok.nl/rws/ahn/wcs/v1_0?service=WCS&request=GetCoverage&version=2.0.1&coverageId=dtm_05m"
                   "&subset=x(%d,%d)&subset=y(%d,%d)&scalefactor=0.25&format=image/tiff" % (tx, tx + tile, ty, ty + tile))
            im = Image.open(io.BytesIO(get(url)))
            tiles[(tx, ty)] = fill_gaps(np.array(im, dtype=np.float64))
            print("  AHN tile", int(tx), int(ty), im.size)
    out = np.zeros(len(xs))
    for i, (x, y) in enumerate(zip(xs, ys)):
        tx = math.floor(x / tile) * tile; ty = math.floor(y / tile) * tile
        a = tiles.get((tx, ty))
        if a is None: continue
        n = a.shape[0]
        c = (x - tx) / res - 0.5; r = (ty + tile - y) / res - 0.5     # rows from the top (north)
        c0, r0 = int(np.clip(math.floor(c), 0, n - 2)), int(np.clip(math.floor(r), 0, n - 2))
        fc, fr = np.clip(c - c0, 0, 1), np.clip(r - r0, 0, 1)
        out[i] = (a[r0, c0] * (1 - fc) * (1 - fr) + a[r0, c0 + 1] * fc * (1 - fr) + a[r0 + 1, c0] * (1 - fc) * fr + a[r0 + 1, c0 + 1] * fc * fr)
    return out.reshape(lats.shape)


def terrarium_sampler(lats, lons, z=15):
    n = 2 ** z
    def tile_xy(lat, lon):
        x = (lon + 180) / 360 * n
        y = (1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * n
        return x, y
    cache = {}
    def tile(tx, ty):
        if (tx, ty) not in cache:
            img = Image.open(io.BytesIO(get("https://s3.amazonaws.com/elevation-tiles-prod/terrarium/%d/%d/%d.png" % (z, tx, ty)))).convert("RGB")
            a = np.array(img, dtype=np.float64)
            cache[(tx, ty)] = a[:, :, 0] * 256 + a[:, :, 1] + a[:, :, 2] / 256 - 32768
        return cache[(tx, ty)]
    out = np.zeros(lats.size)
    for i, (la, lo) in enumerate(zip(lats.ravel(), lons.ravel())):
        fx, fy = tile_xy(la, lo)
        px, py = fx * 256 - 0.5, fy * 256 - 0.5
        x0, y0 = int(math.floor(px)), int(math.floor(py))
        def at(x, y): return tile(x // 256, y // 256)[y % 256, x % 256]
        tx_, ty_ = px - x0, py - y0
        out[i] = (at(x0, y0) * (1 - tx_) * (1 - ty_) + at(x0 + 1, y0) * tx_ * (1 - ty_) + at(x0, y0 + 1) * (1 - tx_) * ty_ + at(x0 + 1, y0 + 1) * tx_ * ty_)
    print("  terrarium tiles:", len(cache))
    return out.reshape(lats.shape)


# ------------------------------------------------------------------ georeferenced rasters (services answering in EPSG:4326)

def tiff_geo(im):
    """(x of the left edge, y of the top edge, pixel width, pixel height) from GeoTIFF tags."""
    t = im.tag_v2
    if 33550 in t and 33922 in t:
        sx, sy = t[33550][0], t[33550][1]; tp = t[33922]
        return tp[3] - tp[0] * sx, tp[4] + tp[1] * sy, sx, sy
    m = t[34264]
    return m[3], m[7], m[0], -m[5]


def tiff_array(raw, big_endian_data=False):
    im = Image.open(io.BytesIO(raw))
    geo = tiff_geo(im)
    if big_endian_data:
        # NRCan's WCS: a little-endian TIFF header with big-endian floats in uncompressed tiles
        t = im.tag_v2; w, h = t[256], t[257]; tw, th = t.get(322, w), t.get(323, h)
        offs, cnts = t.get(324) or t.get(273), t.get(325) or t.get(279)
        a = np.full((((h + th - 1) // th) * th, ((w + tw - 1) // tw) * tw), np.nan)
        ntx = (w + tw - 1) // tw
        for i, (o, c) in enumerate(zip(offs, cnts)):
            tile = np.frombuffer(raw[o:o + c], ">f4").reshape(th, tw)
            r, q = divmod(i, ntx); a[r * th:(r + 1) * th, q * tw:(q + 1) * tw] = tile
        a = a[:h, :w]
    else:
        a = np.array(im, dtype=np.float64)
    a[(a < -1000) | (a > 9000)] = np.nan
    return a, geo


def sample_rasters(rasters, xs, ys):
    """Bilinear heights at (xs, ys) in the rasters' coordinates; NaN where none covers a point."""
    from scipy.ndimage import map_coordinates
    out = np.full(xs.shape, np.nan)
    for a, (x0, y0, sx, sy) in rasters:
        c = (xs - x0) / sx - 0.5; r = (y0 - ys) / sy - 0.5
        inside = (c >= 0) & (r >= 0) & (c <= a.shape[1] - 1) & (r <= a.shape[0] - 1) & np.isnan(out)
        if inside.any():
            filled = np.where(np.isnan(a), np.nanmean(a) if np.isfinite(a).any() else 0, a)
            v = map_coordinates(filled, [r[inside], c[inside]], order=1)
            bad = map_coordinates(np.isnan(a).astype(float), [r[inside], c[inside]], order=1) > 0.5
            v[bad] = np.nan
            out[inside] = v
    return out


def chunks(lats, lons, size_deg_lat, pad=0.0002):
    """The grid's lat/lon box in chunks of about size_deg_lat (keeps each request small)."""
    s, n, w, e = lats.min() - pad, lats.max() + pad, lons.min() - pad, lons.max() + pad
    k = max(1, int(math.ceil((n - s) / size_deg_lat)))
    dlon = (e - w) / k; dlat = (n - s) / k
    return [(w + i * dlon, s + j * dlat, w + (i + 1) * dlon, s + (j + 1) * dlat) for i in range(k) for j in range(k)]


def service_sampler(url_of, res_m, chunk_m=500, big_endian=False, workers=4):
    def sampler(lats, lons):
        rasters = []
        boxes = chunks(lats, lons, chunk_m / 111320.0)
        lat0 = float(lats.mean())
        def fetch(b):
            w, s, e, n = b
            px = max(16, int((e - w) * 111320 * math.cos(math.radians(lat0)) / res_m)); py = max(16, int((n - s) * 111320 / res_m))
            return tiff_array(get(url_of(w, s, e, n, px, py)), big_endian)
        with ThreadPoolExecutor(workers) as ex:
            for r in ex.map(fetch, boxes): rasters.append(r)
        print("  %d rasters" % len(rasters))
        return sample_rasters(rasters, lons, lats)
    return sampler


USGS = ("https://elevation.nationalmap.gov/arcgis/rest/services/3DEPElevation/ImageServer/exportImage?bbox={w},{s},{e},{n}"
        "&bboxSR=4326&imageSR=4326&size={px},{py}&format=tiff&pixelType=F32&noData=-9999&interpolation=RSP_BilinearInterpolation&f=image")
EA = ("https://environment.data.gov.uk/spatialdata/lidar-composite-digital-terrain-model-dtm-1m/wcs?service=WCS&version=2.0.1"
      "&request=GetCoverage&coverageId=13787b9a-26a4-4775-8523-806d13af58fc__Lidar_Composite_Elevation_DTM_1m&format=image/tiff"
      "&subset=Long({w},{e})&subset=Lat({s},{n})&subsettingCrs=http://www.opengis.net/def/crs/EPSG/0/4326&outputCrs=http://www.opengis.net/def/crs/EPSG/0/4326")
FLANDERS = ("https://geo.api.vlaanderen.be/el-dtm/wcs?service=WCS&version=1.0.0&request=GetCoverage&coverage=EL.GridCoverage.DTM"
            "&format=GeoTIFF&crs=EPSG:4326&bbox={w},{s},{e},{n}&width={px}&height={py}")
IGN = ("https://servicios.idee.es/wcs-inspire/mdt?service=WCS&version=1.0.0&request=GetCoverage&coverage=Elevacion4258_5"
       "&format=image/tiff&crs=EPSG:4326&bbox={w},{s},{e},{n}&width={px}&height={py}")
GA = ("https://services.ga.gov.au/gis/services/DEM_LiDAR_5m_2025/MapServer/WCSServer?SERVICE=WCS&VERSION=1.0.0&REQUEST=GetCoverage"
      "&COVERAGE=1&FORMAT=GeoTIFF&CRS=EPSG:4283&BBOX={w},{s},{e},{n}&RESX=0.00005&RESY=0.00005")
NRCAN = ("https://datacube.services.geo.ca/ows/elevation?service=WCS&version=1.1.1&request=GetCoverage&identifier=dtm"
         "&format=image/geotiff&BoundingBox={s},{w},{n},{e},urn:ogc:def:crs:EPSG::4326")


def url(template):
    return lambda w, s, e, n, px, py: template.format(w="%.7f" % w, s="%.7f" % s, e="%.7f" % e, n="%.7f" % n, px=px, py=py)


# ------------------------------------------------------------------ Japan: GSI elevation tiles

def gsi_sampler(lats, lons):
    out = np.full(lats.shape, np.nan)
    for layer, z in (("dem1a_png", 17), ("dem5a_png", 15), ("dem_png", 14)):
        n = 2 ** z; cache = {}
        def tile(tx, ty):
            if (tx, ty) not in cache:
                try:
                    a = np.asarray(Image.open(io.BytesIO(get("https://cyberjapandata.gsi.go.jp/xyz/%s/%d/%d/%d.png" % (layer, z, tx, ty), tries=2))).convert("RGB")).astype(np.int64)
                    v = a[..., 0] * 65536 + a[..., 1] * 256 + a[..., 2]
                    h = np.where(v < 2 ** 23, v * 0.01, (v - 2 ** 24) * 0.01).astype(np.float64)
                    h[v == 2 ** 23] = np.nan
                except Exception:
                    h = np.full((256, 256), np.nan)
                cache[(tx, ty)] = h
            return cache[(tx, ty)]
        todo = np.isnan(out)
        fx = (lons[todo] + 180) / 360 * n * 256 - 0.5
        fy = (1 - np.arcsinh(np.tan(np.radians(lats[todo]))) / math.pi) / 2 * n * 256 - 0.5
        x0, y0 = np.floor(fx).astype(int), np.floor(fy).astype(int); tx_, ty_ = fx - x0, fy - y0
        def at(x, y): return np.array([tile(xi // 256, yi // 256)[yi % 256, xi % 256] for xi, yi in zip(x, y)])
        v = (at(x0, y0) * (1 - tx_) * (1 - ty_) + at(x0 + 1, y0) * tx_ * (1 - ty_) + at(x0, y0 + 1) * (1 - tx_) * ty_ + at(x0 + 1, y0 + 1) * tx_ * ty_)
        out[todo] = v
        print("  %s: %d tiles, %.1f%% still missing" % (layer, len(cache), 100 * np.isnan(out).mean()))
        if not np.isnan(out).any(): break
    return out


# ------------------------------------------------------------------ transverse Mercator (NZTM2000, UTM) without pyproj

def tm(lat, lon, lon0, k0, fe, fn, a=6378137.0, f=1 / 298.257222101):
    e2 = f * (2 - f); ep2 = e2 / (1 - e2); phi = np.radians(lat); lam = np.radians(lon - lon0)
    N = a / np.sqrt(1 - e2 * np.sin(phi) ** 2); T = np.tan(phi) ** 2; C = ep2 * np.cos(phi) ** 2; A = lam * np.cos(phi)
    M = a * ((1 - e2 / 4 - 3 * e2 ** 2 / 64 - 5 * e2 ** 3 / 256) * phi - (3 * e2 / 8 + 3 * e2 ** 2 / 32 + 45 * e2 ** 3 / 1024) * np.sin(2 * phi)
             + (15 * e2 ** 2 / 256 + 45 * e2 ** 3 / 1024) * np.sin(4 * phi) - (35 * e2 ** 3 / 3072) * np.sin(6 * phi))
    x = fe + k0 * N * (A + (1 - T + C) * A ** 3 / 6 + (5 - 18 * T + T * T + 72 * C - 58 * ep2) * A ** 5 / 120)
    y = fn + k0 * (M + N * np.tan(phi) * (A * A / 2 + (5 - T + 9 * C + 4 * C * C) * A ** 4 / 24 + (61 - 58 * T + T * T + 600 * C - 330 * ep2) * A ** 6 / 720))
    return x, y


class RangeFile:
    """A remote file read with HTTP range requests (for tifffile)."""
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
            d = get(s.u, {"Range": "bytes=%d-%d" % (a, b)}); s.p += len(d); return d
        out = b""
        while k > 0 and s.p < s.n:
            bi = s.p // s.BLOCK
            if not hasattr(s, "cache"): s.cache = {}
            if bi not in s.cache:
                a = bi * s.BLOCK; b = min(s.n, a + s.BLOCK) - 1
                s.cache[bi] = get(s.u, {"Range": "bytes=%d-%d" % (a, b)})
            blk = s.cache[bi]; o = s.p - bi * s.BLOCK
            piece = blk[o:o + k]; out += piece; s.p += len(piece); k -= len(piece)
            if not piece: break
        return out
    def readinto(s, b): d = s.read(len(b)); b[:len(d)] = d; return len(d)
    def seekable(s): return True


# ------------------------------------------------------------------ New Zealand: LINZ lidar DEM (cloud-optimised GeoTIFFs on AWS)

LINZ = {"Wellington": "wellington/wellington-city_2025/dem_1m/2193/", "Auckland": "auckland/auckland-part-1_2024/dem_1m/2193/"}

def linz_sampler(name):
    return linz_prefix_sampler(LINZ[name])


def linz_prefix_sampler(prefix):
    """A LINZ elevation collection (DEM or DSM) on AWS: heights at (lats, lons)."""
    def sampler(lats, lons):
        import tifffile, imagecodecs
        base = "https://nz-elevation.s3.ap-southeast-2.amazonaws.com/" + prefix
        col = json.loads(get(base + "collection.json"))
        items = [l["href"] for l in col["links"] if l["rel"] == "item"]
        s, n, w, e = lats.min(), lats.max(), lons.min(), lons.max()
        def item(h): return json.loads(get(base + h.lstrip("./")))
        with ThreadPoolExecutor(16) as ex:
            hits = [it for it in ex.map(item, items) if it["bbox"][0] < e and it["bbox"][2] > w and it["bbox"][1] < n and it["bbox"][3] > s]
        X, Y = tm(lats, lons, 173.0, 0.9996, 1600000, 10000000)
        rasters = []
        for it in hits:
            href = [a["href"] for a in it["assets"].values() if a["href"].endswith(".tiff")][0]
            u = href if href.startswith("http") else base + href.lstrip("./")
            rf = RangeFile(u)
            with tifffile.TiffFile(rf) as tf:
                pg = tf.pages[0]; sx, sy, _ = pg.tags["ModelPixelScaleTag"].value; tp = pg.tags["ModelTiepointTag"].value
                x0, y0 = tp[3], tp[4]; tw = pg.tilewidth; ntx = (pg.imagewidth + tw - 1) // tw; nty = (pg.imagelength + tw - 1) // tw
                c0 = max(0, int((X.min() - x0) / sx) // tw); c1 = min(ntx - 1, int((X.max() - x0) / sx) // tw)
                r0 = max(0, int((y0 - Y.max()) / sy) // tw); r1 = min(nty - 1, int((y0 - Y.min()) / sy) // tw)
                if c1 < c0 or r1 < r0: continue
                a = np.full(((r1 - r0 + 1) * tw, (c1 - c0 + 1) * tw), np.nan)
                for r in range(r0, r1 + 1):
                    for c in range(c0, c1 + 1):
                        ti = r * ntx + c; rf.seek(pg.dataoffsets[ti]); raw = rf.read(pg.databytecounts[ti])
                        t = np.asarray(imagecodecs.lerc_decode(raw) if pg.compression == 34887 else pg.decode(raw, ti)[0], dtype=np.float64).squeeze()
                        a[(r - r0) * tw:(r - r0 + 1) * tw, (c - c0) * tw:(c - c0 + 1) * tw] = t[:tw, :tw]
                a[a < -1000] = np.nan
                rasters.append((a, (x0 + c0 * tw * sx, y0 - r0 * tw * sy, sx, sy)))
                print("  LINZ", u.rsplit("/", 1)[-1], a.shape)
        return sample_rasters(rasters, X, Y)
    return sampler


# ------------------------------------------------------------------ Montevideo: IDE Uruguay lidar DTM sheets (UTM 21S)

UY = "https://visualizador.ide.uy/descargas/datos/CU_Remesa_01/01_MDT_MDS/01_Ciudad_MVD/04_MDT_GeoTIFF_Resolucion_100cm/%s_MDT_Remesa_01_MVD.tif"
UY_SHEETS = {"Montevideo": ["K29D6O2", "K29D6O3", "K29D6O5", "K29D6O6"]}

def uy_sampler(name):
    def sampler(lats, lons):
        X, Y = tm(lats, lons, -57.0, 0.9996, 500000, 10000000, f=1 / 298.257222101)
        rasters = [tiff_array(get(UY % sheet)) for sheet in UY_SHEETS[name]]
        return sample_rasters(rasters, X, Y)
    return sampler


# ------------------------------------------------------------------ elsewhere: GEDTM30 (one global cloud-optimised BigTIFF)

GEDTM = "https://s3.opengeohub.org/global/dtm/v1.2/gedtm_rf_m_30m_s_20060101_20151231_go_epsg.4326.3855_v1.2.tif"
# from the file's directory (v1.2): pixel scale, tie point, size, tile size, where the tile offsets and byte counts are
GEDTM_SCALE, GEDTM_TIE = 0.00025, (-180.0, 85.0)
GEDTM_W, GEDTM_T = 1296009, 2048
GEDTM_OFFSETS, GEDTM_COUNTS = 432406307428, 432405638980

def gedtm_sampler(lats, lons):
    def rng(a, n): return get(GEDTM, {"Range": "bytes=%d-%d" % (a, a + n - 1)})
    tail = rng(432407644324, 72); sc = struct.unpack("<3d", tail[0:24]); tp = struct.unpack("<6d", tail[24:72])
    ntx = (GEDTM_W + GEDTM_T - 1) // GEDTM_T
    cols = ((lons - tp[3]) / sc[0]); rows = ((tp[4] - lats) / sc[1])
    tiles = {}
    for ti in set(((rows.astype(int) // GEDTM_T) * ntx + cols.astype(int) // GEDTM_T).ravel().tolist()):
        off = struct.unpack("<Q", rng(GEDTM_OFFSETS + 8 * ti, 8))[0]; cnt = struct.unpack("<I", rng(GEDTM_COUNTS + 4 * ti, 4))[0]
        a = np.frombuffer(zlib.decompress(rng(off, cnt)), "<u4").reshape(GEDTM_T, GEDTM_T)
        tiles[ti] = np.cumsum(a, axis=1, dtype=np.uint32).view("<f4").astype(np.float64)   # predictor 2 on 32-bit words
    rasters = []
    for ti, a in tiles.items():
        r, c = divmod(ti, ntx)
        a = a.copy(); a[(a < -1000) | (a > 9000)] = np.nan
        rasters.append((a, (tp[3] + c * GEDTM_T * sc[0], tp[4] - r * GEDTM_T * sc[1], sc[0], sc[1])))
    print("  GEDTM30 tiles:", len(tiles))
    return sample_rasters(rasters, lons, lats)


def sampler_for(name, source):
    return {"ahn": ahn_sampler, "usgs": service_sampler(url(USGS), 1.0), "ea": service_sampler(url(EA), 1.0, 400),
            "flanders": service_sampler(url(FLANDERS), 1.0), "ign": service_sampler(url(IGN), 5.0, 1500),
            "ga": service_sampler(url(GA), 5.0, 1000), "nrcan": service_sampler(url(NRCAN), 1.0, 500, big_endian=True),
            "gsi": gsi_sampler, "linz": linz_sampler(name) if source == "linz" else None, "uy": uy_sampler(name) if source == "uy" else None,
            "gedtm": gedtm_sampler, "terrarium": terrarium_sampler}[source]


def blur(a, sigma_px):
    r = int(math.ceil(sigma_px * 2.5))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma_px) ** 2); k /= k.sum()
    p = np.pad(a, r, mode="edge")
    p = np.apply_along_axis(lambda m: np.convolve(m, k, mode="valid"), 1, p)
    p = np.apply_along_axis(lambda m: np.convolve(m, k, mode="valid"), 0, p)
    return p


def build(name, terrarium=False):
    lat0, lon0 = centre(name)
    s, w, n, e = bounds(name)
    xa, za = to_local(s, w, lat0, lon0); xb, zb = to_local(n, e, lat0, lon0)
    half = max(abs(xa), abs(xb), abs(za), abs(zb)) + 200
    source, step, sigma = ("terrarium", 4, 10) if terrarium else SOURCE.get(name, ("gedtm", 4, 15))
    nx = int(math.ceil(2 * half / step)) + 1
    xs = -half + np.arange(nx) * step
    X, Z = np.meshgrid(xs, xs)
    # local -> geographic (the simulator's GeoProjection.ToGeo; under 0.5% off over a map: fine for terrain)
    lats = lat0 + Z / 111320.0
    lons = lon0 + X / (111320.0 * math.cos(math.radians(lat0)))
    print("%s: %d x %d points, %.0f m step, %s" % (name, nx, nx, step, source))
    H = sampler_for(name, source)(lats, lons)
    if source == "terrarium":
        H = np.maximum(H, 0.0)   # tiles give the seabed: sea level
        # the global tiles are partly surface models in cities (SRTM sees buildings): an opening (minimum, then
        # maximum over 60 m) takes off bumps narrower than a block of buildings and keeps the slopes of the land
        from scipy.ndimage import grey_opening
        H = grey_opening(H, size=(int(60 / step) | 1, int(60 / step) | 1))
    missing = np.isnan(H).mean()
    if missing > 0.5: raise RuntimeError("%.0f%% of the map without heights" % (100 * missing))
    if missing > 0: print("  %.1f%% without heights (water, gaps): filled from around" % (100 * missing))
    H = fill_gaps(H)
    H = blur(H, sigma / step)
    ci = nx // 2
    H = H - H[ci, ci]
    credit = AHN_CREDIT if source == "ahn" else TERRARIUM_CREDIT if source == "terrarium" else CREDITS[source]
    path = os.path.join(MAPS, name + ".height.bin.gz")
    with gzip.open(path, "wb") as f:
        f.write(b"HGT1"); f.write(struct.pack("<dd", lat0, lon0)); f.write(struct.pack("<fff", -half, -half, step))
        f.write(struct.pack("<ii", nx, nx)); c = credit.encode("utf-8"); f.write(struct.pack("<i", len(c))); f.write(c)
        f.write(H.astype("<f4").tobytes())
    print("%s: heights %.1f .. %.1f m around the centre -> %s (%d kB)" % (name, H.min(), H.max(), os.path.relpath(path, ROOT), os.path.getsize(path) // 1024))


if __name__ == "__main__":
    a = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    a.add_argument("map", nargs="?")
    a.add_argument("--all", action="store_true")
    a.add_argument("--terrarium", action="store_true", help="the old global Terrain Tiles instead of the bare-earth models")
    a = a.parse_args()
    for m in (maps() if a.all else [a.map]):
        try: build(m, a.terrarium)
        except Exception as ex: print("%s: failed: %s" % (m, ex))
