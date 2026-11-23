# Traffic and crowds on the city maps

On every city map (`map=...` or `location=lat,lon,...`), the simulator fills the streets with cars, vans, lorries,
buses, cyclists, race cyclists, pedestrians and animals. This page explains how many there are and how to change them.

## How busy a map is

Each density is a base figure for an average city by day. It is multiplied by the city's own factor and by the time of
day's:

| Who | Base (per km) | City factor | Where they go |
|---|---|---|---|
| Vehicles (cars, vans, lorries, buses) | 20 per km of route lane | cars a dashcam sees per minute in the city in [CROWD](https://github.com/crowd-dataset/crowd), against the world's average (0.5-2.5) | the route lanes, nearest the participants first |
| Cyclists | 8 per km of route | the country's cycling: the Netherlands 3, Denmark 2.4, Belgium 1.7, Germany 1.5, Japan 1.6, most others 0.5-1 | the cycle path beside the road where there is one, else near the kerb |
| Pedestrians | 24 per km of pavement | pedestrians a dashcam sees per minute in the city in CROWD, against the world's average (0.25-4) | the pavements, never the carriageway or cycle paths |

CROWD's figures come from the nearest city within 25 km that has at least 5 hours of footage. If there is none, the
country's are used, then the continent's, then the world's. With the base figures, the busiest cities get busier
streets:

| Map | Cars | Pedestrians | Cyclists | From |
|---|---|---|---|---|
| New York | 1.28 | 1.15 | 0.5 | locality New York (1466 h) |
| London | 0.74 | 2.05 | 0.7 | locality London (1179 h) |
| Barcelona | 0.75 | 1.55 | 0.6 | locality Barcelona (47 h) |
| Eindhoven | 0.83 | 0.87 | 3 | locality Eindhoven (171 h) |
| Delft | 0.59 | 0.47 | 3 | locality Delft (6 h) |
| Montreal | 1.03 | 0.55 | 0.6 | locality Montreal (203 h) |
| Sydney | 1.36 | 0.54 | 0.5 | locality Sydney (681 h) |

The time of day sets a preset:

| Preset | When (`traffic=auto`) | Vehicles | Cyclists | Pedestrians | Buses |
|---|---|---|---|---|---|
| `rush` | 07:00-09:30 and 16:00-18:30 | 1.6x, and 1.2x the cap | 2.5x, and 1.5x the cap | 1.4x | 1.8x |
| `normal` | the rest of the day | 1x | 1x | 1x | 1x |
| `night` | 22:00-06:00 | 0.4x | 0.3x | the city's night share in CROWD | 0.5x |
| `quiet` | only when asked for | 0.5x | 0.5x | 0.5x | 0.5x |

At most 150 vehicles, 100 cyclists and 100 pedestrians are placed, the nearest to the participants first, none in a
participant's way. In a city busier than the average one, the caps for vehicles and pedestrians grow with its factor, up
to 1.5 times (New York: 192 vehicles, 115 pedestrians). The log says
what a map got, for example:

```
MapTraffic: densities 25.6 cars, 4.0 cyclists and 27.6 pedestrians per km, 4 % buses, at most 192 vehicles,
100 cyclists, 115 pedestrians (normal; city factors: cars 1.28, pedestrians 1.15, cyclists 0.50, from locality New York)
```

## Changing them

Set any of these as trial parameters in the experiment (for example `map=Eindhoven;time=08:15;traffic=rush;cyclistsPerKm=40`)
or on the command line (for example `-traffic rush -busShare 0.1`):

| Parameter | Meaning | Default |
|---|---|---|
| `traffic` | `auto`, `normal`, `rush`, `night` or `quiet` (see above) | `auto` |
| `mapTraffic` | vehicles per km of lane, used as it is (no city or time factor) | 20 x factors |
| `cyclistsPerKm` | cyclists per km of route, used as it is | 8 x factors |
| `pedestriansPerKm` | pedestrians per km of pavement, used as it is | 24 x factors |
| `busShare` | share of the vehicles that are buses (0-0.5) | 0.04 x preset |
| `raceCyclistShare` | share of the cyclists that are race cyclists (0-1) | 0.1 |
| `maxCars`, `maxCyclists`, `maxPedestrians` | caps on how many are placed, the nearest to the participants (used as they are) | 150, 100, 100, grown in busy cities and at rush hour |
| `crowd` | `0` ignores CROWD: every city as an average one | `1` |
| `time` | `HH:MM`; also picks the `auto` preset | the experiment's |

The same fields are on the `MapTraffic` component (`Assets/Scripts/Traffic/MapTraffic.cs`) for scenes that place it
themselves.

## Who they are

- **Vehicles:** mostly cars, with vans, lorries, taxis, the odd ambulance or police car, and buses by `busShare`. Lorries
  and buses keep to routes whose bends and street widths they fit; on other routes a smaller vehicle takes their place.
- **Cyclists:** city cyclists on the cycle path or near the kerb at about 16 km/h. Race cyclists ride at about 32 km/h on
  the carriageway up to 50 km/h, and on the cycle path in the Netherlands, Belgium and Denmark, where it is compulsory.
  Every bicycle prefab with "Bicycle" in its name is used, so more rider variants can be added as prefabs.
- **Pedestrians:** 14 pedestrian avatars in everyday clothes: women, men, a girl and a boy. Each walks at their age group's speed (adults about
  1.34 m/s, children 1.2, older people 1.05), looks at traffic at crossings and glances at passing vehicles. One adult
  in six walks as an older person.
- **Animals:** by default 12 pigeons, 2 dogs, 2 cats and a fox within 60 m of the first participant (AnimalSpawner).
  `animals=2` doubles them, `animals=0` leaves them out, and `animalMix=Horse:1,Deer:2,Husky:1,Pigeon:8` picks the
  species and counts (Pigeon, Dog, Husky, Cat, Fox, Deer, Horse). They walk, run, eat and flee from vehicles, their
  animations matched to their pace and their feet on the ground; pigeons hop and fly.
  `animalRole=1` gathers them around that role's start (here the pedestrian's), in its sight, instead of the first role's,
  `animalRadius=25` within 25 m of it instead of 60, and `animalAhead=15` 15 m ahead of it, where a participant walking
  or riding off will be (the README's walking video in Boulder: `animalRole=1;animalRadius=30;animalAhead=12`).

## Keeping clear of each other

Every vehicle and rider checks its path before it moves and stops before touching anyone or anything. Drivers indicate
before turns at junctions and lane changes, take turns in streets too narrow for two, and back up out of deadlocks.
Pedestrians step round vehicles standing across their way. Streets, cycle paths and footways that run through a
building (OpenStreetMap's `tunnel=building_passage`, as De Zaale under the TU/e's Atlas) are open, lined with walls and a
ceiling. `-debugCollisions` logs every contact between moving agents, and with fixed objects, as `CollisionAudit:`
lines.

## Data

CROWD: City Road Observations With Dashcams ([crowd-dataset/crowd](https://github.com/crowd-dataset/crowd)), CC BY 4.0.
Cite Alam, M. S., Bazilinska, O., & Bazilinskyy, P. (2026). A global dataset of continuous urban dashcam driving.
arXiv:2604.01044. The figures are in `Assets/StreamingAssets/Crowd/crowd_density.csv`, taken from coupled-carla's
`coupledcarla/data/crowd_pedestrians.json` (`tools/crowd_table.py`), CROWD version 14204a2d (2026-11-14).
