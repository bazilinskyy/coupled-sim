
# Coupled simulator for research on driver-pedestrian interactions made in Unity.
## Usage of the simulator
The simulator is open-source and free to use. It is aimed for, but not limited to, academic research. We welcome forking of this repository, pull requests, and any contributions in the spirit of open science and open-source code :heart_eyes::smile: For enquiries about collaboration, you may contact p.bazilinskyy@tue.nl.

### Citation
If you use coupled-sim for academic work please cite the following paper:

> Bazilinskyy, P., Kooijman, L., Dodou, D., & De Winter, J. C. F. (2020). Coupled simulator for research on the interaction between pedestrians and (automated) vehicles. 19th Driving Simulation Conference (DSC). Antibes, France. 

### Examples of use of the simulator
1. Get out of the way! Examining eHMIs in critical driver-pedestrian encounters in a coupled simulator - [repo](https://github.com/bazilinskyy/coupled-sim-evasive), [paper](https://doi.org/10.1145/3543174.3546849).
1. Stopping by looking: A driver-pedestrian interaction study in a coupled simulator using head-mounted displays with eye-tracking - [paper](http://doi.org/10.1016/j.apergo.2022.103825).

## Description of the simulator
:tv: These days, a video is worth more than a million words. The image below points to a youtube video of the recording of a demo of the simulator with 3 agents:

[![demo video](ReadmeFiles/thumbnail_demo_video.png)](https://www.youtube.com/watch?v=W2VWLYnTYrM)

### Environment
![](ReadmeFiles/night_mode_view.png)

The coupled simulator supports both day and night-time settings. Figure above shows a view of the night mode. Figure below shows the top view of the environment. It is a model of a city centre containing:
- Network of 2-lane roads
- Loop of 4-lane road (partially surounded by buildings)
- Loop of 6-lane road (partially surounded by buildings)
- Half-clover interchange
- 10 intersections with traffic lights that can be turned on and off before the experiment or programmatically in real-time
- 34 zebra crossings
- Static objects (buildings, parked cars, trees)

Drivable cars:
- small (similar to Smart Fortwo) - DrivableSmartCommon
- medium (similar to Ford Focus 2011) - DrivableHatchbackCommon
- ~~medium (similar to Pontiac GTO)~~
- ~~large (similar to Nissan Datsun)~~

Cars that are not controlled by the human participants can be instructed to follow a defined trajectory or can be programmed to respond at runtime to other road users.

![](ReadmeFiles/world_top_view.png)

#### Input
The coupled simulator supports a keyboard and a gaming steering wheel as input sources for the driver of the manual car, a keyboard for the passenger of the AV to control the external human-machine interface, and a motion suit for the pedestrian. At the moment, supported motion suit is Xsens Motion Suit.

#### Output
The supported sources of output are a head-mounted display (HMD) and computer screen for the driver, a computer screen for the passenger, and a head-mounted display for the pedestrian. ~~At the moment, supported HDM is Oculus Rift CV1.~~

#### Networking and data logging
The current number of human participants supported by the coupled simulator is four (host and three clients). However, this number can be expanded up to the number of agents supported by the network. Synchronization in a local network is handled by a custom-made network manager designed to support the exchange of information between agents with low latency and real-time data logging at 50 Hz for variables from the Unity environment and up to 700Hz from the motion suit. The data that are logged include the three-dimensional position and rotation of the manual car and the AV, the use of blinkers, high-beam, stop light, and 150 position and angular variables from the motion suit. The data are stored in binary format, and the coupled simulator contains a function to convert the saved data into a CSV file (_Convert log to csv_ button in _Play Mode_).
Besides logging data to the binary file, the same set of frame data (in a very similar binary format) is being sent with requested intervals (that can be set with _NetworkingManger.RealtimeLogInterval_ property) during the simulation to UDP port 40131 on localhost. The data can be used to monitor the simulation with external tools at runtime. _Tools/log_receiver.py_ file contains an example python script that consumes binary data and assembles it into a structure that is easy to interact with.
It should be started before starting the simulation with 'python3 log_receiver.py' from within the _Tools_ folder.
The structure of the frame object (with example data) is as follows:
```
{
   "timestamp":1.711700439453125,
   "roundtrip":0.0,
   "Driver [car index]":{ # single integer-indexed entry for a player controlled car avatar
      "position":(-94.58521270751953, -0.056745946407318115, 64.8435287475586),
      "rotation":(0.5252280235290527, 90.8692626953125, 359.9905700683594),
      "blinker":0,
      "front":false,
      "stop":false,
      "rigidbody":(3.1246094703674316, -0.0026992361526936293, -0.046936508268117905)
   },
   "Driver [car index]":{ # multiple integer-indexed entries for each AI-controlled car avatars
      "position":(-112.08060455322266, -0.056995689868927, 21.383098602294922),
      "rotation":(0.5083497762680054, 157.48184204101562, -0.0032447741832584143),
      "blinker":0,
      "front":false,
      "stop":false,
      "rigidbody":(0.5082536935806274, -0.0017757670721039176, -1.2219140529632568),
      "speed":5.904001235961914,
      "braking":false,
      "stopped":false,
      "takeoff":false,
      "eyecontact":false
   },
   "Pedestrian [pedestrian index]":{ # multiple integer-indexed entries for each player or AI-controlled pedestrian avatars
       "bone  [bone index]":{ # multiple integer-indexed entries for each bone of pedestrian rig
          "position":(-94.58521270751953, -0.056745946407318115, 64.8435287475586),
          "rotation":(0.5252280235290527, 90.8692626953125, 359.9905700683594)
       }
   },
   "CarLight [car traffic light index]":{ # multiple integer-indexed entries for each car's traffic light
      "state":"b""\\x00"
   },
   "PedestrianLight [pedestrian traffic light index]":{ # multiple integer-indexed entries for each pedestrain's traffic light
      "state":"b""\\x02"
   },
}
```
The host agent may use _Visual syncing_ button to display a red bar across all clients and the host for 1 s to allow for visual synchronization in case screen capture software is used.

## Installation
The simualator was tested on Windows 10 and macOS Mojave. All functionality is supported by both platforms. However, support for input and output devices was tested only on Windows 10.

After checking out this project, launch Unity Hub to run the simulator with the correct version of Unity (currently **2022.1.23f1**).

## Running a project
Select the project from the Unity Hub projects list. Wait until the project loads in. If it is not in the Unity Hub list (it is the first time you are running the project), it has to be added first - click *Add* and select a folder containing the project files.
Once the project is loaded into the Unity editor open StartScene scene.  
![](ReadmeFiles/running.png)
### Running simulation as a host
1. Make sure that all three checkboxes (_Hide Gui_, _Run Trail Sequence Automatically_, _Record Videos_) in _NetworkingManager_ component on _Managers_ game object are unchecked.
2. Press the Play button to run enter _Play Mode_.
3. Once in _Play Mode_, press _Start Host_ button. 
4. If needed, wait for clients to join.
5. Once all clients have connected to the host or in case the host is handling the only participant, select one of the experiments listed under _Experiment:_ section.
6. Assign roles to participants in _Role binding_ section. If no role is selected, the participant will run a "headless" mode.
7. Select one of control modes listed under _Mode_ section.
8. Start an experiment with the _Initialize experiment_ button - all clients will load selected experiment.
9. Once all connected clients are ready - the experiment scene is fully loaded on each client, press _Start simulation_ button.

### Running simulation as a client
1. Make sure that all three checkboxes - _Hide Gui_, _Run Trail Sequence Automatically_, _Record Videos_ (_Managers_ (game object) -> _NetworkingManager_ (component)), are unchecked.
2. Press the Play button to run enter _Play Mode_.
3. Once in _Play Mode_, press _Start Client_ button.
4. Enter the host IP address. 
5. Press _Connect_.
6. Once connected, select one of control modes listed under _Mode_ section.
7. Wait until host starts the simulation.

### Running simulation trails automatically
![](ReadmeFiles/Instant.png)
If the simulation only has one participant that is controlled on a host machine, simulation trails can be set up beforehand and run automatically. It is especially useful when using simulator to record videos for trails which is described in next section. Most of a times user should have any GUI disabled, which can be done by checking _Hide Gui_ checkbox. Gui can be enabled at runtime by pressing _Tab_ button on the keyboard.
To run simulation trails automatiacally, both _Run Trail Sequence Automatically_ has to be checked and trail sequence has to be set up. Once it is done, press Play button to enter _Play Mode_ - first trail in the sequence should start automatically. To finish current trail and either start next one or exit simulator (if currently played trail the last one), press _Escape_ button on the keyboard.
In order to set up trail sequence, user has to define entries on the _Trails_ list (_StartScene_ (scene) -> _Managers_ (game object) -> _NetworkingManager_ (component) -> _Trails_ (field)). Each entry consists of the following fields:
- _ExperimentIndex_: int variable which indicates a zero-based index of a selected experiment in _Experiments_ list (_NetworkingManager_ (component) -> _Experiments_ (field)).
- _RoleIndex_: int variable which indicates a zero-based index of a selected role in _Roles_ list (_ExperimentDefinition_ (component) -> _Roles_ (field)) of a selected experiment prefab.
- _InputMode_: enum variable, that sets participants display/controller pair for the trail. Available values are:
	- _Flat_: use a flat-screen to display simulation and mouse&keyboard/gamepad/steering wheel to control it.
	- _VR_: use virtual reality headset to display simulation and mouse&keyboard/gamepad/steering wheel to control it.  
	- _Suite_: use virtual reality headset to display simulation and XSense suite to control it (only pedestrian avatar).

Additional experiment parameters can be defined for each trail that would modify baseline scenario implemented in experiment. Parametes take form of name-value pair defined in _ExperimentParameters_ list. Those parameters are consumed by any enabled scripts contained in experiment prefab that implement _IExperimentModifier_ interface right after experiment is loaded and before simulation has started. An example of such a script is _EnableAVLabel_.

### Recording trail videos
Simulator is able to record trail videos for "offline" use. Most of the setup is the same as for automatical trails running described above. There are three additional steps that need to be taken to set up automatic trails recording.
1. _Record Videos_ checkbox has to be checked.
2. Each trail in _Trails_ list has to have _Recording Start Time_ and _Recording Duration_ defined. _Recording Start Time_ defines at which second after the trail has started video recording should start. _Recording Duration_ defines how long the recording will last. After recording is finished simulator will proceed to the next trail in the sequence.
3. Following video output parameters can be set up in _Managers_ (game object) -> _Recorder_ (component):
-- _Directory_ - output directory relative to Application.dataPath (when running from Unity Editor it is _Assets_ folder).
-- _Resolution_ - videos output resolution
-- _Framerate_ - videos output framerate
![](ReadmeFiles/recorder.png)
Filenames of recorded videos conform following naming scheme: 
```{trail index}\_{ExperimentDefinition.ShortName}\_roleIdx-{role index}\_{multiple "\_" separated " paremeter name-value pairs}\_{date and time in "yy-MM-dd\_hh-mm" format}```

## Configuration
The central point for simulators configuration are two major components on _Managers_ game object from the _StartScene_ scene:
- _PlayerSystem_: gathering references to player avatar prefabs,
- _NetworkingManager_: gathering references to experiment definitions and elements spawned during networked experiment runtime (currently only waypoint-tracking cars - _AICar_).

![](ReadmeFiles/player_system.png)

The experiment is defined solely with prefab containing the _ExperimentDefinition_ component in the root object.
To make newly created experiment selectable you have to add its prefab to _Experiments_ list on _NetworkingManager_ component.

![](ReadmeFiles/networking_manager.png)

To edit the experiment definition, double click the experiment prefab in the _Project_ window.

![](ReadmeFiles/project.png)

Prefab will be opened in edit mode along with the currently defined _Regular Prefab Editing Environment_. When defining the experiment it is worth setting _Regular Prefab Editing Environment_ variable to the Unity scene defined in the experiment (_Edit -> Project Settings -> Editor -> Prefab Editing Environments -> Regular Environment_).

![](ReadmeFiles/project_settings.png)

_ExperimentDefinition_ component defines the following fields:
- _ShortName_: the short name of the experiment used in video recording output filenames
- _Name_: the name of the experiment
- _Scene_: Unity scene name to be loaded as an experiment environment
- _Roles_: list defining roles that can be taken during an experiment by participants
- _Points of Interest_: static points that are logged in experiment logs to be used in the log processing and analysis
- _Car Spawners_: references to game objects spawning AI-controlled cars
- _AI Pedestrians_: defines AI-controlled pedestrians behaviour with a list of _PedestrianDesc_ structs containing a pair of an _AIPedestrian_ (the game object that defines an AI-controlled pedestrian avatar) and _WaypointCircuit_ (defining a path of waypoints for the linked avatar) 

_Points of interest_ is a list of _Transform_ references.
_CarSpawners_ list references game objects containing component inheriting from _CarSpawnerBase_. It defines, with overridden _IEnumerator SpawnCoroutine()_  method, spawn sequence (see _BaseSyncedCarSpawner_ for reference implementation). Car prefabs spawned by the coroutine with _AICar Spawn(CarSpawnParams parameters, bool yielding)_ method must be one of the referenced prefabs in _AvatarPrefabDriver_ list on _NetworkManager_ component. 

_Base.prefab_ from _ExperimentDefinitions_ folder is an example experiment definition showcasing most of simulator features.

![](ReadmeFiles/experiment_definition.png)

### Configuration of agents
![](ReadmeFiles/roles.png)
_Roles_ field is a list of _ExperimentRoleDefinition_ struct's defining experiment roles with the following base data fields:
 - _Name_: short name/description of the role
 - _SpawnPoint.Point_: defines where player avatar will be spawned
 - _SpawnPoint.Type_: a type of player avatar. It may be either:
-- _PlayerControlledPedestrian_, 
-- _PlayerControlingCar_,
-- _PlayerInAIControlledCar_.

#### Adding and removing agents
To add a new agent either increase the size of _Roles_ array or duplicate existing role by right-clicking on the role name and selecting Duplicate from the context menu.

To remove the agent right click on the role name and select Delete from the context menu or decrease the list size removing end entries that don't fit the resized list.

#### Configuration of starting location of agents
Add a new game object to the prefab and set its position and rotation.
Drag the newly created game object object to the _SpawnPoint.Point_ field in role definition.

#### Agent camera configuration
Avatar prefab can have multiple cameras defined (_PlayerAvatar_ (component) -> _Cameras_ (field)), for example for a driver and passenger position. Camera that will dispalying world for the avatar is defined by providing _SpawnPoint.CameraIndex_.
Additionally to the location, additional camera settings can be provided for a spawned agent with  _CameraSetup_ component. The component can be added to the _SpawnPoint.Point_ game object.
The component exposes two fields:
- _FieldOfView_: value which is set at spawn-time to _Camera.fieldOfView_ property.
- _Rotation_: value which is set at spawn-time to _Transform.localRotation_ of a game object hosting _Camera_ component.
![](ReadmeFiles/CameraSetup.png)

#### Configuration for pedestrain (_PlayerControlledPedestrian_) agents
No additional configuration is needed for pedestrian type agents.

#### Common configuration for car (_PlayerControlingCar_ and _PlayerInAIControlledCar_) agents
Following additional fields has to be defined:
- _CarIdx_ - indicates car prefab that will be spawned for this role. Selected prefab is the one on the indicated index on _AvatarPrefabDriver_ list (field on _PlayerSystem_ component)
- _VehicleType_ - indicates how spawned car will be configured. It may be either _AV_ or _MDV_. This parameter corresponds to _AV_ and _MDV_ sections in _PlayerAvatar_ component.

#### Configuration for _PlayerInAIControlledCar_ agents
Following additional fields has to be defined:
- _TopHMI_, _WindshieldHMI_, _HoodHMI_ fields - defines which HMI prefab to spawn on corresponding spots. Spots are defined in player avatar prefabs (_PlayerAvatar_ (component) -> _HMI Slots_ (field)).
- _AutonomusPath_ - references game object defining waypoints for the autonomous car via _WaypointCirciut_ component

### Waypoints configuration
![](ReadmeFiles/traffic_circuit.png)

Paths that can be followed both by non-playable pedestrians and vehicles are defined with the _WaypointCircuit_ component.
To add waypoint  - press + sign at the bottom of the list and drag waypoint Transform into the newly added field.
To remove waypoint - select waypoint on the _Items_ list and press + sign at the bottom of the list.
To reorder waypoint - drag selected list item to the new position on the list.
To change position of a waypoint - select waypoint transform (by double clicking on the reference from the _Items_ list) and move it do the desired position.

#### Configuration of the movement AI-controlled pedestrians
Additionally, for pedestrains, _PedestrianWaypoint_ along with trigger _BoxCollider_ component might be used to further configure agents behaviour on a tracked path.
##### _PedestrianWaypoint_ component
![](ReadmeFiles/pedestrian_waypoint.png)
_PedestrianWaypoint_ component allows to change walking speed when pedestrian avatar enters _BoxCollider_ with following parameters:
- _targetSpeed_ - controls movement speed
- _targetBlendFactor_ - controls animation speed, by blending between idle and full speed walk
#### Configuration of the movement AI-controlled cars
Additionally, for vehicles, _SpeedSettings_ along with trigger _BoxCollider_ component might be used to further configure agents behaviour on a tracked path.

##### _SpeedSettings_ component
![](ReadmeFiles/speed_settings.png)
- _SpeedSettings_ component allows to change car behaviour when car avatar enters _BoxCollider_.
- _Type_ - Indicates what kind of waypoint is it
--  InitialSetSpeed - Waypoint placed at the spawnpoint that sets up initial speed to _speed_ or to 0 if _causeToYield_ is set to true.
-- SetSpeedTarget - Regular waypoint that changes car behaviour according to rest of parameters.
-- Delete - Destroys car avatar.
- _speed_ - Indicates target speed that car will be trying to reach.
- _acceleration_ - Indicates acceleration that will be used to reach target speed (If target speed is lower than current speed, value has to be negative.)
- _BlinkerState_ - Indicates whether car should have any of turn indicator blinking.
- _causeToYield_ - If checked, car will deaccelerate with _breakingAcceleration_, wait _yieldTime_ and resume driving by accelerating with _acceleration_ until car reaches _speed_.
- _yieldTime_ - Indicates how long car should stay in place after making a full stop.
- _brakingAcceleration_ - If _causeToYield_ is true, this value needs to be negative.
Eye contact related parameters are described in "Configuration of driver's eye-contact behavior" section.
##### Custom behaviours
Waypoint can trigger custom behaviour, on _AICar_ with linked _CustomBehaviour_ derived component, by providing _ScriptableObject_ deriving form _CustomBehaviourData_ (See reference implementation in _BlinkWithFrontLights_ and _BlinkPatternData_). _CustomBehaviour_ components should be linked to _AICar_ at spawn time (See reference implementation in _BaseSyncedCarSpawner_).
####  Exporting and importing WaypointCircuit's
_WaypointCirciut_ can be serialized into CSV format (semicolon separated) with an _Export to file_ button. 
CSV file can be modified in any external editor and then imported with an _Import from file_ button. Importing files will remove all current waypoint objects and replace them with newly created ones according to the data in the imported CSV file. Following parameters are serialized to and deserialized from CSV files:
1. _GameObject_ properties

| Property name | CSV column | Type | Description
| --- | --- | --- | ---
| [name](https://docs.unity3d.com/ScriptReference/Object-name.html) | name | string | name
| [tag](https://docs.unity3d.com/ScriptReference/GameObject-tag.html) | tag | string | tag
| [layer](https://docs.unity3d.com/ScriptReference/GameObject-layer.html) | layer | integer | unity layer index

2. _Transform_ properties

| Property name | CSV columns | Units| Description
| --- | --- | --- | ---
| [position](https://docs.unity3d.com/ScriptReference/Transform-position.html) | x; y; z | meters | position
| [eulerAngles](https://docs.unity3d.com/ScriptReference/Transform-eulerAngles.html) | rotX; rotY; rotZ | degrees | rotation in euler angles
| [localScale](https://docs.unity3d.com/ScriptReference/Transform-localScale.html) | scaleX; scaleY; scaleZ | - | local scale

3. _SpeedSettings_ properties

| Property name | CSV column | Type/Units| CSV values
| --- | --- | --- | --- | ---
| Type | waypointType | WaypointType enum | 0 for InitialSetSpeed, 1 for SetSpeedTarget, 2 for Delete | 
| speed | speed | km/h | - | 
| acceleration | acceleration | m/s<sup>2</sup> | - | 
| BlinkerState | blinkerState | BlinkerState enum | (0 for None, 1 for Left, 2 for Right) | 
| causeToYield | causeToYield | boolean | True/False | 
| EyeContactWhileYielding | lookAtPlayerWhileYielding | boolean | True/False | 
| EyeContactAfterYielding | lookAtPlayerAfterYielding | boolean | True/False | 
| yieldTime | yieldTime | seconds | - | 
| brakingAcceleration | brakingAcceleration | m/s<sup>2</sup> | - | 
| YieldingEyeContactSince | lookAtPedFromSeconds | seconds | - | 
| YieldingEyeContactUntil | lookAtPedToSeconds | seconds | - | 
| customBehaviourData | customBehaviourDataString | any class derived from CustomBehaviourData | multiple entries of % separated {scriptable object name}#{scriptable object instance id} pairs | 

4. _BoxCollider_ properties

| Property name | CSV column(s) | CSV values | Description
| --- | --- | --- | ---
| [enabled](https://docs.unity3d.com/ScriptReference/Collider-enabled.html) | collider_enabled | (True/False) | is component enabled
| [isTrigger](https://docs.unity3d.com/ScriptReference/Collider-isTrigger.html) | isTrigger | (True/False) | is collider a trigger
| [center](https://docs.unity3d.com/ScriptReference/BoxCollider-center.html) | centerX; centerY; centerZ | - | box collider center
| [size](https://docs.unity3d.com/ScriptReference/BoxCollider-size.html) | sizeX; sizeY; sizeZ | - | box collider size

### Configuration of driver's eye-contact behavior
Initial eye contact tracking state and base tracking parameters are defined with fields in the _EyeContact_ component.
_EyeContactTracking_ defines the initial (and current at runtime) driver's eye contact behavior while the car is not fully stopped.
- _MinTrackingDistance_ and _MaxTrackingDistance_ define (in meters) the range of distances at which eye contact tracking is possible. Distance is measured between the driver's head position and the pedestrian's root position (ignoring a distance on a vertical axis).
- _MaxHeadRotation_ (in degrees) limits head movement on a vertical axis.
_EyeContact_, if tracking is enabled, selects as the target the closest game object tagged with a _"Pedestrian"_ tag that is within the distance range, if it meets rotation constraint (this constrain is checked when the closest object is already selected).
![](ReadmeFiles/Pedestrian.png)

_EyeContactRigControl_ is a component that consumes tracking target provided by _EyeContact_ component and animates drivers head movement.

![](ReadmeFiles/eye-contact.png)

Eye contact behavior tracking state can be changed when the car reaches the waypoint. Behavior change is defined by the _SpeedSettings_ - the component embedded on waypoint objects. The following four fields control those changes:
- _EyeContactWhileYielding_: defines how the driver will behave while the car is fully stopped
- _EyeContactAfterYielding_: defines how the driver will behave when the car resumes driving after a full stop. This value simply overwrites the current value of _EyeContact.EyeContactTracking_ if the car has fully stopped.
- _YieldingEyeContactSince_: defines how many seconds need to pass before the driver will make eye contact (starting from the moment the car has fully stopped)
- _YieldingEyeContactUntil_: defines how many seconds need to pass before the driver ceases to maintain eye contact (starting from the moment the car has fully stopped)

#### Configuration of daylight conditions
![](ReadmeFiles/day_night_control.png)

_DayNightControl_ component helps to define different experiment daylight conditions. It gathers lighting-related objects and allows defining two lightings presets - Day and Night, that can be quickly switched for a scene. This component is intended to be used at the experiment definition setup stage. When the development of the environment is complete, it is advised, to save the environment into two separate scenes (with different light setups) and bake lightmaps.

#### Configuration of traffic lights
![](ReadmeFiles/street_light_manager.png)
Creating a traffic street lights system is best started with creating an instance of _ExampleStreetLightCrossSection_ prefab and adjusting it. 
_TrafficLightsManager_ component manages state of _CarSection_ and _PedestrainSection_ objects that group respectively _CarTrafficLight_ and _PedestrianTrafficLight_ instances that share common behaviour.
Traffic light initial state is defined with list of _TrafficLightEvent_ structs stored in _initialStreetLightSetup_ field. This events are triggered once before simulation has started.
Traffic light initial state is defined with list of _TrafficLightEvent_ structs stored in _streetLightEvents_ field. This events are triggered sequentially in a loop. 
Each event is defined with the following fields:
- _Name_: descriptive name of an event
- _Delta Time_: relative time that has to pass since previous event to activate the event
- _CarSections_: cars traffic light group that the event applies to
- _PedestrianSections_: pedestrian traffic light group that the event applies to
- _State_: state to be set on the lights specified by sections

_CurrentIndex_ and _CurrentTimer_ allow to skip ceratin parts of sequence. 
- _CurrentIndex_ selects _TrafficLightEvent_ that the sequence will start from. 
- _CurrentTimer_ sets time offset from the start of the selected _TrafficLightEvent_.

### Details on editing car prefabs
_Speedometer_ component controls a speed indicator. 
To use digital display, set the _Speedometer Text_ field in order to do that.
To use analog display, set the following fields:
- _Pivot_ - the pivot of an arrow
- _PivotMinSpeedAngle_ - the inclination of an arrow for 0 speed
- _PivotMaxSpeedAngle_ - the inclination of an arrow for max speed
- _MaxSpeed_ - max speed displayed on the analog display

## Troubleshooting
### Troubleshooting MVN suit
#### Running the simulation
Connect the Asus Router with the USB Adapter to the PC and plug the Ethernet in a yellow port of the router. Let it boot up for some time while you prepare the software and the suit. Connect the Oculus Rift HDMI and USB 3.0 to the computer.
Plug in the black MVN dongle in a USB 3.0 port of the computer Run the latest version of MVN Analyze. Fill out the anthropometric data and participant name by creating a new recording (1).

Check under _Options -> Preferences -> Miscellaneous -> Network Streamer_ that the data streamer is set to _Unity3D_.

Note: if you find out later on that somehow the avatar looks buggy in Unity, play around with the down sampling skip factor in the _Network Streamer_ overview to improve rendering.

Turn on the Body Pack with all the sensors connected. This will sound like one beep. Wait for 30 seconds and press the WPS button on the router. When the Body Pack of the suit beeps again, continuously press the button for 2 seconds until you hear two sequential beeps. Look if the light of the button becomes a strobe light. If this does not happen within a minute or 5, you’ve got a problem named Windows.

##### The problem named Windows
Delete the following software from the pc and re-install the latest version of MVN Analyze. This will re-install also the bonjour print services.

![](ReadmeFiles/mvn_windows.png)
 
##### The post era of having problems with Windows
If you have an avatar in MVN Analyze and all the sensors are working, boot Unity for the simulation. See figure below: press play to launch the simulation, use the dropdown menu to select a participant and press the trial button to launch a trial.

![](ReadmeFiles/mvn_unity.png)

**Note.** If you want to match the orientation of the Oculus to the orientation of the avatar’s head, make sure you have left-clicked the game screen in Unity and press the R-key on your keyboard. Pressing the R-key to match the visuals with the head orientation is an iterative process which requires feedback from the participants.

If you want to start a new trial, click play again at the top of the Unity screen to end the simulation. Also match the head orientations again with the R-key loop.

### Troubleshooting Oculus Rift
Always run Oculus Home software when using Oculus Rift. Otherwise you will encounter black screens. Make sure your graphics driver and USB 3.0 connections are up to date. If Oculus gives a critical hardware error, disconnect Rift and set Oculus Home software to Beta (Public Test Channel, 1st figure below) and check if Oculus Home setting is set to allow Unknown apps (2nd figure below).
![](ReadmeFiles/troubleshooting_rift_1.png)

![](ReadmeFiles/troubleshooting_rift_2.png)

### Troubleshooting connection
The agent PCs need to be connected via a local network. If you cannot reach the host machine, try to ping it.

#### Windows firewall
![](ReadmeFiles/windows_firewall.png)

Inbound rules: Set correct Unity Editor version to allow all connections.

![](ReadmeFiles/inbound_rules.png)

### Troubleshooting steering wheel
Check if supporting software is installed (e.g., Logitech gaming software G27 is used in our case). In Unity, you can check which button corresponds to your specific wheel. You can find this out by using the following asset in an empty project: https://assetstore.unity.com/packages/tools/input-management/controller-tester-43621

Then make sure you assign the correct inputs in Unity under _Edit -> Project Settings -> Input_ (see figure below).

![](ReadmeFiles/wheel_inputs.png)

## Used assets
We have used the following free assets:

| Name | Developer | License
| --- | --- | ---
| [ACP](https://github.com/Saarg/Arcade_Car_Physics) | Saarg | MIT
| [Textures](https://cc0textures.com) | Various | Creative Commons CC0
| [Oculus Integration](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022) | Oculus | Oculus SDK License
| [Simple modular street Kit](https://assetstore.unity.com/packages/3d/environments/urban/simple-modular-street-kit-13811) | Jacek Jankowski | Unity asset
| [Realistic Tree 10](https://assetstore.unity.com/packages/3d/vegetation/trees/realistic-tree-10-54724) | Rakshi Games | Unity asset
| [Small Town America - Streets](https://assetstore.unity.com/packages/3d/small-town-america-streets-free-59759) | MultiFlagStudios | Unity asset
| [Cars Free - Muscle Car Pack](https://assetstore.unity.com/packages/3d/small-town-america-streets-free-59759) | Super Icon LTD | Depricated, Unity asset
| [Mini Cargo Truck](https://assetstore.unity.com/packages/3d/vehicles/land/mini-cargo-truck-68663) | Marcobian Games | Unity asset
| [Street Bench](https://assetstore.unity.com/packages/3d/props/exterior/street-bench-656) | Rakshi Games | Unity asset
| [waste bin](https://assetstore.unity.com/packages/3d/waste-bin-73303) | Lowpoly_Master | Unity asset
| [Smart fortwo](https://grabcad.com/library/smart-fortwo-1) | Filippo Citati | MIT
| [Vehicle - Essentials](https://assetstore.unity.com/packages/audio/sound-fx/transportation/vehicle-essentials-194951) | Nox Sound | Unity asset
| [Road System](https://assetstore.unity.com/packages/tools/level-design/road-system-192818) | Maxi Barmetler | Unity asset
| [Kajaman's Roads - Free](https://assetstore.unity.com/packages/3d/environments/roadways/kajaman-s-roads-free-52628) | Kajaman | Unity asset
