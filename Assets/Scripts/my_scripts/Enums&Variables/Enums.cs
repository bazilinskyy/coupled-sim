
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
    easy,
    medium,
    hard
}

public enum GameStates
{
    Experiment,
    Waiting,
    Transition,
    Ended
}

public enum NavigationType
{
    VirtualCable,
    HighlightedRoad
}