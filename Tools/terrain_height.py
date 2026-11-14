#!/usr/bin/env python3
"""
The terrain heights of a city map, from open elevation data:
  - the Netherlands: AHN4 bare-earth model (DTM 0.5 m, via PDOK, CC0), at 2 m;
  - elsewhere: Terrain Tiles (Mapzen/Tilezen terrarium on AWS: SRTM, EU-DEM, 3DEP, ...; credits below), at about 4 m.

Writes Assets/StreamingAssets/Maps/{map}.height.bin.gz: a grid in the simulator's local metres around the map
centre, smoothed, relative to the ground at the centre. The simulator lifts the whole map onto it (TerrainHeight).

  python3 Tools/terrain_height.py Wellington
  python3 Tools/terrain_height.py --all

File: "HGT1", lat0, lon0 (float64), x0, z0, step (float32), nx, nz (int32), credit (int32 length + UTF-8),
then nz rows of nx float32 heights (row k at z0 + k*step).
"""
import argparse, gzip, io, math, os, re, struct, sys, urllib.request
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAPS = os.path.join(ROOT, "Assets", "StreamingAssets", "Maps")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from citygml_buildings import to_local  # the simulator's projection

UA = {"User-Agent": "coupled-sim-terrain/1.0 (open-source driving simulator)"}
NL = {"Eindhoven", "Delft"}
TERRARIUM_CREDIT = ("Terrain Tiles (Mapzen/Tilezen, AWS Open Data): includes SRTM and GMTED2010 courtesy of the U.S. Geological Survey, "
                    "3DEP, EU-DEM (Copernicus, funded by the European Union), Geoscience Australia, Open Government Licence - Canada, "
                    "and Environment Agency (UK) data; see github.com/tilezen/joerd/blob/master/docs/attribution.md")
AHN_CREDIT = "AHN4 digital terrain model (Actueel Hoogtebestand Nederland, via PDOK, CC0)"


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
        return r.read()


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


def blur(a, sigma_px):
    r = int(math.ceil(sigma_px * 2.5))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma_px) ** 2); k /= k.sum()
    p = np.pad(a, r, mode="edge")
    p = np.apply_along_axis(lambda m: np.convolve(m, k, mode="valid"), 1, p)
    p = np.apply_along_axis(lambda m: np.convolve(m, k, mode="valid"), 0, p)
    return p


def build(name):
    lat0, lon0 = centre(name)
    s, w, n, e = bounds(name)
    xa, za = to_local(s, w, lat0, lon0); xb, zb = to_local(n, e, lat0, lon0)
    half = max(abs(xa), abs(xb), abs(za), abs(zb)) + 200
    nl = name in NL
    step = 2.0 if nl else 4.0
    nx = int(math.ceil(2 * half / step)) + 1
    xs = -half + np.arange(nx) * step
    X, Z = np.meshgrid(xs, xs)
    # local -> geographic (the simulator's GeoProjection.ToGeo; under 0.5% off over a map: fine for terrain)
    lats = lat0 + Z / 111320.0
    lons = lon0 + X / (111320.0 * math.cos(math.radians(lat0)))
    print("%s: %d x %d points, %.0f m step, %s" % (name, nx, nx, step, "AHN" if nl else "terrarium"))
    H = ahn_sampler(lats, lons) if nl else np.maximum(terrarium_sampler(lats, lons), 0.0)   # tiles give the seabed: sea level
    if not nl:
        # the global tiles are partly surface models in cities (SRTM sees buildings): an opening (minimum, then
        # maximum over 60 m) takes off bumps narrower than a block of buildings and keeps the slopes of the land
        from scipy.ndimage import grey_opening
        H = grey_opening(H, size=(int(60 / step) | 1, int(60 / step) | 1))
    H = blur(H, (6.0 if nl else 10.0) / step)
    ci = nx // 2
    H = H - H[ci, ci]
    credit = AHN_CREDIT if nl else TERRARIUM_CREDIT
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
    a = a.parse_args()
    for m in (maps() if a.all else [a.map]):
        try: build(m)
        except Exception as ex: print("%s: failed: %s" % (m, ex))
