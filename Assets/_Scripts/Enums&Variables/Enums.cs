
public enum Operation
{
    TurnRightShort,
    TurnRightLong,
    TurnLeftLong,
    Straight,
    EndPoint,
    StartPoint,
    SplinePoint,
    None
}

public enum TargetDifficulty
{
    easy_6,
    easy_5,
    medium_4,
    medium_3,
    hard_2,
    hard_1
}

public enum GameStates
{
    Experiment,
    Waiting,
    TransitionToWaitingRoom,
    TransitionToCar,
    Finished
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
