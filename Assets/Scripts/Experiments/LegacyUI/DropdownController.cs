using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// This script is used in the main scene to select a participant number. The participant number is stored in the script StoredParticipantNumber.cs
// StoredParticipantNumber is called in CarSpawner to create the order in which the cars are presented to the participant in the experiment.
// If you wish to extend the list; Write more participants in the string below. Increase the element size in the gameobject 'MainScene.'
// Make sure that GameObject 'DropDownController' in the Canvas refers to the GameObject 'MainScene' (i.e., not the script, but the Object 'MainScene') in the box 'On Value Change (Int32).'
// See tutorial: https://www.youtube.com/watch?v=Q4NYCSIOamY

public class DropdownController : MonoBehaviour
{
    public List<string> participants = new List<string>() { "Participant 1","Participant 2","Participant 3","Participant 4","Participant 5",
                                                            "Participant 6","Participant 7","Participant 8","Participant 9","Participant 10",
                                                            "Participant 11","Participant 12","Participant 13","Participant 14","Participant 15",
                                                            "Participant 16","Participant 17","Participant 18","Participant 19","Participant 20",
                                                            "Participant 21","Participant 22","Participant 23","Participant 24","Participant 25",
                                                            "Participant 26","Participant 27","Participant 28","Participant 29","Participant 30"};
    public Dropdown dropdown;
    public Text selectedName;    
    public static int ParticipantNumber;
    // Use this for initialization

    void Start ()
    {
        ParticipantList();        	                         // Creates the contents of the list in the game
	}

    public void Dropdown_IndexChanged(int index)
    {
        selectedName.text = participants[index];             // Allows you to select the participant name based on the index
        ParticipantNumber = dropdown.value + 1;              // Stores the participant number based on the position of the selected name in the list
    }

    void ParticipantList()
    {
        dropdown.AddOptions(participants);
    }
}
