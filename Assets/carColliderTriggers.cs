using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class carColliderTriggers : MonoBehaviour
{
    public newNavigator navigator;
    public newExperimentManager experimentManager;
   
    private void OnTriggerEnter(Collider other)
    {
        string[] triggers = { "CorrectTurn", "WrongTurn", "OutOfBounce", "NavigationFinished", "EndStraight", "EnterCrossing" };

        if (!triggers.Contains(other.tag)) { return; }
        //Check the trigger (this pscripts prevents multiple trigger events happening in quick succesion)
        if (!other.GetComponent<MyCollider>().Triggered()) { return; }
        //Handle all triggers
        if (other.CompareTag("CorrectTurn")){ navigator.CorrectTurnTrigger(); }
        else if (other.CompareTag("WrongTurn")) { experimentManager.TookWrongTurn(); }
        else if (other.CompareTag("OutOfBounce")) { experimentManager.CarOutOfBounce(); }
        else if (other.gameObject.CompareTag("NavigationFinished")) { navigator.NavigationFinishedTrigger(); }
        else if (other.gameObject.CompareTag("EndStraight")) { experimentManager.EndOfCalibrationTrial(); }
        else if (other.gameObject.CompareTag("EnterCrossing")) { navigator.EnterCrossingTrigger(); }
    }
}
