#!/usr/bin/env python3
"""
Real street trees, street lights and traffic signs of a city map, from open government registers.

Writes Assets/StreamingAssets/Maps/{map}.{layer}.csv (layer: trees, lamps, signs) with WGS84 positions;
the map builder (OsmMapGenerator) uses them instead of the OpenStreetMap trees, the lamps it spaces
along streets and the signs it derives from OSM tags. The first line is "# <credit>".

  python3 Tools/city_data.py Eindhoven            # every layer with a source
  python3 Tools/city_data.py Delft --layers signs
  python3 Tools/city_data.py --list

Trees: the register's, and where it leaves trees out (a campus, gardens, company grounds) the crowns of Meta and WRI's
canopy height map (Tools/canopy.py, needs numpy; --no-canopy for the register alone).

Columns:
  trees: lat,lon,height,kind,species,crown    (height in m; kind deciduous|conifer|palm; crown: the crown's
                                               diameter in m where the register has it, else empty)
  lamps: lat,lon,height,lumen,colour          (height of the light in m; colour warm|white|unknown)
  signs: lat,lon,bearing,code,text            (bearing: compass direction the sign faces, degrees;
                                               code: the Dutch RVV code, e.g. C2, A1-50, B6)
"""
import argparse, csv, gzip, io, json, os, re, sys, urllib.parse, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAPS = os.path.join(ROOT, "Assets", "StreamingAssets", "Maps")
UA = {"User-Agent": "coupled-sim-city-data/1.0 (open-source driving simulator)"}


def bounds(name, margin=0.0):
    head = gzip.open(os.path.join(MAPS, name + ".osm.gz")).read(4000).decode("utf-8", "ignore")
    b = re.search(r'<bounds minlat="(-?[\d.]+)" minlon="(-?[\d.]+)" maxlat="(-?[\d.]+)" maxlon="(-?[\d.]+)"', head)
    if not b: sys.exit("no <bounds> in the map")
    s, w, n, e = map(float, b.groups())
    return s - margin, w - margin, n + margin, e + margin


def get(url, data=None, headers=None):
    req = urllib.request.Request(url, data=data, headers={**UA, **(headers or {})})
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


CONIFERS = ("pinus", "picea", "abies", "taxus", "thuja", "larix", "pseudotsuga", "metasequoia", "taxodium", "chamaecyparis",
            "cedrus", "juniperus", "sequoia", "sequoiadendron", "cupressus", "tsuga", "cryptomeria", "araucaria", "ginkgo_no")
PALMS = ("phoenix", "washingtonia", "trachycarpus", "chamaerops", "livistona", "syagrus", "butia", "archontophoenix", "howea")


def crown_m(v):
    """A crown diameter in metres from a register's field (a number or text like "8 m"), or "" when unknown."""
    m = re.search(r"[\d.]+", str(v or ""))
    try: c = float(m.group()) if m else 0.0
    except ValueError: c = 0.0
    return round(c, 1) if 1.0 <= c <= 40.0 else ""


def kind_of(species):
    s = (species or "").strip().lower()
    genus = s.split(" ")[0] if s else ""
    if genus in CONIFERS: return "conifer"
    if genus in PALMS: return "palm"
    return "deciduous"


# ------------------------------------------------------------------ Opendatasoft portals (Eindhoven, ...)

def ods_records(portal, dataset, bbox, field="geo_point_2d"):
    s, w, n, e = bbox
    where = "in_bbox(%s,%f,%f,%f,%f)" % (field, s, w, n, e)
    url = "https://%s/api/explore/v2.1/catalog/datasets/%s/exports/json?%s" % (portal, dataset, urllib.parse.urlencode({"where": where}))
    return json.loads(get(url))


def eindhoven_trees(bbox):
    def height(h):
        if not h: return 9.0
        if h.startswith(">"): return 26.0
        a, b = (float(x) for x in h.split("-"))
        return (a + b) / 2
    rows = []
    for r in ods_records("data.eindhoven.nl", "bomen", bbox):
        p = r.get("geo_point_2d") or {}
        if "lat" not in p: continue
        sp = r.get("boomsoort") or ""
        rows.append(dict(lat=p["lat"], lon=p["lon"], height=round(height(r.get("hoogte")), 1), kind=kind_of(sp), species=sp))
    return rows


def eindhoven_lamps(bbox):
    rows, seen = [], set()
    for r in ods_records("data.eindhoven.nl", "data-openbare-verlichting", bbox):
        p = r.get("geo_point_2d") or {}
        if "lat" not in p: continue
        key = (round(p["lat"], 6), round(p["lon"], 6))
        if key in seen: continue          # one row per lamp; masts with two lamps once
        seen.add(key)
        lph = r.get("lph_armatuur") or 0
        h = lph / 100.0 if lph >= 150 else 0.0
        if h == 0:
            m = re.search(r"(\d+)[,.](\d)", r.get("type_drager_mast") or "")
            h = float(m.group(1) + "." + m.group(2)) if m else 5.0
        colour = {"wit": "white", "geel": "warm", "oranje": "warm", "warmwit": "warm"}.get((r.get("kleur_lamp") or "").lower(), "unknown")
        rows.append(dict(lat=p["lat"], lon=p["lon"], height=round(min(max(h, 2.5), 14.0), 2), lumen=r.get("lumen_lamp") or "", colour=colour))
    return rows


# ------------------------------------------------------------------ ArcGIS feature services (Delft, Sydney)

def arcgis_points(service, bbox):
    """All point features of an ArcGIS FeatureServer layer in the box, as (lon, lat, properties)."""
    s, w, n, e = bbox
    out, offset, seen = [], 0, set()
    while True:
        q = urllib.parse.urlencode(dict(geometry="%f,%f,%f,%f" % (w, s, e, n), geometryType="esriGeometryEnvelope", inSR=4326,
                                        spatialRel="esriSpatialRelIntersects", outFields="*", outSR=4326, f="geojson",
                                        resultOffset=offset, resultRecordCount=2000))
        d = json.loads(get(service + "/query?" + q))
        feats = d.get("features", [])
        new = 0
        for f in feats:
            g = f.get("geometry") or {}
            if g.get("type") == "Point":
                lon, lat = g["coordinates"][0], g["coordinates"][1]
                key = (round(lon, 6), round(lat, 6))          # one per place (some registers repeat records)
                if key in seen: continue                 # some services ignore the offset and send the same page again
                seen.add(key); new += 1
                if s <= lat <= n and w <= lon <= e:      # some ignore the box
                    out.append((lon, lat, f.get("properties") or {}))
        offset += len(feats)
        if not feats or new == 0 or not (d.get("exceededTransferLimit") or d.get("properties", {}).get("exceededTransferLimit")): break
    return out


def class_height(text, fallback=9.0):
    """A height class such as "<6 m.", "6-9 m.", "9 - 12 m", ">24 m" as its middle in metres."""
    t = (text or "").replace(",", ".")
    nums = [float(x) for x in re.findall(r"\d+(?:\.\d+)?", t)]
    if not nums: return fallback
    if "<" in t: return nums[0] * 0.6
    if ">" in t: return nums[0] + 2
    return sum(nums[:2]) / len(nums[:2])


def delft_trees(bbox):
    url = "https://services3.arcgis.com/j07voPd56xoB4c87/arcgis/rest/services/Bomen%20in%20beheer%20door%20gemeente%20Delft/FeatureServer/0"
    rows = []
    for lon, lat, p in arcgis_points(url, bbox):
        sp = p.get("BOOMSORTIMENT") or ""
        rows.append(dict(lat=lat, lon=lon, height=round(class_height(p.get("HOOGTE")), 1), kind=kind_of(sp), species=sp))
    return rows


def sydney_trees(bbox):
    rows = []
    for lon, lat, p in arcgis_points("https://services1.arcgis.com/cNVyNtjGVZybOQWZ/arcgis/rest/services/Trees/FeatureServer/0", bbox):
        sp = p.get("SpeciesName") or ""
        h = p.get("TreeHeight")
        rows.append(dict(lat=lat, lon=lon, height=round(float(h), 1) if h else 9.0, kind=kind_of(sp), species=sp,
                         crown=crown_m(p.get("TreeCanopyNS"))))    # the canopy's spread north-south, m
    return rows


def sydney_lamps(bbox):
    rows = []
    for lon, lat, p in arcgis_points("https://services1.arcgis.com/cNVyNtjGVZybOQWZ/arcgis/rest/services/Light_poles/FeatureServer/0", bbox):
        h = p.get("Height")
        try: h = float(h)
        except (TypeError, ValueError): h = 7.0
        rows.append(dict(lat=lat, lon=lon, height=round(min(max(h, 2.5), 14.0), 2), lumen="", colour="unknown"))
    return rows


# ------------------------------------------------------------------ Ghent, New York, Montreal, Barcelona, Tokyo

def ghent_trees(bbox):
    rows = []
    for r in ods_records("data.stad.gent", "locaties-bomen-gent", bbox):
        p = r.get("geo_point_2d") or {}
        if "lat" not in p: continue
        sp = r.get("sortiment") or ""
        girth = r.get("stamomtrek")
        # height from the trunk girth (cm), roughly: 15 m at 150 cm, as for common street trees
        h = min(28.0, max(4.0, 3.0 + float(girth) * 0.08)) if girth else 10.0
        rows.append(dict(lat=p["lat"], lon=p["lon"], height=round(h, 1), kind=kind_of(sp), species=sp))
    return rows


def nyc_trees(bbox):
    s, w, n, e = bbox
    q = urllib.parse.urlencode({"$where": "within_box(location,%f,%f,%f,%f) AND tpstructure='Full'" % (n, w, s, e),
                                "$select": "dbh,genusspecies,location", "$limit": 50000})
    rows = []
    for r in json.loads(get("https://data.cityofnewyork.us/resource/hn5i-inap.json?" + q)):
        loc = r.get("location") or {}
        if "coordinates" not in loc: continue
        lon, lat = loc["coordinates"][:2]
        sp = (r.get("genusspecies") or "").split(" - ")[0]
        dbh = float(r.get("dbh") or 0)           # inches
        h = min(26.0, max(4.0, 4.0 + dbh * 0.55)) if dbh else 10.0
        rows.append(dict(lat=lat, lon=lon, height=round(h, 1), kind=kind_of(sp), species=sp))
    return rows


def montreal_trees(bbox):
    s, w, n, e = bbox
    sql = ('SELECT "Essence_latin","DHP","Longitude","Latitude" FROM "64e28fe6-ef37-437a-972d-d1d3f1f7d891" '
           'WHERE "Latitude"::float BETWEEN %f AND %f AND "Longitude"::float BETWEEN %f AND %f' % (s, n, w, e))
    d = json.loads(get("https://donnees.montreal.ca/api/3/action/datastore_search_sql", data=json.dumps({"sql": sql}).encode(),
                       headers={"Content-Type": "application/json"}))
    rows = []
    for r in d.get("result", {}).get("records", []):
        try: lat, lon = float(r["Latitude"]), float(r["Longitude"])
        except (TypeError, ValueError, KeyError): continue
        sp = r.get("Essence_latin") or ""
        try: dhp = float(r.get("DHP") or 0)     # cm
        except ValueError: dhp = 0
        h = min(26.0, max(4.0, 3.5 + dhp * 0.25)) if dhp else 10.0
        rows.append(dict(lat=lat, lon=lon, height=round(h, 1), kind=kind_of(sp), species=sp))
    return rows


def cached_download(url, name):
    """A whole-city file (no box query), kept in ~/.cache/coupled-sim-city-data."""
    folder = os.path.join(os.path.expanduser("~"), ".cache", "coupled-sim-city-data"); os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name)
    if not os.path.exists(path):
        print("downloading", url)
        with open(path, "wb") as f: f.write(get(url))
    return path


def barcelona_trees(bbox):
    s, w, n, e = bbox
    path = cached_download("https://opendata-ajuntament.barcelona.cat/resources/bcn/Arbrat/OD_Arbrat_Viari_BCN.csv", "barcelona_arbrat_viari.csv")
    size = {"PRIMERA": 16.0, "SEGONA": 11.0, "TERCERA": 7.0, "EXEMPLAR": 20.0}
    rows = []
    with open(path, encoding="utf-8-sig", errors="ignore") as f:
        for r in csv.DictReader(f):
            try: lat, lon = float(r["latitud"]), float(r["longitud"])
            except (TypeError, ValueError, KeyError): continue
            if not (s <= lat <= n and w <= lon <= e): continue
            sp = r.get("cat_nom_cientific") or ""
            kind = "palm" if "PALMERA" in (r.get("tipus_element") or "").upper() else kind_of(sp)
            rows.append(dict(lat=lat, lon=lon, height=size.get((r.get("categoria_arbrat") or "").upper(), 10.0), kind=kind, species=sp))
    return rows


def tokyo_trees(bbox):
    s, w, n, e = bbox
    path = cached_download("https://www.opendata.metro.tokyo.lg.jp/kensetsu/tokyo_gairoju.csv", "tokyo_gairoju.csv")
    rows = []
    with open(path, encoding="cp932", errors="ignore") as f:
        for r in csv.DictReader(f):
            try: lat, lon = float(r["緯度"]), float(r["経度"])
            except (TypeError, ValueError, KeyError): continue
            if not (s <= lat <= n and w <= lon <= e): continue
            try: h = float(r.get("樹高(m)") or 0)
            except ValueError: h = 0
            rows.append(dict(lat=lat, lon=lon, height=round(h, 1) if h > 0 else 8.0, kind="deciduous", species=r.get("樹種") or "",
                             crown=crown_m(r.get("枝張(m)"))))    # branch spread, m
    return rows


def london_trees(bbox):
    """All 32 boroughs' trees (GLA list, 209 MB): streamed and filtered, not stored."""
    s, w, n, e = bbox
    url = "https://data.london.gov.uk/download/2r45m/e62a6a1f-390d-4193-ae32-3aabd9846f36/Borough_tree_list_2025Nov.csv"
    print("streaming", url)
    rows = []
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=300) as r:
        lines = io.TextIOWrapper(r, encoding="utf-8-sig", errors="ignore", newline="")
        for t in csv.DictReader(lines):
            try: lat, lon = float(t["lat"]), float(t["lon"])
            except (TypeError, ValueError, KeyError): continue
            if not (s <= lat <= n and w <= lon <= e): continue
            m = re.search(r"[\d.]+", t.get("height_m") or "")
            h = float(m.group()) if m else 0.0
            sp = t.get("taxon_species") or t.get("taxon_genus") or ""
            rows.append(dict(lat=lat, lon=lon, height=round(h, 1) if 1.5 < h < 45 else 10.0, kind=kind_of(sp), species=sp,
                             crown=crown_m(t.get("canopy_m"))))    # text like "8 m"
    return rows


def boulder_trees(bbox):
    """City of Boulder's tree inventory. Its ArcGIS server sits behind a browser check, so the ArcGIS Hub's cached
    CSV export (WGS84, about 17 MB, the whole city) is streamed and filtered, not stored. No heights: estimated
    from the trunk's diameter (inches)."""
    s, w, n, e = bbox
    url = "https://hub.arcgis.com/api/v3/datasets/dbbae8bdb0a44d17934243b88e85ef2b_0/downloads/data?format=csv&spatialRefId=4326"
    print("streaming", url)
    rows = []
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=300) as r:
        for t in csv.DictReader(io.TextIOWrapper(r, encoding="utf-8-sig", errors="ignore", newline="")):
            try: lat, lon = float(t["Y"]), float(t["X"])
            except (TypeError, ValueError, KeyError): continue
            if not (s <= lat <= n and w <= lon <= e): continue
            sp = t.get("LATINNAME") or t.get("GENUS") or ""
            try: dbh = float(t.get("DBHINT") or 0)
            except ValueError: dbh = 0
            h = min(26.0, max(4.0, 4.0 + dbh * 0.55)) if dbh else 10.0
            kind = "conifer" if t.get("LEAFTYPE") == "Needleleaved" else kind_of(sp)
            rows.append(dict(lat=lat, lon=lon, height=round(h, 1), kind=kind, species=sp, crown=""))
    return rows


def wellington_trees(bbox):
    """Wellington City Council's trees (parks, streets, waterfront)."""
    rows = []
    for lon, lat, p in arcgis_points("https://gis.wcc.govt.nz/arcgis/rest/services/Parks/Parks/MapServer/53", bbox):
        sp = p.get("botanical_name") or ""
        if sp.lower() == "other": sp = p.get("botanical_name_other") or ""
        h = p.get("height")
        # csr: crown spread, m (undocumented; as a diameter it is 0.6 of the height, as in Sydney's, London's and
        # Tokyo's registers)
        rows.append(dict(lat=lat, lon=lon, height=round(float(h), 1) if h and 1.5 < h < 45 else 9.0, kind=kind_of(sp), species=sp,
                         crown=crown_m(p.get("csr"))))
    return rows


def saopaulo_trees(bbox):
    """Arborização viária (GeoSampa WFS): every street tree as a point (from aerial photos of 2010); sizes and
    species only for those inspected since (Portuguese common names)."""
    s, w, n, e = bbox
    rows, start = [], 0
    while True:
        q = urllib.parse.urlencode(dict(service="WFS", version="2.0.0", request="GetFeature", typeNames="geoportal:arvore",
                                        outputFormat="application/json", srsName="urn:ogc:def:crs:EPSG::4326",
                                        bbox="%f,%f,%f,%f,urn:ogc:def:crs:EPSG::4326" % (s, w, n, e), count=10000, startIndex=start))
        d = json.loads(get("https://wfs.geosampa.prefeitura.sp.gov.br/geoserver/geoportal/wfs?" + q))
        feats = d.get("features", [])
        for f in feats:
            lon, lat = f["geometry"]["coordinates"][:2]
            p = f.get("properties") or {}
            h = p.get("qt_altura_arvore")
            rows.append(dict(lat=lat, lon=lon, height=round(h, 1) if h and 1.5 < h < 45 else 9.0, kind="deciduous",
                             species=p.get("nm_vulgar_especie") or "", crown=crown_m(p.get("qt_diametro_copa"))))
        start += len(feats)
        if not feats or start >= (d.get("numberMatched") or 0): break
    return rows


# ------------------------------------------------------------------ BGT light poles (every Dutch municipality, positions only)

def bgt_lamps(bbox):
    s, w, n, e = bbox
    import datetime
    url = ("https://api.pdok.nl/kadaster/bgt/ogc/v1/collections/paal/items?bbox=%f,%f,%f,%f&plus_type=lichtmast&datetime=%sT00:00:00Z&limit=1000&f=json"
           % (w, s, e, n, datetime.date.today().isoformat()))
    rows = []
    while url:
        d = json.loads(get(url))
        for f in d.get("features", []):
            g = f.get("geometry") or {}
            if g.get("type") != "Point": continue
            lon, lat = g["coordinates"][:2]
            rows.append(dict(lat=lat, lon=lon, height=6.0, lumen="", colour="unknown"))
        url = next((l["href"] for l in d.get("links", []) if l.get("rel") == "next"), None)
    return rows


# ------------------------------------------------------------------ BGT surfaces (every Dutch municipality)

def bgt_kind(collection, props):
    """The surface kind used by the map builder for a BGT polygon (None: left to the aerial photo)."""
    f = (props.get("functie") or "").lower()
    v = (props.get("fysiek_voorkomen") or "").lower()
    plus = (props.get("plus_fysiek_voorkomen") or "").lower()
    closed = "gesloten" in v
    bricks = "klinker" in plus or "tegel" in plus or (v == "open verharding")
    if collection == "wegdeel":
        if f.startswith("rijbaan") or f in ("ov-baan", "transitie", "baan voor vliegverkeer"):
            if "half verhard" in v or "onverhard" in v: return "gravel"
            return "road" if closed else "road_bricks"
        if "fietspad" in f: return "cycleway"
        if "parkeervlak" in f or "inrit" in f: return "parking"
        if "voetpad" in f or "voetgangersgebied" in f or "woonerf" in f or "ruiterpad" in f: return "footway" if not closed else "footway_asphalt"
        return "footway"
    if collection == "ondersteunendwegdeel":
        if "groen" in v or f == "berm" and not closed and "verharding" not in v: return "grass"
        return "footway"
    if collection == "begroeidterreindeel":
        if v in ("zand", "duin"): return "gravel"
        return "grass"
    if collection == "onbegroeidterreindeel":
        if "zand" in v or "onverhard" in v or "half verhard" in v: return "gravel"
        return "footway" if v in ("erf", "open verharding") else "footway_asphalt"
    return None


def _cross_seg(p1, p2, p3, p4):
    """Whether segments p1-p2 and p3-p4 cross (touching at an end does not count)."""
    def o(a, b, c): return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    d1, d2, d3, d4 = o(p3, p4, p1), o(p3, p4, p2), o(p1, p2, p3), o(p1, p2, p4)
    return d1 * d2 < 0 and d3 * d4 < 0


def bridge_holes(outer, holes):
    """One ring from an outer ring and its holes (a bridge from each hole's easternmost vertex to the nearest vertex
    of the ring it can see, i.e. without crossing the ring or another hole), so a simple ear-clipping triangulation
    keeps the holes open."""
    ring = list(outer)
    todo = sorted(holes, key=lambda h: -max(p[0] for p in h))
    for k, hole in enumerate(todo):
        mi = max(range(len(hole)), key=lambda i: hole[i][0]); m = hole[mi]
        others = [h for h in todo[k + 1:]] + [hole]
        def visible(i):
            q = ring[i]
            for poly in [ring] + others:
                n = len(poly)
                for e in range(n):
                    a, b = poly[e], poly[(e + 1) % n]
                    if a in (q, m) or b in (q, m): continue
                    if _cross_seg(m, q, a, b): return False
            return True
        order = sorted(range(len(ring)), key=lambda i: (ring[i][0] - m[0]) ** 2 + (ring[i][1] - m[1]) ** 2)
        bi = next((i for i in order[:60] if visible(i)), order[0])
        h = hole[mi:] + hole[:mi + 1]
        ring = ring[:bi + 1] + h + [ring[bi]] + ring[bi + 1:]
    return ring


def bgt_surfaces(bbox):
    import datetime
    s, w, n, e = bbox
    today = datetime.date.today().isoformat()
    polys = []
    for coll in ("wegdeel", "ondersteunendwegdeel", "begroeidterreindeel", "onbegroeidterreindeel"):
        url = ("https://api.pdok.nl/kadaster/bgt/ogc/v1/collections/%s/items?bbox=%f,%f,%f,%f&datetime=%sT00:00:00Z&limit=1000&f=json"
               % (coll, w, s, e, n, today))
        while url:
            d = json.loads(get(url))
            for f in d.get("features", []):
                g = f.get("geometry") or {}
                props = f.get("properties") or {}
                kind = bgt_kind(coll, props)
                if not kind: continue
                # the level relative to the ground: below it (tunnels, underpasses) left out, as the map draws the
                # ground; above it (bridge decks) marked "@1": the map keeps those over water only
                level = props.get("relatieve_hoogteligging") or 0
                if level < 0: continue
                if level > 0: kind += "@%d" % level
                parts = [g["coordinates"]] if g.get("type") == "Polygon" else g.get("coordinates", []) if g.get("type") == "MultiPolygon" else []
                for rings in parts:
                    if not rings or len(rings[0]) < 4: continue
                    outer = [(round(x, 7), round(y, 7)) for x, y in rings[0][:-1]]
                    holes = [[(round(x, 7), round(y, 7)) for x, y in r[:-1]] for r in rings[1:] if len(r) >= 4]
                    polys.append({"k": kind, "r": bridge_holes(outer, holes)})
            url = next((l["href"] for l in d.get("links", []) if l.get("rel") == "next"), None)
    return polys


# ------------------------------------------------------------------ Dutch traffic signs (NDW, every municipality)

def ndw_signs(bbox):
    """The national register of traffic signs (Nationaal Dataportaal Wegverkeer, CC0): every placed sign with its
    RVV code, the number or arrow on it (blackCode) and the direction of the traffic it is for (bearing)."""
    s, w, n, e = bbox
    url = "https://data.ndw.nu/api/rest/static-road-data/traffic-signs/v4/current-state?bbox=%f,%f,%f,%f&status=PLACED" % (w, s, e, n)
    d = json.loads(get(url))
    rows = []
    for f in d.get("features", []):
        p = f.get("properties", {}); g = f.get("geometry") or {}
        code = p.get("rvvCode")
        if not code or code == "onbekend" or g.get("type") != "Point": continue
        lon, lat = g["coordinates"][:2]
        if not (s <= lat <= n and w <= lon <= e): continue
        rows.append(dict(lat=lat, lon=lon, bearing=p.get("bearing", ""), code=code, text=p.get("blackCode") or ""))
    return rows


# ------------------------------------------------------------------ registry: (map, layer) -> (function, credit)

NDW = "Verkeersborden, Nationaal Dataportaal Wegverkeer (NDW, CC0)"
SOURCES = {
    ("Eindhoven", "signs"): (ndw_signs, NDW),
    ("Eindhoven", "surfaces"): (bgt_surfaces, "Basisregistratie Grootschalige Topografie (BGT, PDOK, CC0)"),
    ("Delft", "surfaces"): (bgt_surfaces, "Basisregistratie Grootschalige Topografie (BGT, PDOK, CC0)"),
    ("Delft", "trees"): (delft_trees, "Bomen in beheer door gemeente Delft (CC BY 4.0)"),
    ("Delft", "lamps"): (bgt_lamps, "Lichtmasten, Basisregistratie Grootschalige Topografie (BGT, PDOK, CC0)"),
    ("Sydney", "trees"): (sydney_trees, "Trees, City of Sydney (CC BY 4.0)"),
    ("Sydney", "lamps"): (sydney_lamps, "Light poles, City of Sydney (CC BY 4.0)"),
    ("Ghent", "trees"): (ghent_trees, "Locaties bomen Gent, Stad Gent (Modellicentie Gratis Hergebruik)"),
    ("NewYork", "trees"): (nyc_trees, "Forestry Tree Points, NYC Parks (NYC Open Data)"),
    ("Montreal", "trees"): (montreal_trees, "Arbres publics, Ville de Montréal (CC BY 4.0)"),
    ("Barcelona", "trees"): (barcelona_trees, "Arbrat viari, Ajuntament de Barcelona (Open Data BCN, CC BY 4.0)"),
    ("London", "trees"): (london_trees, "Local authority maintained trees, Greater London Authority / GiGL (Open Government Licence v3)"),
    ("Boulder", "trees"): (boulder_trees, "Tree Inventory Open Data, City of Boulder Parks and Recreation (CC0 1.0)"),
    ("Wellington", "trees"): (wellington_trees, "WCC Trees, Wellington City Council (CC BY 3.0 NZ)"),
    ("SaoPaulo", "trees"): (saopaulo_trees, "Arborização viária, Prefeitura de São Paulo / GeoSampa (CC BY-SA 4.0)"),
    ("Tokyo", "trees"): (tokyo_trees, "都道の街路樹 (Street trees on metropolitan roads), Tokyo Metropolitan Government (CC BY 4.0)"),
    ("Delft", "signs"): (ndw_signs, NDW),
    ("Eindhoven", "trees"): (eindhoven_trees, "Bomen, Gemeente Eindhoven (data.eindhoven.nl, public domain)"),
    ("Eindhoven", "lamps"): (eindhoven_lamps, "Openbare verlichting in Eindhoven, Gemeente Eindhoven (data.eindhoven.nl, public domain)"),
}

COLUMNS = {"trees": ["lat", "lon", "height", "kind", "species", "crown"],
           "lamps": ["lat", "lon", "height", "lumen", "colour"],
           "signs": ["lat", "lon", "bearing", "code", "text"]}


def map_centre(name):
    src = open(os.path.join(ROOT, "Assets", "Scripts", "Maps", "MapCatalog.cs"), encoding="utf-8").read()
    m = re.search(r'Name = "%s".*?Latitude = (-?[\d.]+), Longitude = (-?[\d.]+)' % re.escape(name), src)
    return float(m.group(1)), float(m.group(2))


def clip_ring(ring, x0, z0, x1, z1):
    """Sutherland-Hodgman clipping of a ring to a rectangle."""
    def clip(pts, inside, cut):
        out = []
        for i in range(len(pts)):
            a, b = pts[i - 1], pts[i]
            if inside(b):
                if not inside(a): out.append(cut(a, b))
                out.append(b)
            elif inside(a): out.append(cut(a, b))
        return out
    def at_x(x): return lambda a, b: (x, a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0]))
    def at_z(z): return lambda a, b: (a[0] + (b[0] - a[0]) * (z - a[1]) / (b[1] - a[1]), z)
    for inside, cut in ((lambda p: p[0] >= x0, at_x(x0)), (lambda p: p[0] <= x1, at_x(x1)), (lambda p: p[1] >= z0, at_z(z0)), (lambda p: p[1] <= z1, at_z(z1))):
        ring = clip(ring, inside, cut)
        if len(ring) < 3: return []
    return ring


def simplify(ring, tol):
    """Douglas-Peucker on a closed ring (tolerance in metres)."""
    if len(ring) < 5: return ring
    def dp(pts):
        if len(pts) < 3: return pts
        (ax, az), (bx, bz) = pts[0], pts[-1]
        dx, dz = bx - ax, bz - az; L = (dx * dx + dz * dz) ** 0.5 or 1e-9
        i, dmax = 0, 0.0
        for k in range(1, len(pts) - 1):
            d = abs((pts[k][0] - ax) * dz - (pts[k][1] - az) * dx) / L
            if d > dmax: i, dmax = k, d
        if dmax <= tol: return [pts[0], pts[-1]]
        return dp(pts[:i + 1])[:-1] + dp(pts[i:])
    half = len(ring) // 2
    return dp(ring[:half + 1])[:-1] + dp(ring[half:] + [ring[0]])[:-1]


def write(name, layer, rows, credit):
    if layer == "surfaces":
        # local metres around the map centre (as the simulator's projection), clipped to the map, simplified to 5 cm,
        # as centimetre integers: "BGS1 lat0 lon0" then "credit" then one "kind x z x z ..." line per polygon (gzip)
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from citygml_buildings import to_local
        lat0, lon0 = map_centre(name)
        s, w, n, e = bounds(name, margin=0.0006)
        x0, z0 = to_local(s, w, lat0, lon0); x1, z1 = to_local(n, e, lat0, lon0)
        x0, x1 = min(x0, x1), max(x0, x1); z0, z1 = min(z0, z1), max(z0, z1)
        path = os.path.join(MAPS, "%s.surfaces.txt.gz" % name)
        kept = 0
        with gzip.open(path, "wt", encoding="utf-8") as f:
            f.write("BGS1 %.7f %.7f\n%s\n" % (lat0, lon0, credit))
            for poly in rows:
                ring = [to_local(lat, lon, lat0, lon0) for lon, lat in poly["r"]]
                ring = clip_ring(ring, x0, z0, x1, z1)
                ring = simplify(ring, 0.05) if ring else []
                if len(ring) < 3: continue
                f.write(poly["k"] + " " + " ".join("%d %d" % (round(x * 100), round(z * 100)) for x, z in ring) + "\n")
                kept += 1
        print("%s: %d surface polygons -> %s (%d kB)" % (name, kept, os.path.relpath(path, ROOT), os.path.getsize(path) // 1024))
        return
    path = os.path.join(MAPS, "%s.%s.csv" % (name, layer))
    with open(path, "w", newline="", encoding="utf-8") as f:
        f.write("# %s\n" % credit)
        w = csv.DictWriter(f, fieldnames=COLUMNS[layer], extrasaction="ignore")
        w.writeheader()
        for r in rows: w.writerow(r)
    print("%s: %d %s -> %s" % (name, len(rows), layer, os.path.relpath(path, ROOT)))


def building_index(name):
    """The map's OSM building outlines as a point-in-building test (5 m grid of their boxes)."""
    import xml.etree.ElementTree as ET
    nodes, polys = {}, []
    for _, el in ET.iterparse(gzip.open(os.path.join(MAPS, name + ".osm.gz"))):
        if el.tag == "node": nodes[el.get("id")] = (float(el.get("lat")), float(el.get("lon")))
        elif el.tag == "way":
            if any(t.get("k") in ("building", "building:part") for t in el.findall("tag")):
                ring = [nodes[n.get("ref")] for n in el.findall("nd") if n.get("ref") in nodes]
                if len(ring) >= 4: polys.append(ring)
            el.clear()
    cell = 0.0001
    grid = {}
    for i, ring in enumerate(polys):
        las = [p[0] for p in ring]; los = [p[1] for p in ring]
        for a in range(int(min(las) / cell), int(max(las) / cell) + 1):
            for b in range(int(min(los) / cell), int(max(los) / cell) + 1): grid.setdefault((a, b), []).append(i)
    def inside(lat, lon):
        for i in grid.get((int(lat / cell), int(lon / cell)), ()):
            ring, c = polys[i], False
            for k in range(len(ring)):
                (y1, x1), (y2, x2) = ring[k - 1], ring[k]
                if (y1 > lat) != (y2 > lat) and lon < x1 + (lat - y1) * (x2 - x1) / (y2 - y1): c = not c
            if c: return True
        return False
    return inside


def add_canopy_trees(name, bbox, rows, area, budget_mb):
    """The canopy map's crowns where no tree is within 6 m (Tools/canopy.py), outside buildings and, on a map with
    corridors, within 60 m of its area: the trees the register leaves out (a campus, gardens, company grounds)."""
    import canopy, math
    try:
        g = canopy.grid(name, bbox, budget_mb)
    except Exception as err:      # offline, no numpy, a broken service: the register's trees alone
        print("  canopy: not read (%s)" % err); return rows, None
    if g is None: return rows, None
    in_building = building_index(name)
    lat0 = (bbox[0] + bbox[2]) / 2; ky, kx = 111200.0, 111200.0 * math.cos(math.radians(lat0))
    taken = {}
    def cell(lat, lon): return (int(lat * ky / 6.0), int(lon * kx / 6.0))
    def near(lat, lon):
        a, b = cell(lat, lon)
        for da in (-1, 0, 1):
            for db in (-1, 0, 1):
                for (la, lo) in taken.get((a + da, b + db), ()):
                    if ((la - lat) * ky) ** 2 + ((lo - lon) * kx) ** 2 < 36.0: return True
        return False
    for r in rows: taken.setdefault(cell(r["lat"], r["lon"]), []).append((r["lat"], r["lon"]))
    added = 0
    for lat, lon, c in canopy.crowns(g, bbox):
        if area is not None and not map_area.inside(area, lat, lon, 60.0): continue
        if near(lat, lon) or in_building(lat, lon): continue
        rows.append(dict(lat=lat, lon=lon, height=canopy.height(c), kind="deciduous", species="", crown=""))
        taken.setdefault(cell(lat, lon), []).append((lat, lon))
        added += 1
    print("  canopy: %d trees added where no tree was within 6 m" % added)
    return rows, canopy.CREDIT if added else None


def main():
    a = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    a.add_argument("map", nargs="?")
    a.add_argument("--layers", default="trees,lamps,signs,surfaces")
    a.add_argument("--no-canopy", action="store_true", help="trees: the register's alone, not the canopy map's crowns")
    a.add_argument("--canopy-mb", type=float, default=400.0, help="trees: the most canopy data to read (MB)")
    a.add_argument("--list", action="store_true")
    a = a.parse_args()
    if a.list or not a.map:
        for (m, layer), (_, credit) in sorted(SOURCES.items()): print("%-10s %-6s %s" % (m, layer, credit))
        return
    bbox = bounds(a.map, margin=0.001)
    # a map with corridors (Tools/map_area.py): fetched in tiles of 500 m that cover its area, kept within 60 m of it
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    global map_area
    import map_area
    area = map_area.read(a.map)
    done = 0
    for layer in a.layers.split(","):
        src = SOURCES.get((a.map, layer))
        if src is None: continue
        fn, credit = src
        if area is None: rows = fn(bbox)
        else:
            rows, seen = [], set()
            tiles = map_area.tiles(area, 500.0, 60.0)
            for k, t in enumerate(tiles):
                for r in fn(t):
                    pts = r["r"] if layer == "surfaces" else [(r["lon"], r["lat"])]
                    key = (layer, tuple(pts[0]), len(pts), r.get("k") or r.get("code") or r.get("species") or "")
                    if key in seen: continue
                    if not any(map_area.inside(area, lat, lon, 60.0) for lon, lat in pts[::max(1, len(pts) // 12)]): continue
                    seen.add(key); rows.append(r)
                print("  %s: tile %d/%d, %d so far" % (layer, k + 1, len(tiles), len(rows)), flush=True)
        if layer == "trees" and not a.no_canopy:
            rows, extra = add_canopy_trees(a.map, bbox, rows, area, a.canopy_mb)
            if extra: credit = credit + "; " + extra
        write(a.map, layer, rows, credit)
        done += 1
    if done == 0: sys.exit("no source for %s (see --list)" % a.map)


if __name__ == "__main__":
    main()
