using UnityEngine;


[CreateAssetMenu]
public class GameState : ScriptableObject
{
    public GameStates gameState;
    
    public bool isWaiting()
    {
        if (gameState == GameStates.Waiting) { return true; }
        else { return false; }
    }

    public bool isTransitionToWaitingRoom() 
    {
        if (gameState == GameStates.TransitionToWaitingRoom) { return true; }
        else { return false; }
    }
    public bool isTransitionToCar()
    {
        if (gameState == GameStates.TransitionToCar) { return true; }
        else { return false; }
    }
    public bool isExperiment()
    {
        if (gameState == GameStates.Experiment) { return true; }
        else { return false; }
    }

    public bool isFinished()
    {
        if (gameState == GameStates.Finished) { return true; }
        else { return false; }
    }
    public void SetGameState(GameStates _gameState)
    {
        gameState = _gameState;
    }
    public GameStates GetGameState()
    {
        return gameState;
    }
}
