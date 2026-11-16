#!/usr/bin/env python3
"""
The skyline around a city map: the tall buildings (35 m and up, or 10 storeys and up) within a few kilometres of the map
centre but outside the map itself, from OpenStreetMap (Overpass API, (c) OpenStreetMap contributors, ODbL). The
simulator draws them as plain towers on the horizon (no collisions, no shadows), so a street view in Midtown Manhattan
ends in skyscrapers instead of empty sky.

  python3 Tools/skyline.py NewYork [--radius 4000]

Writes Assets/StreamingAssets/Maps/{map}.skyline.txt.gz: "SKY1 lat0 lon0", a credit line, then one line per building:
"height x z x z ..." (height in decimetres, the outline in centimetres in the simulator's local metres).
"""
import argparse, gzip, json, os, sys, time, urllib.parse, urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from citygml_buildings import to_local  # the simulator's projection
from terrain_height import centre, MAPS, bounds

UA = {"User-Agent": "coupled-sim-skyline/1.0 (open-source driving simulator)"}
OVERPASS = ["https://overpass-api.de/api/interpreter", "https://overpass.kumi.systems/api/interpreter"]
CREDIT = "Skyline: buildings from OpenStreetMap (c) OpenStreetMap contributors, ODbL"


def height_of(tags):
    def num(v):
        try:
            return float(str(v).split(";")[0].replace("m", "").strip())
        except ValueError:
            return None
    h = num(tags.get("height")) if "height" in tags else None
    if h is None and "building:levels" in tags:
        lv = num(tags["building:levels"])
        h = lv * 3.5 if lv else None
    return h


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("map")
    ap.add_argument("--radius", type=float, default=4000)
    ap.add_argument("--min-height", type=float, default=35)
    a = ap.parse_args()
    lat0, lon0 = centre(a.map)
    s, w, n, e = bounds(a.map)
    q = ('[out:json][timeout:240];('
         'way["building"]["height"~"^[1-9][0-9][0-9]?(\\\\.[0-9]+)?( ?m)?$"](around:%d,%f,%f);'
         'way["building"]["building:levels"~"^[1-9][0-9]$"](around:%d,%f,%f);'
         'way["building:part"]["height"~"^[1-9][0-9][0-9]?(\\\\.[0-9]+)?( ?m)?$"](around:%d,%f,%f);'
         ');out geom tags;') % (a.radius, lat0, lon0, a.radius, lat0, lon0, a.radius, lat0, lon0)
    data = None
    for url in OVERPASS:
        try:
            req = urllib.request.Request(url, data=urllib.parse.urlencode({"data": q}).encode(), headers=UA)
            data = json.loads(urllib.request.urlopen(req, timeout=300).read())
            break
        except Exception as ex:  # try the next server
            print("  %s: %s" % (url, ex))
            time.sleep(5)
    if data is None:
        sys.exit("no Overpass server answered")
    lines, skipped = [], 0
    for el in data.get("elements", []):
        g = el.get("geometry")
        if not g or len(g) < 4:
            continue
        h = height_of(el.get("tags", {}))
        if h is None or h < a.min_height or h > 600:
            continue
        clat = sum(p["lat"] for p in g) / len(g); clon = sum(p["lon"] for p in g) / len(g)
        if s <= clat <= n and w <= clon <= e:   # in the map: built there from its own data
            skipped += 1
            continue
        pts = [to_local(p["lat"], p["lon"], lat0, lon0) for p in g[:-1]]
        lines.append("%d %s" % (round(h * 10), " ".join("%d %d" % (round(x * 100), round(z * 100)) for x, z in pts)))
    out = os.path.join(MAPS, a.map + ".skyline.txt.gz")
    with gzip.open(out, "wt", encoding="utf-8") as f:
        f.write("SKY1 %.7f %.7f\n%s\n" % (lat0, lon0, CREDIT))
        f.write("\n".join(lines) + "\n")
    print("%s: %d tall buildings around the map (%d inside it left out) -> %s (%d kB)" % (a.map, len(lines), skipped, out, os.path.getsize(out) // 1024))


if __name__ == "__main__":
    main()
