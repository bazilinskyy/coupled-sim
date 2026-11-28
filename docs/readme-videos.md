# Making clips like the README's

Every short video in the README is a smoke test of experiment 4 (the city maps) rendered in batch mode with the
participant on the autopilot. This page lists each clip's settings and explains how to change where and when things
happen. It is a quick way to make clips for a student project or a crowdsourced study.

## One clip

```
Unity -batchmode -projectPath . -executeMethod SmokeTest.Run -smokeExperiment 4 -smokeRole 2 \
  -smokeParams "map=Ghent;time=10:30;weather=Clear" -smokeSeconds 8 -autopilot \
  -videoCams "chase=rel:0,1.8,-3.6,0,8,62" -videoStart 6 -videoSeconds 24 -videoFps 30 \
  -videoSize 1920x1080 -videoCrf 20 -ffmpeg /path/to/ffmpeg -logFile clip.log
```

The video is written to `ExperimentLogs/Videos/`.

**Who rides or drives:** `-smokeRole` selects the participant:
- `0` drives (`-driverCar ToyotaCorolla`, `ArticulatedBus`, ...)
- `1` walks
- `2` cycles
- `3` rides an e-scooter
- `4` rides a moped
- `6` flies a drone (`-smokeFly climb,yaw,forward,lateral`, `-droneView chase`)
- `7` rides a race bicycle

`-autopilot [km/h]` moves the participant along the map's roads (35 km/h in town without a speed). Riders pedal and
walkers walk at their own pace.

**Where and when:** the `-smokeParams` are trial parameters:
- `map=`: the city map.
- `start=lat,lon,heading`: where the participant starts and which way it faces (degrees, 0 = north, 90 = east).
- `destination=lat,lon`: where its route ends: a driver or a rider on the autopilot takes the quickest way there,
  through building passages too, and stops there. Without it, the participant goes on along the nearest route (a
  walker always keeps to the pavements near the start).
- `time=` (`17:30`) and `date=` (`12-15`): the time of day and the date; the sun follows the city's clock.
- `weather=`: `Clear`, `Overcast`, `LightRain`, `HeavyRain`, `Fog`, `LightSnow`, ...

**What is around:** also `-smokeParams`, all optional:
- `mapTraffic=`: cars per km of lane.
- `maxCyclists=`
- `animals=`: a multiplier (`0` = none).
- `animalMix=Dog:1,Pigeon:6`: the species and how many of each.
- `animalRole=1`, `animalRadius=30` and `animalAhead=12`: where the animals go. They are placed around that role, in
  its sight, within that radius, around a point that far ahead of it.

**The camera:** `-videoCams` gives the camera:
- `chase=rel:x,y,z,yaw,pitch,fov` follows the participant, x right, y up and z ahead, in metres; for example
  `rel:0,1.8,-3.6,0,8,62` is 1.8 m up and 3.6 m behind, pitched 8° down, with a 62° field of view.
- `own=driver` is the participant's own view.
- `street=x,y,z,yaw,pitch` is a fixed camera.

## Timing: arriving at the right moment

The traffic is placed from the map's seed, so the same parameters give nearly the same scene each time; the
participant's moment at a junction moves only by the frame timing. To have it reach a junction, a crossing or an
animal at a chosen moment of the clip, adjust these:

1. **`-videoStart` (seconds):** when recording starts, after the experiment starts. The participant sets off when
   the experiment starts, so a later `-videoStart` begins the recording further along its way.
2. **`-videoSeconds`, and the window you cut from the video:** with
   `ffmpeg -ss 6 -t 20 -i in.mp4 out.mp4` the clip shows seconds 6–26. To show the arrival, move the window, not the
   ride.
3. **`start=`:** a start further back along the same road makes it arrive later. At the autopilot's pace that is about
   1 s per 4 m for a cyclist (15 km/h), 1 s per 10 m for a car (35 km/h) and 1 s per 1.4 m for a walker.
4. **`-autopilot 25`:** a lower speed for a driver, so it arrives later and more slowly.

The log (`-debugTraffic`) prints the participant's position, speed and what it is waiting for every second (lines
`TrafficDebug: participant t=` for a driver, `TrafficDebug: rider t=` for a rider). Read these to see when it reaches
the junction, then move `start=` or the window by that much.

## The README's clips

All at 1920x1080, `-videoFps 30 -videoCrf 20`, with `-autopilot`. "Window" is the part of the render that the README
shows: seconds from, length.

| Clip | Role | `-smokeParams` | Camera | `-videoStart` / `-videoSeconds` | Window | Extra |
|---|---|---|---|---|---|---|
| rush_eindhoven | 2 | `map=Eindhoven;start=51.44844,5.48535,90;destination=51.44838,5.49000;time=08:15;weather=Clear;maxCyclists=250` | `chase=rel:0,3.4,-7.5,0,13,64` | 4 / 40 | 6, 20 | `-videoListener 1` |
| cycle_ghent | 2 | `map=Ghent;time=10:30;weather=Clear;start=51.054091,3.718936,235` | `chase=rel:0,1.8,-3.6,0,8,62` | 6 / 40 | 13, 20 | `-videoListener 1` |
| walk_boulder | 1 | `map=Boulder;time=08:30;weather=Clear;start=40.01795,-105.2788,90;animalMix=Dog:1,Deer:1,Pigeon:6;animalRole=1;animalRadius=30;animalAhead=12` | `chase=rel:0,1.9,-3.2,0,6,62` | 6 / 30 | 0, 20 | |
| race_stellenbosch | 7 | `map=Stellenbosch;time=10:30;weather=Clear;animalMix=Horse:1,Dog:1,Pigeon:4` | `chase=rel:0,1.8,-3.8,0,8,62` | 6 / 30 | 6, 20 | `-videoListener 1` |
| escooter_tokyo | 3 | `map=Tokyo;time=11:00;weather=Clear` | `chase=rel:0,1.8,-3.8,0,8,62` | 6 / 30 | 0, 20 | `-videoListener 1` |
| drive_saopaulo | 0 | `map=SaoPaulo;time=16:00;weather=Clear;start=-23.5605,-46.6573,129` | `chase=rel:0,2.4,-6.5,0,10,60` | 6 / 30 | 0, 20 | `-autopilot 40 -driverCar ToyotaCorolla` |
| bus_nairobi | 0 | `map=Nairobi;time=10:00;weather=Clear` | `chase=rel:0,5,-16,0,12,60` | 6 / 30 | 4, 20 | `-autopilot 35 -driverCar ArticulatedBus` |
| moped_hcmc | 4 | `map=HoChiMinhCity;time=17:30;weather=Clear` | `chase=rel:0,2.4,-4.5,0,12,62` | 6 / 30 | 0, 20 | |
| rain_london | 0 | `map=London;time=17:30;weather=HeavyRain;animalMix=Fox:1,Pigeon:6` | `own=driver` | 6 / 48 | 12, 20 | `-autopilot 35` |
| snow_montreal | 0 | `map=Montreal;time=15:00;weather=LightSnow;animalMix=Husky:1,Pigeon:4` | `chase=rel:0,2.3,-6.5,0,9,58` | 30 / 24 | 6, 18 | `-autopilot 40` |
| dusk_newyork | 0 | `map=NewYork;time=20:25;weather=Clear` | `chase=rel:0,2.4,-6.5,0,10,60` | 8 / 24 | 4, 20 | `-autopilot 40` |
| dusk_sydney | 0 | `map=Sydney;time=16:50;weather=Clear` | `chase=rel:0,2.4,-6.5,0,10,60` | 8 / 24 | 2, 20 | `-autopilot 40` |
| dusk_barcelona | 0 | `map=Barcelona;time=21:20;weather=Clear` | `chase=rel:0,2.4,-6.5,0,10,60` | 8 / 24 | 2, 20 | `-autopilot 40` |
| drone_tue | 6 | `location=51.4479,5.4904,500;time=10:30;weather=Clear` | `own=driver` | 9 / 20 | 0, 20 | `-smokeFly 0.3,0.05,0.5,0 -droneView chase` |
| drone_delft | 6 | `map=Delft;time=10:30;weather=Clear;droneSwarm=10` | `own=driver` | 5 / 20 | 0, 20 | `-smokeFly 0.25,0.06,0.55,0 -droneView chase` |

The clips are cut and encoded with `ffmpeg -ss FROM -t 20 -i render.mp4 -c:v libx264 -preset slow -crf 24
-pix_fmt yuv420p -af "volume=4dB" -c:a aac -b:a 192k -movflags +faststart clip.mp4`.

The clips are not in the repository: they are assets of the
[demo-videos release](https://github.com/bazilinskyy/coupled-sim/releases/tag/demo-videos), which the README links to,
so that a new version of a clip does not add to the size of every clone. Only the posters
(`ReadmeFiles/videos/NAME.jpg`, a frame of the clip) are committed. To replace clips, upload them under the same names:
`gh release upload demo-videos clip.mp4 --clobber` (the link stays the same), and commit their new posters.
