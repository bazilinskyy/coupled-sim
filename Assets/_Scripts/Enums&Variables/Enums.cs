using System;
using System.Collections.Generic;
using System.Linq;
public static class EnumUtil
{
    public static IEnumerable<T> GetValues<T>()
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }
}
public enum Operation
{
    TurnRightShort,
    TurnRightLong,
    TurnLeftLong,
    Straight,
    EndPoint,
    StartPoint,
    SplinePoint,
    None,
    TurnLeft,
    TurnRight,

}
static class OperationMethods
{
    public static bool IsTurn(this Operation operation)
    {
        Operation[] turns = { Operation.TurnRightShort, Operation.TurnRightLong, Operation.TurnLeftLong, Operation.TurnRight, Operation.TurnLeft };
        if (turns.Contains(operation)) { return true; }
        else { return false; }
    }
    public static bool IsLeftTurn(this Operation operation)
    {
        Operation[] turns = { Operation.TurnLeftLong, Operation.TurnLeft };
        if (turns.Contains(operation)) { return true; }
        else { return false; }
    }
    public static bool IsRightTurn(this Operation operation)
    {
        Operation[] turns = { Operation.TurnRightShort, Operation.TurnRightLong, Operation.TurnRight };
        if (turns.Contains(operation)) { return true; }
        else { return false; }
    }

    
}

public enum TurnType
{
    Left,
    Right,
    Straight,
    None,
    EndPoint
}
static class TurnTypeMethods
{
    public static bool IsTurn(this TurnType turn)
    {
        TurnType[] turns = { TurnType.Right, TurnType.Left };
        if (turns.Contains(turn)) { return true; }
        else { return false; }
    }
    public static bool IsLeftTurn(this TurnType turn)
    {
        if (turn == TurnType.Left) { return true; }
        else { return false; }
    }
    public static bool IsRightTurn(this TurnType turn)
    {
        if (turn == TurnType.Right) { return true; }
        else { return false; }
    }
    public static bool IsOperation(this TurnType turn)
    {
        TurnType[] turns = { TurnType.None, TurnType.EndPoint };
        if (turns.Contains(turn)) { return false; }
        else { return true; }
    }
}


//These tags are used for the gazelogger to track what we are looking at exactly, (world is not a real tag but will be used as keyword when nothin else is being looked at)
public enum LoggedTags
{
    Environment,
    Target,
    HUDSymbology,
    HUDText,
    ConformalSymbology,
    InsideCar,
    LeftMirror,
    RightMirror,
    RearMirror,
    Unknown,
}
public enum TargetDifficulty
{
    easy,
    medium,
    hard,
    EasyAndMedium,
    None,
}

public enum GameStates
{
    Experiment,
    Waiting,
    TransitionToWaitingRoom,
    TransitionToCar,
    Finished,
    PractiseDrive,
}

public enum NavigationType
{
    VirtualCable,
    HighlightedRoad,
    HUD_low,
    HUD_high,
}

public enum CameraPosition
{
    Car,
    WaitingRoom,
}
public enum MyCameraType
{
    Normal,
    Varjo,
    Leap,
}
