# Coupled simulator for research on driver-pedestrian interactions made in Unity.
## Usage of the simualator
The simulator is open-source and free to use. It is aimed for, but not limited to, academic research. We welcome forking of this repository, pull requests, and any contributions in the spirit of open science and open-source code :heart_eyes::smile: For enquiries about collaboration, you may contact p.bazilinskyy@tudelft.nl.

### Citation
If you use coupled-sim for academic work please cite the following paper.

> Bazilinskyy, P., Kooijman, L., Dodou, D., & De Winter, J. C. F. (2020). Coupled simulator for research on the interaction between pedestrians and (automated) vehicles. 19th Driving Simulation Conference (DSC). Antibes, France. 

## Description of the simulator
:tv: These days, a video is worth more than a million words. The image below points to a youtube video of the recording of a demo of the simulator with 3 agents:
[![demo video](ReadmeFiles/thumbnail_demo_video.png)](https://www.youtube.com/watch?v=W2VWLYnTYrM)

### Environment
![](ReadmeFiles/night_mode_view.png)

The coupled simulator supports both day and night-time settings. Figure above shows a view of the night mode. Figure below shows the top view of the environment. It is a model of a city centre containing:
- Network of 2-lane roads
- 10 intersections with traffic lights that can be turned on and off before the experiment or programmatically in real-time
- 34 zebra crossings
- Static objects (buildings, parked cars, trees)
- Advertisements (programmable and can be used as visual distractions)

Drivable cars:
- small (similar to Smart Fortwo)
- medium (similar to Pontiac GTO)
- large (similar to Nissan Datsun)

Cars that are not controlled by the human participants can be instructed to follow a trajectory before the experiment or can be programmed to respond to other road users.

![](ReadmeFiles/world_top_view.png)

#### Input
The coupled simulator supports a keyboard and a gaming steering wheel as input sources for the driver of the manual car, a keyboard for the passenger of the AV to control the external human-machine interface, and a motion suit for the pedestrian. At the moment, supported motion suit is Xsens Motion Suit.

#### Output
The supported sources of output are a head-mounted display (HMD) and computer screen for the driver, a computer screen for the passenger, and a head-mounted display for the pedestrian. At the moment, supported HDM is Oculus Rift CV1.

#### Networking and data logging
The current number of human participants supported by the coupled simulator is three. However, this number can be expanded up to the number of agents supported by the network. Synchronisation in a local network is handled by a custom-made network manager designed to support the exchange of information between agents with low latency and real-time data logging at 50 Hz for variables from the Unity environment and up to 700Hz from the motion suit. The data that are logged include the three-dimensional position and rotation of the manual car and the AV, the use of blinkers by the driver of the manual car, and 150 position and angular variables from the motion suit. The data are stored in binary format, and the coupled simulator contains a function to convert the saved data into a CSV file. The host agent initiates a red bar that is displayed across all agents for 1 s to allow for visual synchronisation in case screen capture software is used.

## Installation
The simualator was tested on Windows 10 and macOS Mojave. All functionality is supported by both platforms. However, support for input and output devices was tested only on Windows 10.

After checking out this project, launch Unity Hub to run the simulator with the correct version of Unity (currently **2019.3.5f1**).

## How to run
Select the project from the Unity Hub projects list. Wait until the project loads in. If it is not in the Unity Hub list (it is the first time you are running the project), it has to be added first - click *Add* and select a folder containing the project files.
Once the project is loaded into the Unity editor press the Play button to run it.

Steps to run an experiment:
1. Start host and wait for clients to join if needed.
To start host press _Start Host_ button. 
To start the client press _Start Client_ button, enter the host IP address and press _Connect_.
2. Once all clients have joined, on the host, select one of the experiments listed under _Experiment:_.
3. On the host, assign roles to participants.
4. On both host and clients, each participant has to select control mode.
5. Start an experiment with the _Start Game_ button.

## Configuration
The central point for configuring the simulator is _Managers_ game object from the _StartScene_ scene. It has two components:
- _PlayerSystem_: gathering references to player avatar prefabs,
- _NetworkingManager_: gathering references to experiment definitions and elements spawned during networked experiment runtime (currently only waypoint-tracking cars - _AICar_).

![](ReadmeFiles/player_system.png)

![](ReadmeFiles/networking_manager.png)

The experiment is defined solely with prefab containing the _ExperimentDefinition_ component in the root object.
To edit the experiment definition, double click the prefab in the _Project_ window.
To make newly created experiment selectable you have to add its prefab to _Experiments_ list on _NetworkingManager_ component.

![](ReadmeFiles/experiment_definition.png)

![](ReadmeFiles/project.png)

Prefab will be opened in edit mode along with the currently defined _Regular Prefab Editing Environment_. When defining the experiment it is worth setting _Regular Prefab Editing Environment_ variable to the Unity scene defined in the experiment (_Edit -> Project Settings -> Editor -> Prefab Editing Environments -> Regular Environment_).

![](ReadmeFiles/project_settings.png)

_ExperimentDefinition_ component defines the following fields:
- _Name_: the name of the experiment
- _Scene_: Unity scene name to be loaded as an experiment environment
- _Roles_: list defining roles that can be taken during an experiment by participants
- _Points of Interest_: static points that are logged in experiment logs to be used in the log processing and analysis
- _Car Spawners_: references to game objects spawning non-player controlled cars

_Points of interest_ is a list of _Transform_ references.
_CarSpawners_ list references game objects containing component inheriting from _CarSpawnerBase_. It defines, with overridden _IEnumerator SpawnCoroutine()_  method, spawn sequence (see _TestSyncedCarSpawner_ for reference implementation). Car prefabs spawned by the coroutine with _AICar Spawn(AICar prefab, bool yielding)_ method must be one of the referenced prefabs in _AICarSystem_ list on _NetworkManager_ component. 

### Configuration of agents
Roles field is a list of _ExperimentRoleDefinition_ struct's defining experiment roles with the following data fields:
- _Name_: short name/description of the role
- _SpawnPoint.Point_: defines where player avatar will be spawned
- _SpawnPoint.Type_: a type of player avatar. It may be either _Pedestrian_, _Driver_, _Passenger_ of an autonomous car.

#### Adding and removing agents
To add a new agent either increase the size of Roles array or duplicate existing role by right-clicking on the role name and selecting Duplicate from the context menu.

To remove the agent right click on the role name and select Delete from the context menu or decrease the list size removing end entries that don't fit the resized list.

#### Configuration of starting location of agents
Add a new game object to the prefab and set its position and rotation.
Drag the newly created object to the _SpawnPoint.Point_ in role definition.

#### Configuration of the pedestrian agent
No additional configuration is needed for pedestrian type agents.

#### Configuration of the driver agent
For _Driver_ role following field has to be defined:
- _CarIdx_ - points to a car prefab on the _AvatarPrefabDriver_ (field on _PlayerSystem_ component) list that will be spawned for this role. 

#### Configuration of the passenger agent
For _Passenger_ type of agent following additional fields has to be defined:
- Car Idx - indicates car prefab that will be spawned for this role. Selected prefab is the one on the indicated index on PlayerSystem lists (for PassengerAvatarPrefabPassenger list)
- Three _DriverHMI_ fields - define which HMI prefab to spawn on indicated spots
- _AutonomusPath_ - references game object defining waypoints for the autonomous car via WaypointCirciut component

### Configuration of non-playable characters
![](ReadmeFiles/traffic_circuit.png)

Paths for both non-playable pedestrians and vehicles are defined with WaypointCircuit component.
To add waypoint press plus sign and drag waypoint Transform into the newly added field.
To remove waypoint press a minus sign next to the waypoint.
To reorder waypoint click up/down signs next to a waypoint.
To change the position of a waypoint select waypoint transform and move it do the desired position.

#### Configuration of the movement of the non-playable vehicles
Additionally, for vehicles, SpeedSetting along with Collider component might be used to further configure tracked path. 
![](ReadmeFiles/speed_settings.png)

#### Configuration of daylight conditions
![](ReadmeFiles/day_night_control.png)

DayNightControl component helps to define different experiment daylight conditions. It gathers lighting-related objects and allows defining two lightings presets - Day and Night, that can be quickly switched for a scene. This component is intended to be used at the experiment definition setup stage. When the development of the environment is complete, it is advised, to save the environment into two separate scenes (with different light setups) and bake lightmaps.

#### Configuration of traffic lights
![](ReadmeFiles/street_light_manager.png)

Creating a traffic street lights system is best started with creating an instance of _ExampleStreetLightCrossSection_ and adjusting it. 
Traffic light sequence is defined in _StreetLightManager_ component as a list of _StreetLightEvents_. Events are processed sequentially. Each event is defined with the following fields:
- _Name_: descriptive name of an event
- _Delta Time_: relative time that has to pass since previous event to activate the event
- _CarSections_: cars traffic light group that the event applies to
- _PedestrianSections_: pedestrian traffic light group that the event applies to
- _State_: state to be set on the lights specified by sections, LOOP_BACK is a special state that restarts the whole sequence

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

# Master Thesis Johnson Mok

In this section, the additions made by Johnson Mok during his Master Thesis are outlined.

## Table of Content
- Logging
	- Add variable to the worldlogger.
	- Logging varjo eye-tracking data.
	- Logging bodysuit bool.
	- Varjo's own logger.
- eHMI
	- Fixed eHMI msg.
	- Remove eHMI GUI when eHMI is fixed.
- Distraction car spawner.
- Restructure AICar.cs.
	- Brake on input.
	- AV driving initiation 
- Varjo
	- Varjo camera on prefab.
	- Position setter for varjo.
	- Visualize eye-gaze vector.
	- Adding eye-tracking to the prefab.
	- Gaze Ray hit pedestrian.
	- Brake AV based on eye-gaze
- Vive controller
	- Trigger input.

# Logging
## Add variable to the worldlogger
The logger makes use of a byte stream, thus the order is of importance. The location of where to add the variable depends on your own preference. In this chapter, I describe how to add data from the varjo HMD to the logger.
I added the distance between the start (passenger) and end the point (pedestrian) of the eye-gaze ray as the variable *distance*. The *distance* variable is defined in the *VarjoGazeRay_CS.cs* script. 
This script is attached to a GameObject called *Gaze*, which is a child of the vehicle prefab. 

**The first step is to make the data usable in other script**, which is done as follows:

**1.** Calculate/define the wanted variable in the corresponding script. In this case, the *VarjoGazeRay_CS.cs* script.

**2.** Make sure that the variable is public, or create a public function to retrieve the variable. 
```
public DataType variable;
```
or
```
private DataType variable;

public DataType getVariable()
{
	return variable;
}
```
**The second step is to log the data.** The data logging is done in the *WorldLogging.cs* script in the *LogFrame* function. 

**3.** The *distance* variable is part of the vehicle prefab, thus within the *LogFrame* function we move to the part where data from the avatar prefabs are logged.
```
foreach (var driver in _driverBuffer)
```
**4.** Right under the foreach statement there are 3 lines of code which log the position, the rotation and the car blinker stater of the vehicle. I want to log the distance variable right after the car blinker state. Thus, right under the car blinker state line is where I put the code to log the distance data.
Code to check varjo gaze status. To only log the gaze data when the eye-calibration has been done. Log arbitrary value when the gaze status is INVALID.
```
float distance;
if (VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID)
{
	distance = driver.transform.GetComponentInChildren<VarjoGazeRay_CS>().getGazeRayHit().distance;
}
else if(VarjoPlugin.GetGaze().status != VarjoPlugin.GazeStatus.VALID)
{
	distance = -1.0f; 
}
_fileWriter.Write(distance);
```
**5.** All variables which are logged in the *LogFrame* function need to be declared in the class *SerializedFrame* publicly so that the *TranslateBinaryLogToCsv* function can access the variable.

**The third step is to translate from binary to csv.**

**6.** In the *TranslateBinaryLogToCsv* function, add a column header for your variable at the appropriate position. Since the distance data is written after the blinker state, the *columnsPerDriver* will be as follows:
```
const int columnsPerDriver = 3 /*pos x,y,z*/ + 3 /*rot x,y,z */ + 1 /*blinkers*/ + 1 /*distance*/ + 3 /* local velocity */ + 3 /* local smooth velocity */ + 3 /* world velocity */ + 3 /* world velocity smooth */;
```

**7.** Read the data from the binary file. The *distance* data is stored in the *frame* from *LogFrame*. On top of that, it needs to be read from every avatar in the game (vehicle and pedestrian). Thus the read line for distance is added in the for-statement of the *numDriversThisFrame*.
```
int numDriversThisFrame = numAICars + numPersistentDrivers;
for (int i = 0; i < numDriversThisFrame; i++)
{
	frame.DriverPositions.Add(reader.ReadVector3());
	frame.DriverRotations.Add(reader.ReadQuaternion());
	frame.BlinkerStates.Add((BlinkerState)reader.ReadInt32());
	frame.Distance = reader.ReadSingle(); // Varjo data distance
}
```

**8.** Next, in the *TranslateBinaryLogToCsv* function under the HEADER ROWS section. You need to add the name of the variable to the const string *driverTransformHeader*.
```
const string driverTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;blinkers;*distance*;vel_local_x;vel_local_y;vel_local_z;vel_local_smooth_x;vel_local_smooth_y;vel_local_smooth_z;vel_x;vel_y;vel_z;vel_smooth_x;vel_smooth_y;vel_smooth_z"; 
```

**9.** Lastly, in the *TranslateBinaryLogToCsv* function under the ACTUAL DATA section. You need to add the distance variable again in the for-statement of the *numDriversThisFrame to retrieve the data, which in a few lines later is then added to the *line*.
```
for (int i = 0; i < numDriversThisFrame; i++)
{
	var pos = PosToRefPoint(frame.DriverPositions[i]);
	...
	var blinkers = frame.BlinkerStates[i];
	var distance = frame.Distance;
	...
}
if (prevFrame == null || prevFrame.DriverPositions.Count <= i)
{
	if (i == localDriver)
	{
		line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{*distance*};0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0");
	}
	else
	{
		line.Add($"{pos.x};{pos.y};{pos.z};{euler.x};{euler.y};{euler.z};{(BlinkerState)blinkers};{*distance*};0;0;0;0;0;0;0;0;0;0;0;0");
	}
}
```

## Logging varjo eye-tracking data
To add varjo eye-tracking data, you need to follow the same steps as described above. Additionally, you need to add a check beforehand.
The varjo eye-tracking data is only available after the eye-tracking calibration. Thus the following if-statements are added:
```
if (VarjoPlugin.GetGaze().status == VarjoPlugin.GazeStatus.VALID && driver.transform.Find("Gaze"))
{
	// do stuff with eye-tracking data
}
else if(VarjoPlugin.GetGaze().status != VarjoPlugin.GazeStatus.VALID)
{
	// Fill in variable with arbitrary value.
}
```

## Logging bodysuit bool
In the worldlogger the bodysuit data would automatically be logged for a pedestrian. However, in my experiments no bodysuits are used. 
Resulting in a large number of empty columns in the logged data file. Thus, I created a bool to turn off/on the logging of the bodysuit data.
In the *WorldLogging.cs* script:

**1.** Create bool and get function:
```
public static Boolean bodySuit = false;

public static Boolean returnBodySuit()
{
	return bodySuit;
}
```

**2.** In the *PlayerAvatar.cs* script, change the AvatarPose GetPose function such that it only gets the bodysuit pose when the input is set to true.
```
public AvatarPose GetPose(bool bodySuit)
{
...
int i_end = 0;
if (bodySuit == true)
{
	i_end = SyncTransforms.Length;
}
```

**3.** In the *LogFrame* function, add the condition to only get the pose when the bool is set to true.
```
foreach (var pedestrian in _playerSystem.Pedestrians)
{
	pedestrian.GetPose(**returnBodySuit()**).SerializeTo(_fileWriter); 
}
```

**4.** Only set column headers for bodysuit when the bool is set to true. Changes in the *TranslateBinaryLogToCsv.cs* script:
```
if (WorldLogger.returnBodySuit()) { 
	columnsPerPedestrian = pedestrianSkeletonNames.Length * columnsPerBone + columnsPerBone; // + columnsPerBone is for the root transform;
}
```

**5.** Same thing as for step 3 in the HEADER ROWS section:
```
if (WorldLogger.returnBodySuit())
{
	for (int i = 0; i < pedestrianSkeletonNames.Length; i++)
	{
		sb.Append(string.Join(separator, Enumerable.Repeat(pedestrianSkeletonNames[i], columnsPerBone)));
		if (i < pedestrianSkeletonNames.Length - 1)
		{
			sb.Append(separator);
		}
	}
}
...
if (WorldLogger.returnBodySuit())
{
	writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians * (pedestrianSkeletonNames.Length + 1))));
}
else
{
	writer.Write(string.Join(separator, Enumerable.Repeat(boneTransformHeader, numPedestrians)));
}
writer.Write("\n");
```

## Varjo's own logger
The varjo plugin provides it's own logger. However, it does not seem to work with the coupled-sim without some modifications. 
This script is not used, since it is desired to log all data into one file. Synchronizing data requires a lot of manual work.

This logger logs the following:
- Role (of the HMD wearer)
- Time, starting after eye-calibration
- Eye gaze ray distance between the passenger and the pedestrian.
- Direction of the eye gaze ray in both the coordinate system of the world and the HMD.
- Origin of the eye gaze ray in both  the coordinate system of the world and the HMD.
- HMD position.
- HMD rotation.

**1.** The *VarjoGazeRay* script must first be modified to recognize the role. This is done by looking at the tag assigned to the prefab on which this script is assigned to.
In the *Update* function:
```
if (transform.parent.CompareTag("Pedestrian"))
{
	role_varjo = "Pedestrian";
	target = "Passenger";
}
else if (transform.parent.CompareTag("AutonomousCar"))
{
	role_varjo = "Passenger";
	target = "Pedestrian";
}
```

The data is taken from the *VarjoGazeRay* script directly. For this to work the scripts must be attached to the same object.
**2.** Create a get function in the *VarjoGazeRay* script to get all the wanted data.
```
public string getRoleVarjo()
{
	return role_varjo;
}

public RaycastHit getGazeRayHit()
{
	return gazeRayHit;
}

public Vector3 getGazeRayForward() // HMD space
{
	return gazeRayForward;
}

public Vector3 getGazePosition() // HMD space
{
	return gazePosition;
}

public Vector3 getGazeRayDirection() // world space
{
	return gazeRayDirection;
}

public Vector3 getGazeRayOrigin() // world space
{
	return gazeRayOrigin;
}
```

**2.** In the *VarjoGazeLog* script, define the data to log. 
```
RaycastHit gazeRayHit;
Vector3 gazeRayForward;
Vector3 gazeRayDirection;
Vector3 gazePosition;
Vector3 gazeRayOrigin;
string role_varjo;
float distance;
float time = 0.0f;
```

**3.** Modify the header rows by changing the names in the static string ColumnNames:
```
static readonly string[] ColumnNames = { Role, "Time", "Distance", "HMDPosition", "HMDRotation", "Gaze Forward (HMD)", "Gaze Position (HMD)", "Gaze Direction (world)", "Gaze Origin (world)" };
```

**4.** Update the time vaiable inside the *Update* function:

**5.** In the *LogGazeData* function, get the varjo HMD datat using the previously created get functions.
```
gazeRayHit = this.GetComponent<VarjoGazeRay_CS>().getGazeRayHit();
distance = gazeRayHit.distance;
gazeRayForward = this.GetComponent<VarjoGazeRay_CS>().getGazeRayForward();      // hmd space
gazeRayDirection = this.GetComponent<VarjoGazeRay_CS>().getGazeRayDirection();  // hmd space
gazePosition = this.GetComponent<VarjoGazeRay_CS>().getGazePosition();          // world space
gazeRayOrigin = this.GetComponent<VarjoGazeRay_CS>().getGazeRayOrigin();        // world space
role_varjo = this.GetComponent<VarjoGazeRay_CS>().getRoleVarjo();

hmdPosition = VarjoManager.Instance.HeadTransform.position;
hmdRotation = VarjoManager.Instance.HeadTransform.rotation.eulerAngles;
```

**6.** Convert the data to type string and add to the logdata matrix.
```
string[] logData = new string[9]; 

logData[0] = role_varjo;
logData[1] = time.ToString();
logData[2] = distance.ToString("F3");
etc...
```

# eHMI
## Fixed eHMI message
I copied and adapted the *ClientHMIController.cs* script to show a fixed eHMI message on the autonomous car. This newly adapted script is renamed into *eHMIShowJohn.cs*. 

First, I changed the *update* function, such that the eHMI is only dependent on public variables which can be set in the inspector as opposed to being dependent on key input.
```
public bool ShowDisabled = false;
public bool ShowStop = false;
public bool ShowWalk = false;

if (ShowDisabled == true)
{
	ChangeToState(HMIState.DISABLED);
}
else if (ShowStop == true)
{
	ChangeToState(HMIState.STOP);
}
else if (ShowWalk == true)
{
	ChangeToState(HMIState.WALK);
}
```

Next, you need to attach the *eHMIShowJohn.cs* script to the car prefab that you want to use it on. Select in the inspector the desired message to show.

Lastly, you need to tell the playersystem when to use the fixed eHMI. This is done in the *PlayerSystem.cs* script. For this you need a public variable to turn on the fixed eHMI in the inspector.
```
public bool eHMIFixed = false;
```
On top of that, an if-statement is needed to use the *eHMIShowJohn.cs* script instead of the *ClientHMIController.cs* script when the eHMIFixed bool is set to true. This is done in the function *SpawnLocalPlayer*
```
...
if(eHMIFixed == true)
{
	var hmiControl = LocalPlayer.GetComponent<eHMIShowJohn>();
	hmiControl.Init(_hmiManager);
}
else
{
	var hmiControl = LocalPlayer.GetComponent<ClientHMIController>();
	hmiControl.Init(_hmiManager);
}
```



## Remove eHMI GUI when eHMI is fixed
The GUI for the ehMI is removed when the eHMI is fixed. To do this one needs to adapt the *Host.cs* script.
One simply needs to add a condition on when to activate the GUI, which in this case is done with an if-statement.
```
if (_playerSys.eHMIFixed == false)
{
	_hmiManager.DoHostGUI(this);
	_visualSyncManager.DoHostGUI(this);
}
```


# Distraction car spawner
To spawn a(n unmanned) distraction car, I used the *TestSyncedCarSpawner.cs* script. 
The passenger used to "teleport" to the distraction car, due to the camera object in the car prefab. 
To solve this "teleportation" problem, I disabled the camera in the *PlayerAvatar.cs* script for the mode HostAI in the initialization function.
```
if(mode == PlayerSystem.Mode.HostAI)
{
	GetComponentInChildren<Camera>().gameObject.SetActive(false);
}
```



## Restructure AICar
Having structured and clear code is important in maintaining workable code, especially in large projects. 
I restructured the *AICar.cs* script using functions such that making adaptations to the code becomes easier.
The *FixedUpdate* function describes all the main tasks. These main tasks are further divided into functions. 
The overall overview has now become as follows:
- Normal driving
- Braking
- Restarting AV after braking




# Brake on input
The AV was only able to brake on collision with brake triggers. To brake the AV using input adaptations are made to the *AICar.cs* script. 
The adaptations are made such that the input will activate the same sequence of code as would have happend in a collision with a brake trigger.

**1.** First, a new bool is needed to determine the state of activation of the braking. 

**2.** Second, in the *Update* function, write the condition to stop the AV when a button is pressed. Here is an example using the spacebar. 
```
// Brake using spacebar
if (Input.GetKeyDown("space") == true)
{
            Brake_Spacebar();
        }
```
When the spacebar is pressed, the function *Brake_Spacebar* is executed. This function sets the necessary variables to brake the AV.
```
void Brake_Spacebar()
{
	SpaceBar = true;
	triggerlocation = this.gameObject.transform.position.z;
	braking = true;
	set_speed = 0;
	set_acceleration = -2;
	jerk = -Mathf.Abs(-6);
}
```

**3.** Now that we have set the variables to the right values and triggered the SpaceBar bool. The next step is to change the braking conditions in the helper functions found in the *FixedUpdate* function.
The first one is the *Decelerate_Car* function. The braking is direction dependend, the braking condition is adapted in the z-direction, since in my experiment the AV drives in the z-direction.
```
else if (WaitTrialZ == true || BrakeZ == true || SpaceBar == true)
{
	delta_distance = Mathf.Abs(this.gameObject.transform.position.z - triggerlocation);
}
```

**4.** Next, in my experiment, the AV needs to drive again after standing still for a certain amount of time. Thus changes are made in the *Reset_Speed_After_Stopping* function.
Similarily to step 4, the resetting condition is adapted.
```
if (WaitTrialZ == true || SpaceBar)
{
	delta_distance = Mathf.Abs(this.gameObject.transform.position.z - startlocation);
}
```

## AI Driving initiation
A manual start of the AV driving initiation is added to give the passenger enough time to perform the eye-calibration. Otherwise the AV would start driving as soon as the experiment starts. 
Leaving no time for the participant to perform the eye-calibration.
For the implementation of the manual initiation, adaptations needs to be made in the *AICar.cs* script.
**1.** First, a new bool is needed to determine the state of activation of the braking. 

**2.** Second, in the *Update* function, write the condition to start the AV when a button is pressed. Here is an example using the up arrow key.
```
if(Input.GetKeyDown("up") == true)
{
	startCar = true;
}
```

**3.** Lastly, in the *FixedUpdate* function. Add the condition that the AV only starts driving when the startCar boolean is set to true.
```
if ((braking == false) && (reset == false) && (startCar == true))
{
Car_Driving_Behaviour();
}
```

# Varjo
The varjo headset requires varjobase and SteamVR to work. To implement the varjo into unity, one needs the varjo plugin.

These can all be found in the list below:
- VarjoBase: [https://varjo.com/downloads/#varjo-base](https://varjo.com/downloads/#varjo-base)
- Varjo plugin (for unity + examples): [https://developer.varjo.com/downloads#unreal-developer-assets](https://developer.varjo.com/downloads#unreal-developer-assets)
- Steam: [https://store.steampowered.com/](https://store.steampowered.com/)
- SteamVR: [https://store.steampowered.com/app/250820/SteamVR/](https://store.steampowered.com/app/250820/SteamVR/)


## Varjo camera on prefab
To make use of the varjo headset in the coupled-sim, one needs to adapt the existing player avatar prefabs. This holds for both the pedestrian (participant) and the AV's.
For this purpose, I duplicated the existing participant prefab and created a new "Participant_varjo" prefab.

**1.** Add the VarjoCameraRig prefab on the same level as the CameraPositionSet. Make sure to set the same transform values for the VarjoCameraRig as the CameraPositionSet

**2.** Add the *Position Setter* script to the VarjoCameraRig and set as the target "CameraPosition".

With these two steps you should be able to see the world from the perspective of the pedestrian.

## Visualize eye-gaze vector
The visualization of the eye=gaze vector is done by adapting the provided *VarjoGazeRay* script. 
The visualization is done by drawing a line between the origin (the eyes) and the end (the gaze point).

**1.** To fulfill this task, I created the LineDrawer helper function.
```
public struct LineDrawer
{
	private LineRenderer lineRenderer;
...
```

**2.** The init function adds the [LineRenderer](https://docs.unity3d.com/Manual/class-LineRenderer.html) to the gameobject which this script is attached to.
```  
private void init(GameObject gameObject)
{
	if (lineRenderer == null)
	{
		lineRenderer = gameObject.AddComponent<LineRenderer>();
                //Particles/Additive
                lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));
	}
}
```

**3.** Adds the linerenderer to the gameobject if it has not been done yet and set the properties of the linerenderer.
```
public void DrawLineInGameView(GameObject gameObject, Vector3 start, Vector3 end, Color color)
{
	if (lineRenderer == null)
	{
		init(gameObject);
	}

	//Set color
	lineRenderer.startColor = color;
	lineRenderer.endColor = color;

	//Set width
	lineRenderer.startWidth = 0.01f;
	lineRenderer.endWidth = 0.01f;

	//Set line count which is 2
	lineRenderer.positionCount = 2;

	//Set the postion of both two lines
	lineRenderer.SetPosition(0, start);
	lineRenderer.SetPosition(1, end);
}
```

**4.** Destroy the gameobject after use.
```
public void Destroy()
{
	if (lineRenderer != null)
	{
		UnityEngine.Object.Destroy(lineRenderer.gameObject);
	}
}
```

**5.** Now, one simply has to fill in the *DrawLineInGameView* arguments to visualize the eye-gaze
```
lineDrawer.DrawLineInGameView(gameObject, gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.green);
```

## Positionsetter for varjo.
With the old positionsetter you would appear floating on top of the prefab. This is due the headtracking of the HMD. 
To solve this problem I adjusted the *PositionSetter* script. This snippet of code removes the height tracking of the HMD. 
Resetting you back to the position of the prefab.

```
hmdPosition = VarjoManager.Instance.GetHMDPosition(VarjoPlugin.PoseType.CENTER);
changePos = target.position;
if (hmdPosition.y > 0.0f)
{
	changePos.y = changePos.y - hmdPosition.y; 
}
transform.position = changePos;
```

## Adding eye-tracking to the prefab
Additional scripts need to be added to initialize the eye-tracking calibration and visualize the eye-gaze vector in the coupled-sim.

**1.** Create an empty on the particpant_varjo prefab. Put it on the same level as the VarjoCameraRig prefab and name the empty "Gaze".

**2.** Add the "Varjo Gaze Calibration Request" and "Varjo Gaze Ray" script to the "Gaze".

One should be able to see their eye-gaze visualized after performing the eye-calibration.

## Gaze ray hit pedestran
The next part of the eye gaze ray interaction is to register the "hits" ONLY with the pedestrian. 
For this purpose, a new layer is created.
**1.** Create new layer. I called this layer "target".

**2.** Assign the pedestrian prefab with the layer "target".

**3.** Assign the pedetrian with the tag "Pedestrian".

**4.** In the *VarjoGazeRay.cs* script, adapt the raycast physics condition such that it only sees objects in the "target" layer.
```
if (Physics.SphereCast(gazeRayOrigin, gazeRayRadius, gazeRayDirection, out gazeRayHit, Mathf.Infinity, 1 << LayerMask.NameToLayer(target)))
```

**5.** Next, inside the target layer, add a condition to perform a certain action on objects with the tag "Pedestrian".
```
if (gazeRayHit.collider.gameObject.CompareTag(target) && target == "Pedestrian")
```

## Brake AV based on eye-gaze
To brake the AV based on eye-gaze, both the *AICar* script and the *VarjoGazeRay* script need to be modified. 
In the previous section I already set up the condition for target hits. The first step is to send a message from the *VarjoGazeRay* script to the *AICar* script when the passenger gazes at the pedestrian.

**1.** In the *AICar* script, create a bool and a function to change the bool from another script. 
In the code snippet below, a counter "n" is added. This is because we want the message to be send only once. 
```
private bool boolVarjoSaysStop;
public void VarjoSaysStop()
{
	boolVarjoSaysStop = true;
	n += 1;
}
```

**2.** In the *VarjoGazeRay* script, add the condition to send the message when the distance between the passenger
and the pedestrian is under a certain threshold.
```
if (gazeRayHit.collider.gameObject.CompareTag(target) && target == "Pedestrian")
{
	if(gazeRayHit.distance < 25.0f ) // threshold set at 25m
	{
		this.GetComponentInParent<AICar>().VarjoSaysStop();
	}
}
```

**3.** Now I use the boolVarjoSaysStop to activate the manual braking (previously activated with the spacebar button).
Inside the *AICar* script, in the *Update* function.
```
if ((Input.GetKeyDown("space") == true || (StopWithEyeGaze == true && boolVarjoSaysStop == true && n == 1)) && startCar == true)
```

**4.** A new public bool is introduced to activate/deactivate the eye-gaze stopping, since it's not always desired to use the eye-gaze to stop the car.
This bool is set in the inspector of the vehicle prefab. **Note: this bool is also set in the condition in the previous code snippet**.
```
public bool StopWithEyeGaze = false;

```
Modify the braking conditions inside the Brake_AV(Collider other) function. 
```
if (other.gameObject.CompareTag("StartTrial_Z") && StopWithEyeGaze == false)  
```

# Vive controller
## Trigger input
Rather than copy pasting the guide I followed to set up the vive controller, I put some links down referring to the guides I follwed. 

-[A Complete Guide to the SteamVR 2.0 Input System in Unity](https://medium.com/@sarthakghosh/a-complete-guide-to-the-steamvr-2-0-input-system-in-unity-380e3b1b3311)

-[Video: Unity SteamVR 2.0 Input using actions](https://www.youtube.com/watch?v=bn8eMxBcI70)

In this section, I will briefly explain the *ViveInput* script.
In the start function one needs to define the actions to be used. Here we take the GapAcceptance button defined in the UI button layout called viveController. 
Two lines are added to define the actions to be taken for trigger pressed and trigger released.
```
private void Start()
{
	SteamVR_Actions.viveController_GapAcceptance.AddOnStateDownListener(OnTriggerPressed, SteamVR_Input_Sources.Any);
        SteamVR_Actions.viveController_GapAcceptance.AddOnStateUpListener(OnTriggerReleased, SteamVR_Input_Sources.Any);
}
```
Inside the two functions called *OnTriggerPressed* and *OnTriggerReleased* one can define the actions to be taken.
```
private void OnTriggerPressed(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
{
	//Debug.Log("Trigger is pressed");
        triggerPulled = 1.0f;
}

private void OnTriggerReleased(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
{
        //Debug.Log("Trigger is released");
        triggerPulled = 0.0f;
}
```




