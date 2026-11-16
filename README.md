<h1 align="center"><img src="ReadmeFiles/logo.svg" width="48" alt="" align="top">&nbsp;Coupled simulator</h1>

<p align="center">
  <b>Several people in one traffic scene at the same time.</b><br>
  Drivers, AV passengers, pedestrians, cyclists, riders and drone pilots together in real cities, with AI traffic, eHMIs, sound and full logging: a Unity simulator for research on interactions between road users.
</p>

<p align="center">
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-22c55e"></a>
  <a href="https://github.com/bazilinskyy/coupled-sim/actions/workflows/tests.yml"><img alt="Tests" src="https://github.com/bazilinskyy/coupled-sim/actions/workflows/tests.yml/badge.svg"></a>
  <img alt="Unity 6" src="https://img.shields.io/badge/Unity-6000.6-000000?logo=unity&logoColor=white">
  <img alt="URP" src="https://img.shields.io/badge/render%20pipeline-URP-6366f1">
  <img alt="Windows, macOS, Linux" src="https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-0ea5e9">
  <img alt="VR through OpenXR" src="https://img.shields.io/badge/VR-OpenXR-f97316">
  <img alt="Up to 8 participants" src="https://img.shields.io/badge/participants-up%20to%208-0f172a">
  <img alt="20 real cities" src="https://img.shields.io/badge/city%20maps-20%20%2B%20any%20place-94a3b8">
  <a href="#citation"><img alt="Cite: DSC 2020" src="https://img.shields.io/badge/cite-DSC%202020-b91c1c"></a>
  <a href="https://github.com/bazilinskyy/coupled-sim/stargazers"><img alt="GitHub stars" src="https://img.shields.io/github/stars/bazilinskyy/coupled-sim?style=flat&logo=github"></a>
  <a href="https://github.com/sponsors/bazilinskyy"><img alt="Sponsor on GitHub" src="https://img.shields.io/badge/sponsor-GitHub-ea4aaa?logo=githubsponsors&logoColor=white"></a>
  <a href="https://ko-fi.com/bazilinskyy"><img alt="Support on Ko-fi" src="https://img.shields.io/badge/support-Ko--fi-ff5e5b?logo=kofi&logoColor=white"></a>
</p>

<p align="center">
  <a href="#getting-started-step-by-step">Getting started</a> ·
  <a href="docs/first-study.md">First study (tutorial)</a> ·
  <a href="#features">Features</a> ·
  <a href="#short-videos">Videos</a> ·
  <a href="#running-the-simulator">Running</a> ·
  <a href="#configuration">Configuration</a> ·
  <a href="#study-tools">Study tools</a> ·
  <a href="#experimenter-dashboard">Dashboard</a> ·
  <a href="#citation">Citation</a> ·
  <a href="#support-the-coupled-simulator">Support</a>
</p>

The coupled simulator lets several people take part in the same traffic scene at the same time: one or more drivers of manual cars, passengers of automated vehicles (AVs) who control an external human-machine interface (eHMI), and pedestrians who walk with a keyboard or a motion suit, on flat screens or in head-mounted displays. AI-controlled cars and pedestrians fill the scene. Everything that happens is logged for analysis.

![City maps: a Toyota Corolla in Eindhoven, a street in Tokyo, cycling in Ghent (top); heavy rain in London, night in Barcelona, an articulated bus in Eindhoven (bottom)](ReadmeFiles/overview.jpg)

The simulator is open source and free to use. It is aimed at, but not limited to, academic research. Forks, pull requests and other contributions in the spirit of open science are welcome 😍. For enquiries about collaboration, contact p.bazilinskyy@tue.nl.

📺 Demo of the initial version of the simulator with 3 agents (the town only, before the city maps, the new roles and most of the features below; the short videos further down show the current version):

[![demo video](ReadmeFiles/thumbnail_demo_video.jpg)](https://www.youtube.com/watch?v=W2VWLYnTYrM)

<a id="short-videos"></a>🎬 **Short videos** (15 s each, with the simulator's sound; click to play). They were made by the simulator itself in batch mode ([videos from fixed cameras](#videos-from-fixed-cameras), frame by frame with the sound rendered in step), with the participants on the [autopilot](#city-maps-openstreetmap) and the built-in traffic of the city maps; the soundtracks are normalised to -23 LUFS each, so their levels are not comparable with each other:

<table>
<tr><td width="33%" valign="top"><a href="ReadmeFiles/videos/drive_eindhoven.mp4"><img src="ReadmeFiles/videos/drive_eindhoven.jpg" width="100%" alt="Driving in Eindhoven on the autopilot, with traffic"></a><br>Driving in Eindhoven on the autopilot, with traffic</td><td width="33%" valign="top"><a href="ReadmeFiles/videos/cycle_ghent.mp4"><img src="ReadmeFiles/videos/cycle_ghent.jpg" width="100%" alt="Cycling on cobblestones in Ghent"></a><br>Cycling on cobblestones in Ghent</td><td width="33%" valign="top"><a href="ReadmeFiles/videos/walk_delft.mp4"><img src="ReadmeFiles/videos/walk_delft.jpg" width="100%" alt="Walking in Delft in the morning (birds, footsteps)"></a><br>Walking in Delft in the morning (birds, footsteps)</td></tr>
<tr><td width="33%" valign="top"><a href="ReadmeFiles/videos/drone_tue.mp4"><img src="ReadmeFiles/videos/drone_tue.jpg" width="100%" alt="Flying a drone over the TU/e campus in Eindhoven (a map built from its coordinates)"></a><br>Flying a drone over the TU/e campus in Eindhoven (a map built from its coordinates)</td><td width="33%" valign="top"><a href="ReadmeFiles/videos/drone_delft.mp4"><img src="ReadmeFiles/videos/drone_delft.jpg" width="100%" alt="Flying a drone over Delft, with an AI swarm"></a><br>Flying a drone over Delft, with an AI swarm</td><td width="33%" valign="top"><a href="ReadmeFiles/videos/four_agents.mp4"><img src="ReadmeFiles/videos/four_agents.jpg" width="100%" alt="Four participants together in Eindhoven: driver, cyclist, moped rider, pedestrian (a replay)"></a><br>Four participants together in Eindhoven: driver, cyclist, moped rider, pedestrian (a replay)</td></tr>
<tr><td width="33%" valign="top"><a href="ReadmeFiles/videos/rain_london.mp4"><img src="ReadmeFiles/videos/rain_london.jpg" width="100%" alt="Heavy rain in London, right-hand drive"></a><br>Heavy rain in London, right-hand drive</td><td width="33%" valign="top"><a href="ReadmeFiles/videos/snow_montreal.mp4"><img src="ReadmeFiles/videos/snow_montreal.jpg" width="100%" alt="Snow in Montreal, on the autopilot"></a><br>Snow in Montreal, on the autopilot</td><td width="33%" valign="top"><a href="ReadmeFiles/videos/rush_eindhoven.mp4"><img src="ReadmeFiles/videos/rush_eindhoven.jpg" width="100%" alt="Morning traffic and cyclists in Eindhoven"></a><br>Morning traffic and cyclists in Eindhoven</td></tr>
<tr><td width="33%" valign="top"><a href="ReadmeFiles/videos/night_tokyo.mp4"><img src="ReadmeFiles/videos/night_tokyo.jpg" width="100%" alt="Night in Tokyo, following a car (left-hand traffic)"></a><br>Night in Tokyo, following a car (left-hand traffic)</td><td width="33%" valign="top"><a href="ReadmeFiles/videos/dusk_sydney.mp4"><img src="ReadmeFiles/videos/dusk_sydney.jpg" width="100%" alt="Evening in Sydney (left-hand traffic)"></a><br>Evening in Sydney (left-hand traffic)</td><td width="33%" valign="top"><a href="ReadmeFiles/videos/moped_newyork.mp4"><img src="ReadmeFiles/videos/moped_newyork.jpg" width="100%" alt="A moped in Midtown New York"></a><br>A moped in Midtown New York</td></tr>
</table>

## Contents
- [Citation](#citation) · [Examples of use](#examples-of-use) · [License](#license)
- [Features](#features) · [Agents at a glance](#agents-at-a-glance) · [Graphics](#graphics)
- [Getting started, step by step](#getting-started-step-by-step) (for first-time users) · [Installation](#installation)
- [Running the simulator](#running-the-simulator): [host](#running-as-a-host), [client](#running-as-a-client), [several agents on one machine](#several-agents-on-one-machine), [builds and command line](#builds-and-command-line-options), [automatic trials](#running-trials-automatically), [trial files and counterbalancing](#trial-files-and-counterbalancing), [recording videos](#recording-trial-videos), [dashboard](#experimenter-dashboard)
- [Controls](#controls)
- [Environment and vehicles](#environment-and-vehicles): [city maps](#city-maps-openstreetmap), [traffic signs](#traffic-signs), [vehicles](#vehicles)
- [Networking](#networking)
- [Data logging](#data-logging), [road network export](#exporting-the-road-network)
- [Configuration](#configuration): [experiments](#experiment-definitions), [new experiments](#setting-up-a-new-experiment), [agents](#configuration-of-agents), [waypoints](#configuration-of-waypoints), [AI pedestrians](#ai-pedestrians), [AI cars](#ai-cars), [cyclists and riders](#cyclists-e-scooter-and-moped-riders), [drones](#drones), [automated driving and take-over](#automated-driving-and-take-over-requests), [scripted paths](#scripted-paths), [background traffic](#configuration-of-background-traffic), [SUMO](#traffic-from-sumo), [animals](#animals), [time of day and weather](#time-of-day-and-weather), [eye contact](#configuration-of-drivers-eye-contact), [daylight](#configuration-of-daylight-conditions), [traffic lights](#configuration-of-traffic-lights), [eHMIs](#configuration-of-ehmis), [auditory eHMIs](#auditory-ehmis), [car prefabs](#details-on-editing-car-prefabs)
- [Study tools](#study-tools): [participant responses](#participant-responses), [designed eHMIs](#designed-ehmis), [in-car displays](#in-car-displays-head-up-display-centre-screen-instrument-cluster-and-timed-messages), [haptic cues](#haptic-cues), [sound](#sound), [several participants in one car and voice](#several-participants-in-one-car-and-voice), [pedestrian groups](#pedestrian-groups), [gaze over the network](#gaze-over-the-network-and-gaze-triggers), [OpenSCENARIO](#openscenario), [videos from fixed cameras](#videos-from-fixed-cameras), [scene descriptions and AI agents](#scene-descriptions-and-ai-agents)
- [Testing](#testing)
- [Hardware](#hardware): [monitors](#several-monitors-and-mirrors), [headsets](#head-mounted-displays-vr), [wheels](#steering-wheels-pedals-and-gamepads), [motion suit](#xsens-mvn-motion-suit), [motion platforms](#motion-platforms), [network](#network)
- [FAQ](#faq) · [Support](#support-the-coupled-simulator)
- [Used assets](#used-assets)

**Note on the git history (November 2026):** the history was rewritten to keep only files that the simulator still uses, which cut a clone from 5.2 GB to 0.7 GB. Clones and forks made before then cannot be merged with the new history: clone the repository again. Older study branches and the full earlier history are kept in an archive repository; ask the maintainer for access.

## Citation
If you use the coupled simulator for academic work, please cite:

> Bazilinskyy, P., Kooijman, L., Dodou, D., & De Winter, J. C. F. (2020). Coupled simulator for research on the interaction between pedestrians and (automated) vehicles. 19th Driving Simulation Conference (DSC). Antibes, France.

## Examples of use
1. Get out of the way! Examining eHMIs in critical driver-pedestrian encounters in a coupled simulator - [repo](https://github.com/bazilinskyy/coupled-sim-evasive), [paper](https://doi.org/10.1145/3543174.3546849).
1. Stopping by looking: A driver-pedestrian interaction study in a coupled simulator using head-mounted displays with eye-tracking - [paper](http://doi.org/10.1016/j.apergo.2022.103825).
1. Enhancing cyclist safety in the EU: A study on lateral overtaking distance across seven scenarios using lab and crowdsourced methods - [paper](https://bazilinskyy.github.io/p/sapienza2025enhancing), [doi](https://doi.org/10.1145/3744333.3747813).

## License
The source code of the coupled simulator is released under the [MIT License](LICENSE). Third-party assets included in this repository (see [Used assets](#used-assets)) are not covered by this license. They remain under the license of their developers, such as the Unity Asset Store EULA, MIT, CC0 or CC BY 4.0 (with the credits in [Used assets](#used-assets)).

## Features
- **Ready-made simulators for Windows and macOS** on the [Releases page](https://github.com/bazilinskyy/coupled-sim/releases), with the tutorial [Your first study](docs/first-study.md): a complete small study without Unity.
- Up to **8 human participants** in one scene (a host and seven clients) over a local network; several of them can sit in one car and talk to each other (voice is recorded).
- Roles: driver of a manual car, passenger of an AV (controls its eHMI), pedestrian, cyclist, e-scooter rider, moped rider, drone pilot.
- Input: keyboard, gamepads and steering wheels with pedals (Logitech, Thrustmaster, Fanatec, Moza; profiles and in-simulator calibration; force feedback on Logitech wheels), Xsens MVN motion suit for pedestrians.
- Output: flat screens (also several monitors side by side, and mirror views) or head-mounted displays through OpenXR (Meta Quest, Varjo, HTC Vive, Valve Index, HP Reverb, Pico), with eye-tracking logs on headsets that have eye tracking.
- AI cars that follow waypoint paths with configurable speed, acceleration, yielding, blinkers and driver eye contact.
- Realistic car physics: engine and automatic gearbox, brakes with ABS, tyre grip that drops in the rain, suspension and anti-roll bars, matched to the real cars.
- Background traffic with a choice of car-following models (IDM, Gipps, Krauss) that follows other cars, stops at red lights, slows down for bends and yields to pedestrians; on city maps a realistic mix of 18 cars, vans, trucks and buses that also turn at junctions, and AI cyclists near the kerb.
- Participants can also be cyclists, e-scooter riders or moped riders, with two-wheeler dynamics (power, legal speed limits, lean and steering).
- Drones: a participant can fly a quadrotor (gimbal camera, line of sight or chase view), and AI drones deliver parcels, patrol and fly as a swarm; quadrotor rigid-body dynamics with a PX4-style flight controller, Dryden turbulence in the scene's wind, and flocking by the model of Vásárhelyi et al. (2018).
- Animals moving around the world (pigeons, dogs, cats, foxes, deer, horses) that wander, eat and flee from traffic and people; traffic stops for them.
- Time of day (solar position) and weather (clouds, rain with wet roads, fog), set per trial; URP graphics with post-processing and ambient occlusion, and a quality level that adapts to the PC (`-quality auto`).
- 20 city maps from OpenStreetMap on six continents (Eindhoven, Delft, London, Tokyo, New York, Cairo, São Paulo, Sydney, ...), built at load time, and maps of any place by coordinates; real 3D buildings in Delft, Eindhoven, Tokyo, New York and Montreal; windows light up at night.
- Coupling with [SUMO](https://eclipse.dev/sumo/) over TraCI: SUMO traffic in the scene, participants' cars and the AV in SUMO.
- AI pedestrians with individual walking speeds, gap-acceptance crossing decisions that react to yielding cars and eHMIs, collision avoidance (social force model), traffic-aware head movements, and 12 realistic avatars.
- eHMIs on the hood, roof and windshield of AVs, switched by the passenger or the host; auditory eHMIs (cues on eHMI states and on yielding, stopping, driving off) and AVAS sounds.
- Interfaces described in small files instead of prefabs: eHMIs (light bars, text, arrows, chevrons, shapes, animations; on the car, its bumper, projected on the road, on infrastructure or wearables) and in-car displays (head-up display, centre screen with live speed and take-over countdown); haptic cues on gamepads, headset controllers, wheels and external devices (UDP).
- Continuous responses of every participant (hold a button, e.g. "safe to cross"; a 0-100 slider, e.g. trust), logged every frame.
- Virtual companion pedestrians that follow, cross or hesitate on cue, or decide with a language model; every participant's gaze on the host, with cars that react to being looked at.
- Sound with calibrated dB levels: tyre-road and engine sound of every car by speed, the participant's car from inside, cabin music, city ambience.
- Motion cues for motion platforms over UDP; tyres from Pacejka Magic Formula coefficients.
- ASAM OpenSCENARIO playback; videos of trials from fixed cameras (also in builds and batch mode) for online studies; text descriptions of the scene for language-model studies.
- Automated driving with take-over requests (visual and auditory warning, reaction times logged) and hand-back to the automation; AVs that follow waypoints, a car-following model or a path file (speed profiles, recorded trajectories).
- Traffic lights with programmable sequences, with turn arrows and protected turn phases on city maps; traffic signs (stop, give way, speed limits, crossings, one way, ...) and working traffic lights, placed from OpenStreetMap on city maps; day and night versions of the town.
- Logging of all agents (cars, pedestrian skeletons, drones, traffic lights, eye contact) to binary files, conversion to CSV, and live streaming over UDP; replay of every trial with a free camera; export of the road network (GeoJSON, OBJ) for analysis.
- Automatic trial sequences with per-trial parameters and video recording; trial files (CSV/JSON) with counterbalancing per participant (balanced Latin square).
- An experiment setup window: new experiments from templates on any map, a check of the whole set-up before Play.
- A web dashboard for the experimenter: run the session, switch eHMIs, weather and time from any device, and watch a live map of all agents.

## Agents at a glance
Every kind of agent in the simulator, as the smoke test's line-up (`-smokeLineup`, see [Testing](#testing)) parks them on Delft's Markt: children and adults (Microsoft Rocketbox avatars), a cyclist, an e-scooter rider and a moped rider, with the drone hovering at the right edge (top); the animals: pigeon, cat, dog, husky, fox, deer and horse (bottom). Participants can take the roles of a driver, an AV passenger, a pedestrian, a cyclist, an e-scooter or moped rider and a drone pilot; all of them also exist as AI agents.

![Pedestrians, riders and a drone (top) and the animals (bottom) on the Markt in Delft](ReadmeFiles/agents.jpg)

The vehicles are shown under [Vehicles](#vehicles).

## Graphics
The simulator uses the **Universal Render Pipeline** (URP 17.6): the pipeline asset is `Assets/URPDefaultResources/Fantastic.asset` (HDR, 4× MSAA, soft shadows up to 150 m, forward rendering; the 24 street lamps nearest to the viewer are lit) with its renderer `Default_Forward_Renderer` (screen-space ambient occlusion). Post-processing (ACES tonemapping, bloom, colour grading, vignette) comes from the global volume `Post-processing (URP)` in the environment scenes with `Assets/Settings/Rendering/SimulatorVolumeProfile.asset`; every participant camera gets post-processing and SMAA anti-aliasing when it starts rendering (`UrpCameraSetup`). Materials were converted with the Render Pipeline Converter; `Assets/Editor/Graphics/GraphicsSetup.cs` holds the conversion steps (including baking the Tree Creator trees to normal meshes).

![Barcelona from 120 m with its aerial photo, Delft from above, cobblestones in Ghent, headlights at night in Eindhoven](ReadmeFiles/graphics_overview.jpg)

**Quality levels** for slower PCs and VR (`-quality <name>` on the command line, or `Edit` ➡️ `Project Settings` ➡️ `Quality`). `-quality auto` chooses by itself: a few seconds after a scene starts it measures the frame rate and steps down (Fantastic, Balanced, Performance) while it is below 25 fps (`-targetFps` to change); on a laptop GPU (Intel UHD 630) Performance draws a city map in 59 ms where Fantastic takes 150 ms:

| Level | Shadows | Anti-aliasing | Ambient occlusion | Render scale | 1080p view on an Intel UHD 630 |
| --- | --- | --- | --- | --- | --- |
| `Fantastic` (default) | 4 cascades to 150 m, 4096, soft | MSAA 4× | yes | 1 | about 100 ms |
| `Balanced` | 2 cascades to 100 m, 2048, soft | MSAA 2× | yes | 1 | about 70 ms |
| `Performance` | 1 cascade to 60 m, 1024, hard | none | no | 0.8 | about 47 ms |

Shadows cost most (about 40% of the frame on that GPU). In the town, the trees have mesh LODs, flat surfaces cast no shadows, and occlusion culling is baked (`Coupled Sim` ➡️ `Graphics` ➡️ `Optimise scenes` redoes this after changing the scenes); the smoke test measures the render time of the participant's view (`-smokeBench` also measures the cost of each feature).

## Getting started, step by step
This part is for anyone who has never used Unity or GitHub. Follow the steps in order; each takes a few minutes, except the first opening of the project (step 4), which takes up to an hour. Words in **bold** are the names of buttons or windows you will see on the screen.

### Words you will meet
| Word | What it means here |
| --- | --- |
| **Unity** | The program the simulator is made with. You open the simulator in it, like you open a document in Word. |
| **Unity Hub** | A small program that installs Unity and opens Unity projects. |
| **Project** | The simulator's folder. Unity calls a folder like this a project. |
| **Scene** | One "world" of the simulator: the start screen (`StartScene`), the town (`MainScene`), a city map (`OsmMap`). |
| **Play / Play mode** | The ▶ button at the top of Unity starts the simulator inside Unity; pressing it again stops it. |
| **Host** and **client** | Every computer taking part is an *agent*. One of them is the **host**: it chooses the experiment and starts it. The others are **clients**: they connect to the host. Alone, you are the host. |
| **Role** | Who a participant is in the scene: driver, passenger of an automated car (AV), pedestrian, cyclist, e-scooter or moped rider. |
| **Experiment** | A ready-made situation: a world, the roles in it, the AI cars and pedestrians, the weather. |
| **Trial** | One run of an experiment with one participant. A session is usually several trials. |
| **Inspector** | The window on the right of Unity that shows the settings of whatever you clicked. |
| **Prefab** | A saved, reusable object (a car, a pedestrian, a whole experiment). Changing the prefab changes every copy of it. |
| **Spawn point** | The place where a participant starts in the world. |

**New to it? Start with the tutorial [Your first study](docs/first-study.md)**: a complete small study (an eHMI on an automated car and a pedestrian, two participants, counterbalanced trials, responses and logs, a short analysis) with the ready-made simulator from the [Releases page](https://github.com/bazilinskyy/coupled-sim/releases), without Unity. The steps below set up Unity, which you need to make new experiments.

### 1. What you need
- A computer with **Windows 10 or 11** (or a Mac, for trying things out: steering wheels, headsets and the motion suit only work on Windows).
- About **30 GB of free disk space** (Unity takes about 10 GB, the simulator about 15 GB once opened).
- An internet connection and a free **Unity account** (make one at [unity.com](https://unity.com) with your university e-mail; the free "Personal" license is enough).
- For a good frame rate: a graphics card such as an NVIDIA GeForce RTX 3060 or better. On a laptop or office PC the simulator also runs, but slower (see [Graphics](#graphics) for the faster quality levels).

### 2. Install Unity Hub and Unity
1. Download **Unity Hub** from [unity.com/download](https://unity.com/download) and install it like any other program.
2. Open Unity Hub and **Sign in** with your Unity account. If it asks for a license, choose the free **Personal** license.
3. You do not have to install Unity itself yet: in step 4, Unity Hub notices which version the simulator needs (**6000.6.9f1**, also called Unity 6.6) and offers to install it. When it asks which **modules** to add, you need none to use the simulator inside Unity. Tick **Windows Build Support (Mono)** only if you want to make a stand-alone program for the lab PCs from a Mac (on Windows it is included); you do not need IL2CPP, Android, iOS or WebGL. Windows may ask for administrator permission while installing: that is normal.

Use exactly version 6000.6.9f1. A newer Unity may change or break the project; an older one cannot open it.

### 3. Download the simulator
The easiest way, without typing commands:
1. Install **GitHub Desktop** from [desktop.github.com](https://desktop.github.com) (you can skip signing in to GitHub).
2. In GitHub Desktop choose **File** ➡️ **Clone repository** ➡️ the **URL** tab, and paste `https://github.com/bazilinskyy/coupled-sim`.
3. Under **Local path** choose a short folder path **without spaces**, for example `C:\Unity\coupled-sim` (long paths cause errors on Windows). Press **Clone** and wait until it finishes (about 0.7 GB).

Later, to get the newest version, open GitHub Desktop and press **Fetch origin** and then **Pull origin**. If GitHub Desktop says you have local changes (Unity changes a few files by itself), choose **Stash changes and continue**; your own experiments are best kept in new files (step 9), so nothing of yours is lost.

(If you know git: `git clone https://github.com/bazilinskyy/coupled-sim.git` does the same. Downloading the ZIP file from GitHub also works, but then you cannot update it easily.)

### 4. Open the project in Unity
1. In Unity Hub, go to **Projects** and press **Add** ➡️ **Add project from disk**. Select the `coupled-sim` folder you just downloaded (the folder that contains `Assets`), and confirm.
2. The project appears in the list. If Unity Hub shows a warning that the editor version is missing, press **Install** (this is the step-2 install of Unity 6000.6.9f1; it takes 10-30 minutes).
3. Click the project to open it. **The first time takes long** (20-60 minutes): Unity prepares all the files. A small window shows "Importing" or "Compiling" with a progress bar that sometimes stands still for minutes; that is normal, do not close Unity. Wait until the Unity window appears and the progress bar at the bottom right is gone.
4. If Unity asks whether to open in **Safe Mode**, choose **Ignore** (this happens when something did not finish preparing; see [When something goes wrong](#11-when-something-goes-wrong)).

### 5. The Unity window
![The Unity window](ReadmeFiles/guide/editor.png)

1. **Play** ▶: starts and stops the simulator. While it runs, the button is blue.
2. **Hierarchy**: what is in the open scene. In `StartScene` there is an object called `Managers` that holds the simulator's settings.
3. **Scene** and **Game**: the **Game** tab shows what a participant sees when you press Play.
4. **Inspector**: the settings of what you clicked (here `Managers`).
5. **Project** and **Console**: the **Project** tab shows the files of the simulator; the **Console** tab shows messages. Red messages are errors (one red line about "light baking" or "OpenCL" can be ignored).

The **Coupled Sim** menu (in the menu bar at the very top of the screen on a Mac, or at the top of the Unity window on Windows) has the simulator's own tools: setting up experiments, checking them, building, and more.

**Open the start scene first.** In the **Project** tab open the folder `Assets` ➡️ `_Scenes` and double-click **StartScene**. The **Hierarchy** then shows `StartScene` with `Managers` in it. The simulator always starts from this scene: if another scene of the simulator (the town, a city map) is open when you press **Play**, Unity starts from `StartScene` anyway, but the settings you change in the **Inspector** (step 10) are those of `StartScene`, so keep it open.

**Closing Unity:** **File** ➡️ **Quit** (on a Mac **Unity** ➡️ **Quit**). If Unity asks whether to save changes to a scene you did not mean to change, choose **Don't Save**.

### 6. Your first drive (alone, on one computer)
1. Press **Play** ▶. The **Game** tab shows three buttons:

   ![Start screen](ReadmeFiles/guide/start_screen.png)

   **1** Start host (you run the experiment), **2** Start client (you join someone else's experiment), **3** Convert log to csv (turn recorded data into a spreadsheet file, see step 7).
2. Press **Start host**. You now see the host's choices:

   ![Choosing the experiment and the role](ReadmeFiles/guide/host_lobby.png)

   1. **Experiment**: click the experiment. **Demo with 3 agents** is a good first one (the arrow `<--` shows which one is selected).
   2. **Role binding**: click the role you want to play, for example **Driver**. "Host role" then shows the role's number instead of -1 (the roles are counted from 0, so Driver, the first one, is 0).
   3. **Mode**: click **Keyboard mode** to play on the screen with keyboard and mouse (**HMD mode** is for a VR headset, **Suite mode** for the Xsens motion suit).
   4. Press **Initialise experiment** (the top button in the picture).
3. Wait until the world has loaded (up to a minute). The screen then shows who is ready and a **Start simulation** button:

   ![Ready to start](ReadmeFiles/guide/host_ready.png)

   **Ready: 1** means your computer is ready; the other columns are for other computers (clients) and stay 0 when no client is connected, which is fine. Press **Start simulation**.
4. Drive! First **click once in the Game view**, so the keys go to the simulator:

   ![Driving](ReadmeFiles/guide/driving.jpg)

   - `W` / `S` (or the arrow keys): accelerate / brake; `A` / `D`: steer; `Space`: brake; `R` then `W`: reverse (only when the car stands still); `F`: forward again; `Q` / `E`: blinkers; `Esc`: end the drive.
   - As a pedestrian: `W` `A` `S` `D` to walk, `Shift` to run, hold the right mouse button and move the mouse to look around.
   - All keys are listed under [Controls](#controls). The buttons and numbers in the corner are for the experimenter: **Visual syncing** flashes the screen to line up video recordings with the data, and the frame rate (FPS) shows how smoothly the simulator runs (60 or more is good). `Tab` hides them; press `Tab` again to show them.
5. To stop, press **Play** ▶ again. Everything you changed while playing is undone; the data that was recorded stays (step 7).

### 7. Where your data are
Every run writes its data into the folder **`ExperimentLogs`** inside the `coupled-sim` folder, next to the `Assets` folder. To open it from Unity: in the **Project** tab right-click the `Assets` folder and choose **Show in Explorer** (Windows) or **Reveal in Finder** (Mac); this opens the `coupled-sim` folder, where you see `ExperimentLogs`. The folder only appears after your first run. The files are named after the role and the date and time, for example `HostLog-Driver-2026_11_13_14_05_22.binLog`; some experiments write extra `.csv` files next to it.

`.binLog` files are compact but cannot be opened in Excel. To turn one into a spreadsheet file:
1. Press **Play**, and on the start screen press **Convert log to csv** (3).
2. Click the file you want (the newest is at the top). Ignore **Custom reference points** (they are for measuring from a chosen point, such as a crossing) and, under **Stored reference Point**, press **Transform with World Root**.
3. A line "Saved ..." shows where the new file `World Root-HostLog-....csv` is (in `ExperimentLogs`). Open it in Excel, R or Python.

In Excel, open it with **Data** ➡️ **From Text/CSV** and choose **Semicolon** as the delimiter. The numbers use a decimal point; if Excel shows them as dates or text (computers set to Dutch or German use a decimal comma), choose **Transform Data**, then **Use Locale** "English (United States)" for the number columns.

To watch a run again, press **Replay a recording** on the start screen instead (see [Data logging](#data-logging)).

What is in these files, and the other files there (pedestrian decisions, eye gaze, take-over times, animals), is described under [Data logging](#data-logging).

### 8. With several participants (several computers)
1. Install the simulator on every computer as above, **the same version on all** (in GitHub Desktop: Fetch and Pull on each).
2. Connect all computers to the same network (the same lab switch or Wi-Fi). The first time you press **Start host** or **Start client**, Windows asks whether to allow Unity on the network: tick **Private networks** (and **Domain networks** on university PCs) and press **Allow access**. See [Network](#network) if the computers cannot find each other.
3. On the host computer: press **Play** and **Start host**. The **Console** shows a line `Listening on ...` with the host's address (four numbers with dots, for example `192.168.1.20` or `10.0.0.5`).
4. On each other computer: press **Play** and **Start client**. The address box already holds `192.168.1.11`: replace it with the host's address. The **Connect** button appears only when the address is complete (four numbers); press it, then choose the mode (for example **Keyboard mode**).
5. On the host, the clients appear ("Connected: 1", ...). Choose the experiment, give each participant a role under **Role binding** (one row per computer), press **Initialise experiment**, wait until every column under **Ready** shows 1, and press **Start simulation**.

The experimenter can also do all of this from a phone or tablet in the same network with the [dashboard](#experimenter-dashboard).

### 9. Making your own experiment
Open **Coupled Sim** ➡️ **Experiments** ➡️ **Setup…**. The window has three tabs.

**New experiment**: give it a name, pick a **Template** (start from **Base** for the town, or **CityMap** for a real city), choose **Where** it takes place (the town, one of 20 cities, or any place by coordinates), untick roles you do not need, choose traffic, animals, time of day and weather, and press **Create and register**. Then place the spawn points:
1. Press **Open it to place spawn points**. Unity opens the experiment's world with a copy of your experiment in it (for city maps, the experiment itself opens).
2. In the **Hierarchy**, unfold the experiment and click a spawn point; move it with the arrows in the **Scene** view.
3. When all are in place, click the experiment at the top of the **Hierarchy**, and in the **Inspector** choose **Overrides** ➡️ **Apply All**. This saves the positions into the experiment.
4. Delete the copy from the **Hierarchy** (right-click ➡️ **Delete**), save the scene (`Ctrl+S`, on a Mac `Cmd+S`), and open **StartScene** again (step 5).

![New experiment](ReadmeFiles/guide/setup_new_experiment.png)

**Trials**: plan a session in a spreadsheet instead of clicking through it (step 10). Choose a trial file and a participant number and press **Preview** to see that participant's order:

![Trials](ReadmeFiles/guide/setup_trials.png)

**Check**: press **Check experiments and trials** to find mistakes before you run anything. Errors (red) must be fixed; warnings (yellow), such as older experiments that are not registered, can be ignored. The same check runs every time you press Play.

![Check](ReadmeFiles/guide/setup_check.png)

More detail: [Setting up a new experiment](#setting-up-a-new-experiment).

### 10. Running a whole session automatically
For a real study you usually want each participant to get a fixed list of trials, in a counterbalanced order, without clicking:
1. Make a trial file: in the **Project** tab open `Assets/StreamingAssets/Trials`, click `example.csv`, press `Ctrl+D` (Mac `Cmd+D`) to copy it, and give the copy a new name. Open it in Excel (or double-click it in Unity). Each row is one trial: the experiment, the role, the conditions (weather, time, ...), and a `block` letter for the conditions whose order should change between participants. Keep the lines that start with `#` (they hold settings). Save it as CSV.
2. **With Play off**, click `Managers` in the **Hierarchy**. In the **Inspector**, under **Networking Manager**: type the file name in **Trial File** (or press **Use in StartScene** in the **Trials** tab of step 9), the participant's number in **Participant**, and tick **Run Trial Sequence Automatically** (and **Hide Gui**, so the participant does not see the buttons). Save the scene (`Ctrl+S` / `Cmd+S`); changes made while playing are lost.

   ![Trial settings](ReadmeFiles/guide/inspector_networking_manager.png)

3. Press **Play**: the first trial starts by itself. **Esc** ends a trial and starts the next one; after the last trial the simulator stops.
4. For the next participant, change **Participant** (Play off) and save. To go back to choosing by hand (step 6), untick **Run Trial Sequence Automatically** and **Hide Gui**.

The log files of that session start with the participant and trial number (for example `HostLog-P03-T02-...`), and `ExperimentLogs/Session-P03-....csv` lists the order this participant got. Details: [Trial files and counterbalancing](#trial-files-and-counterbalancing).

### 11. When something goes wrong
| What you see | What to do |
| --- | --- |
| Unity Hub says the editor version is not installed | Press **Install** next to the project (step 4). Use 6000.6.9f1, not another version. |
| Unity asks about **Safe Mode** when opening | Choose **Ignore**. If red errors stay in the **Console**, close Unity, delete the `Library` folder inside `coupled-sim`, and open the project again (Unity rebuilds it). |
| Pressing **Play** shows a dialog **Experiment setup problems** | The check (step 9) found a mistake. Read it, fix it, or press **Play anyway** if you are only trying things out. |
| Pressing **Play** shows no buttons | Open **StartScene** (step 5). If it is open, press `Tab` (it hides and shows the buttons) and check that **Hide Gui** on `Managers` is unticked. |
| **Convert log to csv** shows no files | Nothing has been recorded yet: run an experiment first (step 6). |
| Nothing happens when you press keys | Click once inside the **Game** view first. |
| Parts of the world are **pink** | A material was not converted for the current graphics; see the [FAQ](#faq). |
| The simulator is very slow | Close other programs, or use a lower quality level (see [Graphics](#graphics)). In Unity, close the **Scene** tab while playing. |
| Clients cannot connect | Same network? Same simulator version? Firewall allowed? See [Network](#network). |
| The steering wheel does nothing | While playing, press `F2` to open the wheel calibration window and follow it: |

![Wheel calibration (F2)](ReadmeFiles/guide/wheel_calibration.png)

If you are stuck, ask in the lab or open an issue on GitHub with a screenshot of the **Console**.

## Installation
**Without Unity:** ready-made simulators for Windows and macOS are on the [Releases page](https://github.com/bazilinskyy/coupled-sim/releases): unzip and run (on macOS, right-click the app and choose Open the first time). They take the same command-line options as below and contain the city maps, the trial files and the designed eHMIs (`StreamingAssets`), which you can edit without Unity. To change experiments, scenes or code you need the project in Unity:

Step by step: see [Getting started](#getting-started-step-by-step). In short: Unity **6000.6.9f1** (Unity 6.6) through Unity Hub, with Windows Build Support to build for the lab PCs; clone `https://github.com/bazilinskyy/coupled-sim.git`; add the folder in Unity Hub. The first import takes 20-60 minutes.

Platforms: the simulator is developed and used on Windows 10 and 11, and also runs on macOS (tested on macOS 15). Input and output devices (steering wheel, motion suit, HMD) are supported on Windows only. Unity stops providing the editor for Intel Macs after Unity 6.7 LTS. The version of the simulator for Unity 2022.3 is in the archive repository (see the note on the git history above).

## Running the simulator
Open `Assets/_Scenes/StartScene.unity`. All settings for a run are on the `Managers` game object, mainly in its `NetworkingManager` and `PlayerSystem` components.

![The Unity window with StartScene open](ReadmeFiles/guide/editor.png)

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
| `-quality Fantastic\|Balanced\|Performance\|auto` | [graphics quality](#graphics) for slower PCs (`auto`: picked from the measured frame rate) |
| `-sumoConnect <host:port>` | connect to a running [SUMO](#traffic-from-sumo) |
| `-views "LeftScreen:2,RightScreen:3,RearMirror:1"` | [extra monitors and mirrors](#several-monitors-and-mirrors) |
| `-replay <binLog>` | play a recording back (see [Data logging](#data-logging)) instead of the start screen |
| `-benchmark <seconds> <file>` | measure the frame rate once the simulation runs (average and 1% low), write it to the file and quit; with `-autoRun` |
| `-screenshot <seconds> <file>`, `-quitAfter <seconds>` | a screenshot after that time; quit after that time (tests, documentation) |
| `-response button,slider[,hidden]` | [participant responses](#participant-responses) for this agent's role |
| `-ehmiDesign <name>[@Hood\|Top\|Windshield\|Bumper\|Road]`, `-ehmiState WALK` | a [designed eHMI](#designed-ehmis) on AV roles; its first state |
| `-hud <name>`, `-centreScreen <name>` | [in-car displays](#in-car-displays-head-up-display-centre-screen-instrument-cluster-and-timed-messages) |
| `-cluster <name>\|none`, `-inCarMessages <name>` | [instrument cluster and timed messages](#in-car-displays-head-up-display-centre-screen-instrument-cluster-and-timed-messages) |
| `-carSpec smart\|hatchback\|suv\|estate\|van\|bus\|truck` | [vehicle physics of another vehicle type](#vehicles) |
| `-driverCar <name>`, `-passengerCar <name>` | [the vehicle of the drivers or passengers](#vehicles), e.g. `ToyotaCorolla`, `LandRoverDefender`, `DafXF95`, `ArticulatedBus` |
| `-blinker left\|right` | start with a turn signal on (previews) |
| `-hapticsUdp <host:port>[,...]` | [haptic cues](#haptic-cues) to external devices |
| `-vehicleSounds 1`, `-electricCars 0`, `-ambienceDb <dB>`, `-cabinMusic <file>`, `-cabinMusicDb <dB>`, `-audioReferenceDb <dB>`, `-audioCalibration` | [sound](#sound) |
| `-seatRole "<role>:<car role>:<seat>"`, `-voice [ptt]`, `-voiceDevice <name>`, `-voiceRecordOnly` | [several participants in one car, voice](#several-participants-in-one-car-and-voice) (give `-seatRole` to every agent) |
| `-companions "follow:-1.2,0;cross:1.4,0.4,3;..."` | [pedestrian groups](#pedestrian-groups) (every agent) |
| `-gaze`, `-gazeTrigger "<looker>><car role>:<seconds>:yield,ehmi"` | [gaze over the network](#gaze-over-the-network-and-gaze-triggers) |
| `-motionUdp <host:port>`, `-motionFormat json\|csv\|flypt`, `-motionRate <Hz>` | [motion platforms](#motion-platforms) |
| `-magicFormulaTyres` | [tyres from Magic Formula coefficients](#vehicles) |
| `-roadSurface name` | [one road surface everywhere](#vehicles) (grip, roughness, tyre noise) |
| `-noRealData`, `-flatTerrain`, `-noFacadeRelief`, `-noPhotoTextures`, `-liveReflections 0`, `-noSignals` | city maps from OpenStreetMap alone, flat, with flat façades, with drawn instead of photo wall textures, without live reflections, without traffic lights ([real-world data](#real-world-data-on-the-city-maps); comparisons, slow PCs) |
| `-openscenario <file.xosc>` | [OpenSCENARIO](#openscenario) playback (every agent) |
| `-videoCams "<name>=x,y,z,yaw,pitch[,fov];chase;front;driver"`, `-videoSeconds`, `-videoStart`, `-videoFps`, `-videoSize WxH`, `-videoQuit`, `-videoKeepFrames`, `-ffmpeg <path>` | [videos from fixed cameras](#videos-from-fixed-cameras) |
| `-sceneDescriptions`, `-llmModel`, `-llmEffort`, `-llmEndpoint` | [scene descriptions and AI agents](#scene-descriptions-and-ai-agents) |

For example, a driver (host) and a pedestrian (client) on two PCs, run without anyone at the host:
```
"Coupled Simulator.exe" -autoRun -clients 1 -trials pedestrian_study.csv -participant 4
"Coupled Simulator.exe" -connect 192.168.1.11
```
The host starts the simulation once every connected agent has loaded the level.

### Running trials automatically
![NetworkingManager in the Inspector](ReadmeFiles/guide/inspector_networking_manager.png)

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
The host serves a dashboard at `http://<host ip>:8080` (the address is in the console; change the port with `-dashboardPort N`): one page from which the experimenter runs a session with several participants as it happens, from any computer, tablet or phone on the lab network, without walking to the host PC or to the participants' PCs. It needs no internet and no installation (a plain web page from the simulator); allow TCP port 8080 in the host's firewall to use it from other devices. The `ExperimentDashboard` component sits on `Managers` in `StartScene`.

**Before the trial (Session):**
- start the host, choose the experiment and, for city maps, the city or any place by coordinates (`lat,lon[,radius]`), and the host's own mode (flat screen, headset, motion suit);
- see every participant's PC as it connects (up to 8: the host and players 1-7), give each a role (driver, AV passenger, pedestrian, cyclist, e-scooter or moped rider, drone pilot, ...), and see when each is ready;
- `Initialise experiment` loads the scene on every agent; `Start simulation` starts it for all at the same moment; `Next trial` goes on in automatic trial sequences.

**During the trial:**
- a live map of the scene from above (rendered from the scene, or the aerial photo of a city map) with every participant (ringed), the AV, AI cars, SUMO vehicles, two-wheelers, pedestrians and the traffic lights, and a table of all agents with their kind and position; the header shows the host's state, the scene and its frame rate;
- `Take-over request` sends a take-over request to the AV now (see [automated driving](#automated-driving-and-take-over-requests)); the trial line shows the automation's state;
- eHMI: switch all eHMIs off, to "stop" or to "walk";
- conditions: change the time of day and the weather for every agent at once, and trigger visual syncing (a flash for synchronising external recordings).

![The dashboard during a session with four participants on the Delft map: the host drives (on the autopilot), players 1-3 are a pedestrian, a cyclist and a moped rider; all connected and ready, ringed on the live map with the built-in traffic, the agents listed below](ReadmeFiles/dashboard.jpg)

## Controls
| Who | Action | Keyboard | Steering wheel / gamepad |
| --- | --- | --- | --- |
| Driver | Throttle / brake | `W`/`S` or `↑`/`↓` (axis `Throttle`) | pedals |
| Driver | Steer | `A`/`D` or `←`/`→` | wheel |
| Driver | Brake (axis `Break`) | `Space` | pedal |
| Driver | Forward / reverse gear (reverse engages at standstill; then the throttle drives backwards, up to 25 km/h) | `F` / `R` | buttons of the wheel profile (G27: 6 / 7) |
| Driver | Turn signal left / right / off (the same key again also switches it off; it cancels itself when the wheel returns after a turn) | `Q` / `E` / `2` | buttons of the wheel profile (G27: 5 / 4 / 16) |
| AV passenger | eHMI disabled / stop / walk | `1` / `2` / `3` | |
| AV passenger | Resume driving where the AV waits for input (`WaitInputX`) | `Space` | |
| AV driver | Take over after a take-over request / hand back to the automation | `Enter` (or steer, brake, accelerate) | profile button (G27: 3), wheel, pedals |
| Experimenter | Take-over request | `T` | |
| Pedestrian (keyboard mode) | Walk forward / back, sideways, turn; run | `W`/`S`, `A`/`D`, `Q`/`E` (or arrows); hold `Shift` | |
| Pedestrian (keyboard mode) | Look around | hold the right mouse button and move the mouse | |
| Pedestrian (HMD) | Recenter the headset | `R` | |
| Drone pilot | Forward / back, sideways | `W`/`S`, `A`/`D` | axes set in `DroneController` |
| Drone pilot | Climb / descend, turn | `↑`/`↓` (or `Space`/`Left Shift`), `←`/`→` (or `Q`/`E`) | |
| Drone pilot | Camera: gimbal (FPV), line of sight, chase | `C` (cycles) | |
| Host | Next trial (automatic trials) / show-hide GUI | `Esc` / `Tab` | |
| Experimenter | Steering wheel calibration window | `F2` | |
| Experimenter | Sound calibration tone (1 kHz, full scale) on/off | `F3` | |
| Any participant with [responses](#participant-responses) | Response button (hold) / slider down, up | `B` / `Z`, `X` | right shoulder or right trigger / d-pad; in a headset: right trigger / right thumbstick |
| Any participant with [push-to-talk voice](#several-participants-in-one-car-and-voice) | Talk | hold `V` | |

With a steering wheel, the car's steering wheel turns as far as the real one, without smoothing; keyboard steering is smoothed and steers less at speed. The steering wheels of AVs and of other agents' cars follow how those cars turn. The keyboard pedestrian walks at 1.4 m/s (runs at 3.5 m/s) with a walking animation.

Keyboard bindings are set in `Edit` ➡️ `Project Settings` ➡️ `Input Manager`; wheels and pedals through their profile and calibration (see [Hardware](#steering-wheels-pedals-and-gamepads)). The host can also switch eHMIs and trigger [visual syncing](#data-logging) from its GUI during the simulation.

## Environment and vehicles

The town (`MainScene`, with a night version `MainScene-night`) is a model of a US-like city with:
- a network of 2-lane roads, a loop of 4-lane road and a loop of 6-lane road (partially surrounded by buildings);
- a half-clover interchange for the motorway;
- 10 intersections with traffic lights that can be turned on and off before the experiment or programmatically at runtime;
- 34 zebra crossings;
- static objects (buildings, parked cars, trees).

`DriverTestScene` is a small scene for trying out the driven car.

![The town from above](ReadmeFiles/world.jpg)
![The town at night](ReadmeFiles/night_view.jpg)

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

\* left-hand traffic: spawn points and SUMO traffic keep left, and the participant's car is right-hand drive.

**Right-hand drive.** In cities with left-hand traffic the participant's car (driver and AV passenger roles) has its steering wheel on the right: the body and interior are mirrored, and the driver, passenger, steering wheel, instruments, mirrors and cameras move to the other side; the instrument text stays readable and the driving physics do not change. Each role chooses this with **Steering** in the experiment's role list (`Auto`: right in left-hand-traffic cities; `Left`; `Right`, also in the town). AI cars keep the steering wheel on the left.

![Right-hand drive in London](ReadmeFiles/right_hand_drive.jpg)

**Choosing a map.** Use the `City map (OpenStreetMap)` experiment (`Assets/ExperimentDefinitions/CityMap.prefab`, scene `OsmMap`): the host picks the city in its GUI, or a trial sets it with the parameter `map` (e.g. `map=Tokyo`). The roles (driver, pedestrian, cyclist, e-scooter and moped rider, second driver) use `OsmSpawnPoint`s, which put them on a driving lane, a pavement, a cycle path or the road edge near the map centre, where no tree, parked car, post or wall stands in their place, so the experiment works in every city. Time of day uses the city's location and time zone.

**Built-in traffic and the autopilot.** Without SUMO (or with `-mapTraffic 14`, cars per km of lane; trial parameter `mapTraffic`), a city map gets its own traffic: lane routes across the map from its roads (`MapRoutes`: in at the map's edge, straight on at junctions, turning where the road ends, on the driver's side of the road, out at the edge again), driven by `TrafficCar` (the car-following model; they stop at red lights, wait for their turn arrow, and stop for pedestrians and animals) and started again at the beginning of their route, out of sight, when they reach its end. Each entry gets three routes: one straight on and two that turn off at junctions now and then. The vehicles are a realistic mix (mostly cars, some vans, few trucks, an occasional bus), and cyclists ride on the cycle path or track beside the road where there is one, else on the carriageway near its kerb, 1 m clear of parked cars (6 per km of route, 12 in the Netherlands, Belgium and Denmark). Where OpenStreetMap has street parking (`parking:left`, `parking:right`, `parking:both`), parked cars stand on the road where two lanes still fit beside them, else half on or fully on the kerb, as in narrow Dutch streets, and the lanes run beside them. At junctions without lights, cars give way to crossing, turning and merging traffic that will reach a point of their path first; a car that could no longer stop comfortably before that point (or is on it already) clears the junction instead of stopping in it, and the other gives way (`-debugTraffic` logs overlapping vehicles, yielding and cars stopped for long, every 10 s). Vehicles steer by pure pursuit of a point ahead on their route: they turn at their own speed within their smallest turning circle (about 3 m for a bicycle, 5.5 m for a car, 10-12 m for trucks and buses), so they do not swerve, turn on the spot or circle a point they cannot reach, and they lie on the slope of the road under them. `-autopilot [km/h]` (trial parameter `autopilot`) lets the participant move by itself, e.g. for demonstrations and videos: a car drives along the nearest route with the AV's automated driving, a pedestrian walks along the pavement, a cyclist, e-scooter or 25 km/h moped rider follows a route on the cycle path beside the road, or near the kerb where there is none, and a 45 km/h moped rider on the road, braking for vehicles, people, animals and parked cars on its way, and giving way to vehicles about to cross its path (on every agent with the option).

**Driving anywhere.** The trial parameter `location=lat,lon[,radius]` (e.g. `location=51.4479,5.4904,350` for the TU/e campus) builds a map of any place: the OpenStreetMap data (radius 100-1000 m, default 400 m) is downloaded the first time (internet needed) and cached in the simulator's data folder; with SUMO installed, a SUMO network and random traffic for the place are built too. The host sends the location to the clients. To add a place to the catalogue for good, save its `.osm.gz` in `Assets/StreamingAssets/Maps` and add it to `MapCatalog`.

**Real 3D buildings (Netherlands).** For Dutch maps the buildings come from the [3D BAG](https://3dbag.nl) instead of OpenStreetMap: every building's measured shape with its real roof (gabled canal houses in Delft, flat-roofed blocks in Eindhoven), placed at its real position. Roofs get red or anthracite roof tiles. The façades (brick, plaster or concrete) have windows in the style of the building's construction year from the 3D BAG: tall sash windows before 1920, ribbon windows on post-war blocks, glass curtain walls on newer offices and towers (on other maps chosen by height and OpenStreetMap building type). The data is in `Assets/StreamingAssets/Maps/<Map>.buildings.bin.gz` (Delft and Eindhoven); to make it for another Dutch map in the catalogue (internet needed, about a minute):

```bash
python3 Tools/bag3d_buildings.py Delft
```

**Real 3D buildings from laser scans (Ghent, London, Wellington, Auckland).** Where no open 3D city model exists in a usable format, `Tools/scan_buildings.py <map>` makes one from open data: each building's footprint (Ghent: the Flemish large-scale base map, GRB; elsewhere OpenStreetMap) and its roof from the laser scans (the surface model minus the terrain model, 1 m: Digitaal Vlaanderen DHMV II for Ghent, the Environment Agency's LIDAR composite for London (Open Government Licence v3), Toitū Te Whenua LINZ for Wellington and Auckland (CC BY 4.0)). Up to four roof planes are fitted to each building's scan (RANSAC); the roof is the lowest of them (gables, hips, pyramids, flat roofs), the nearest one where a flat roof has several levels, or flat at the measured height when planes do not explain the scan. Courtyards keep their own walls. Ghent 2,483 buildings, London 605, Wellington 307, Auckland 351 (as many as OpenStreetMap has footprints). Sydney has no keyless open surface model, so it keeps OpenStreetMap's heights.

**Real 3D buildings (Tokyo, New York, Montreal).** These maps use the cities' open 3D city models (CityGML) instead of the OpenStreetMap buildings:
- Tokyo: [Project PLATEAU](https://www.mlit.go.jp/plateau/), the open 3D city models of Japan's Ministry of Land, Infrastructure, Transport and Tourism (MLIT), up to the 229 m of Shibuya Scramble Square;
- New York: the [NYC 3-D Building Model](https://catalog.data.gov/dataset/3-d-building-model) of the City of New York (Office of Technology and Innovation), with Midtown's towers and their setbacks;
- Montreal: the [3D buildings](https://donnees.montreal.ca/dataset/68f256ec-6dac-4921-98f9-9826db1a15fa) of the Ville de Montréal (LoD2, 2009).

They have LoD2 roofs and walls where the model has them, else LoD1 blocks with their measured height. Buildings over a road (station buildings, passages) are raised 5 m, so the road under them stays open. `Tools/citygml_buildings.py` makes the file from a CityGML zip (or `.gml`) of any city: it downloads only the CityGML files the map needs (for PLATEAU only the 1 km tiles of the map), not the textures. The coordinate system is read from the file (PLATEAU, New York's EPSG:2263) or given with `--crs` (Montreal: `EPSG:2950`); others can be added in `crs_of()`:

```bash
python3 Tools/citygml_buildings.py Tokyo https://assets.cms.plateau.reearth.io/assets/e5/29b4f5-f0bf-433d-8f98-deb594bfbebd/13113_shibuya-ku_pref_2023_citygml_2_op.zip
python3 Tools/citygml_buildings.py NewYork https://s-media.nyc.gov/agencies/oti/DA_WISE_GML.zip --member DA12_
python3 Tools/citygml_buildings.py Montreal <the zips of tiles VM03, VM04, VM07, VM08 and VM12 from donnees.montreal.ca> --crs EPSG:2950
```

For another Japanese map, take the CityGML zip of its ward from the dataset page on [geospatial.jp](https://www.geospatial.jp/ckan/dataset?q=plateau).

![Tokyo (Shibuya) with PLATEAU buildings](ReadmeFiles/tokyo_plateau.jpg)

![New York (Midtown, left) and Montreal (right) from their open 3D city models](ReadmeFiles/citygml_newyork_montreal.jpg)

Maps without such a file use the OpenStreetMap buildings.

**Real aerial photos (ground, roofs and surroundings).** Ten maps have the city's open aerial photo, about 0.2 m per pixel: Delft, Eindhoven, Ghent, Barcelona, Tokyo, New York, Boulder, Sydney, Wellington and Auckland (Toitū Te Whenua LINZ, CC BY 4.0, read from its cloud-optimised GeoTIFFs; needs tifffile and imagecodecs). It lies on the ground wherever no road, pavement, grass or water is mapped, so car parks, yards, squares and building sites look as they really are; on Delft and Eindhoven the national topography's surfaces (below) cover the ground instead, and the photo gives the roofs and the surroundings. A fine grain repeats every 2 m on top, so the photo isn't blurry up close. Flat and low-pitched roofs up to 35 m show their real colours, solar panels and dormers (taller buildings lean in the photo, so their roofs keep plain materials). Pitched roof faces (steeper than 30°) would smear in a photo taken from above: they get clay pantiles instead, red or anthracite as the photo shows them there, with the rows of tiles along the eaves. A coarser photo of the 6 km around the map runs to the horizon for distant and high views, and some haze (always on, also in clear weather) blends it into the sky. The photos are in `Assets/StreamingAssets/Maps/<Map>.aerial.jpg`, `.aerial-far.jpg` and `.aerial.json`. `-noAerial` on the command line turns them off. To make them for a map (internet needed; needs Pillow and numpy; about a minute):

```bash
python3 Tools/aerial_photo.py Delft
python3 Tools/aerial_photo.py --list
```

`--list` shows the sources the tool knows. All are open data without a key: PDOK (the Netherlands), Digitaal Vlaanderen (Flanders), PNOA (Spain), GSI (Japan), NYS ITS (New York State), USGS (the United States, about 1 m per pixel) and NSW Spatial Services (New South Wales). Another source can be added to `SOURCES` and `MAP_SOURCE` in the tool: any XYZ tile server, or a WMS. Tiles are cached in `~/.cache/coupled-sim-aerial`.

![Delft without (top left) and with the aerial photo (top right); Barcelona from 120 m and a street there at eye level (bottom)](ReadmeFiles/aerial_photos.jpg)

**Road surfaces and details.** Roads use OpenStreetMap's `surface` tag:
- brick paving (Dutch "klinkers", `paving_stones` and `bricks`);
- cobblestones (`sett` and `cobblestone`);
- gravel (also `unpaved`, `compacted` and `dirt`);
- asphalt otherwise.

Asphalt gets repair patches and manhole covers, and brick roads get manhole covers too. Every road can get puddles along the kerb, which show only while it rains. All are placed the same way on every agent. At night (sun below 1°), the 8 cars nearest the viewer switch on their headlights, which light the road ahead. These are spot lights without shadows, so they cost little frame time.

![Eindhoven: manhole cover and kerb when dry (left), puddle in rain (middle), headlights at night (right)](ReadmeFiles/road_details.jpg)

**Street furniture and parked vehicles.** From the OpenStreetMap data the map also gets benches, waste bins, bicycle racks with parked bicycles, bollards, bus stops (sign, and shelter with bench where mapped), café terraces, recycling containers, street cabinets, garden walls, fences and hedges, and parked cars in parking lots, parking spaces and along streets with street-side parking (at most 250 cars and 400 bicycles, those nearest the centre, so the frame rate stays up). Placement is the same on every agent. The parked bicycle and car prefabs are in `Assets/Prefabs/Props`; to rebuild them after changing the bicycle or car models use `Coupled Sim` ➡️ `Maps` ➡️ `Build street props`.

![Delft from the 3D BAG, a parking lot with bicycle racks, bicycles on a square, a bus stop in Eindhoven](ReadmeFiles/city_maps_3dbag.jpg)

![Window styles by age and type: Delft (left), London (right)](ReadmeFiles/facade_styles.jpg)

**Depth in the façades.** Each window style also has a normal map, a height map (parallax) and an ambient occlusion map made from the same layout, so windows sit back in their reveals behind their frames, sills and lintels stand out and brick has mortar joints, and the light, shadows and screen-space ambient occlusion (radius 25 cm) follow that relief. `-noFacadeRelief` draws flat façades (for comparisons or slow PCs).

**Photo textures of the walls.** Up close, the walls show photos of real brick, plaster and concrete (CC0 textures from [ambientCG](https://ambientcg.com): two red bricks, a beige brick, plaster, concrete; with their normal maps, so mortar joints catch the light) instead of drawn bricks. They are URP detail maps over the façade textures, masked by the window layout, so glass, frames, sills and shop fronts stay clean, and each wall kind keeps its colour (the photos are scaled to mid grey; in `Assets/ThirdParty/Textures/Walls`, assigned by `MapSetup.AddWallTextures`). `-noPhotoTextures` draws the bricks of earlier versions.

![Solid buildings with photo textures of brick: London (top) and Delft, up to the shop fronts (bottom)](ReadmeFiles/building_walls.jpg)

**Lit windows at night.** Every façade texture holds 4 × 4 window bays with their own lights, so at dusk (from 2° sun elevation, full from 4° below the horizon) about a third of the windows light up, mostly warm, some cool white, differently from window to window, on OpenStreetMap and real 3D buildings alike.

![Delft at 23:30: street and aerial view](ReadmeFiles/night_windows.jpg)

![New York at 22:30 (left) and Sydney at 22:00 (right)](ReadmeFiles/city_night.jpg)

**Road networks from OpenDRIVE or SUMO.** A map can also be an [OpenDRIVE](https://www.asam.net/standards/detail/opendrive/) file (`.xodr`) or a SUMO network (`.net.xml` or `.net.xml.gz`): put it in `Assets/StreamingAssets/Maps` and set the trial parameter `map` to its file name (e.g. `map=GridTown.xodr`; a full path also works). The roads are built like those of the city maps: lanes with their widths, markings, pavements and cycle lanes (from the network's pedestrian and bicycle lanes), traffic signals, speed limits, street lamps; spawn points work the same. There are no buildings. With SUMO installed, the `City map` experiment's SUMO traffic runs on these maps too: random cars and bicycles made with SUMO's `randomTrips` the first time (cached). OpenDRIVE files are converted by SUMO's `netconvert`, which must be installed (with `SUMO_HOME` set, as for [traffic from SUMO](#traffic-from-sumo)); the result is cached in the simulator's data folder. The sample `GridTown.xodr` (a 4 × 4 grid) is included, and the same network as `GridTown.net.xml.gz`, which works without SUMO. A network without geographic reference gets Eindhoven's latitude for the sun.

![The GridTown sample from OpenDRIVE](ReadmeFiles/opendrive_gridtown.jpg)

#### Real-world data on the city maps
Where open government data exists, the maps use it instead of what would otherwise be derived from OpenStreetMap:

| Data | Maps | Source (licence) |
|---|---|---|
| **Street and park trees**, at their real place and height (pines, spruces, cypresses and other conifers, and palms, with their own shapes) | Eindhoven, Delft, Ghent, Barcelona, London, Montreal, New York, Sydney, Tokyo (metropolitan roads) | the cities' tree registers (public domain, CC BY 4.0, Modellicentie Gratis Hergebruik, NYC Open Data, Open Government Licence v3 for London's boroughs); OSM's trees fill in where a register is partial |
| **Street lights**: every pole, at its real height, turned to its road | Eindhoven (with lumen and colour), Delft, Sydney | Gemeente Eindhoven (public domain), BGT light poles (CC0), City of Sydney (CC BY 4.0) |
| **Traffic signs**: every sign of the national register, with its number or arrow, facing the traffic it is for, several on one pole | Eindhoven (636), Delft (477) | NDW Verkeersborden (CC0) |
| **Ground surfaces**: carriageways, footpaths, cycle paths, parking bays, verges and green areas at their exact outlines, with the matching tyre grip; drawn instead of the roads, cycle paths and pavements made from OpenStreetMap's lines (which stay as invisible driving surfaces), grass where nothing is mapped | Eindhoven, Delft | BGT, Basisregistratie Grootschalige Topografie (CC0) |
| **Skyline**: the tall buildings (35 m and up) within 4 km around the map, as plain towers with rows of windows (lit at night), so views along the streets end in the city's towers | New York, London, Montreal, Sydney, Tokyo | OpenStreetMap (ODbL), `Tools/skyline.py` |
| **Terrain**: the whole map on the real ground (bare-earth models: no buildings or trees in them), streets and walls following slopes, bridges straight between their ends; the roads' profiles without humps narrower than about 60 m (where a model's gaps under viaducts were filled from the embankments) | all 20 maps; 1-5 m lidar models for 13 of them | AHN4 (Eindhoven, Delft; CC0); USGS 3DEP (New York, Boulder; public domain); Environment Agency LIDAR DTM (London; OGL v3); Digitaal Vlaanderen DHMV II (Ghent); IGN MDT05 (Barcelona; CC BY 4.0); GSI elevation tiles (Tokyo); Geoscience Australia 5 m lidar (Sydney; CC BY 4.0); NRCan HRDEM (Montreal; OGL Canada); LINZ 1 m lidar (Wellington, Auckland; CC BY 4.0); IDE Uruguay 1 m lidar (Montevideo); GEDTM30, 30 m (the other 7; OpenGeoHub, CC BY 4.0) |
| **Weather** that really was at a date and hour (`realWeather=2025-06-01T14:00`) | all maps | Open-Meteo historical weather (CC BY 4.0), cached |

The NDW signs added 18 Dutch sign kinds to `TrafficSign` (cycle and foot paths, parking, no parking and no stopping, closed to vehicles, end of speed limit, mandatory directions, dead end, residential zone). Buildings also got **ground floors** (shop fronts where OSM has a shop, café, restaurant, bar, bank or pharmacy at that stretch of wall, lit softly at night; lobbies under glass façades; front doors elsewhere) and **cornices** along flat roofs. The data is fetched by `python3 Tools/city_data.py <map>` (trees, lamps, signs, BGT surfaces) and `python Tools/terrain_height.py <map>` (needs numpy, Pillow, SciPy; for Wellington and Auckland also tifffile and imagecodecs) into `Assets/StreamingAssets/Maps`; the build log lists what a map used. `-noRealData` builds a map from OpenStreetMap alone and `-flatTerrain` keeps it flat (comparisons).

![Real signs in Eindhoven (speed limit 30 and a cycle path, NDW), shop fronts on the ground floors along a street in Delft, Eindhoven from above with the BGT surfaces and the register's trees and lamps (top); the terrain of Wellington and Cusco, the same street in Delft at night with its shops lit (bottom)](ReadmeFiles/real_world_data.jpg)

Conifers and palms are made from code (`OsmMapGenerator.Trees.cs`, no third-party models): a tapering trunk with tiers of drooping needle branches, and a slender, slightly bent, ringed trunk with a crown of pinnate fronds about half the tree's height long; from the registers' species and OpenStreetMap's `leaf_type`, `genus`, `species` and `taxon` (Eindhoven 130 conifers, Sydney 70 palms). They sway in the wind like the others.

Limitations: outside the Netherlands, building shapes and heights are as good as the OSM data (buildings without a height get 3.5 floors), roofs are flat (their colours come from the aerial photo where the map has one); façades come from five window styles, not the real buildings; terrain is smoothed over a few metres (kerbs and steps are drawn by the map, not the terrain); on Delft and Eindhoven the BGT has no heights, so pavements there are flush with the road (no kerbs); water lies 0.9 m below the streets between brick quay walls everywhere (real canals, rivers and harbours differ); road widths come from OpenStreetMap's tags or estimates, so a tree or post that would stand in the carriageway is moved to its edge; for Cairo, Nairobi, Cusco, São Paulo, Ho Chi Minh City, Luang Prabang and Stellenbosch it comes from a 30 m global model, so small dips and rises are lost.

Map data © OpenStreetMap contributors, available under the [Open Database License](https://www.openstreetmap.org/copyright). Dutch buildings: [3D BAG](https://3dbag.nl) by the 3D geoinformation group of TU Delft and 3DGI, under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Tokyo's buildings: source 3D都市モデル（Project PLATEAU）渋谷区（2023年度）([G空間情報センター](https://www.geospatial.jp/ckan/dataset/plateau-13113-shibuya-ku-2023)), converted to the simulator's format; used under the Public Data License 1.0 ([PLATEAU site policy](https://www.mlit.go.jp/plateau/site-policy/), compatible with CC BY 4.0). New York's buildings: 3-D Building Model, City of New York, Office of Technology and Innovation ([NYC Open Data](https://opendata.cityofnewyork.us/overview/#termsofuse)), converted to the simulator's format. Montreal's buildings: Ville de Montréal, Bâtiment 3D 2009 (Maquette CityGML LOD2 avec textures), [Données ouvertes de la Ville de Montréal](https://donnees.montreal.ca), licence [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); converted to the simulator's format (geometry only, without textures). Aerial photos: [PDOK Luchtfoto](https://www.pdok.nl/introductie/-/article/pdok-luchtfoto-rgb-open-) (Beeldmateriaal Nederland, CC BY 4.0); [Digitaal Vlaanderen](https://www.vlaanderen.be/digitaal-vlaanderen/onze-oplossingen/geografische-webdiensten/ons-gis-aanbod/orthofotomozaieken) orthophoto (Modellicentie Gratis Hergebruik); [PNOA](https://pnoa.ign.es) © Instituto Geográfico Nacional / CNIG (CC BY 4.0); 出典：国土地理院 ([GSI tiles](https://maps.gsi.go.jp/development/ichiran.html), seamless photo); [NYS ITS Geospatial Services](https://orthos.its.ny.gov) orthoimagery; [USGS The National Map](https://www.usgs.gov/programs/national-geospatial-program/national-map) (public domain); © [Spatial Services NSW](https://www.spatial.nsw.gov.au) (CC BY 4.0).

![Eindhoven and Tokyo (top), New York and Sydney (bottom) from about 100 m](ReadmeFiles/city_maps.jpg)

### Lane markings on city maps
Roads are marked from OpenStreetMap's lane tags: the lanes of each direction (`lanes`, `lanes:forward`, `lanes:backward`; else from the width) with dashed lines between lanes of one direction and a centre line between the directions (dashed on two-lane roads, a double line from three lanes), turn arrows 12 m before the junction a lane leads to (`turn:lanes`, `turn:lanes:forward`, `turn:lanes:backward`: through, left, right, slight turns, merges, U-turns and combinations), cycle lanes on the road (`cycleway=lane` and its sides: a line 1.5 m from the edge, on red asphalt in the Netherlands and Belgium) and bus lanes (`busway=lane`, `bus:lanes`, `psv:lanes`, `lanes:bus`: the outermost lane behind a solid line, with BUS painted every 60 m). Barcelona has 40 turn arrows, 34 bus lane pieces and 4 cycle lanes; Eindhoven 57 turn arrows.

### Traffic lights on city maps
City maps get working traffic lights where OpenStreetMap has them (`highway=traffic_signals`, and crossings tagged `crossing=traffic_signals`): a signal head on a mast arm over each approach lane, with a stop line; a signal mapped just before a junction (the usual Dutch and German mapping, with or without `traffic_signals:direction`) serves only the traffic towards it, a signal on the junction node every approach. Signalised crossings get pedestrian lights on both kerbs, and dashed lines instead of zebra stripes unless mapped as a zebra. Signals within 40 m of each other form one junction with one controller: the approaches are split into the main direction (the widest road) and the crossing direction, each green in turn (24 s and 16 s, 3 s amber, 2 s all red; red and amber before green in the UK, Germany, Austria and Poland), and pedestrians walk while the road they cross is red, their green blinking for the last 5 s. A signal on one road only (a crossing in a street) alternates the cars with the pedestrians. Junctions start at different points of their cycle (the same on every agent).

**Turn arrows.** An approach whose `turn:lanes` (OpenStreetMap) has a lane only for turning across the oncoming traffic (left, or right in left-hand traffic) gets a second head with arrow lenses over that lane, on the same mast arm, and its own protected phase: the junction's cycle is then main road straight on (with its pedestrians), its turns, crossing road straight on, its turns (straight-on greens shortened, 7 s for the turns). AI cars obey the arrow when their route turns that way and the main head otherwise. Eindhoven has 11 such heads, London 1 (right arrows); `-smokeShot arrow:0` shows the nearest one.

![A junction in Eindhoven: the head with arrow lenses over the left-turn lane, showing red (left), and the built-in traffic approaching it (right)](ReadmeFiles/traffic_turn_arrows.jpg)

They are ordinary `CarTrafficLight` and `PedestrianTrafficLight`s run by a `TrafficLightsSystem` (see [configuration of traffic lights](#configuration-of-traffic-lights)), so the host keeps every agent's lights in step, AI cars (`TrafficCar`) stop at red and amber, logs record every light's state and replays show them. With [SUMO traffic](#traffic-from-sumo), SUMO's signals show what the simulator's lights show (`Follow Simulator Lights` on the `SumoBridge`): each controlled link of a SUMO traffic light takes the state of the head over its approach, so SUMO cars stop where participants see red; SUMO lights with no head near them keep SUMO's own programme (Eindhoven: 15 of 25 follow the map's lights). `-noSignals` leaves the traffic lights out.

**Pedestrians at the lights.** Each signalised crossing knows the pedestrian light its waiting pedestrians face (`PedestrianCrossing.Signal`, set by the map; in hand-built scenes in the Inspector). AI pedestrians with behaviour models cross on green (still checking the gaps), not when the green blinks, and wait on red; a share of "opportunists" (25% of adults, 10% of children and older people) cross on red in a safe gap once they have waited longer than their own tolerance, drawn from a Weibull distribution (shape 1.6, scale 40 s), as in hazard-based duration models of pedestrians' waiting at red lights (e.g. Guo et al. 2011). The draws use their own random generator, so earlier trials repeat as before; calibrate the shares to the study's population in `PedestrianProfile.SampleSignalCompliance`.

Signal heads: Eindhoven 35 at 16 junctions (most of its 110 signals are for cycle paths, footways and bus lanes, which are left out), Barcelona 64 at 22, London 28 at 11; and 86, 332 and 52 pedestrian lights.

![Traffic lights at a junction in Eindhoven, as a driver sees them and from above (top), in Barcelona and London (bottom)](ReadmeFiles/traffic_lights_map.jpg)

### Traffic signs
`TrafficSign` draws a sign on a pole: stop, give way, speed limit (any number), pedestrian crossing, no entry, one way, priority road, children; in the style of the Vienna Convention used in Europe, made from code (no third-party assets). Add one with `GameObject` ➡️ `Coupled Sim` ➡️ `Traffic sign` and turn its blue (forward) axis towards the traffic it is for. City maps place them from OpenStreetMap: stop and give-way signs where mapped, pedestrian crossing signs at zebra crossings, speed limits at the start of longer roads (`maxspeed`), one-way and no-entry signs at one-way streets; on the left of the road where traffic drives on the left. Dutch maps instead get every real sign from the national register (NDW), including 18 more Dutch kinds (see [real-world data](#real-world-data-on-the-city-maps)).

![Real traffic signs in Eindhoven from the national register: speed limit 30 and a cycle path (left), no entry (right)](ReadmeFiles/traffic_signs.jpg)

### Vehicles
The repository contains these drivable car prefabs in `Assets/Prefabs/Cars`:
1. small (similar to a Smart Fortwo): `DrivableSmartCommon` (and `DrivableSmartCommon-no_driver`);
2. medium (similar to a 2011 Ford Focus): `DrivableHatchbackCommon`;
3. medium (similar to a Pontiac GTO): `DrivableFireGTOCommon`;
4. large (similar to a Nissan Datsun): `DrivableNissanCommon`.

The `Avatar Prefab Driver` list of `Managers` ➡️ `PlayerSystem` holds the Smart prefabs and the [detailed vehicles](#detailed-vehicles-with-interiors). A role drives the car its `carIdx` points to; `-driverCar ToyotaCorolla` (or another name from the list: `LandRoverDefender`, `DafXF95`, `ArticulatedBus`) puts the drivers in another vehicle without changing the experiment, and `-passengerCar name` does the same for passengers of automated cars. Give the same option on every PC. To use another car, add its prefab to that list. AI cars spawned by car spawners must also be in the `Prefabs` list of `NetworkingManager` ➡️ `AI Car System`.

AI cars follow a defined path (see [waypoints](#configuration-of-waypoints)) or can be programmed to react at runtime to other road users.

**Driving physics.** The participant's car drives with `RealisticVehicleDynamics` (on the drivable car prefabs; `Car` = `SmartFortwo`, `Hatchback`, `Suv`, `Estate`, `Van`, `CityBus` or `Truck`): the real mass, centre of mass and inertia of the car; suspension tuned to its mass (`SuspensionFrequency`, `DampingRatio`); tyre grip for asphalt that drops on wet roads (`WetGrip`, with the rain of the [weather](#time-of-day-and-weather)); an engine with torque curve and maximum power, an automatic gearbox (early shifts when driving gently, kick-down), engine braking and an auto-hold at standstill; front/rear brake balance with ABS; anti-roll bars; Ackermann steering geometry; with the keyboard, steering is reduced at speed. The Smart (52 kW) reaches 60 km/h in about 6 s and stops from 70 km/h in under 2 s, as the real car. The models are in `Assets/Scripts/Vehicles/Models` with tests against the manufacturers' acceleration and top speed figures. Remove the component to drive with the original arcade physics.

**Vehicle types.** `Car` selects the physics of a vehicle type, with the published figures of a real model. The tests in `VehicleModelTests` check each one against those figures.

| `Car` | Model | Mass | Power, torque | Published | Simulated |
|---|---|---|---|---|---|
| `SmartFortwo` | Smart Fortwo 451 | 900 kg | 52 kW, 92 Nm | 0-100 km/h 13.3 s, 145 km/h | in range |
| `Hatchback` | Ford Focus 1.6 | 1350 kg | 92 kW, 159 Nm | 0-100 km/h 10.9 s, 196 km/h | in range |
| `Suv` | VW Tiguan 2.0 TSI | 1750 kg | 132 kW, 320 Nm | 0-100 km/h 7.7 s, 208 km/h | 6.5-9.5 s, 190-225 km/h |
| `Estate` | VW Passat Variant 2.0 TDI | 1625 kg | 110 kW, 340 Nm (diesel) | 0-100 km/h 8.9 s, 216 km/h | 7.5-10.5 s, 200-230 km/h |
| `Van` | Ford Transit 2.0 EcoBlue | 2300 kg | 96 kW, 385 Nm (diesel, RWD) | 0-100 km/h about 15 s, about 150 km/h | 12-18 s, 140-165 km/h |
| `CityBus` | Mercedes Citaro, 12 m | 14 t with passengers | 260 kW, 1400 Nm | 0-50 km/h about 12-18 s, limited to 80 km/h | 10-22 s, 76-81 km/h |
| `Truck` | DAF LF 18 t box truck | 12 t half loaded | 191 kW, 1000 Nm, 10 gears | limited to 90 km/h | 0-80 km/h 25-50 s, 86-91 km/h |

Each type also sets the wheelbase, track, centre-of-mass height, tyre size and wheel mass, drag, gear ratios, brake torque and the speed limiter of buses and trucks. The suspension is tuned to the mass, so a bus rolls and pitches like a heavy vehicle. `-carSpec bus` (or another type) gives any drivable car that physics without changing the prefab, e.g. to try a truck's braking distance in the Smart.

**More vehicles.** Besides the Smart and the hatchback, traffic has 18 realistic, unbranded vehicles from two packs by Comrade1280 on Sketchfab (CC BY 4.0, [credits](Assets/ThirdParty/Vehicles/CREDITS.md)):
- cars (`DrivableGeneric...`): a compact, a hatchback, a sedan, an estate (`Wagon`), a coupé, a sports car, a minivan, an SUV, an off-roader and a pickup;
- service vehicles: a taxi, a police car, an ambulance, a post van, a road service truck, a tow truck, a garbage truck and a fire truck.

They are scaled to the real sizes of their types (e.g. a sedan 4.75 m, a compact 3.85 m, a garbage truck 8.2 m long), and each drives with the physics of its type (`Estate`, `Hatchback`, `Suv`, `Van` or `Truck`). They take part in background traffic (the traffic model and SUMO) and AI cars, and they park on the city maps. They have tinted windows and no interiors, so they are meant for traffic; participants drive the Smart, the hatchback or the detailed models below. The taxi, police car, ambulance and fire truck have North American liveries. The builder takes each vehicle out of its pack with its wheels and turns it straight (`PackNode`, `PackYaw`).

The 15 stylised vehicles of the [Kenney Car Kit](https://kenney.nl/assets/car-kit) (CC0), which these replace, can still be built (`-vehicleFilter Kenney`) but are no longer in traffic.

#### Detailed vehicles with interiors
Five detailed models from Sketchfab (CC BY 4.0, [credits](Assets/ThirdParty/Vehicles/CREDITS.md)) are scaled to the real vehicles, drive with the physics of their type and take part in traffic:

| Prefab | Vehicle | Size (length × width with mirrors × height; wheelbase) | Type | Driven by participants |
|---|---|---|---|---|
| `DrivableToyotaCorolla` | 2014 Toyota Corolla (E180), 84k triangles | 4.62 × 2.17 × 1.47 m; 2.71 m | `Hatchback` | yes: full interior, turning steering wheel, live instrument cluster in the car's own gauges |
| `DrivableLandRoverDefender` | Land Rover Defender 110, right-hand drive | 4.64 × 2.31 × 2.35 m (roof rack); 2.85 m | `Suv` | yes |
| `DrivableDafXF95` | DAF XF 95 tractor unit, 6×2 with a tandem rear axle | 7.45 × 3.07 × 3.53 m; 4.47 m | `Truck` | yes |
| `DrivableArticulatedBus` | 18 m articulated city bus with full interior | 18.0 × 2.99 × 3.39 m; 5.13 m (front section) | `CityBus` | yes (no passenger figure) |
| `DrivableBoxTruck` | box truck | 7.4 × 3.0 × 3.33 m; 4.11 m | `Truck` | traffic only |

The articulated bus's rear section is a trailer (`ArticulatedTrailer`): its axle follows the joint, so it cuts corners like a real articulated bus (`DetailedVehicleTests` checks the angle between the sections on a steady circle against the geometry of a trailer). It is moved from the front section's motion on every PC, so it needs no network data. The extra axles of the truck's tandem and the bus's rear section turn with the driven wheels.

![The detailed vehicles waiting at a junction in Montreal (top), the Corolla's and the DAF's driver views with the live instrument cluster in Eindhoven (bottom left, middle) and the articulated bus in Eindhoven, from above (bottom right)](ReadmeFiles/detailed_vehicles.jpg)

`Coupled Sim` ➡️ `Vehicles` ➡️ `Build vehicle prefabs` (`VehicleBuilder`) makes these prefabs from 3D models. It copies the hatchback's prefab with all its parts:
- physics and AI driving;
- turn signals and lights;
- driver's and passenger's cameras;
- driver and passenger figures;
- dashboard and instrument cluster;
- eHMI slots.

It then puts the model in place of the body and fits the wheel colliders, the seats, the dashboard, the lamps and the eHMI slots to it. A new model needs one line in `VehicleBuilder.Defs`: the model file (FBX, or glTF/GLB through glTFast), the vehicle type, its real size and the driver's eye position. For a detailed model (`Wheels = null`, `VehicleModelRig`) the builder finds everything else itself:
- the axles, from where the tyres touch the ground (any number; a tandem drives as one axle in its middle);
- the wheels, cut out of the meshes by their real tyre radius, even when a model has all four wheels in one mesh;
- the body, merged into one mesh per material (the DAF had 1,329 parts), saved in compact binary meshes;
- the lamps, put on the body's own surface at its corners and on the door mirrors;
- the instrument cluster, on the model's own gauges (`ClusterNode`) or on the dashboard behind the steering wheel;
- the steering wheel (`SteeringWheel`), turned about its own column, with the driver's hands on it.

Optionally, `ArticulationFromFront` makes an articulated vehicle and `Remove` leaves out parts of the model (helper objects, a seated driver). `Coupled Sim` ➡️ `Vehicles` ➡️ `Build vehicle prefabs` builds all of them (`-vehicleFilter Name` builds some). The prefabs are added to the AI car list, background traffic, SUMO and the parked cars automatically. `Coupled Sim` ➡️ `Vehicles` ➡️ `Render line-up` renders all vehicles side by side for a check, and `VehicleShowcase.RenderDriverViews` renders each driver's view.

![All cars (top: Smart, hatchback, Toyota Corolla, Land Rover Defender, the ten generic cars, the taxi and the police car) and all vans, trucks and buses (bottom: post van, ambulance, road service truck, tow truck, garbage truck, fire truck, box truck, DAF XF 95 and the articulated bus), parked at an angle on the Markt in Delft](ReadmeFiles/vehicle_range.jpg)

**Road surfaces under each wheel.** Each wheel gets the grip and roughness of the surface it is on. On city maps the surface comes from the road meshes, which follow OpenStreetMap's `surface` tag. In other scenes everything counts as asphalt unless `-roadSurface name` sets one surface everywhere (`asphalt`, `bricks`, `cobblestones`, `gravel`, `grass`, `paving` or `unpaved`).

| Surface | Grip (dry asphalt = 1) | Extra loss when wet | Body shaking at 70 km/h | Tyre noise |
|---|---|---|---|---|
| asphalt | 1.0 | none | 0.15 m/s² | reference |
| bricks (klinkers) | 0.95 | ×0.9 | 0.6 m/s² | +4 dB |
| cobblestones (setts) | 0.85 | ×0.75 | about 2 m/s² | +8 dB |
| gravel | 0.6 | ×0.95 | about 1.7 m/s² | +4 dB |
| grass | 0.45 | ×0.7 | 1 m/s² | −4 dB |
| pavements, kerbs | 0.9 | ×0.9 | light | +2 dB |

Wet grip is the car's `WetGrip` times the extra loss, so wet cobblestones drop to 0.45. The car then takes longer to stop and spins its wheels when pulling away: from about 54 km/h it stops in 1.4 s on asphalt, 1.5 s on bricks, 1.6 s on cobblestones and 2.7 s on gravel or grass (chart below). On a rough surface each wheel gets a smooth random vertical force, which grows with speed and comes faster the smaller the stones (at most 15 Hz). The car shakes, rolls and pitches a little, and [motion platforms](#motion-platforms) get it through the heave signal. The tyre noise of every car follows the surface under it (see [Sound](#sound)). The values are in `Assets/Scripts/Vehicles/RoadSurfaces.cs`. `SurfaceEffects` on `RealisticVehicleDynamics` turns this off, and its `Surface` and `RoughnessRms` fields show the current surface and shaking.

![Brick street in Delft (left) and cobblestones in Ghent (middle, right) from OpenStreetMap's surface tags](ReadmeFiles/road_surfaces.jpg)

![Full braking from about 54 km/h on asphalt, bricks, cobblestones, gravel and grass](ReadmeFiles/road_surface_braking.png)

**Tyres from Magic Formula coefficients.** `RealisticVehicleDynamics` ➡️ `Use Magic Formula` (or `-magicFormulaTyres`) sets the tyres from Pacejka Magic Formula coefficients (B, C, D, E for the longitudinal and lateral force, e.g. from tyre data) instead of the built-in asphalt curves; the formula is fitted to Unity's wheel friction curve (the same peak slip and peak, the sliding value at four times the peak slip). The defaults are typical dry asphalt (longitudinal B 10, C 1.9, D 1, E 0.97: peak at 18% slip; lateral B 8, C 1.3, D 1, E -1.6); wet roads still lower the grip.

## Networking
- Agents communicate over UDP with Unity Transport on **port 7770**. Allow this port (or the Unity editor / the built simulator) through the firewall on every PC.
- The number of agents is fixed in code to 8 (`UNetConfig.MaxPlayers`): the host plus seven clients (tested with two).
- The host runs the simulation (AI cars, AI pedestrians, traffic lights) and sends poses to the clients; each client sends the pose of its own avatar to the host.
- `Visual syncing` (host GUI, during the simulation) shows a red bar on all agents for 1 s, to synchronise screen recordings afterwards.

## Data logging
Every agent writes binary logs to the `ExperimentLogs` folder in the project folder (next to `Assets`):
- `HostLog-{role}-{yyyy_MM_dd_HH_mm_ss}.binLog` / `ClientLog-...`: one frame per rendered frame;
- `HostFixedTimeLog-{role}-...` / `ClientFixedTimeLog-...`: one frame per physics step, at a fixed 50 Hz (`Fixed Timestep` 0.02 s).

The [study tools](#study-tools) add CSV files there (with the participant and trial at the start of the name in automatic sessions, also on the clients: the host sends them): `Responses-*` (button and slider, every frame), `Haptics-*` (cues), `Voice-*.wav` with `Voice-*.csv` (each agent's microphone), `Companions-*` (virtual pedestrians' actions and language-model answers), `Gaze-*` (on the host: every participant's gaze and the object looked at, gaze triggers), `OpenScenario-*` (scenario events), `SceneDescriptions-*.jsonl`, and `Videos/` (videos from fixed cameras).

Each frame contains the time, the position and rotation of every car with its blinkers, front lights and stop lights, AI-car state (speed, braking, stopped, take-off, eye contact), the position and rotation of every bone of every pedestrian (players and AI; with the Xsens suit, the suit's pose at the time of the frame), and the state of every traffic light. Experiment `Points of Interest` are stored in the log header.

**Convert to CSV.** In `Play Mode`, before pressing `Start host` or `Start client`, press `Convert log to csv`, pick a log and a reference frame (the world origin, a point of interest of the experiment, or a custom point). The result is a semicolon-separated file `ExperimentLogs/{reference}-{log name}.csv` with positions relative to that reference.

**Replay.** Every log has a small `.replay.json` file next to it with the experiment, scene, city map, time of day, weather and the vehicle and pedestrian models. In `Play Mode`, before `Start host` or `Start client`, press **Replay a recording** and pick a recording (newest first): the simulator loads that world and plays the trial back with every participant, AI car and traffic light, without network or new logs. At the bottom: a time slider, play/pause and speed, and buttons to follow a participant. Keys: `Space` play/pause, `←`/`→` 5 s back/forward, `-`/`+` slower/faster (0.1× to 8×), `1`-`9` follow participant 1-9, `C` the participant's own view or from behind, `0` free camera (`W` `A` `S` `D`, `Q`/`E` down/up, `Shift` faster, hold the right mouse button to look around), `T` trails of the participants' paths, `Esc` back to the start screen. Logs from before the replay existed have no `.replay.json`: they are listed as "older"; after clicking one, choose the experiment it was recorded in (and the city map, for map experiments), and the replay uses the standard car and pedestrian models. Logs from much older versions of the simulator may be in a different format and cannot be read. From the command line: `-replay ExperimentLogs/<log>.binLog` (for older logs add `-replayExperiment <short name>` and, for map experiments, `-replayMap <map>`). Drones (the participants' and the AI drones) are in the logs since November 2026 and are replayed with their propellers; a participant's drone can be followed like the other participants.

![Replay of a drive in Delft: the participant's own view (left) and from above with the trail (right)](ReadmeFiles/replay.jpg)

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

### Exporting the road network
`Coupled Sim` ➡️ `Maps` ➡️ `Export road network` writes the road network of the loaded world to `ExperimentLogs` (city maps: in Play mode), for analysis together with the logs:
- `RoadNetwork-{name}.geojson`: roads (OSM type, width, one-way), cycle paths, pavements, pedestrian crossings, traffic lights, traffic signs and the waypoint paths of AI cars and pedestrians; in longitude/latitude on city maps (with the scene coordinates of every point in `scene_xz`), in scene coordinates otherwise;
- `RoadNetwork-{name}.obj`: the road, pavement and crossing surfaces as meshes, in scene coordinates.

City maps write these files by themselves the first time they are built.

For the hand-built town (`MainScene`) in batch mode: `Unity -batchmode -quit -projectPath . -executeMethod RoadNetworkMenu.ExportScene -scene Assets/_Scenes/MainScene.unity` (the experiments in that scene add their paths, crossings and lights).

## Configuration
### Experiment definitions
The central configuration is on the `Managers` game object in `StartScene`:
- `PlayerSystem`: the player avatar prefabs (pedestrian avatar and the `Avatar Prefab Driver` list of cars);
- `NetworkingManager`: the list of experiments, the trial sequence, and the prefabs of AI cars that can be spawned during an experiment (`AI Car System` ➡️ `Prefabs`).

![PlayerSystem in the Inspector](ReadmeFiles/guide/inspector_player_system.png)

An experiment is a prefab with an `ExperimentDefinition` component on its root object. To make a new experiment selectable, add its prefab to the `Experiments` list of `NetworkingManager` (or right-click it ➡️ `Coupled Sim` ➡️ `Register experiment in StartScene`). `Assets/ExperimentDefinitions` contains `Base` (the example used by default, showing most features) and the experiments of earlier studies: `2Pedestrian`, `EvasiveManeuvers`, `Scenario_1_eHMI`, `SoloAuto`, `SoloPedestrian`, `SoloPedestrianWithNPCs-Nighttime` and `TrolleyProblem`.


To edit an experiment, double-click its prefab in the `Project` window.

![The experiment prefabs in the Project window](ReadmeFiles/guide/project_experiments.png)

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

![ExperimentDefinition of Base with its first role unfolded](ReadmeFiles/guide/inspector_experiment_definition.png)

#### Worlds from the Asset Store (e.g. Windridge City)
Environments whose licence does not allow putting their files in a public repository, such as [Windridge City](https://assetstore.unity.com/packages/3d/environments/roadways/windridge-city-132222) (Unity Asset Store EULA: free to use in your project and builds, not to redistribute), are imported on each lab PC into `Assets/LocalAssets/`, which git ignores:
1. In Unity, `Window` ➡️ `Package Manager` ➡️ `My Assets`, sign in with a Unity account that has the asset, `Download` and `Import`. Move the imported folder into `Assets/LocalAssets/` (create it) in the Project window, so Unity keeps the references.
2. Convert its materials to URP if they show pink: select them and `Edit` ➡️ `Rendering` ➡️ `Materials` ➡️ `Convert Selected Built-in Materials to URP`.
3. `Coupled Sim` ➡️ `Experiments` ➡️ `Setup…` ➡️ `New experiment` from the `Base` template, then set the new `ExperimentDefinition`'s `Scene` to the asset's demo scene, place the spawn points and waypoint paths in it (`Open it to place spawn points`) and add colliders to its roads if it has none (cars drive on colliders). Keep the experiment prefab in `Assets/LocalAssets/` too, and leave the change to `StartScene` (the experiment added to `NetworkingManager` ➡️ `Experiments`) out of your commits, or work in a private fork.

### Configuration of agents
The roles are in `ExperimentDefinition` ➡️ `Roles` (see the picture above).

`Roles` is a list of `ExperimentRoleDefinition`s with these fields:
- `Name`: short name of the role;
- `SpawnPoint.Point`: where the avatar is spawned;
- `SpawnPoint.Type`: the kind of avatar: `PlayerControlledPedestrian`, `PlayerControlingCar` (driver of a manual car), `PlayerInAIControlledCar` (passenger of an AV) or `PlayerRidingTwoWheeler` (cyclist, e-scooter or moped rider);
- `SpawnPoint.CameraIndex`: which camera of the avatar is used (see below);
- `SpawnPoint.VehicleType`: for cars, `AV` or `MDV` (manually driven vehicle); it enables the matching `AV` or `MDV` elements of the car's `PlayerAvatar`;
- `carIdx`: for cars, the index of the car prefab in `PlayerSystem` ➡️ `Avatar Prefab Driver`;
- `twoWheelerIdx`: for two-wheeler riders, the index in `PlayerSystem` ➡️ `Avatar Prefab Two Wheeler` (0 bicycle, 1 e-scooter, 2 moped);
- `TopHMI`, `WindshieldHMI`, `HoodHMI`: for AV passengers, the eHMI prefab spawned on each slot (slots are set on the car's `PlayerAvatar` ➡️ `HMI Slots`);
- `AutonomousPath`: for AV passengers, the `WaypointCircuit` the AV follows;
- `AutonomousIsYielding`: puts the AV on the `Yielding` layer instead of `Car`.

![A windshield eHMI seen from the driver's seat (the town)](ReadmeFiles/windshield_ehmi.jpg)

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
- **Models**: the bicycle (Dutch city bike) and e-scooter are built from code, the moped from a 3D model of a Vespa ("Moto Vespa", CC BY 4.0, see the credits), by `Coupled Sim` ➡️ `Two-wheelers` ➡️ `Build avatar prefabs` (`TwoWheelerPrefabBuilder`): wheels spin, the handlebar turns on its head tube, frame and rider lean, the cranks turn when pedalling; the rider is a Rocketbox avatar whose hands and feet are placed on the grips and pedals (or footboard) by IK (`RiderIK`). The camera is at the rider's eyes and follows only part of the lean (`CameraLeanFactor`) for comfort in VR.
- Riders are logged and synchronised like vehicles (position, rotation, velocity; lean, steering and wheels as synced parts); AI pedestrians, model-driven traffic and SUMO (as a bicycle or moped) react to them.


![Cycling in Ghent on a cobbled street, behind the rider, and towards a junction with a zebra crossing](ReadmeFiles/cyclist.jpg)

### Drones
Roles with `SpawnPoint.Type` = `PlayerFlyingDrone` let a participant fly a drone (`droneIdx` 0 in `PlayerSystem` ➡️ `Avatar Prefab Drone`); the `CityMap` experiment has the role `Drone pilot`. The same experiment has AI drones (the `Drones (AI)` object, `DroneSpawner`). Both are made by `Coupled Sim` ➡️ `Drones` ➡️ `Set up drones` (`DronePrefabBuilder`); new map experiments made in the setup window get them too.
- **Flight model** (`Quadrotor`, in the engine-free assembly `CoupledSim.Drones.Models`, with tests): a rigid body with four rotors, each with thrust and drag torque proportional to the square of its speed and a first-order motor lag, linear and quadratic air drag and the rotors' gyroscopic effect, integrated at 500 Hz (the standard multirotor model, e.g. Mahony, Kumar & Corke, 2012, *IEEE Robotics & Automation Magazine* 19(3)). It is flown by a cascaded controller of the kind in PX4 and ArduPilot: position (P) ➡️ velocity (PI with anti-windup) ➡️ thrust and attitude (the geometric controller of Lee, Leok & McClamroch, 2010, *IEEE CDC*) ➡️ body rates (P) ➡️ the four motor speeds. Two airframes: `Consumer` (0.9 kg, 0.38 m between motors, up to 15 m/s, as a Mavic-class camera drone) and `Industrial` (6.3 kg, up to 17 m/s, a delivery drone). Wind and gusts come from the scene's [weather](#time-of-day-and-weather) (`windSpeed`) through the Dryden turbulence model's low-altitude form (MIL-F-8785C).
- **Flying it**: in position mode (the default, as on consumer drones) the sticks set the velocity and releasing them holds the position against the wind; `-droneMode atti` flies in attitude mode (the sticks tilt the drone and it drifts). Keys are in [Controls](#controls); gamepad axes are set in `DroneController` (`ClimbAxis`, `YawAxis`, `ForwardAxis`, `LateralAxis`). Views (`C` cycles, `-droneView fpv|los|chase`): the stabilised gimbal camera under the nose (what pilots watch on their controller), the pilot's line of sight from 2 m behind the take-off point, or a chase camera 1.3 m behind (above the drone when a wall is in the way). Hitting a building, tree or vehicle is a crash: the rotors stop and it falls; lift off again with the climb stick.
- **AI drones** (`DroneSpawner`): `Deliveries` industrial drones take off, climb to a cruise height 20 m above the highest roof under their route (`Clearance`), fly to a drop point with smooth acceleration and braking, land, wait and fly back; `Patrols` consumer drones circle the area about 35 m above its roofs; a swarm of `Swarm` consumer drones flies round the area as a flock by the model of Vásárhelyi et al. (2018, "Optimized flocking of autonomous drones in confined environments", *Science Robotics* 3(20), eaat3536): short-range repulsion, velocity alignment with a braking-curve friction term, and avoidance of the participants' drones. All fly with the same flight model as the participant's drone. Trial parameters: `drones` (a multiplier of the delivery and patrol counts; `0` for none) and `droneSwarm` (the swarm's size), e.g. `drones=2;droneSwarm=12`.
- **Sound**: each drone hums at its blade-passing frequency (rotor speed × blades; about 200 Hz for the consumer drone in hover), with harmonics and broadband noise, rising and falling with the thrust.
- **Network and logs**: the host flies the AI drones and sends their poses to the clients 20 times a second; participants' drones are synchronised like other avatars. The participant's flight is logged to `ExperimentLogs/Drone-{date}.csv` (50 Hz: position, velocity, attitude, height above the ground, the four rotor speeds, blade-passing frequency, mode and stick inputs), the AI drones to `ExperimentLogs/Drones-{date}.csv` (10 Hz: mission, phase, position, velocity, heading, height, blade-passing frequency).
- **Smoke test**: `-smokeRole N` (the drone pilot's role) with `-smokeFly "climb,yaw,forward,lateral"` (each -1..1) flies the drone with fixed sticks (climbing for the first 2 s); the screenshot view `aidrone` (or `aidrone:2`) shows the nearest (or the third nearest) AI drone.

![Drones over Delft: the participant's drone from the chase camera (left), a delivery drone landed at its drop point (middle) and a drone of a swarm over the tiled roofs (right)](ReadmeFiles/drones.jpg)

### Automated driving and take-over requests
The AV of a role with `SpawnPoint.Type` = `PlayerInAIControlledCar` gets an `AutomatedDriving` component when the participant is spawned. It drives in one of two modes (trial parameter `automation`):
- `Waypoints` (default): the speeds, accelerations and yielding set on the waypoints of its `AutonomousPath` (`SpeedSettings`, see [AI cars](#ai-cars));
- `TrafficModel`: smooth car following with a [car-following model](#configuration-of-background-traffic) (IDM by default) at `avSpeed` km/h: it adapts its speed to the vehicle ahead, slows down for bends, and stops at red lights and for pedestrians and animals on the road.

A **take-over request** (TOR) hands the car to the participant: a red "TAKE OVER" warning in front of the driver's eyes (also in VR) and three beeps per second. The driver takes over by steering, braking or pressing the throttle (thresholds `SteerThreshold`, `PedalThreshold`) or with the `automation` button (`Enter`; on a wheel the profile's `Automation` button, G27: 3), and then drives with the full [vehicle physics](#vehicles). Without a reaction within `TimeBudget` seconds (trial parameter `takeOverBudget`, default 7 s), the AV brakes to a stop (minimal-risk manoeuvre; `WhenNoReaction` = `KeepDriving` lets it drive on). The `automation` button hands the car back to the automation, which continues from the nearest point of its path.

Requests come from:
- the trial parameter `takeOverAt` (seconds after the start; host);
- a `TakeOverPoint` on the road (a trigger collider; the AV issues the request when it drives into it);
- the experimenter: `T` on the machine of the participant, or `Take-over request` on the [dashboard](#experimenter-dashboard).

`ExperimentLogs/TakeOver-{date}.csv` lists the events (automation mode, request with its source, take-over with the input used and the reaction time, no reaction, engaged) with the time, speed and position.

![A take-over request in the driver's view (the town)](ReadmeFiles/take_over_request.jpg)

### Scripted paths
A car can follow a path from a file instead of waypoints in the scene: a CSV with the columns `x` and `z` (scene coordinates in metres, as in the logs; `y` optional) and optionally `speed` (km/h) and `t` (seconds):
- with `t`, the car is where the file says at each moment (for example a trajectory recorded in an earlier trial or in naturalistic data);
- with `speed`, it follows that speed profile along the path and brakes in time (up to `MaxDeceleration`) for slower points ahead;
- otherwise it drives at `DefaultSpeedKmh`.

It slows down for bends (at most 2.5 m/s² sideways) and stops at the last point. Scripted cars do not yield to pedestrians or stop at lights: the file decides.

Files are looked up as given, then in `Assets/StreamingAssets/Paths`. A `ScriptedPathSpawner` (a car spawner: add it to the experiment's `Car Spawners`, with a car prefab from `AI Car System` ➡️ `Prefabs`) spawns a car at the start of the path after `Delay` seconds; the trial parameter `avPath` makes the participant's AV follow a path file. Example: `Assets/StreamingAssets/Paths/example_path.csv`.

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
- SUMO's traffic lights follow the simulator's lights where a signal head stands over the approach (`FollowSimulatorLights`; see [traffic lights on city maps](#traffic-lights-on-city-maps));
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

The experiment parameter `animals` multiplies the numbers per trial (`0` = no animals, `2` = twice as many). Positions and states are logged to `ExperimentLogs/Animals-{date}.csv` (time, animal, species, position, heading, state). The dog (a pit bull), cat, pigeon, deer (a doe) and horse are realistic animated models from Sketchfab (CC BY 4.0, [credits](Assets/ThirdParty/Animals/CREDITS.md)); the husky and the fox are low-poly CC0 animals by Quaternius, as no realistic animated ones are free. Each is scaled to a real body length in its walking pose, with walk, run, idle and eating (and for pigeons flying) animations; where a model's walk carries the body forward, `RootMotionLock` holds it in place (the animal agent does the moving); `Coupled Sim` ➡️ `Animals` ➡️ `Build prefabs and add to experiments` (`AnimalPrefabBuilder`) rebuilds the prefabs.

![Animals on the city maps: a dog in Ghent, with pigeons behind it (left), and a cat in Delft (right); all the animals are in the line-up under Agents at a glance](ReadmeFiles/animals.jpg)

### Time of day and weather
Every experiment has an `EnvironmentController` (child object `Environment (time of day, weather)`) that sets:
- **time of day** (`TimeOfDay`, clock time; `Latitude`, `Longitude`, `UtcOffset`, `DayOfYear`): the sun's position from a solar model, its colour and strength, a physically based sky, ambient light and the street lights (a light under each `StreetLamp`, the nearest 24 to the viewer switched on at dusk); `TimeSpeed` lets time run (e.g. 60 = one minute per second);
- **weather** (`Clear`, `PartlyCloudy`, `Overcast`, `LightRain`, `HeavyRain`, `Fog`, `LightSnow`, `HeavySnow`): clouds (sky colour, softer and weaker sunlight), fog, rain around the viewer and wet, darker, shinier roads and pavements, and puddles on city maps; `ExtraFog` and `RainOverride` fine-tune it. Some haze is always there, as in real air: under 1% at 300 m, about half at 3 km (`EnvironmentController.MinHaze`).
- **snow**: flakes falling and fluttering around the viewer (`LightSnow`, `HeavySnow`), and snow on the ground (`SnowCover` 0..1; from the weather when negative): grass, pavements, cycle paths, roofs and the ground white from a quarter cover, roads lighter with slush, and less tyre grip (down to 45% with full cover). Façades stay as they are.
- **wind** (`WindSpeed` in m/s, `WindDirection` in compass degrees it comes from): the trees of city maps lean downwind and sway at their own pace, more with the square of the speed and with gusts (`TreeSway`; about 1° at 10 m/s, at most 6°), and rain and snow slant and drift with it.
- **reflections** (`LiveReflections`, on by default; `-liveReflections 0` turns them off): a realtime reflection probe a few metres above the viewer, renewed when the time of day or weather changes and when the viewer has moved 30 m (`LiveReflectionDistance`; 128 pixels per face, `LiveReflectionResolution`). Car paint, windows and wet roads then reflect the actual sky and streets: dark at night, grey under clouds, the buildings around. The scene's own reflection probes still apply inside their areas. The viewer's own vehicle is left out of it (its roof would show up as a smear of its colour in the windscreen); a small probe inside the cabin instead makes the windscreen reflect the dashboard, as a real one does.

![Delft at night without (left) and with live reflections (right)](ReadmeFiles/live_reflections.jpg)

They can be set per trial with the experiment parameters `time` (`17:30` or `17.5`), `weather`, `fog`, `rain`, `snow` (cover 0..1), `wind` (m/s), `windDirection`, `timeSpeed`, `dayOfYear`, `latitude`, or all at once from the weather that really was at the map's place: `realWeather=2024-11-23T14:00` (local time) sets the time, the day of the year, the weather from the WMO weather code and the measured rain and snowfall (e.g. Delft that afternoon: 4.6 °C, drizzle, light rain), snow on the ground from the measured snow depth (5 cm and more: covered), the wind's speed and direction, and fog from the visibility (Open-Meteo historical weather, CC BY 4.0; cached in the data folder, so it also works offline later). The host sends the conditions to all clients. With `UseBakedLighting` on (the experiments of earlier studies), the scene keeps its baked lightmaps and sun and only the weather effects apply; with it off (Base and the new examples), the sun follows the time of day with realtime lighting.


![The same street in Delft on a clear day, overcast, in heavy rain, in fog and at night](ReadmeFiles/weather.jpg)

![Heavy snow in Delft (top) and London, a London roof under snow, rain in a 10 m/s wind (bottom)](ReadmeFiles/snow_wind.jpg)

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

### Auditory eHMIs
An `AuditoryEhmiSettings` component on the experiment prefab (child `Auditory eHMI` in the example experiments, off by default: tick `Enabled` or set the trial parameter `auditoryEhmi=1`) gives every AV in the experiment sounds, played from the front of the car (3D sound) on every agent:
- **cues**: a sound (`Chime`, `Beeps`, `Ding` or your own `AudioClip`) when the car's visual eHMI shows `STOP` or `WALK`, when it starts to brake, stops, drives off, when a pedestrian is ahead within a distance, or at a time; optionally repeated while the condition holds;
- **AVAS**: the acoustic vehicle alerting sound of electric cars (a hum that rises with speed and fades out at 20 km/h, as required in the EU).

Trial parameters `auditoryEhmi` (0/1) and `avas` (0/1) switch them per trial; `AllCars` gives the sounds to manually driven cars too. The cues played are logged to `ExperimentLogs/AuditoryEhmi-{date}.csv`.

### Details on editing car prefabs
The `Speedometer` component shows the speed:
- digital: set `Speedometer Text`;
- analog: set `Pivot` (pivot of the needle), `PivotMinSpeedAngle` and `PivotMaxSpeedAngle` (needle angle at 0 and at maximum speed) and `MaxSpeed`.

Other components on car prefabs: `CarConfigurator` (colour, driver and passenger visibility of AI cars), `SteeringWheel` (turns the steering wheel model), `RearMirrorsReflection` (mirror cameras), `CarBlinkers`, `DriverIKSetup` (driver's hands on the wheel) and the engine sound managers.

## Study tools

### Participant responses
A role can give its participant a **hold-to-respond button** (e.g. "hold while you feel safe to cross") and/or a **slider from 0 to 100** (e.g. trust in the AV, perceived risk): tick `Button` and/or `Slider` in the role's `Response` (`ExperimentDefinitions` ➡️ `Roles`), with the texts shown, the slider's start value, and `Hidden` to log without showing anything (or `-response button,slider` on the agent's command line). A small panel in front of the participant's eyes (on screens and in headsets) shows the text, a light while the button is held and the slider. Keys: `B` for the button, `Z` / `X` for the slider; on a gamepad the right shoulder or right trigger (its analog value is logged too) and the d-pad; in a headset the right controller's trigger and thumbstick. Every frame goes to `ExperimentLogs/Responses-{role}-{date}.csv` (UTC time, game time, frame, button 0/1, analog value, number of presses, slider), on every agent.

![The response panel (here a slider) in front of the driver in Montreal; it is drawn over the car's interior](ReadmeFiles/response_panel.jpg)

### Designed eHMIs
An eHMI can be described in a file instead of a prefab: `Assets/StreamingAssets/eHMI/<name>.json` gives the panel's size (m), its background and, for each eHMI state (`DISABLED`, `STOP`, `WALK`), the elements shown:

| Element (`type`) | Look | Settings |
| --- | --- | --- |
| `lightbar` | a row of lamps | `segments` |
| `rect`, `circle` | filled shapes | |
| `arrow`, `chevrons` | an arrow; a row of `>` | `direction` (`left`, `right`, `up`, `down`), `segments` (chevrons) |
| `text` | text, shrunk to fit | `text` |

Each element has a place in the panel (`x`, `y`, `w`, `h` from 0 to 1, from the bottom left), a `color` (`#RRGGBB` or `#RRGGBBAA`) and an `animation` with its `period` (s): `steady`, `pulse`, `blink`, `sweep` (fills left to right), `sweepleft`, `sweepout` (from the middle out), `sweepin`. Elements of a state named `ANY` show in every state. For example, a cyan light bar that pulses while the AV yields:

```json
{ "width": 1.2, "height": 0.08, "background": "#101010",
  "states": [ { "state": "WALK", "elements": [ { "type": "lightbar", "segments": 16, "color": "#00E5FF", "animation": "pulse", "period": 1.2 } ] } ] }
```

Give a role's AV a designed eHMI with `EhmiDesign` (the file name) and `EhmiDesignPlacement`: `Hood`, `Top`, `Windshield`, `Bumper`, or `Road` (projected on the road 1.8-4.2 m ahead of the car). Per trial, the trial parameter `ehmiDesign=LightBarCyan@Bumper` (or `none`) sets the AVs' designed eHMI on every agent (the host sends it), e.g. to compare eHMIs within participants (see the tutorial [Your first study](docs/first-study.md)). It is switched and synchronised like the other eHMIs (passenger keys, host, dashboard). On any other object (a pole at a crossing, a pedestrian's wearable) add a `DesignedEhmi` component and set `Follow Ehmi` to the number of the eHMI it should show. Included: `LightBarCyan`, `TextWalk`, `DirectionalChevrons`, `RoadProjection`. To preview one: `-ehmiDesign RoadProjection@Road -ehmiState WALK`.

![Designed eHMIs on the automated car in the town, seen from the kerb: light bar on the hood, text on the roof, chevrons on the bumper, a projection on the road](ReadmeFiles/ehmi_designs.jpg)

### In-car displays (head-up display, centre screen, instrument cluster) and timed messages
The participant's car (driver or AV passenger) can have three displays, each designed like a [designed eHMI](#designed-ehmis) in `Assets/StreamingAssets/InCar/<name>.json`:
- a **head-up display**, seen about 2.5 m ahead through the windshield (`HudDesign` of the role, or `-hud name`);
- a **centre screen** on the dashboard (`CentreScreenDesign`, `-centreScreen name`);
- the **instrument cluster** behind the steering wheel. It takes the place and size of the car's own dashboard panel and is on by default with the design `Cluster` (`ClusterDesign`, `-cluster name`; `none` keeps the car's old speedometer).

The default cluster shows real data from the car's physics:
- a speedometer and a rev counter with needles (red from 6000 rpm);
- the digital speed and the gear (`P`, `R`, `D1`…);
- the clock (the scene's time of day);
- turn signal arrows that flash with the car's indicators;
- the headlight lamp, the automation status and the take-over request with the seconds left.

**Designing a display.** A design has a size in metres, a background and the elements of each state. States follow the automation: `MANUAL`, `AUTOMATED`, `TAKEOVER`, and `ANY` for elements that are always shown. Elements are placed with `x`, `y`, `w`, `h` from 0 to 1 (from the bottom left):
- `rect`, `circle`, `arrow`, `chevrons`, `lightbar` and `text`, optionally animated (`pulse`, `blink`, `sweep`…);
- `gauge`: a dial with a needle, with `value` (e.g. `"{speed}"`), `min`, `max`, `segments` (major ticks), `labels`, `labelScale`, `redFrom`, `startAngle` and `endAngle`;
- `bar`: a fill bar of a `value` between `min` and `max` (`"direction": "up"` for a vertical one).

Live values, for text and for gauges and bars: `{speed}` (km/h), `{rpm}`, `{gear}`, `{mode}`, `{time_left}`, `{reaction}`, `{slider}`, `{clock}`, `{trial_time}`, `{distance}` (km driven), `{surface}` and `{message}`.

Any element can have a condition, `"when"`: `left`, `right`, `blinkers`, `automated`, `manual`, `takeover`, `braking`, `reverse`, `headlights`, `wet`, `snow`, `rough` or `message`. `!` negates it, e.g. `"when": "!message"`. Gauges and bars are drawn by the GPU, and the rest of the panel is redrawn only when something on it changes, so a cluster costs little frame time. Included designs: `Cluster`, `HudSpeed`, `CentreScreen`.

**Messages at set times.** Text can be shown on these displays at a time after the start of the trial, after a distance driven, or when the automation state starts. Each message stays for a set number of seconds. The easiest way is a schedule file, `Assets/StreamingAssets/InCar/Messages/<name>.csv`, which can be edited in Excel. Choose it per role (`InCarMessages`) or with `-inCarMessages name`:

```csv
at;seconds;display;text;color;atDistance;onState
2;4;hud;Please drive to the end of the street;#FFFFFF;;
6;5;cluster;Pedestrian ahead;#FFD60A;;
;5;centre;Turn right at the next junction;#FFFFFF;150;
;4;all;Automation engaged;#00E5FF;;AUTOMATED
```

Columns:
- `display`: `hud`, `centre`, `cluster` or `all`;
- `color`: optional;
- a `background` column is also read.

Messages can also be put in a design's own `"messages"` list, with their place on the panel (`x`, `y`, `w`, `h`). Every message shown is logged to `ExperimentLogs/InCarMessages-*.csv`, with the time, the display, the text and the distance driven. `Example.csv` is included.

![The Toyota Corolla's live instrument cluster at 63 km/h in D2 with a scheduled message (left); the driver’s view with the head-up display, in Eindhoven (right)](ReadmeFiles/instrument_cluster.jpg)

![Head-up display and centre screen while driving automated (left) and during a take-over request (right), in the town](ReadmeFiles/incar_displays.jpg)

### Haptic cues
`Haptics.Cue(name, intensity, duration, pattern)` (pattern `pulse`, `double`, `triple`, `continuous`) plays a cue on gamepads (rumble), headset controllers, Logitech wheels (vibration; with the Logitech SDK, see [wheels](#steering-wheels-pedals-and-gamepads)) and external devices: with `-hapticsUdp host:port` every cue is also sent as JSON, `{"cue":"takeover","intensity":1.0,"duration":1.2,"pattern":"triple","time":12.3}`, e.g. to an Arduino or Raspberry Pi driving a vibrating seat, belt, helmet or handlebar. Take-over requests play a `takeover` cue. Cues are logged to `ExperimentLogs/Haptics-*.csv`.

### Sound
A `Soundscape` makes the scene's sound with its levels from models and its timbres from recordings (on city maps it is on by default; `-vehicleSounds 0` turns it off; elsewhere add the component to the experiment or use `-vehicleSounds 1`):
- **Vehicles** (the 20 nearest): sound power by the European road traffic noise model **CNOSSOS-EU** (Commission Directive (EU) 2015/996, Annex, Table F-1): rolling (tyre/road) and propulsion noise per octave band from 63 Hz to 8 kHz, for cars and vans, medium trucks, heavy trucks and buses, and mopeds; a car at 50 km/h is 70.5 dB(A) at 7.5 m, at 100 km/h 80 dB(A) (tested). Tyre noise is louder on bricks and cobblestones (the road surfaces) and its spectrum shifts up with speed. The engine runs through its gears (`EngineModel`: idle, shift points that rise with the throttle, a variator on scooters) and is heard at its firing frequency (recordings of petrol and diesel engines pitched to it; a synthesised two-stroke for mopeds), louder when accelerating and quieter at idle. A share of the cars is electric (`-electricCars 0.15` or trial parameter `electric`; city buses are electric, as in most Dutch cities): propulsion 12 dB quieter (it matters only below about 40 km/h, where electric cars are measurably quieter) and a motor whine instead of the engine. Bicycles roll on narrow tyres (rumbling on cobblestones) and their freewheel ticks when they coast; e-scooters whine and rumble on small wheels.
- **Propagation**: spherical spreading (-6 dB per doubling of distance), air absorption by ISO 9613-1 (a low-pass whose cut-off falls with distance: above 8 kHz near by, about 4 kHz at 100 m), about 15 dB of shielding and the highs lost when a building is between the source and the listener, and the Doppler shift. Inside the participant's car its body keeps out about 20 dB and the highs of everything outside, and its own tyres and engine come through the floor.
- **Ambience**: distant traffic that follows the time of day (about 55 dB in the morning and afternoon peaks, 43 dB in the night; `-ambienceDb` sets a fixed level), rain (and on the roof of the car), wind at the ears with gusts (riders also hear their own speed), birds singing in the nearest trees (most at dawn, little at midday, none at night or in the rain; `-birds 0`), and reverberation between buildings: the more of the horizon is wall and the closer, the longer and louder the echo.
- **People**: footsteps of the participant and of the pedestrians nearby, on hard (paving, asphalt), soft (grass) or loose ground (gravel, snow), and the ticking of pedestrian signals: once a second at red to find the pole, fast at green, as on Dutch "rateltikkers", quieter at night (`-footsteps 0`, `-signalTicks 0`).
- **Cabin music**: a WAV, OGG or MP3 in `Assets/StreamingAssets/Audio` at `CabinMusicDb`.

Levels are in dB SPL once the sound is **calibrated**: press `F3` (or `-audioCalibration`) to play a 1 kHz tone at full scale, measure it with a sound level meter at the participant's ears (headphones: with a coupler), and enter the reading as `ReferenceDb` (`-audioReferenceDb`). Trial parameters (host): `vehicleSounds`, `electric`, `ambienceDb`, `cabinMusic`, `cabinMusicDb`. The models are in `Assets/Scripts/Environment/SoundModels` (engine-free, with tests). The sounds are real recordings where there are any (CC0, from Freesound, in `Assets/Resources/Sounds` with [credits](Assets/Resources/Sounds/CREDITS.md)): engines at idle and on load, crossfaded and pitched by the engine speed; the inside of a car on asphalt and on cobblestones; a bicycle's tyres and freewheel; footsteps on hard, loose and soft ground; birds and distant traffic. Each is set to the model's level (A-weighted where the model gives a spectrum). Tyre noise of other vehicles, rain, wind, electric motors and mopeds are synthesised in `SoundClips`, as is every sound whose recording is missing.

### Several participants in one car, and voice
A role can sit in another role's car, e.g. three participants in one shared AV: give it a pedestrian spawn type, `RideInCarOf` (the other role's name) and a `Seat` (`FrontPassenger`, `RearLeft`, `RearRight`, `RearMiddle`; rear seats need the hatchback, `carIdx` 1; in AVs the AV passenger role sits in the front passenger seat). The participant then rides in that seat on every agent, with a seated figure for the others to see; their camera, headset, responses and logs work as for a pedestrian. Up to 8 participants can take part.

**Voice.** With `-voice` (open microphone) or `-voice ptt` (push to talk: hold `V`) on each agent, the participants hear each other from their avatars (3D sound, through the host; 16 kHz). Each agent records its own microphone to `ExperimentLogs/Voice-*.wav`, with the UTC time of every chunk in `Voice-*.csv` for synchronisation; `-voiceDevice` picks the microphone, `-voiceRecordOnly` records without sending.

![A participant (client) in the passenger seat of another participant's car in the town: seen by the host (left), and their own view (right)](ReadmeFiles/shared_car.jpg)

### Pedestrian groups
Virtual pedestrians can stand next to a participant pedestrian, e.g. to study crossing in groups: `PedestrianGroup` on the experiment (or `-companions` on every agent) lists them with an offset (metres to the right and ahead of the participant) and a behaviour:
- `Follow`: walks along, keeping its offset;
- `Cross`: starts crossing (straight ahead, 12 m) a delay after the start;
- `Hesitate`: steps forward 0.7 m, waits and steps back;
- `Stand`;
- `Llm`: asks a language model whether to cross (see [AI agents](#scene-descriptions-and-ai-agents)).

For example `-companions "follow:-1.2,0;cross:1.4,0.4,3;hesitate:2.6,0,2"` (behaviour:right,ahead[,delay]). The host moves them and clients see them like other AI pedestrians; their actions are logged to `ExperimentLogs/Companions-*.csv`. Avatars come from `Resources/PedestrianAvatars` (`Coupled Sim` ➡️ `Pedestrians` ➡️ `Build avatar list` after adding avatars).

![A participant (blue) with a following, a crossing and a hesitating companion on a canal street in Delft](ReadmeFiles/pedestrian_group.jpg)

### Gaze over the network and gaze triggers
With `-gaze` every agent sends its participant's gaze to the host 20 times a second (eye tracking through OpenXR when the headset has it, else the direction of the head), and the host logs all of them together to `ExperimentLogs/Gaze-*.csv` (role, eyes or head, origin, direction, the object looked at). **Gaze triggers** make cars react to being looked at, as in "stopping by looking" studies: `-gazeTrigger "Pedestrian>Automated - Passenger:0.5:yield,ehmi"` (or `NetworkGaze` ➡️ `Triggers`): when the pedestrian has looked at that role's car for 0.5 s, the car brakes to a stop (about 3 m/s²; AVs driven on the host) and/or its eHMIs switch to `WALK`, while it is looked at and for `HoldSeconds` (3 s) after.

### OpenSCENARIO
`-openscenario <file.xosc>` (in `Assets/StreamingAssets/Scenarios`, or a full path; on every agent) plays the vehicles and pedestrians of an [ASAM OpenSCENARIO](https://www.asam.net/standards/detail/openscenario/) 1.x file in the scene, e.g. scenarios made for esmini or CARLA. Supported: parameter declarations; entities (Vehicle, Pedestrian, catalogue references); `TeleportAction` to a `WorldPosition` and `SpeedAction` to an absolute speed (step, linear or cubic, over a time, a rate or a distance), at the start or at a `SimulationTimeCondition`. Actors move straight along their heading (no lane following); other actions and conditions are logged as unsupported. Positions are the scene's world coordinates (x east, y north; on [OpenDRIVE maps](#city-maps-openstreetmap) those of the .xodr file). Events are logged to `ExperimentLogs/OpenScenario-*.csv`. Example: `CrossingPedestrian.xosc` in the town (experiment `Base`): a car drives in its lane and brakes to a stop before a zebra crossing, and the pedestrian waiting on the kerb crosses in front of it.

![CrossingPedestrian.xosc in the town rendered as a video from a fixed street camera (-videoCams): the pedestrian waits on the far kerb, the car stops before the zebra crossing, and the pedestrian crosses](ReadmeFiles/openscenario_video.jpg)

### Videos from fixed cameras
For online (crowdsourced) versions of a study, the same trial can be rendered as videos from fixed cameras, in the editor, in builds and in batch mode: `-videoCams "street=-30,6,-25,60,12;chase;front;driver"` (a camera at x, y, z with yaw and pitch (and field of view), or behind the participant, ahead of it looking back, or the participant's own view), `-videoStart 2 -videoSeconds 10 -videoFps 30 -videoSize 1920x1080`. More cameras: `rel:x,y,z,yaw,pitch[,fov]` relative to the participant (following its heading, smoothed) and `grid` (the participants' own views in a 2x2 grid, e.g. of a replay with `-replay log.binLog`). The video has the sound at the participant's ears (or at the first camera with `-videoListener 1`), rendered in step with the frames; `-videoAudio 0` leaves it out, `-videoCrf 28` makes smaller files. The simulation runs in step with the frames, so the videos are smooth even on a slow PC. [ffmpeg](https://ffmpeg.org) (on the PATH, or `-ffmpeg path`) joins the frames into `ExperimentLogs/Videos/{camera}.mp4`; without it the JPEG frames stay and the log shows the command to join them. `-videoQuit` quits when done. (The [Unity Recorder set-up](#recording-trial-videos) records the game view in the editor.)

### Scene descriptions and AI agents
`-sceneDescriptions` logs, once a second, a text description of what is around the participant to `ExperimentLogs/SceneDescriptions-*.jsonl`: place, daylight, every car within 60 m (where, distance, speed, approaching or not, braking, its eHMI state) and the pedestrians nearby, e.g. to compare people's and language models' judgements of the same moments.

**Language-model pedestrians.** A companion with the behaviour `Llm` ([pedestrian groups](#pedestrian-groups)) asks Claude every 3 s, with such a description, whether to cross now, and crosses when the answer is CROSS; the answers and reasons are logged with the scenes. It calls the Anthropic Messages API (model `claude-opus-5-5` at low effort; `-llmModel`, `-llmEffort`) with the API key in the environment variable `ANTHROPIC_API_KEY` (never stored in the project); without a key it sends nothing. Requests send the scene descriptions to Anthropic and are billed to that key.

## Testing
`SmokeTest` runs a whole trial without a screen and checks that it works: it opens `StartScene`, starts a host that runs the first trial in keyboard mode, plays for the given time once the experiment runs, and writes a report with errors and exceptions, how far each car and pedestrian moved, and screenshots of the active cameras. It exits with code 0 when no errors occurred.

```
Unity -batchmode -projectPath . -executeMethod SmokeTest.Run -smokeSeconds 30 -smokeOut SmokeTest -logFile smoke.log
```

Options: `-smokeTrial N` (index in `Trials`), `-smokeExperiment N` and `-smokeRole N` (override the trial's experiment and role), `-smokeShot "x,y,z,yaw,pitch;..."` (extra screenshots from these camera poses; also `chase`, `front`, `rel:x,y,z,yaw,pitch[,fov]` relative to the participant, which works on every map, `animal[:Dog]` at the nearest animal, `sign[:CyclePath]` at the nearest traffic sign, `aidrone[:n]` at the n-th nearest AI drone, and `lineup`/`lineupside`), `-smokeDrive seconds` and `-smokeSteer -1..1` (drive the participant's car), `-smokeLineup "ToyotaCorolla,DafXF95,..."` (park those vehicles in a row ahead of the participant, for pictures of vehicles in a real street; besides vehicles also `Bicycle`, `EScooter` and `Moped` with their riders, pedestrians `ped:Female_Adult_01`, animals `animal:Dog` and the hovering `Drone`), `-smokeLineupAngle deg` (e.g. `65`: side by side at that angle, as in angle parking, so every agent is seen in full; the `lineup` view then looks at them from opposite the middle of the line and hides posts, signs and trees in the way), `-smokeLineupAt x,z,heading` (where the line starts and its direction, instead of ahead of the participant), `-smokeShotSize 3200x1800` (size of the `-smokeShot` pictures), `-smokeScene <path>` (scene to start from), `-smokeInputMode VR` (head-mounted display mode; without a headset it checks the camera set-up). The report also counts jumps of the participant's view of more than 30° between samples (they should be 0).

The models (pedestrians, traffic, two-wheelers, vehicles, maps, trial files) have edit-mode tests (`Assets/Tests/EditMode`, 125 tests, including the tyre model): run them in `Window` ➡️ `General` ➡️ `Test Runner`, or with `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`. On Windows, `Unity` is `"C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe"`; on macOS `/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity`.

**Every map on the ground.** `AllMapsGroundTests` (category `AllMaps`, 20 tests, about 15 minutes) builds each map of the catalogue on its terrain and fails when more than 2% of its trees, lamps, signs, street furniture or parked vehicles float more than 0.45 m above the surface under them or sink more than 0.7 m into it, when signal heads are not at their height over the road, or when the paths of traffic leave the road; the log lists the worst cases. Run only these with `-testCategory AllMaps`. In the map builder, every placed object is set on the lifted surface that was under it, and roads on bridges above other roads lift only themselves (not the road or ground under the overpass).

**Automatic tests on GitHub.** `.github/workflows/tests.yml` runs the edit-mode tests on every pull request and push to `master` with [GameCI](https://game.ci) (a Linux runner with Unity 6000.6.9f1 in Docker; the first run imports the project for about an hour, later runs use a cached `Library`). Unity needs a license there, given as repository secrets (only people with admin rights to the repository can add them; GitHub never shows them again, also not in the logs). Until they are added, the check passes with the note "Tests skipped". With the [GitHub CLI](https://cli.github.com) (`gh auth login` once), in the `coupled-sim` folder:

```bash
gh secret set UNITY_EMAIL --repo bazilinskyy/coupled-sim
```

```bash
gh secret set UNITY_PASSWORD --repo bazilinskyy/coupled-sim
```

Each asks for the value (typed text is not shown). Then one of these two:
- **Unity Personal:** the license file that Unity Hub wrote when it activated the license, `Unity_lic.ulf` (Windows `C:\ProgramData\Unity\Unity_lic.ulf`, macOS `/Library/Application Support/Unity/Unity_lic.ulf`, Linux `~/.local/share/unity3d/Unity/Unity_lic.ulf`):

  ```bash
  gh secret set UNITY_LICENSE --repo bazilinskyy/coupled-sim < "/Library/Application Support/Unity/Unity_lic.ulf"
  ```
- **Unity Pro, Plus or an education seat with a serial number:**

  ```bash
  gh secret set UNITY_SERIAL --repo bazilinskyy/coupled-sim
  ```

Without the GitHub CLI: on github.com open the repository ➡️ **Settings** ➡️ **Secrets and variables** ➡️ **Actions** ➡️ **New repository secret**, and add the same names with the values (for `UNITY_LICENSE` paste the whole content of the `.ulf` file). The account must sign in with e-mail and password: an account that only signs in through the university (single sign-on) needs a password set in its Unity ID settings; a separate lab account for the tests is better than a personal one. A license used here counts as one of the account's activations. Then run the workflow by hand once (**Actions** ➡️ **Tests** ➡️ **Run workflow**) to see it work.

**Two agents.** A second editor on an instant copy of the project can be a client: on macOS `cp -cR coupled-sim coupled-sim-client` (APFS copies in seconds; delete its `Temp` folder), then run the host with `-clients 1` and the client with `SmokeTest.Run -connect 127.0.0.1`. The reports list what each agent saw.

**Retaking the README pictures.** `Tools/readme_screenshots.sh` (macOS, screen unlocked, about 10 minutes) opens the editor and walks through the windows and screens shown in [Getting started](#getting-started-step-by-step) (`Assets/Editor/ReadmeScreenshots.cs`), saving them to `ReadmeShots/raw`. The pictures in `ReadmeFiles/guide` are made from these (cropped, with numbered markers).

## Hardware
### Several monitors and mirrors
`Managers` ➡️ `DisplayViews` adds views on extra monitors (or on the main screen) for the participant of that agent:
- `LeftScreen` / `RightScreen`: the view continued to the left and right, for a panorama of three monitors side by side (`ScreenAngle` = the angle between them; 0 = seamless);
- `RearMirror`, `LeftMirror`, `RightMirror`: mirror views (shown mirrored); on display 1 they appear at the top of the main view;
- `Custom`: any offset and rotation from the participant's eyes.

Each view has a `Display` (1 = the main monitor, 2 = the second one, ...) and an optional field of view. In a build, the extra monitors are switched on when the participant's avatar appears; in the editor, show them with Game views set to Display 2, 3, ... The command-line option `-views "LeftScreen:2,RightScreen:3,RearMirror:1"` sets the views without the editor. In VR, views on other monitors are for the experimenter (for example a mirror view of what the participant sees behind).

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


### Motion platforms
`-motionUdp host:port` sends the motion of the participant's vehicle over UDP at 100 Hz (`-motionRate`), for FlyPT Mover, SimTools or an own controller: the specific force in the vehicle's axes (surge forward, sway right, heave up, in m/s², gravity removed, smoothed over 30 ms), the roll, pitch and yaw rates (°/s), the attitude (°) and the speed (m/s). Formats (`-motionFormat`): `json` (default, named fields), `csv` (`time,surge,sway,heave,roll_rate,pitch_rate,yaw_rate,roll,pitch,yaw,speed`) and `flypt` (the same values without the time, separated by spaces, one line per packet).
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

**How many participants can take part?** Eight (host and seven clients); tested with two. The limit is `UNetConfig.MaxPlayers`. Several of them can [sit in one car](#several-participants-in-one-car-and-voice).

**Do I need SUMO?** Only for SUMO traffic (`SumoTraffic`, city maps). Install SUMO 1.20 or newer and set `SUMO_HOME`. Without it, the hand-built town has the built-in traffic models (`Traffic`) and city maps run without traffic.

**How do I make a new experiment?** `Coupled Sim` ➡️ `Experiments` ➡️ `Setup…` copies a template (`Base` for the town, `CityMap` for city maps) with the map, roles, traffic and conditions you choose and registers it (see [Setting up a new experiment](#setting-up-a-new-experiment)); then change it in prefab mode (spawn points, paths, pedestrians). The examples `PedestrianCrossings`, `Traffic`, `SumoTraffic` and `CityMap` show the newer features.

**Where are the data?** In `ExperimentLogs` next to the `Assets` folder (in builds: next to the executable): binary logs of all agents per frame and at 50 Hz, pedestrian decisions and eye gaze as CSV. Convert the binary logs to CSV in the start screen (`Convert log to csv`). See [Data logging](#data-logging).

**How do I keep videos and logs in sync?** Use `Visual syncing` (host GUI or dashboard): a red bar on every screen for 1 s, visible in screen recordings, logged as an event.

**How do I set weather, time of day and the city per trial?** With trial parameters, e.g. `time=21:30`, `weather=HeavyRain`, `map=Tokyo` or `location=52.0116,4.3571,400`; or in the dashboard.

**How do I add a city?** Use `location=lat,lon[,radius]` for any place, or add it to the catalogue (see [City maps](#city-maps-openstreetmap)).

**Some objects are pink.** A material uses a shader of the old Built-in render pipeline. Imported assets need converting to URP: `Window` ➡️ `Rendering` ➡️ `Render Pipeline Converter`, or `GraphicsSetup.ConvertToUrp` (see [Graphics](#graphics)).

**The headset is not found / shows black.** See [Head-mounted displays](#head-mounted-displays-vr): the right OpenXR runtime must be active and running before the simulator starts.

**The wheel does nothing, or the pedals are swapped.** Press `F2`: if the wheel is not listed, check the USB connection and its driver; if axes are wrong, run the calibration.

**The frame rate is low.** Start with `-quality auto` (or `-quality Performance`, see [Graphics](#graphics)): shadows and trees cost most (on a city map with a tree register, the nearby trees about a quarter of a frame). `SmokeTest ... -smokeBench` lists what each feature and each group of objects costs on your PC. Run a build rather than the editor; in the editor, close the Scene view and use `Play Maximized`; reduce `EnvironmentController` ➡️ `MaxActiveStreetLights` at night; use a smaller city map or radius. On laptops, make sure the simulator runs on the dedicated GPU (NVIDIA Control Panel ➡️ Manage 3D settings). In VR, the headset's refresh rate (72-90 Hz) is the target.

**Cars drive on the left in London, but the car is left-hand drive.** Yes: maps with left-hand traffic place participants and SUMO traffic on the left, but there is no right-hand drive car model yet.

**Can I use my own avatars or cars?** Pedestrians: any humanoid character; `Coupled Sim` ➡️ `Pedestrians` ➡️ `Build pedestrian prefabs` makes AI pedestrians from Rocketbox-style models. Cars: copy a drivable car prefab and replace the model; AI cars need to be in `NetworkingManager` ➡️ `AI Car System` ➡️ `Prefabs`.

**Is there a way to test the simulator without screens and people?** Yes: the smoke test and the edit-mode tests (see [Testing](#testing)).

## Support the coupled simulator
The coupled simulator is free and stays free. If it is useful for your research or teaching and you would like to give something back, you can [sponsor it on GitHub](https://github.com/sponsors/bazilinskyy) (monthly or once) or [buy me a coffee on Ko-fi](https://ko-fi.com/bazilinskyy). Citing the [paper](#citation), bug reports, ideas and pull requests help just as much.

<p>
  <a href="https://github.com/sponsors/bazilinskyy"><img alt="Sponsor on GitHub" src="https://img.shields.io/badge/sponsor-GitHub-ea4aaa?logo=githubsponsors&logoColor=white&style=for-the-badge"></a>
  <a href="https://ko-fi.com/bazilinskyy"><img alt="Support on Ko-fi" src="https://img.shields.io/badge/support-Ko--fi-ff5e5b?logo=kofi&logoColor=white&style=for-the-badge"></a>
</p>

## Used assets
| Name | Developer | License |
| --- | --- | --- |
| [ACP (Arcade Car Physics)](https://github.com/Saarg/Arcade_Car_Physics) | Saarg | MIT |
| [Textures](https://ambientcg.com) (formerly cc0textures.com), including the wall photos Bricks101, Bricks059, Bricks105, Plaster001, Concrete034 | Various | CC0 |
| [PBR Bark Materials #1](https://assetstore.unity.com/packages/2d/textures-materials/pbr-bark-materials-1-86713) | John's Junkyard | Unity Asset Store EULA |
| Stylized Grass Texture | LowlyPoly | Unity Asset Store EULA |
| Suit Pack (participant avatar) | StudioNewPunch | Unity Asset Store EULA |
| Distance Tool (editor measuring tool) | Wes McDermott | Unity Asset Store EULA |
| Traffic light model | TurboSquid artist | TurboSquid Standard License |
| Claudia and Eric (pedestrian models) | Renderpeople | Renderpeople free license |
| [X Bot](https://www.mixamo.com) idle and walking animations | Adobe Mixamo | Mixamo terms |
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
| [Animated animals](https://quaternius.com) (husky and fox in use; also pigeon, Shiba Inu, cat, deer and horse; via [Poly Pizza](https://poly.pizza)) | Quaternius | CC0 |
| ["PitBull"](https://sketchfab.com/3d-models/pitbull-2be37d5b9f754ed4bcc2d8f41b23a1d7), ["Somali Cat Animated ver 1.2"](https://sketchfab.com/3d-models/somali-cat-animated-ver-12-e185c3fd92b64c32b4515a32b29252fc), ["Animated Bird, Pigeon"](https://sketchfab.com/3d-models/animated-bird-pigeon-797d27b68af3453e865149435df6aa30), ["Realistic Animated Doe 3D Model"](https://sketchfab.com/3d-models/realistic-animated-doe-3d-model-b6172618aa304473a4cf7b9246c0a218), ["Horse"](https://sketchfab.com/3d-models/horse-a6f860e43e364619bccb174a1ac7d0c9) (`Assets/ThirdParty/Animals`, [credits](Assets/ThirdParty/Animals/CREDITS.md)) | [Joseph.AD](https://sketchfab.com/Joseph.AD), [DreamNoms](https://sketchfab.com/DreamNoms), [dudecon](https://sketchfab.com/dudecon), [WildMesh 3D](https://sketchfab.com/WildMesh_3D), [henrysteve973](https://sketchfab.com/henrysteve973) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [Microsoft Rocketbox avatars](https://github.com/microsoft/Microsoft-Rocketbox) (12 avatars, textures reduced to 1024 px) | Microsoft | MIT |
| [HD Low Poly Racing Car No.1201](https://assetstore.unity.com/packages/3d/vehicles/land/hd-low-poly-racing-car-no-1201-118603) | Azerilo | Unity Asset Store EULA (free at time of download) |
| [Sound recordings](Assets/Resources/Sounds/CREDITS.md) (engines, inside a car, bicycle, footsteps, birds, distant traffic; `Assets/Resources/Sounds`) | 14 recordists on [Freesound](https://freesound.org) | [CC0](https://creativecommons.org/publicdomain/zero/1.0/) |
| [Car Kit](https://kenney.nl/assets/car-kit) (15 stylised road vehicles, no longer in traffic; `Assets/ThirdParty/Kenney/CarKit`) | Kenney (www.kenney.nl) | [CC0](https://creativecommons.org/publicdomain/zero/1.0/) |
| ["Generic passenger car pack"](https://sketchfab.com/3d-models/generic-passenger-car-pack-20f9af9b8a404d5cb022ac6fe87f21f5) and ["Generic civil service vehicles pack"](https://sketchfab.com/3d-models/generic-civil-service-vehicles-pack-8ff2a13f30914932a70c7950cfa58465) (`Assets/ThirdParty/Vehicles/GenericCars`, `GenericService`) | [Comrade1280](https://sketchfab.com/comrade1280) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| ["Moto Vespa"](https://sketchfab.com/3d-models/moto-vespa-f1c7cacdbc954b48a4062a4734617afb) (the moped, `Assets/ThirdParty/TwoWheelers/MotoVespa`) | [mohamed ouartassi](https://sketchfab.com/mohamedouartassi) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| ["2014 Toyota Corolla E180 EU (with interior)"](https://sketchfab.com/3d-models/2014-toyota-corolla-e180-eu-with-interior-36f95efb0585464cae43a25a3b3392e8) (`Assets/ThirdParty/Vehicles/ToyotaCorolla`) | [Armored Wave](https://sketchfab.com/armoredwave) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| ["Land Rover Defender 110"](https://sketchfab.com/3d-models/land-rover-defender-110-5773ec04488b46148816b5f7a9f3f44f) (`Assets/ThirdParty/Vehicles/LandRoverDefender`) | [meeww](https://sketchfab.com/meeww) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| ["A long City Bus (FULL INTERIOR)"](https://sketchfab.com/3d-models/a-long-city-bus-full-interior-43d21ebcf51f45da9da0ccb0e6aad19a) (`Assets/ThirdParty/Vehicles/CityBus`) | [Mixmamo.studio](https://sketchfab.com/mixmamo.studio) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| ["DAF XF 95"](https://sketchfab.com/3d-models/daf-xf-95-1e61dfadf7114ad38076f1a6a871ccf1) (`Assets/ThirdParty/Vehicles/DafXF95`) | [Kabourek Roman](https://sketchfab.com/Krakatit) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| ["Box-truck"](https://sketchfab.com/3d-models/box-truck-b0e8788b6b774fe3a18eda1895a2b8fe) (`Assets/ThirdParty/Vehicles/BoxTruck`) | [Cristian Rus](https://sketchfab.com/CristianRus) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |

The CC BY 4.0 models are scaled, cut into body and wheels and merged per material for the simulator; the changes are listed in [`Assets/ThirdParty/Vehicles/CREDITS.md`](Assets/ThirdParty/Vehicles/CREDITS.md). Toyota, Corolla, Land Rover, Defender, DAF and Volvo are trademarks of their owners, who do not endorse Coupled Sim.
