using UnityEngine;
// Stores the participant number selected from the dropdown menu  so it can be used after switching scenes.

public class StoredParticipantNumber : MonoBehaviour {

    //private static bool created = false;
    public static int ParticipantNumber;
    public static int SelectSession;
    
    private void Update()
    {
        ParticipantNumber = DropdownController.ParticipantNumber;
        SelectSession = MainScene.SelectSession;
    }
}
