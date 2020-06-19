# Master Thesis Johnson Mok.
In this readme file the additions made by Johnson Mok during his Master Thesis are outlined.

## Table of Content
- 
-
-
-
-
-

## Add variable to the worldlogger
The logger makes use of a byte stream, thus the order is of importance. The location of where to add the variable depends on your own preference. In this chapter, I describe how to add data from the varjo HMD to the logger.
I added the distance between the start (passenger) and end the point (pedestrian) of the eye-gaze ray as the variable *distance*. The *distance* variable is defined in the *VarjoGazeRay_CS.cs* script. 
This script is attached to a GameObject called *Gaze*, which is a child of the vehicle prefab. 

*The first step is to make the data usable in other script*, which is done as follows:
1. Calculate/define the wanted variable in the corresponding script. In this case, the *VarjoGazeRay_CS.cs* script.
2. Make sure that the variable is public, or create a public function to retrieve the variable. 
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
*The second step is to log the data.* The data logging is done in the *WorldLogging.cs* script in the *LogFrame* function. 
3. The *distance* variable is part of the vehicle prefab, thus within the *LogFrame* function we move to the part where data from the avatar prefabs are logged.
```
foreach (var driver in _driverBuffer)
```
4. Right under the foreach statement there are 3 lines of code which log the position, the rotation and the car blinker stater of the vehicle. I want to log the distance variable right after the car blinker state. Thus, right under the car blinker state line is where I put the code to log the distance data.
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
5. All variables which are logged in the *LogFrame* function need to be declared in the class *SerializedFrame* publicly so that the *TranslateBinaryLogToCsv* function can access the variable.

*The third step is to translate from binary to csv.*
6. In the *TranslateBinaryLogToCsv* function, add a column header for your variable at the appropriate position. Since the distance data is written after the blinker state, the *columnsPerDriver* will be as follows:
```
const int columnsPerDriver = 3 /*pos x,y,z*/ + 3 /*rot x,y,z */ + 1 /*blinkers*/ + 1 /*distance*/ + 3 /* local velocity */ + 3 /* local smooth velocity */ + 3 /* world velocity */ + 3 /* world velocity smooth */;
```
7. Read the data from the binary file. The *distance* data is stored in the *frame* from *LogFrame*. On top of that, it needs to be read from every avatar in the game (vehicle and pedestrian). Thus the read line for distance is added in the for-statement of the *numDriversThisFrame*.
```
int numDriversThisFrame = numAICars + numPersistentDrivers;
for (int i = 0; i < numDriversThisFrame; i++)
{
	frame.DriverPositions.Add(reader.ReadVector3());
	frame.DriverRotations.Add(reader.ReadQuaternion());
	frame.BlinkerStates.Add((BlinkerState)reader.ReadInt32());
	frame.Distance = reader.ReadSingle(); // Varjo data distance
}
8. Next, in the *TranslateBinaryLogToCsv* function under the HEADER ROWS section. You need to add the name of the variable to the const string *driverTransformHeader*.
```
const string driverTransformHeader = "pos_x;pos_y;pos_z;rot_x;rot_y;rot_z;blinkers;*distance*;vel_local_x;vel_local_y;vel_local_z;vel_local_smooth_x;vel_local_smooth_y;vel_local_smooth_z;vel_x;vel_y;vel_z;vel_smooth_x;vel_smooth_y;vel_smooth_z"; 
```
9. Lastly, in the *TranslateBinaryLogToCsv* function under the ACTUAL DATA section. You need to add the distance variable again in the for-statement of the *numDriversThisFrame to retrieve the data, which in a few lines later is then added to the *line*.
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




























	