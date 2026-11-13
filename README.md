# Coupled simulator for research on driver-pedestrian interactions made in Unity

The coupled simulator lets several people take part in the same traffic scene at the same time: one or more drivers of manual cars, passengers of automated vehicles (AVs) who control an external human-machine interface (eHMI), and pedestrians who walk with a keyboard or a motion suit, on flat screens or in head-mounted displays. AI-controlled cars and pedestrians fill the scene. Everything that happens is logged for analysis.

The simulator is open source and free to use. It is aimed at, but not limited to, academic research. Forks, pull requests and other contributions in the spirit of open science are welcome 😍. For enquiries about collaboration, contact p.bazilinskyy@tue.nl.

📺 Demo of the simulator with 3 agents:

[![demo video](ReadmeFiles/thumbnail_demo_video.png)](https://www.youtube.com/watch?v=W2VWLYnTYrM)

## Contents
- [Citation](#citation) · [Examples of use](#examples-of-use) · [License](#license)
- [Features](#features)
- [Installation](#installation)
- [Running the simulator](#running-the-simulator): [host](#running-as-a-host), [client](#running-as-a-client), [several agents on one machine](#several-agents-on-one-machine), [automatic trials](#running-trials-automatically), [trial files and counterbalancing](#trial-files-and-counterbalancing), [recording videos](#recording-trial-videos)
- [Controls](#controls)
- [Environment and vehicles](#environment-and-vehicles)
- [Networking](#networking)
- [Data logging](#data-logging)
- [Configuration](#configuration): [experiments](#experiment-definitions), [agents](#configuration-of-agents), [waypoints](#configuration-of-waypoints), [eye contact](#configuration-of-drivers-eye-contact), [daylight](#configuration-of-daylight-conditions), [traffic lights](#configuration-of-traffic-lights), [eHMIs](#configuration-of-ehmis), [car prefabs](#details-on-editing-car-prefabs)
- [Testing](#testing)
- [Hardware](#hardware): [headsets](#head-mounted-displays-vr), [wheels](#steering-wheels-pedals-and-gamepads), [motion suit](#xsens-mvn-motion-suit), [network](#network)
- [FAQ](#faq)
- [Used assets](#used-assets)

## Citation
If you use the coupled simulator for academic work, please cite:

> Bazilinskyy, P., Kooijman, L., Dodou, D., & De Winter, J. C. F. (2020). Coupled simulator for research on the interaction between pedestrians and (automated) vehicles. 19th Driving Simulation Conference (DSC). Antibes, France.

## Examples of use
1. Get out of the way! Examining eHMIs in critical driver-pedestrian encounters in a coupled simulator - [repo](https://github.com/bazilinskyy/coupled-sim-evasive), [paper](https://doi.org/10.1145/3543174.3546849).
1. Stopping by looking: A driver-pedestrian interaction study in a coupled simulator using head-mounted displays with eye-tracking - [paper](http://doi.org/10.1016/j.apergo.2022.103825).

## License
The source code of the coupled simulator is released under the [MIT License](LICENSE). Third-party assets included in this repository (see [Used assets](#used-assets)) are not covered by this license. They remain under the license of their developers, such as the Unity Asset Store EULA, the Oculus SDK License or CC0.

## Features
- Up to **4 human participants** in one scene (a host and three clients) over a local network.
- Roles: driver of a manual car, passenger of an AV (controls its eHMI), pedestrian, cyclist, e-scooter rider, moped rider.
- Input: keyboard, gamepads and steering wheels with pedals (Logitech, Thrustmaster, Fanatec, Moza; profiles and in-simulator calibration; force feedback on Logitech wheels), Xsens MVN motion suit for pedestrians.
- Output: flat screens or head-mounted displays through OpenXR (Meta Quest, Varjo, HTC Vive, Valve Index, HP Reverb, Pico), with eye-tracking logs on headsets that have eye tracking.
- AI cars that follow waypoint paths with configurable speed, acceleration, yielding, blinkers and driver eye contact.
- Realistic car physics: engine and automatic gearbox, brakes with ABS, tyre grip that drops in the rain, suspension and anti-roll bars, matched to the real cars.
- Background traffic with a choice of car-following models (IDM, Gipps, Krauss) that follows other cars, stops at red lights, slows down for bends and yields to pedestrians.
- Participants can also be cyclists, e-scooter riders or moped riders, with two-wheeler dynamics (power, legal speed limits, lean and steering).
- Animals moving around the world (pigeons, dogs, cats, foxes, deer, horses) that wander, eat and flee from traffic and people; traffic stops for them.
- Time of day (solar position) and weather (clouds, rain with wet roads, fog), set per trial; URP graphics with post-processing and ambient occlusion.
- 20 city maps from OpenStreetMap on six continents (Eindhoven, Delft, London, Tokyo, New York, Cairo, São Paulo, Sydney, ...), built at load time, and maps of any place by coordinates.
- Coupling with [SUMO](https://eclipse.dev/sumo/) over TraCI: SUMO traffic in the scene, participants' cars and the AV in SUMO.
- AI pedestrians with individual walking speeds, gap-acceptance crossing decisions that react to yielding cars and eHMIs, collision avoidance (social force model), traffic-aware head movements, and 12 realistic avatars.
- eHMIs on the hood, roof and windshield of AVs, switched by the passenger or the host.
- Traffic lights with programmable sequences; day and night versions of the town.
- Logging of all agents (cars, pedestrian skeletons, traffic lights, eye contact) to binary files, conversion to CSV, and live streaming over UDP.
- Automatic trial sequences with per-trial parameters and video recording; trial files (CSV/JSON) with counterbalancing per participant (balanced Latin square).
- An experiment setup window: new experiments from templates on any map, a check of the whole set-up before Play.
- A web dashboard for the experimenter: run the session, switch eHMIs, weather and time from any device, and watch a live map of all agents.

## Graphics
The simulator uses the **Universal Render Pipeline** (URP 17.6): the pipeline asset is `Assets/URPDefaultResources/Fantastic.asset` (HDR, 4× MSAA, soft shadows up to 150 m, Forward+ for many lights) with its renderer `Default_Forward_Renderer` (screen-space ambient occlusion). Post-processing (ACES tonemapping, bloom, colour grading, vignette) comes from the global volume `Post-processing (URP)` in the environment scenes with `Assets/Settings/Rendering/SimulatorVolumeProfile.asset`; every participant camera gets post-processing and SMAA anti-aliasing when it starts rendering (`UrpCameraSetup`). Materials were converted with the Render Pipeline Converter; `Assets/Editor/Graphics/GraphicsSetup.cs` holds the conversion steps (including baking the Tree Creator trees to normal meshes).

## Installation
The simulator runs on **Unity 6.6 (6000.6.9f1)**.

1. Install [Unity Hub](https://unity.com/download) and sign in with a Unity account (a free Personal license is enough).
2. Clone this repository: `git clone https://github.com/bazilinskyy/coupled-sim.git`
3. In Unity Hub, choose `Add` ➡️ `Add project from disk` and select the cloned folder. Unity Hub offers to install 6000.6.9f1 if it is missing; add **Windows Build Support (Mono)** to build the simulator for the lab PCs.
4. Open the project. The first import takes a while (the project is about 2 GB).

Platforms: the simulator is developed and used on Windows 10 and 11, and also runs on macOS (tested on macOS 15). Input and output devices (steering wheel, motion suit, HMD) are supported on Windows only. Note that Unity stops providing the editor for Intel Macs after Unity 6.7 LTS.

The previous version of the simulator for Unity 2022.3.5f1 is available in the git history before the Unity 6 upgrade.

## Running the simulator
Open `Assets/_Scenes/StartScene.unity`. All settings for a run are on the `Managers` game object, mainly in its `NetworkingManager` and `PlayerSystem` components.

![](ReadmeFiles/running.png)

### Running as a host
1. Make sure that `Hide GUI`, `Run Trial Sequence Automatically` and `Record Videos` on `Managers` ➡️ `NetworkingManager` are unchecked.
2. Press Play to enter `Play Mode`.
3. Press `Start host`. The console shows `Listening on <ip address>`: clients connect to this address.
4. Wait for the clients to connect, if any.
5. Select an experiment under `Experiment`.
6. Assign a role to each participant under `Role binding`. A participant without a role runs "headless" (no avatar).
7. Select the display/control mode of the host: `Suite mode`, `HMD mode` or `Keyboard mode`.
8. Press `Initialise experiment`: all agents load the experiment scene.
9. When every agent is ready (the scene is loaded everywhere), press `Start simulation`.

### Running as a client
1. Make sure that `Hide GUI`, `Run Trial Sequence Automatically` and `Record Videos` are unchecked.
2. Press Play to enter `Play Mode`.
3. Press `Start client`, enter the IP address of the host (default 192.168.1.11) and press `Connect`.
4. Select a mode (`Suite mode`, `HMD mode`, `Keyboard mode`).
5. Wait until the host starts the simulation.

### Several agents on one machine
For testing and debugging only:
1. Run the host inside the Unity editor as described above and note the IP address it logs.
2. Build the simulator (`Coupled Sim` ➡️ `Build`) and start one or more builds as clients, connecting to that address (127.0.0.1 also works; `-connect 127.0.0.1` connects at once).

Only the window that has focus receives keyboard input.

### Builds and command-line options
`Coupled Sim` ➡️ `Build` ➡️ `Windows` / `macOS` / `Linux` builds the simulator into `Builds/{platform}` (in batch mode: `-executeMethod BuildTools.Build -buildTarget Win64`); the experiment scenes are added to the build settings first. The first build compiles all shader variants and takes a while (about an hour on a slow PC); later builds reuse them. A build (or the editor in batch mode) takes these options:

| Option | Meaning |
| --- | --- |
| `-host` | start as host (manual mode: roles and start from the GUI or the [dashboard](#experimenter-dashboard)) |
| `-connect <ip>` | start as client and connect to the host at that address |
| `-autoRun` | run the trials automatically (`Run Trial Sequence Automatically`) |
| `-clients <n>` | automatic trials: wait for n clients; they get the roles of the trial parameter `clientRoles` (e.g. `Pedestrian,Cyclist`), else the next free roles |
| `-trials <file>`, `-participant <n>`, `-order latin\|random\|none` | [trial file and counterbalancing](#trial-files-and-counterbalancing) |
| `-sumoConnect <host:port>` | connect to a running [SUMO](#traffic-from-sumo) |
| `-screenshot <seconds> <file>`, `-quitAfter <seconds>` | a screenshot after that time; quit after that time (tests, documentation) |

For example, a driver (host) and a pedestrian (client) on two PCs, run without anyone at the host:
```
"Coupled Simulator.exe" -autoRun -clients 1 -trials pedestrian_study.csv -participant 4
"Coupled Simulator.exe" -connect 192.168.1.11
```
The host starts the simulation once every connected agent has loaded the level.

### Running trials automatically
![](ReadmeFiles/instant.png)

When one participant is controlled on the host, a sequence of trials can be set up in advance and run without clicking through the GUI. This is also how trial videos are recorded (next section).

1. Fill in the `Trials` list (`StartScene` ➡️ `Managers` ➡️ `NetworkingManager` ➡️ `Trials`). Each entry has:
   - `Experiment Index`: zero-based index in the `Experiments` list of `NetworkingManager`.
   - `Role Index`: zero-based index in the `Roles` list of that experiment.
   - `Input Mode`: `Flat` (screen with keyboard, gamepad or steering wheel), `VR` (HMD with keyboard, gamepad or steering wheel), `Suite` (HMD with the Xsens suit; pedestrians only) or `None`.
   - `Experiment Parameters`: name-value pairs passed to every enabled component in the experiment prefab that implements `IExperimentModifier`, right after the experiment is loaded and before the simulation starts. `EnableAVLabel` and `EnableAVLabelForLocalPlayer` are example implementations (they show an "AV" label on cars); they only have an effect when added to the experiment prefab.
   - `Recording Start Time` and `Recording Duration`: see [Recording trial videos](#recording-trial-videos).
2. Check `Run Trial Sequence Automatically`, and usually `Hide GUI` as well.
3. Press Play. The first trial starts by itself. `Esc` ends the current trial and starts the next one, or quits after the last one. `Tab` shows or hides the GUI.

### Trial files and counterbalancing
Instead of the `Trials` list, a sequence can come from a file, which is easier to write in a spreadsheet and to change without touching the scene. Put a CSV (or JSON) file in `Assets/StreamingAssets/Trials` and set `NetworkingManager` ➡️ `Trial File` (or start a build with `-trials <file>`). One row per trial:

```csv
#order=latin
experiment,role,block,weather,time,animals,notes
Base,Driver,,PartlyCloudy,13:00,1,practice
Base,Driver,A,Clear,13:00,1,
Base,Driver,B,HeavyRain,13:00,1,
Base,Driver,C,Clear,22:30,1,
Base,Driver,D,Fog,13:00,0,
CityMap,Driver,,PartlyCloudy,17:30,1,
```

- `experiment`: the experiment's `ShortName`, `Name`, prefab name or index in `Experiments`; `role`: the role's name or index (default 0); `input`: `Flat`, `VR` or `Suite` (default: `PlayerSystem` ➡️ `Player Input Mode`); `recordingStart`, `recordingDuration`: for [videos](#recording-trial-videos); `notes`: ignored.
- Every other column is an experiment parameter (`weather`, `time`, `map`, `location`, `animals`, `fog`, `rain`, or your own `IExperimentModifier`'s); empty cells are left out. Commas, semicolons or tabs separate the columns; lines starting with `#` are comments.
- `block`: trials with the same block stay together, and the order of the blocks changes per participant: `#order=latin` (default) uses row *participant* of a balanced Latin square (Williams design: each block in each position equally often and each block after each other block equally often; for an odd number of blocks over twice as many participants), `#order=random` shuffles the blocks and the trials in them (repeatably per participant), `#order=none` keeps the file's order. Trials without a block before the first block (e.g. practice) run first, the other ones last.
- The participant number comes from `NetworkingManager` ➡️ `Participant` or `-participant <n>` (0 = the file's order); `-order latin|random|none` overrides the file. With a participant number, the host's logs are named `HostLog-P03-T02-{role}-...`, and `ExperimentLogs/Session-P03-{date}.csv` lists the order this participant got. `-autoRun` runs the trials automatically.
- JSON: `{"order": "latin", "trials": [{"experiment": "Base", "role": "Driver", "block": "A", "parameters": [{"name": "weather", "value": "Clear"}]}]}`.

For example, a build for participant 7 that runs the whole sequence by itself:
```
"Coupled Simulator.exe" -trials example.csv -participant 7 -autoRun
```
`Coupled Sim` ➡️ `Experiments` ➡️ `Setup…` ➡️ `Trials` previews the order for any participant, checks the file, and makes a trial file from the `Trials` list. Examples: `Assets/StreamingAssets/Trials/example.csv` and `example.json`.

### Recording trial videos
Trial videos for "offline" use (for example online questionnaires) are recorded with Unity Recorder, in the editor only:
1. Set up the trials as described above and also check `Record Videos`.
2. Give each trial a `Recording Start Time` (seconds after the trial starts) and a `Recording Duration` (seconds). When the recording ends, the next trial starts.
3. Set the output on `Managers` ➡️ `Recorder`:
   - `Directory`: output folder relative to `Application.dataPath` (the `Assets` folder in the editor). The default `../../videos` is a `videos` folder next to the project folder.
   - `Resolution` and `Framerate` of the videos.

![](ReadmeFiles/recorder.png)

Videos are named `{trial index}_{ExperimentDefinition.ShortName}_roleIdx-{role index}_{parameter name}-{value}_..._{yy-MM-dd_hh-mm}`, where `hh` is the hour on a 12-hour clock.

### Experimenter dashboard
The host serves a dashboard at `http://<host ip>:8080` (the address is in the console; change the port with `-dashboardPort N`), to run an experiment from any computer, tablet or phone on the lab network, without walking to the host PC:
- start the host, choose the experiment (and for city maps the city, or a location), bind roles to the connected participants, choose the host's mode, initialise the experiment, start the simulation and go to the next trial;
- switch eHMIs, set the weather and time of day, trigger visual syncing;
- watch a live top-down map of the scene (rendered from the scene when it loads) with every participant (ringed), AV, AI car, SUMO vehicle, two-wheeler and pedestrian, and the traffic lights; see who is connected and ready, the trial and the frame rate.

![](ReadmeFiles/dashboard.jpg)

It needs no internet and no installation (a plain web page from the simulator). Allow TCP port 8080 in the host's firewall to use it from other devices. The `ExperimentDashboard` component sits on `Managers` in `StartScene`.

## Controls
| Who | Action | Keyboard | Steering wheel / gamepad |
| --- | --- | --- | --- |
| Driver | Throttle / brake | `W`/`S` or `↑`/`↓` (axis `Throttle`) | pedals |
| Driver | Steer | `A`/`D` or `←`/`→` | wheel |
| Driver | Brake (axis `Break`) | `Space` | pedal |
| Driver | Forward / reverse gear (reverse engages at standstill; then the throttle drives backwards, up to 25 km/h) | `F` / `R` | buttons of the wheel profile (G27: 6 / 7) |
| Driver | Blinker left / right / off | `Q` / `E` / `2` | buttons of the wheel profile (G27: 5 / 4 / 16) |
| AV passenger | eHMI disabled / stop / walk | `1` / `2` / `3` | |
| AV passenger | Resume driving where the AV waits for input (`WaitInputX`) | `Space` | |
| Pedestrian (keyboard mode) | Walk forward / back, sideways, turn; run | `W`/`S`, `A`/`D`, `Q`/`E` (or arrows); hold `Shift` | |
| Pedestrian (keyboard mode) | Look around | hold the right mouse button and move the mouse | |
| Pedestrian (HMD) | Recenter the headset | `R` | |
| Host | Next trial (automatic trials) / show-hide GUI | `Esc` / `Tab` | |
| Experimenter | Steering wheel calibration window | `F2` | |

With a steering wheel, the car's steering wheel turns as far as the real one, without smoothing; keyboard steering is smoothed and steers less at speed. The steering wheels of AVs and of other agents' cars follow how those cars turn. The keyboard pedestrian walks at 1.4 m/s (runs at 3.5 m/s) with a walking animation.

Keyboard bindings are set in `Edit` ➡️ `Project Settings` ➡️ `Input Manager`; wheels and pedals through their profile and calibration (see [Hardware](#steering-wheels-pedals-and-gamepads)). The host can also switch eHMIs and trigger [visual syncing](#data-logging) from its GUI during the simulation.

## Environment and vehicles
![](ReadmeFiles/night_mode_view.png)

The town (`MainScene`, with a night version `MainScene-night`) is a model of a US-like city with:
- a network of 2-lane roads, a loop of 4-lane road and a loop of 6-lane road (partially surrounded by buildings);
- a half-clover interchange for the motorway;
- 10 intersections with traffic lights that can be turned on and off before the experiment or programmatically at runtime;
- 34 zebra crossings;
- static objects (buildings, parked cars, trees).

`DriverTestScene` is a small scene for trying out the driven car.

![](ReadmeFiles/world_all.png)
![](ReadmeFiles/world_centre.png)

### City maps (OpenStreetMap)
Besides the hand-built town, experiments can take place in real city centres, built from [OpenStreetMap](https://www.openstreetmap.org) data while the scene loads (in 1-2 s), much like CARLA's towns: roads with their real widths and markings, red cycle paths, raised pavements with kerbs, zebra crossings (with `PedestrianCrossing`s for AI pedestrians), buildings at their mapped height with brick, plaster or concrete façades, parks, water, mapped trees and street lamps. Every agent builds the same map from the same data. Each map has a SUMO scenario with cars and bicycles on the same streets (`Assets/StreamingAssets/Sumo/Maps`).

| Continent | Maps (population) |
| --- | --- |
| Europe | Eindhoven (240k), Delft (105k), London (8.9M)\*, Barcelona (1.6M), Ghent (265k) |
| Asia | Tokyo, Shibuya crossing (14M)\*, Ho Chi Minh City (9M), Luang Prabang (56k) |
| Africa | Cairo (10M), Nairobi (4.4M)\*, Stellenbosch (175k)\* |
| North America | New York, Midtown (8.3M), Montreal (1.8M), Boulder (105k) |
| South America | São Paulo (11.5M), Montevideo (1.3M), Cusco (430k) |
| Oceania | Sydney (5.3M)\*, Auckland (1.7M)\*, Wellington (215k)\* |

\* left-hand traffic: spawn points and SUMO traffic keep left (the participant's car is still left-hand drive).

**Choosing a map.** Use the `City map (OpenStreetMap)` experiment (`Assets/ExperimentDefinitions/CityMap.prefab`, scene `OsmMap`): the host picks the city in its GUI, or a trial sets it with the parameter `map` (e.g. `map=Tokyo`). The roles (driver, pedestrian, cyclist, e-scooter and moped rider, second driver) use `OsmSpawnPoint`s, which put them on a driving lane, a pavement, a cycle path or the road edge near the map centre, so the experiment works in every city. Time of day uses the city's location and time zone.

**Driving anywhere.** The trial parameter `location=lat,lon[,radius]` (e.g. `location=51.4479,5.4904,350` for the TU/e campus) builds a map of any place: the OpenStreetMap data (radius 100-1000 m, default 400 m) is downloaded the first time (internet needed) and cached in the simulator's data folder; with SUMO installed, a SUMO network and random traffic for the place are built too. The host sends the location to the clients. To add a place to the catalogue for good, save its `.osm.gz` in `Assets/StreamingAssets/Maps` and add it to `MapCatalog`.

Limitations: building shapes and heights are as good as the OSM data (buildings without a height get 3.5 floors), roofs are flat and façades generic; traffic lights in the map are not shown (SUMO runs its own signals); roads have no elevation (bridges are only slightly raised).

Map data © OpenStreetMap contributors, available under the [Open Database License](https://www.openstreetmap.org/copyright).

### Vehicles
The repository contains these drivable car prefabs in `Assets/Prefabs/Cars`:
1. small (similar to a Smart Fortwo): `DrivableSmartCommon` (and `DrivableSmartCommon-no_driver`);
2. medium (similar to a 2011 Ford Focus): `DrivableHatchbackCommon`;
3. medium (similar to a Pontiac GTO): `DrivableFireGTOCommon`;
4. large (similar to a Nissan Datsun): `DrivableNissanCommon`.

Only the Smart prefabs are in the `Avatar Prefab Driver` list of `Managers` ➡️ `PlayerSystem` by default. To use another car for a role, add its prefab to that list and point the role's `carIdx` to it. AI cars spawned by car spawners must also be in the `Prefabs` list of `NetworkingManager` ➡️ `AI Car System`.

AI cars follow a defined path (see [waypoints](#configuration-of-waypoints)) or can be programmed to react at runtime to other road users.

**Driving physics.** The participant's car drives with `RealisticVehicleDynamics` (on the drivable car prefabs; `Car` = `SmartFortwo` or `Hatchback`): the real mass, centre of mass and inertia of the car; suspension tuned to its mass (`SuspensionFrequency`, `DampingRatio`); tyre grip for asphalt that drops on wet roads (`WetGrip`, with the rain of the [weather](#time-of-day-and-weather)); an engine with torque curve and maximum power, an automatic gearbox (early shifts when driving gently, kick-down), engine braking and an auto-hold at standstill; front/rear brake balance with ABS; anti-roll bars; Ackermann steering geometry; with the keyboard, steering is reduced at speed. The Smart (52 kW) reaches 60 km/h in about 6 s and stops from 70 km/h in under 2 s, as the real car. The models are in `Assets/Scripts/Vehicles/Models` with tests against the manufacturers' acceleration and top speed figures. Remove the component to drive with the original arcade physics.

## Networking
- Agents communicate over UDP with Unity Transport on **port 7770**. Allow this port (or the Unity editor / the built simulator) through the firewall on every PC.
- The number of agents is fixed in code to 4 (`UNetConfig.MaxPlayers`): the host plus three clients.
- The host runs the simulation (AI cars, AI pedestrians, traffic lights) and sends poses to the clients; each client sends the pose of its own avatar to the host.
- `Visual syncing` (host GUI, during the simulation) shows a red bar on all agents for 1 s, to synchronise screen recordings afterwards.

## Data logging
Every agent writes binary logs to the `ExperimentLogs` folder in the project folder (next to `Assets`):
- `HostLog-{role}-{yyyy_MM_dd_HH_mm_ss}.binLog` / `ClientLog-...`: one frame per rendered frame;
- `HostFixedTimeLog-{role}-...` / `ClientFixedTimeLog-...`: one frame per physics step, at a fixed 50 Hz (`Fixed Timestep` 0.02 s).

Each frame contains the time, the position and rotation of every car with its blinkers, front lights and stop lights, AI-car state (speed, braking, stopped, take-off, eye contact), the position and rotation of every bone of every pedestrian (players and AI; with the Xsens suit, the suit's pose at the time of the frame), and the state of every traffic light. Experiment `Points of Interest` are stored in the log header.

**Convert to CSV.** In `Play Mode`, before pressing `Start host` or `Start client`, press `Convert log to csv`, pick a log and a reference frame (the world origin, a point of interest of the experiment, or a custom point). The result is a semicolon-separated file `ExperimentLogs/{reference}-{log name}.csv` with positions relative to that reference.

**Live data.** The per-frame logger of each agent also sends its frames over UDP to `localhost:40131`, every `NetworkingManager` ➡️ `Realtime Log Interval` seconds (default 0.2 s; read when the simulator starts), so experiments can be monitored with external tools. `Tools/log_receiver.py` is an example receiver: start it with `python log_receiver.py` (Python 3) from the `Tools` folder before starting the simulation. Packets start with a type (`1` = begin, `2` = frame). A received frame looks like this:
```
{
   "timestamp":1.711700439453125,
   "roundtrip":0.0,
   "Driver [car index]":{ # the car of the local player
      "position":(-94.58, -0.06, 64.84),
      "rotation":(0.53, 90.87, 359.99),
      "blinker":0,
      "front":false,
      "stop":false,
      "rigidbody":(3.12, -0.003, -0.047)
   },
   "Driver [car index]":{ # one entry per AI car
      "position":(-112.08, -0.06, 21.38),
      "rotation":(0.51, 157.48, -0.003),
      "blinker":0,
      "front":false,
      "stop":false,
      "rigidbody":(0.51, -0.002, -1.22),
      "speed":5.90,
      "braking":false,
      "stopped":false,
      "takeoff":false,
      "eyecontact":false
   },
   "Pedestrian [pedestrian index]":{ # one entry per player or AI pedestrian
       "bone [bone index]":{ # one entry per bone of the pedestrian rig
          "position":(-94.58, -0.06, 64.84),
          "rotation":(0.53, 90.87, 359.99)
       }
   },
   "CarLight [index]":{ "state":"b""\\x00" },        # one entry per car traffic light
   "PedestrianLight [index]":{ "state":"b""\\x02" }  # one entry per pedestrian traffic light
}
```

## Configuration
### Experiment definitions
The central configuration is on the `Managers` game object in `StartScene`:
- `PlayerSystem`: the player avatar prefabs (pedestrian avatar and the `Avatar Prefab Driver` list of cars);
- `NetworkingManager`: the list of experiments, the trial sequence, and the prefabs of AI cars that can be spawned during an experiment (`AI Car System` ➡️ `Prefabs`).

![](ReadmeFiles/player_system.png)

An experiment is a prefab with an `ExperimentDefinition` component on its root object. To make a new experiment selectable, add its prefab to the `Experiments` list of `NetworkingManager` (or right-click it ➡️ `Coupled Sim` ➡️ `Register experiment in StartScene`). `Assets/ExperimentDefinitions` contains `Base` (the example used by default, showing most features) and the experiments of earlier studies: `2Pedestrian`, `EvasiveManeuvers`, `Scenario_1_eHMI`, `SoloAuto`, `SoloPedestrian`, `SoloPedestrianWithNPCs-Nighttime` and `TrolleyProblem`.

![](ReadmeFiles/networking_manager.png)

To edit an experiment, double-click its prefab in the `Project` window.

![](ReadmeFiles/project.png)

The prefab opens in prefab mode with the `Regular Prefab Editing Environment`. It helps to set this environment to the experiment's scene (`Edit` ➡️ `Project Settings` ➡️ `Editor` ➡️ `Prefab Editing Environments` ➡️ `Regular Environment`).

![](ReadmeFiles/project_settings.png)

#### Setting up a new experiment
`Coupled Sim` ➡️ `Experiments` ➡️ `Setup…` creates experiments without editing lists by hand:
1. `New experiment`: give a name and short name, pick a template (one of the registered experiments: `Base` for the hand-built town, `CityMap` for city maps), where it takes place (the template's scene, one of the [city maps](#city-maps-openstreetmap), or any place by coordinates), the roles to keep, background traffic, animals, time of day and weather. `Create and register` copies the template to `Assets/ExperimentDefinitions`, adds it to `NetworkingManager` ➡️ `Experiments` and its scene to the build settings.
2. `Open it to place spawn points` opens the experiment in its scene: move the spawn points, waypoint paths and spawners, then apply the overrides to the prefab. On city maps, spawn points (`OsmSpawnPoint`) find a road lane, pavement or crossing near the map centre by themselves.
3. `Trials`: make a [trial file](#trial-files-and-counterbalancing) and preview each participant's order.
4. `Check`: lists problems: experiments without roles or spawn points, cars or two-wheelers missing from `PlayerSystem`'s lists, AI car prefabs missing from `AI Car System` ➡️ `Prefabs`, scenes or maps that do not exist, duplicate role names, trials pointing to experiments or roles that do not exist, experiment prefabs that are not registered.

The same check runs when you press Play in `StartScene` (and adds missing experiment scenes to the build settings); it asks before playing when there are errors. Turn it off with `Coupled Sim` ➡️ `Experiments` ➡️ `Check experiments before Play`, or run it with `Coupled Sim` ➡️ `Experiments` ➡️ `Validate`.

`ExperimentDefinition` has these fields:
- `ShortName`: short name, used in video file names;
- `Name`: name shown in the host GUI;
- `Scene`: the Unity scene loaded as the environment (it must be in the build settings); empty for city maps;
- `Map`: the [city map](#city-maps-openstreetmap) (`MapCatalog` name or `lat,lon[,radius]`) built in the `OsmMap` scene; empty for hand-built scenes;
- `Roles`: the roles participants can take (see below);
- `Points of Interest`: `Transform`s whose positions are stored in the logs, for analysis and as CSV reference frames;
- `Car Spawners`: objects with a component derived from `CarSpawnerBase` that spawn AI cars. The spawn sequence is the `IEnumerator SpawnCoroutine()` of the component (see `BaseSyncedCarSpawner`). Spawned prefabs must be in `NetworkingManager` ➡️ `AI Car System` ➡️ `Prefabs`. A spawner without spawn point, track or car prefab logs a warning and spawns nothing;
- `AI Pedestrians`: pairs of an `AIPedestrian` (the pedestrian avatar in the prefab) and a `WaypointCircuit` (its path).

![](ReadmeFiles/experiment_definition.png)

### Configuration of agents
![](ReadmeFiles/roles.png)

`Roles` is a list of `ExperimentRoleDefinition`s with these fields:
- `Name`: short name of the role;
- `SpawnPoint.Point`: where the avatar is spawned;
- `SpawnPoint.Type`: the kind of avatar: `PlayerControlledPedestrian`, `PlayerControlingCar` (driver of a manual car) or `PlayerInAIControlledCar` (passenger of an AV);
- `SpawnPoint.CameraIndex`: which camera of the avatar is used (see below);
- `SpawnPoint.VehicleType`: for cars, `AV` or `MDV` (manually driven vehicle); it enables the matching `AV` or `MDV` elements of the car's `PlayerAvatar`;
- `carIdx`: for cars, the index of the car prefab in `PlayerSystem` ➡️ `Avatar Prefab Driver`;
- `TopHMI`, `WindshieldHMI`, `HoodHMI`: for AV passengers, the eHMI prefab spawned on each slot (slots are set on the car's `PlayerAvatar` ➡️ `HMI Slots`);
- `AutonomousPath`: for AV passengers, the `WaypointCircuit` the AV follows;
- `AutonomousIsYielding`: puts the AV on the `Yielding` layer instead of `Car`.

![](ReadmeFiles/windshield_ehmi.png)

**Adding and removing agents.** Increase the size of `Roles`, or right-click a role and choose `Duplicate`. To remove one, right-click it and choose `Delete`.

**Starting location.** Add a game object to the experiment prefab, place and rotate it, and drag it to `SpawnPoint.Point`.

**Camera.** An avatar prefab can have several cameras (`PlayerAvatar` ➡️ `cameras`), for example for the driver's and the passenger's seat; `SpawnPoint.CameraIndex` selects one. A `CameraSetup` component on the `SpawnPoint.Point` object sets `Field Of View` and `Rotation` (local rotation) of the avatar's cameras at spawn time.

![](ReadmeFiles/camera_setup.png)

### Configuration of waypoints
![](ReadmeFiles/traffic_circuit.png)

Paths of AI pedestrians and cars are `WaypointCircuit` components with a list of waypoint `Transform`s:
- add a waypoint: press `+` below the list and drag a `Transform` into the new field;
- remove a waypoint: select it in the list and press `−`;
- reorder: drag an item in the list;
- move: select the waypoint object and move it in the scene.

#### AI pedestrians
A trigger `BoxCollider` with a `PedestrianWaypoint` component on a waypoint changes how the pedestrian walks from there on:
- `targetSpeed`: walking speed (m/s);
- `targetBlendFactor`: animation blend between standing (0) and full walking (1).

![](ReadmeFiles/pedestrian_waypoint.png)

#### Pedestrian behaviour models
By default (`Behaviour` = `WaypointsOnly`) AI pedestrians walk their path at the set speed, exactly as in earlier experiments. With `Behaviour` = `BehaviourModels` on the `AIPedestrian` component they behave like individuals:
- **Individual parameters**, drawn per pedestrian from a seeded random generator (`Seed`, or derived from the object name, so trials repeat exactly) for its `AgeGroup` (adult, child, older): free walking speed N(1.34, 0.26) m/s (Weidmann, 1993), a slightly higher speed on the road, critical gap (log-normal, median 4.5 s; Brewer et al., 2006), reaction time, the deceleration at which a car is seen as yielding, trust in eHMIs and patience. Children and older people walk slower; older people choose larger gaps.
- **Crossing decisions (gap acceptance)** at `PedestrianCrossing`s: the pedestrian stops at the kerb, looks for at least 0.8 s and crosses when every approaching car either arrives later than the required gap (the larger of the critical gap and the time to clear that car's lane plus a safety margin) or is seen to yield: it is stopped, brakes hard enough to stop before the crossing, or shows `WALK` on its eHMI (trusted with the pedestrian's `EhmiTrust` probability). A `STOP` eHMI makes pedestrians more careful. The accepted gap shrinks the longer one waits (impatience). Pedestrians step off after their reaction time and hurry when a non-yielding car gets close while they are on the road.
- **Collision avoidance** between pedestrians (including player pedestrians) with the social force model (Helbing & Molnár, 1995) with anticipation (Johansson et al., 2007).
- **Animation**: the walk animation follows the actual speed (no foot sliding), the head and eyes follow approaching traffic at the kerb (or scan left and right) and while crossing, and the feet are placed on kerbs (foot IK). Turn these off with `MatchAnimationToSpeed`, `LookAtTraffic` and `FootIK`.

A `PedestrianCrossing` is a trigger collider at the near kerb with a `FarKerb` target on the other side; `Zebra` marks zebra crossings (pedestrians accept 15% smaller gaps there). `VehicleTracker` estimates speed and acceleration of every car for the pedestrians (it also works for remote, player-driven cars).

Every decision is written to `ExperimentLogs/PedestrianDecisions-{date}.csv` (time, pedestrian, event `arrived at kerb` / `start crossing` / `crossed`, waiting time, number of approaching and yielding vehicles, smallest time to arrival, critical gap, walking speed, reason).

**More pedestrians.** `Assets/Prefabs/AIPedestrians/Rocketbox` contains 12 pedestrians (women, men, children, business and sports outfits) made from the [Microsoft Rocketbox](https://github.com/microsoft/Microsoft-Rocketbox) avatars (MIT). `Coupled Sim` ➡️ `Pedestrians` ➡️ `Build pedestrian prefabs from Rocketbox avatars` rebuilds them; to add more, copy an avatar folder (FBX and textures) from the Rocketbox repository into `Assets/ThirdParty/Rocketbox` and run the menu item again. The logged bones of these prefabs are the same humanoid bones as those of the other pedestrians, but bone rotations depend on each rig.

**Example.** The `PedestrianCrossings` experiment (made with `Coupled Sim` ➡️ `Examples` ➡️ `Create PedestrianCrossings experiment`) adds eight of these pedestrians to `Base`, walking a loop that crosses the road south of the central block twice where the AV of the `Automated - Passenger` role drives. `ExperimentPedestrianTools` has the helpers it uses (create a path, a crossing, add a group of pedestrians to an experiment).

#### AI cars
A trigger `BoxCollider` with a `SpeedSettings` component on a waypoint changes the car's behaviour when it reaches it:
- `Type`: `InitialSetSpeed` (at the spawn point; sets the initial speed to `speed`, or to 0 if `causeToYield`), `SetSpeedTarget` (regular waypoint) or `Delete` (removes the car);
- `speed`: target speed (km/h);
- `acceleration`: acceleration to reach the target speed (m/s²; negative to slow down);
- `jerk`: rate of change of acceleration;
- `BlinkerState`: blinker to switch on;
- `causeToYield`: the car brakes with `brakingAcceleration` (negative) to a stop, waits `yieldTime` seconds and drives on with `acceleration` up to `speed`;
- eye-contact fields, see [eye contact](#configuration-of-drivers-eye-contact);
- `customBehaviourData`: triggers custom behaviour (below).

![](ReadmeFiles/speed_settings.png)

**Custom behaviour.** A waypoint can trigger custom behaviour of an `AICar` by referencing a `ScriptableObject` derived from `CustomBehaviourData`; the car needs a matching `CustomBehaviour` component, linked at spawn time (see `BlinkWithFrontLights`, `BlinkPatternData` and `BaseSyncedCarSpawner`).

#### Exporting and importing waypoint circuits
`WaypointCircuit` has `Export to file` and `Import from file` buttons. The file is a semicolon-separated CSV whose first line is `Smooth: True` or `Smooth: False`, followed by a header and one row per waypoint. Importing replaces all waypoints of the circuit. The columns are:

1. `GameObject`: `name`, `tag`, `layer` (layer index).
2. `Transform`: `x`, `y`, `z` (position, m), `rotX`, `rotY`, `rotZ` (Euler angles, degrees), `scaleX`, `scaleY`, `scaleZ`.
3. `SpeedSettings`:

| Field | CSV column | Values |
| --- | --- | --- |
| Type | waypointType | 0 InitialSetSpeed, 1 SetSpeedTarget, 2 Delete |
| speed | speed | km/h |
| acceleration | acceleration | m/s² |
| jerk | jerk | m/s³ |
| BlinkerState | blinkerState | 0 none, 1 left, 2 right |
| causeToYield | causeToYield | True/False |
| EyeContactWhileYielding | lookAtPlayerWhileYielding | True/False |
| EyeContactAfterYielding | lookAtPlayerAfterYielding | True/False |
| yieldTime | yieldTime | s |
| brakingAcceleration | brakingAcceleration | m/s² |
| YieldingEyeContactSince | lookAtPedFromSeconds | s |
| YieldingEyeContactUntil | lookAtPedToSeconds | s |
| customBehaviourData | customBehaviourDataString | `%`-separated `{asset name}#{entity id}` pairs (valid within one editor session) |

4. `BoxCollider`: `collider_enabled`, `isTrigger` (True/False), `centerX`, `centerY`, `centerZ`, `sizeX`, `sizeY`, `sizeZ`.

### Cyclists, e-scooter and moped riders
Roles with `SpawnPoint.Type` = `PlayerRidingTwoWheeler` let a participant ride a bicycle, an e-scooter or a moped (`twoWheelerIdx` 0, 1, 2 in `PlayerSystem` ➡️ `Avatar Prefab Two Wheeler`). `Base` and the example experiments have the roles `Cyclist`, `E-scooter rider` and `Moped rider`.
- **Physics** (`TwoWheelerDynamics`, with tests): force balance of the rider's pedalling power (city bike: about 18-25 km/h on the flat at full effort) or motor power with the legal cut-off speed (e-bike and e-scooter 25 km/h, moped 25 or 45 km/h), rolling resistance, air drag, slope and braking (bicycle: about 3 m from 20 km/h); a bicycle model for the yaw, with the steering range narrowing with speed and the rider leaning into turns (equilibrium lean, reached with a lag). The vehicle follows the ground, can mount kerbs up to 18 cm and stops when it hits something. Presets are in `TwoWheelerParameters.Preset`.
- **Controls**: the car's axes: `W`/`↑` pedal or throttle, `S`/`↓` or `Space` brake, `A`/`D` steer; steering wheel and pedals work the same.
- **Models**: the bicycle (Dutch city bike), e-scooter and moped are built from code by `Coupled Sim` ➡️ `Two-wheelers` ➡️ `Build avatar prefabs` (`TwoWheelerPrefabBuilder`): wheels spin, the handlebar turns on its head tube, frame and rider lean, the cranks turn when pedalling; the rider is a Rocketbox avatar whose hands and feet are placed on the grips and pedals (or footboard) by IK (`RiderIK`). The camera is at the rider's eyes and follows only part of the lean (`CameraLeanFactor`) for comfort in VR.
- Riders are logged and synchronised like vehicles (position, rotation, velocity; lean, steering and wheels as synced parts); AI pedestrians, model-driven traffic and SUMO (as a bicycle or moped) react to them.

### Configuration of background traffic
A `TrafficSpawner` in an experiment's `Car Spawners` list fills one or more lanes with AI cars that drive with a car-following model instead of the `SpeedSettings` script:
- `Lanes`: `WaypointCircuit`s (usually closed loops) and the number of cars on each; cars are spread along the lane and start tracking from where they are placed;
- `Model`: **IDM** (Intelligent Driver Model; Treiber, Hennecke & Helbing, 2000), **Gipps** (1981) or **Krauss** (1998; SUMO's default, with random "dawdling");
- `DesiredSpeedKmh` and `DesiredSpeedSdKmh`: each car draws its own desired speed from this normal distribution (seeded by `Seed`);
- `CarPrefabs` and `Colors` to pick from (prefabs must be in `NetworkingManager` ➡️ `AI Car System` ➡️ `Prefabs`);
- `YieldToPedestrians`, `ObeyTrafficLights`, `KeepClearOfPlayers` (m).

Each car gets a `TrafficCar` component that looks ahead along its own lane for the nearest car (also player cars, the AV and SUMO cars), a pedestrian on the road, or a red or yellow traffic light (stopping only if it can stop comfortably at yellow), slows down before bends (lateral acceleration up to `MaxLateralAcceleration`, 2.5 m/s²), and gives the speed to `AICar`, which steers along the waypoints and shows brake lights. Scripted AI cars in the same experiment, such as the AV, keep their script but get a collision-avoidance supervisor that caps their speed when a car is close ahead (it does not react to pedestrians or lights, which stay part of the experiment's script). The models and the MOBIL lane-change rule (Kesting, Treiber & Helbing, 2007) are in `Assets/Scripts/Traffic/Models` and have edit-mode tests.

The `Traffic` example experiment (`Coupled Sim` ➡️ `Examples` ➡️ `Create Traffic experiment`) adds IDM traffic in both directions on the roads around the central block (6 cars per direction, Smart and hatchback cars) to `Base`.

AI cars removed by a `Delete` waypoint are now hidden on all agents instead of destroyed, so networked pose updates keep working.

### Traffic from SUMO
[Eclipse SUMO](https://eclipse.dev/sumo/) can run the background traffic instead: for large networks, imported scenarios, or traffic studied with SUMO's own models. A `SumoBridge` in an experiment's `Car Spawners` list connects to SUMO over TraCI (no extra Unity packages):
- SUMO's vehicles appear as AI cars on all agents, interpolated between SUMO steps, placed on the road surface, with SUMO's colours, blinkers and brake lights; vehicles that leave SUMO are hidden and reused;
- the participants' cars and the AV are put into SUMO every step (`SendUnityCars`), so SUMO traffic follows and yields to them;
- SUMO cars stop for pedestrians (AI and players) on the road in front of them (`StopForPedestrians`);
- SUMO runs on its own thread in lockstep with Unity's clock, so the frame rate does not depend on SUMO.

Set-up: install SUMO (1.20 or newer; tested with 1.28) and set `SUMO_HOME`; the bridge then starts `$SUMO_HOME/bin/sumo` with `Sumo Config` (a `.sumocfg`, relative to `Assets/StreamingAssets`). Set `Sumo Binary` to `sumo-gui` to watch SUMO's view as well, or turn `Launch Sumo` off to connect to a SUMO already started with `--remote-port` (`Host`, `Port`; the command line option `-sumoConnect host:port` does the same, also for a SUMO on another PC). `Network Origin` places SUMO's coordinates in the scene (SUMO x along the transform's x, SUMO y along its z).

`Coupled Sim` ➡️ `Examples` ➡️ `Create SumoTraffic experiment` makes the `SumoTraffic` example: SUMO traffic in both directions around the central block of MainScene. `SumoTools.ExportLanes` writes the scene's lanes (`WaypointCircuit`s) as a SUMO network with looping routes and flows to `Assets/StreamingAssets/Sumo/MainScene` (SUMO coordinates = Unity x and z); after changing lanes, rebuild the network with the `netconvert` command in the README of that folder.

### Configuration of driver's eye contact
The `EyeContact` component on a car's driver sets the initial eye-contact behaviour:
- `Tracking`: whether the driver looks at pedestrians while the car moves (changes at runtime);
- `TrackingWhileYielding`: the same while the car is stopped;
- `MinTrackingDistance`, `MaxTrackingDistance` (m): range in which the driver can look at a pedestrian, measured horizontally between the driver's head and the pedestrian's root;
- `MaxHeadRotation` (degrees): limit of the head rotation around the vertical axis.

The driver looks at the closest object tagged `Pedestrian` within that range, if the rotation limit allows it. `EyeContactRigControl` animates the driver's head towards that target.

![](ReadmeFiles/pedestrian.png)

![](ReadmeFiles/eye_contact.png)

`SpeedSettings` on waypoints change eye contact when the car reaches them:
- `EyeContactWhileYielding`: behaviour while the car is stopped;
- `EyeContactAfterYielding`: behaviour after the car drives on again (overwrites `EyeContact.Tracking` once the car has stopped);
- `YieldingEyeContactSince`: seconds after stopping before the driver makes eye contact;
- `YieldingEyeContactUntil`: seconds after stopping when the driver stops making eye contact.

### Animals
Every experiment has an `AnimalSpawner` (child object `Animals`) that places animals around the first role's spawn point (or `Centre`) within `Radius` metres: by default 12 pigeons, 2 dogs, 2 cats and a fox (`CityMap`: 20 pigeons, 4 dogs, 3 cats and a fox); huskies, deer and horses are also available. They are the same on every agent (`Seed`). The host runs their behaviour and sends their poses to the clients:
- they idle, eat, and wander within `HomeRadius` of where they started, sometimes across the road;
- they flee from vehicles that would reach them within `FleeTime` seconds or come closer than `FleeDistance`, running sideways out of the car's path; pigeons fly off and land again;
- cats, pigeons, foxes, deer and horses also keep their distance from pedestrians (`PeopleShyDistance`; dogs are not shy);
- background traffic and SUMO cars stop for animals on the road (not for flying pigeons).

The experiment parameter `animals` multiplies the numbers per trial (`0` = no animals, `2` = twice as many). Positions and states are logged to `ExperimentLogs/Animals-{date}.csv` (time, animal, species, position, heading, state). The models are low-poly CC0 animals by Quaternius with their walk, run, idle and eating animations, scaled to real body lengths; `Coupled Sim` ➡️ `Animals` ➡️ `Build prefabs and add to experiments` (`AnimalPrefabBuilder`) rebuilds the prefabs.

![Animals](ReadmeFiles/animals.jpg)

### Time of day and weather
Every experiment has an `EnvironmentController` (child object `Environment (time of day, weather)`) that sets:
- **time of day** (`TimeOfDay`, clock time; `Latitude`, `Longitude`, `UtcOffset`, `DayOfYear`): the sun's position from a solar model, its colour and strength, a physically based sky, ambient light and the street lights (a light under each `StreetLamp`, the nearest 24 to the viewer switched on at dusk); `TimeSpeed` lets time run (e.g. 60 = one minute per second);
- **weather** (`Clear`, `PartlyCloudy`, `Overcast`, `LightRain`, `HeavyRain`, `Fog`): clouds (sky colour, softer and weaker sunlight), fog, rain around the viewer and wet, darker, shinier roads and pavements; `ExtraFog` and `RainOverride` fine-tune it.

They can be set per trial with the experiment parameters `time` (`17:30` or `17.5`), `weather`, `fog`, `rain`, `timeSpeed`, `dayOfYear`, `latitude`. The host sends the conditions to all clients. With `UseBakedLighting` on (the experiments of earlier studies), the scene keeps its baked lightmaps and sun and only the weather effects apply; with it off (Base and the new examples), the sun follows the time of day with realtime lighting.

### Configuration of daylight conditions
![](ReadmeFiles/day_night_control.png)

`DayNightControl` collects the lighting-related objects of a scene and holds two presets, Day and Night, to switch between while building an environment. When the environment is finished, save it as two scenes with different lighting and bake the lightmaps (as done for `MainScene` and `MainScene-night`). `LightProbePlacer` fills the map with a regular grid of light probes for baking (`Place Light Probes` button).

### Configuration of traffic lights
![](ReadmeFiles/street_light_manager.png)

Start from an instance of the `ExampleStreetLightCrossSection` prefab. `TrafficLightsManager` controls `CarSection` and `PedestrianSection` objects, which group `CarTrafficLight` and `PedestrianTrafficLight`s that switch together.
- `initialStreetLightSetup`: list of `TrafficLightEvent`s applied once, before the simulation starts;
- `streetLightEvents`: list of `TrafficLightEvent`s that run one after another in a loop.

Each event has a `Name`, a `Delta Time` (seconds after the previous event), the `CarSections` and `PedestrianSections` it applies to, and the `State` to set. `CurrentIndex` and `CurrentTimer` let the loop start at a given event and time offset.

### Configuration of eHMIs
eHMIs are prefabs with a component derived from `HMI` that shows one of three states: `DISABLED`, `STOP` (do not cross) or `WALK` (you may cross). `AnimatorHMI` switches textures and animator triggers, `SpriteHMI` switches sprites; `StaticHMI` and `DynamicHMI` in `Assets/Prefabs/HMI` are examples. The AV passenger switches the state with `1`/`2`/`3`, the host with buttons in its GUI; `HMIManager` keeps all agents in sync.

### Details on editing car prefabs
The `Speedometer` component shows the speed:
- digital: set `Speedometer Text`;
- analog: set `Pivot` (pivot of the needle), `PivotMinSpeedAngle` and `PivotMaxSpeedAngle` (needle angle at 0 and at maximum speed) and `MaxSpeed`.

Other components on car prefabs: `CarConfigurator` (colour, driver and passenger visibility of AI cars), `SteeringWheel` (turns the steering wheel model), `RearMirrorsReflection` (mirror cameras), `CarBlinkers`, `DriverIKSetup` (driver's hands on the wheel) and the engine sound managers.

## Testing
`SmokeTest` runs a whole trial without a screen and checks that it works: it opens `StartScene`, starts a host that runs the first trial in keyboard mode, plays for the given time once the experiment runs, and writes a report with errors and exceptions, how far each car and pedestrian moved, and screenshots of the active cameras. It exits with code 0 when no errors occurred.

```
Unity -batchmode -projectPath . -executeMethod SmokeTest.Run -smokeSeconds 30 -smokeOut SmokeTest -logFile smoke.log
```

Options: `-smokeTrial N` (index in `Trials`), `-smokeExperiment N` and `-smokeRole N` (override the trial's experiment and role), `-smokeShot "x,y,z,yaw,pitch;..."` (extra screenshots from these camera poses), `-smokeScene <path>` (scene to start from).

The pedestrian models have edit-mode tests (`Assets/Tests/EditMode`): run them in `Window` ➡️ `General` ➡️ `Test Runner`, or with `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`. On Windows, `Unity` is `"C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe"`; on macOS `/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity`.

## Hardware
### Head-mounted displays (VR)
The simulator uses **OpenXR**, so it works with the PC VR headsets whose runtime supports OpenXR (Windows):

| Headset | Runtime to install and run | Notes |
| --- | --- | --- |
| Meta Quest 2 / 3 / 3S / Pro, Rift, Rift S | Meta Quest Link app ("Meta Horizon Link"); Quest via Link cable or Air Link | set Meta Quest Link as the active OpenXR runtime (Settings ➡️ General) |
| Varjo Aero, VR-3, XR-3, XR-4 | Varjo Base | eye tracking: enable it in Varjo Base |
| HTC Vive, Vive Pro (Eye), Vive Pro 2, Cosmos | SteamVR (or VIVE Console) | set SteamVR as the OpenXR runtime (SteamVR ➡️ Settings ➡️ OpenXR) |
| Valve Index, Pimax, Bigscreen Beyond | SteamVR | as above |
| HP Reverb G2 and other WMR headsets | Mixed Reality Portal / OpenXR Tools for WMR, or SteamVR | Windows Mixed Reality was removed in Windows 11 24H2 |
| Pico 4 / Neo 3 | Pico Connect (streaming) with SteamVR | |

1. Install the headset's runtime, connect the headset and check that its own home environment works.
2. Make that runtime the **active OpenXR runtime** (there can be only one; installing SteamVR or Meta Quest Link may switch it).
3. Start the simulator and choose `HMD mode` (host GUI, dashboard, or trial `Input Mode` = `VR`; the Xsens suit uses `Suite`). VR starts when the participant's avatar is spawned, so flat-screen PCs need no VR software at all (`XRSession`).
4. Press `R` (pedestrian) to recenter the view, with the participant looking straight ahead.

If the console says `XR: no headset found`: the runtime is not running, the headset is asleep or disconnected, or another OpenXR runtime is active. A black or frozen view usually means the runtime lost the headset: restart the runtime first, then the simulator. VR runs on Windows only (there are no OpenXR runtimes for macOS).

**Eye tracking.** With headsets that offer eye gaze through OpenXR (`XR_EXT_eye_gaze_interaction`: Varjo, Meta Quest Pro, HTC Vive Pro Eye with VIVE Console, Pico 4 Pro, HP Reverb G2 Omnicept), the participant's gaze is logged to `ExperimentLogs/EyeGaze-{date}.csv`: time, gaze origin and direction in world coordinates, and the object looked at with its distance (`EyeGazeLogger`). Turn eye tracking on in the headset's software and calibrate it there first.

### Steering wheels, pedals and gamepads
Any wheel that Windows sees as a game controller works: Logitech G25 / G27 / G29 / G920 / G923, Thrustmaster T150 / T248 / T300 / TX / T-GT, Fanatec CSL DD / ClubSport / Podium, Moza, and gamepads.
1. Install the wheel's driver software (Logitech G HUB, Thrustmaster drivers, Fanatec driver, Moza Pit House) and connect the wheel **before** starting the simulator (it detects wheels every few seconds, but some drivers only show up at start).
2. In the driver software, set the rotation range: 900° is realistic for cars, 450-540° is easier for unexperienced participants. Turn off "combined pedals" (Logitech) so throttle and brake are separate axes.
3. The simulator picks a profile by the wheel's name (`WheelProfile`; the G27 layout is the default for unknown wheels). **Calibrate** once per PC: press `F2` in the simulator, turn the wheel fully left and right, press each pedal fully and release it, press the buttons for gear forward/reverse and the blinkers, and save. The calibration window also shows the live axes and buttons, to check a wheel. The profile is saved in the simulator's data folder (`%USERPROFILE%\AppData\LocalLow\<company>\<product>\wheel-profile.json` on Windows).

The car (and the bicycle, e-scooter and moped) then reads steering, throttle and brake from the wheel, combined with the keyboard.

**Force feedback** (Logitech wheels): download the *Steering Wheel SDK* from Logitech's developer site and copy `LogitechSteeringWheelEnginesWrapper.dll` (64-bit) to `Assets/Plugins/x86_64` (it is not included in this repository because of its license). The simulator then adds a self-centring force that gets firmer with speed, damping, and a jolt in collisions (`SteeringFeedback`); without the DLL, the wheel uses its driver's own centring spring. Other brands: use the driver software's centring/spring settings.

**Gamepads** (Xbox, PlayStation): calibrate them like a wheel (`F2`): the left stick steers and the triggers act as pedals.

### Xsens MVN motion suit
1. Connect the suit's router to the PC (Ethernet in a yellow port) and plug the MVN dongle into a USB 3.0 port. Start MVN Analyze and create a recording with the participant's body measurements.
2. In `Options` ➡️ `Preferences` ➡️ `Miscellaneous` ➡️ `Network Streamer`, stream to this PC on **UDP port 9763** (the port `XsStreamReader` listens on) with the default pose datagrams (the old `Unity3D` datagram is not supported). If the avatar is jittery, raise the down-sampling skip factor.
3. Turn on the body pack (one beep), wait 30 s, press WPS on the router; when the body pack beeps again, hold its button for 2 s until it beeps twice and the light blinks. If it does not connect within a few minutes, reinstall MVN Analyze including the Bonjour print services.

![](ReadmeFiles/mvn_windows.png)

4. When the avatar moves in MVN Analyze, start the simulator in `Suite mode` (pedestrian roles; the prefabs are in `Assets/Prefabs/Xsens`). Align the HMD with the avatar's head with `R`, repeated until the participant confirms, for every trial. With a headset, the avatar's head turns with the headset's tracking instead of the suit's head sensor, so the two do not fight (`XsLiveAnimator` ➡️ `Head From Headset`).

![](ReadmeFiles/mvn_unity.png)

### Network
All agents must be in the same local network; the host's IP address is in its console. Allow these ports through the firewall of each PC (on Windows: inbound rules for the Unity editor of the version you use, and for the built simulator):

| Port | Protocol | Used for |
| --- | --- | --- |
| 7770 | UDP | agents (host and clients) |
| 8080 | TCP | experimenter dashboard (host) |
| 40131 | UDP, localhost | live data for external tools |
| 9763 | UDP | Xsens MVN stream |
| 8813 | TCP, localhost | SUMO (TraCI) |

![](ReadmeFiles/windows_firewall.png)

![](ReadmeFiles/inbound_rules.png)

## FAQ
**Which Unity version do I need?** Unity 6.6 (6000.6.9f1). Newer 6.x versions will probably open the project but are not tested; older versions cannot open it. Versions of the simulator for Unity 2022.3 are in the git history.

**Which computer do I need?** Windows 10/11 with a recent GPU: a GeForce RTX 3060 or better for screens, RTX 3070/4070 or better for VR at 90 Hz, 16 GB of RAM. The editor also runs on macOS (no VR, no force feedback). One PC per participant; several agents on one PC are for testing only.

**How many participants can take part?** Four (host and three clients). The limit is `UNetConfig.MaxPlayers`; raising it works in principle but is not tested.

**Do I need SUMO?** Only for SUMO traffic (`SumoTraffic`, city maps). Install SUMO 1.20 or newer and set `SUMO_HOME`. Without it, the hand-built town has the built-in traffic models (`Traffic`) and city maps run without traffic.

**How do I make a new experiment?** Copy an experiment prefab in `Assets/ExperimentDefinitions` (e.g. `Base` or `CityMap`), change it in prefab mode (roles, spawn points, paths, traffic, pedestrians, conditions) and add it to `NetworkingManager` ➡️ `Experiments` in `StartScene` (see [Configuration](#configuration)). The examples `PedestrianCrossings`, `Traffic`, `SumoTraffic` and `CityMap` show the newer features.

**Where are the data?** In `ExperimentLogs` next to the `Assets` folder (in builds: next to the executable): binary logs of all agents per frame and at 50 Hz, pedestrian decisions and eye gaze as CSV. Convert the binary logs to CSV in the start screen (`Convert log to csv`). See [Data logging](#data-logging).

**How do I keep videos and logs in sync?** Use `Visual syncing` (host GUI or dashboard): a red bar on every screen for 1 s, visible in screen recordings, logged as an event.

**How do I set weather, time of day and the city per trial?** With trial parameters, e.g. `time=21:30`, `weather=HeavyRain`, `map=Tokyo` or `location=52.0116,4.3571,400`; or in the dashboard.

**How do I add a city?** Use `location=lat,lon[,radius]` for any place, or add it to the catalogue (see [City maps](#city-maps-openstreetmap)).

**Some objects are pink.** A material uses a shader of the old Built-in render pipeline. Imported assets need converting to URP: `Window` ➡️ `Rendering` ➡️ `Render Pipeline Converter`, or `GraphicsSetup.ConvertToUrp` (see [Graphics](#graphics)).

**The headset is not found / shows black.** See [Head-mounted displays](#head-mounted-displays-vr): the right OpenXR runtime must be active and running before the simulator starts.

**The wheel does nothing, or the pedals are swapped.** Press `F2`: if the wheel is not listed, check the USB connection and its driver; if axes are wrong, run the calibration.

**The frame rate is low.** Run a build rather than the editor; in the editor, close the Scene view and use `Play Maximized`. Lower `Assets/URPDefaultResources/Fantastic.asset` (shadow distance, MSAA, render scale) or remove SSAO from `Default_Forward_Renderer`; reduce `EnvironmentController` ➡️ `MaxActiveStreetLights` at night; use a smaller city map or radius. On laptops, make sure the simulator runs on the dedicated GPU (NVIDIA Control Panel ➡️ Manage 3D settings). In VR, the headset's refresh rate (72-90 Hz) is the target.

**Cars drive on the left in London, but the car is left-hand drive.** Yes: maps with left-hand traffic place participants and SUMO traffic on the left, but there is no right-hand drive car model yet.

**Can I use my own avatars or cars?** Pedestrians: any humanoid character; `Coupled Sim` ➡️ `Pedestrians` ➡️ `Build pedestrian prefabs` makes AI pedestrians from Rocketbox-style models. Cars: copy a drivable car prefab and replace the model; AI cars need to be in `NetworkingManager` ➡️ `AI Car System` ➡️ `Prefabs`.

**Is there a way to test the simulator without screens and people?** Yes: the smoke test and the edit-mode tests (see [Testing](#testing)).

## Used assets
| Name | Developer | License |
| --- | --- | --- |
| [ACP (Arcade Car Physics)](https://github.com/Saarg/Arcade_Car_Physics) | Saarg | MIT |
| [Textures](https://ambientcg.com) (formerly cc0textures.com) | Various | CC0 |
| [Oculus Integration](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022) | Oculus | Oculus SDK License |
| [Simple modular street kit](https://assetstore.unity.com/packages/3d/environments/urban/simple-modular-street-kit-13811) | Jacek Jankowski | Unity Asset Store EULA |
| [Realistic Tree 10](https://assetstore.unity.com/packages/3d/vegetation/trees/realistic-tree-10-54724) | Rakshi Games | Unity Asset Store EULA |
| [Small Town America - Streets](https://assetstore.unity.com/packages/3d/small-town-america-streets-free-59759) | MultiFlagStudios | Unity Asset Store EULA |
| Cars Free - Muscle Car Pack (no longer on the Asset Store) | Super Icon LTD | Unity Asset Store EULA |
| [Mini Cargo Truck](https://assetstore.unity.com/packages/3d/vehicles/land/mini-cargo-truck-68663) | Marcobian Games | Unity Asset Store EULA |
| [Street Bench](https://assetstore.unity.com/packages/3d/props/exterior/street-bench-656) | Rakshi Games | Unity Asset Store EULA |
| [Waste bin](https://assetstore.unity.com/packages/3d/waste-bin-73303) | Lowpoly_Master | Unity Asset Store EULA |
| [Smart fortwo](https://grabcad.com/library/smart-fortwo-1) | Filippo Citati | MIT |
| [Vehicle - Essentials](https://assetstore.unity.com/packages/audio/sound-fx/transportation/vehicle-essentials-194951) | Nox Sound | Unity Asset Store EULA |
| [Road System](https://assetstore.unity.com/packages/tools/level-design/road-system-192818) | Maxi Barmetler | Unity Asset Store EULA |
| [Kajaman's Roads](https://assetstore.unity.com/packages/3d/environments/roadways/kajaman-s-roads-free-52628) | Kajaman | Unity Asset Store EULA |
| [Animated animals](https://quaternius.com) (pigeon, Shiba Inu, husky, cat, fox, deer, horse; via [Poly Pizza](https://poly.pizza)) | Quaternius | CC0 |
| [Microsoft Rocketbox avatars](https://github.com/microsoft/Microsoft-Rocketbox) (12 avatars, textures reduced to 1024 px) | Microsoft | MIT |
| [HD Low Poly Racing Car No.1201](https://assetstore.unity.com/packages/3d/vehicles/land/hd-low-poly-racing-car-no-1201-118603) | Azerilo | Unity Asset Store EULA (free at time of download) |
