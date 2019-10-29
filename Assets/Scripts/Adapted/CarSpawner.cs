// Created by Lars Kooijman - May 2018.
// Based on Instantiator of Andre Dietrich & Koen de Clercq.
// Almost every line is commented in this script to enhance comprehension of the script. However, the understanding of basic coding language is considered a prerequisition. 
// Some variables or lines of code in this script have not been used in the actual experiment. This has been indicated in the comments.
// Feel free to adjust any part of this script to fit your personal use. 

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using System.Collections.Generic;


public class CarSpawner : MonoBehaviour
{
    // Spawner Variables
    public GameObject[] car;                                                                                   // This variable will contain the types of cars. This is public so other scripts can access the cars
    private GameObject[] Clones = new GameObject[5];                                                           // Clone variable. There will be five cars spawned in each trial. 
    private GameObject LastCar;                                                                                // Last car of each trial
    private int conditioncarindex;                                                                             // This variable is used to index the stimulus car in the trial (i.e., the third car)
    private int carindex;                                                                                      // Used to index current car that is spawned
    private int nextcarindex;                                                                                  // Used to index the next car that will be spawned. Needed for the extimation of distance between cars
    private int trialnumber = 1;                                                                               // Starting number of trials
    private float fixed_distance = 13f;                                                                        // Fixed distance between filler cars
    private float variable_distance;                                                                           // Distance between the back of the 2nd car in the trial and the front of the third car in the trial
    private int cars_per_block = 5;                                                                            // Number of cars spawned
    private int nr_conditions = 18;                                                                            // Number of independent variables and thus trials

    // Car related variables 
    private float LengthSmart = 2.73f;                                                                         // Length Smart      
    private float LengthTruck = 7.18f;                                                                         // Length Truck
    private Vector3 SpawnPos;                                                                                  // Spawn Location of filler cars
    private Vector3 SpawnPosCondition;                                                                         // Spawn location of stimulus car (i.e., the third car)
    private int layer;                                                                                         // Layers are used in this script to manipulate the yielding behaviour of the cars

    // Time related variables
    private float cooldownInitial = 1f;                                                                        // Starting cooldown of the script.
    private float pauseExperiment = 10f;                                                                       // Pausing the experiment for 10 seconds, so the participant can take off glasses and rest. Do not forget to pause the whole simulation as well!
    private float pauseExperimentStartUp = 10f;                                                                // 10 second pause until experiment starts again. (fail safe for the first pause)

    // Data related variables
    private int[] list_distances = new int[17];                                                                // Inter-vehicle distances are listed in this list for every trial / condition. 
    private int ParticipantNumber = StoredParticipantNumber.ParticipantNumber;                                 // Grabs the participant number from the mentioned script. This number comes from the dropdown menu from the Main Scene in Unity. 
    private int SelectSession = StoredParticipantNumber.SelectSession;                                         // Grabs the session from the mentioned script. It can be either the test scene or the real experiment.
    private int caseSwitch;                                                                                    // Used for the case switch function - see further in script for detailed explanation of the use of the specific case switch function
    
    private int writeIteration = 0;                                                                            // Filewriting iteration. Used to write a number in a text file, before all the position and orientation data, that is one higher than the previous time that data was written in the text file
    private int buttonGrab;                                                                                    // For loop integer to loop trhough the states of various UI buttons 
    private string info;                                                                                       // Info string used when writing data
    private string data;                                                                                       // Data string used when writing data
    private string Hip;                                                                                        // Hip string used when writing data
    private string Positions;                                                                                  // Position string variable used when writing data
    private string Rotations;                                                                                  // Orientation string variable used when writing data
    private float TTC;                                                                                         // Time-To-Collision variable 

    // Directory related variables
    private static string SavedDataPath = Path.Combine("C:", "SavedData");                                     // Folder where the text file that contains the data recorded in Unity is located 
    private static string FilePath = Path.Combine(SavedDataPath, "Participant_");                              // One folder up from Unity Project Folder
      
    // Game related variables
    private GameObject participant;                                                                            // Used for commands that are related to the avatar in the game
    private GameObject HalfwayCrossing;                                                                        // Used for triggering a UI once the participant crossed a trigger that was halfway of the zebra, however it was not used in the experiment after all.
    private bool Accident;                                                                                     // Used for triggering a UI once the participant collided with a car, however it was not used in the experiment after all.
    private bool paused;                                                                                       // Used when a UI was prompted to pause the game time. This variable has been used!
    private bool TrialEnd = false;                                                                             // Used to start and stop recording at the end of a Trial. Also used to indicate when a trial exactly had finished
    private bool Recording = false;                                                                            // Used to start and stop recording function
    private List<bool> crashlist = new List<bool>();                                                           // This was supposed to be used as a checklist to see whether crashes had occurred or not
    private List<bool> triallist = new List<bool>();                                                           // Used as a list for trial data
    private List<bool> pauselist = new List<bool>();                                                           // Used as a list for puase data
    private bool crossbutton;                                                                                  // This bool was supposed to be used in correspondance with the trigger half way the zebra crossing. The UI that originally prompted once the participant crossed the zebra crossing half way needed an 'okay' input from the experimenter in order to unpause the simulation
    private bool trialbutton;                                                                                  // Used to unpause the simulation each time five trials were completed
    private bool crashbutton;                                                                                  // This bool was supposed to be used in correspondance with the possibility of a crash. The UI that originally prompted after a crash needed an 'okay' input from the experimenter in order to unpause the simulation. 
    private List<bool> crashbuttonlist = new List<bool>();                                                     // This was supposed to be used as a check to see whether the crashbutton was pressed to unpause the simulation
    private List<bool> trialbuttonlist = new List<bool>();                                                     // This was supposed to be used as a check to see whether the trialbutton was pressed to unpause the simulation
    private Rect windowRect = new Rect(425, 225, 300, 50);                                                     // Size of prompt window
    
    // Something someone chugged in and I don't know why.
    private Color[] colors;
    private bool running;
    private UNetHost host;

    // Called by the Host after the level was fully loaded
    public void Init(UNetHost host)
    {
        this.host = host;
        Directory.CreateDirectory(SavedDataPath);                                                              // Creates the SavedData folder in the C:/ drive to save textfiles into    
        string path = string.Format("{0}{1}{2}{3}{4}", FilePath, ParticipantNumber, "_", trialnumber, ".txt"); // Filename of the textfile
        using (StreamWriter sw = File.CreateText(path))                                                        // This function is used to write the first line in the text file
        {
            sw.WriteLine(@"simulation_time;tag;name;pos_x;pos_y;pos_z;euler_x;euler_y;euler_z");
        }
        participant = GameObject.Find("OVRCameraRig");                                                            // Grabs the information of the MVN avatar
        HalfwayCrossing = GameObject.Find("HalfwayCrossing");                                                  // Grabs the information of the Halfway Crossing Trigger

       if (SelectSession == 1)                                                                                 // Practice routine
       {
           StartCoroutine(Practice());
       }

        else if (SelectSession == 2)                                                                           // Real experiment routine
        {    
            StartCoroutine(Trial_Varying_Distances());
        }
        else
        {
            Debug.Log("Please select session. 1 for practice, 2 for final experiment.");
        }
    }

    // The following lines of code are triggered every time unity renders an frame/image/scene. The frequency of an update corresponds to the FPS of the simulation.
    void Update()
    {
        if (!running)
        {
            return;
        }

        if (SelectSession == 2)
        {
            crossbutton = HalfwayCrossing.GetComponent<CrossingTrigger>().crossbutton;                          // Grabs information of the state of the crossing button of the UI that prompts when a participant crossed the zebra half way (not used)

            if (GameObject.FindGameObjectsWithTag("Car").Length != 0)                                           // Finds the cars in the simulation
            {
                GameObject[] Temp = Clones;
                for (buttonGrab = 0; buttonGrab < GameObject.FindGameObjectsWithTag("Car").Length; buttonGrab++)// For loop to loop through states of UI buttons
                {
                    if (Temp[buttonGrab] == null)
                    {
                    }
                    else if (Temp[buttonGrab] != null)
                    {
                        crashbuttonlist.Add(Temp[buttonGrab].GetComponent<AICar>().crashbutton);                 // Not used in the experiment
                        trialbuttonlist.Add(Temp[buttonGrab].GetComponent<AICar>().trialbutton);            
                        crashlist.Add(Temp[buttonGrab].GetComponent<AICar>().Accident);                          // Not used in the experiment
                        triallist.Add(Temp[buttonGrab].GetComponent<AICar>().TrialEnd);
                        pauselist.Add(Temp[buttonGrab].GetComponent<AICar>().paused);

                        if (crashbuttonlist[buttonGrab] == true)                                                 // Not used in the experiment
                        {
                            print("CrashBreak");
                            crashbutton = true; break;
                        }

                        else if (trialbuttonlist[buttonGrab] == true)
                        {
                            print("TrialBreak");
                            trialbutton = true; break;
                        }

                        else if (crashlist[buttonGrab] == true)                                                   // Not used in the experiment
                        {
                            print("CrashBreak");
                            Accident = true; break;
                        }

                        else if (triallist[buttonGrab] == true)
                        {
                            print("TrialBreak");
                            TrialEnd = true; break;
                        }

                        else if (pauselist[buttonGrab] == true)
                        {
                            print("Break");
                            paused = true; break;
                        }
                    }
                }
                // The following if statement checks where the first car of the trial is at and initiates the recording of data from a fixed point. 
                // Option to improve: Make the start of recording from a fixed point using a trigger instead of a dynamic one (i.e., position of car) as used now.
                if (TrialEnd == false && Clones[0] != null && Mathf.RoundToInt((Clones[0].transform.position.x) + 35) >= Mathf.RoundToInt(participant.transform.position.x))
                {
                    Recording = true;
                }

                else if (TrialEnd == true)
                {
                    Recording = false;
                }

                CheckPause();
                CheckRecording();
            }
        }
    }

    // Session 1: Practicing - This subroutine is triggered when initiating the test scene via the main menu
    IEnumerator Practice()
    {
        yield return new WaitForSeconds(cooldownInitial);
        for (int j = 0; j < nr_conditions; j++)
        {
            Recording = false;                          // No Recording                                                                                                 
            TrialEnd = false;                           // No trials. This routine keeps on going until you close the scene in Unity
            layer = 9;                                  // None of the cars yield
            carindex = UnityEngine.Random.Range(0, 2);  // None of the cars have eHMI
            for (int i = 0; i < cars_per_block; i++)    // Five cars each block that is spawned
            {
                nextcarindex = UnityEngine.Random.Range(0, 2);                          // Next car index is used to compute the distance of the front of the next spawned car to the back of the last spawned car
                variable_distance = UnityEngine.Random.Range(20, 26);                   // Varying distance between car 2 and 3
                float CarSpawnPosition = Mathf.RoundToInt(transform.position.z);        // Spawing position of the cars

                if (carindex == (1) | carindex == (3) | carindex == (5))                // This if statement moves the cars up or down based on if it is a smart or a truck. Else smarts would float above the ground.
                {
                    SpawnPos = transform.position - new Vector3(0f, 0.06f, 0f);
                }
                else if (carindex == (0) | carindex == (2) | carindex == (4))
                {
                    SpawnPos = transform.position;
                }

                // The following switch statement spawns a car in each case, being car 1 - 5 in the VR environment. 
                // Each case has four substatements with next to each substatement the type of car that is being spawned and the type of car that will be spawned next.
                switch (i)
                {
                    case 0:
                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation);
                        if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));
                        }
                        carindex = nextcarindex;
                        break;

                    case 1:
                        nextcarindex = conditioncarindex;
                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation);
                        if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (variable_distance + LengthSmart)));
                        }
                        else if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (variable_distance + LengthTruck)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (variable_distance + LengthSmart)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (variable_distance + LengthTruck)));
                        }
                        carindex = conditioncarindex;
                        break;

                    case 2:
                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation);                           
                        Clones[i].layer = LayerMask.NameToLayer("Car");                        

                        if (conditioncarindex == (1) | conditioncarindex == (3) | conditioncarindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (conditioncarindex == (1) | conditioncarindex == (3) | conditioncarindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));
                        }
                        else if (conditioncarindex == (0) | conditioncarindex == (2) | conditioncarindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (conditioncarindex == (0) | conditioncarindex == (2) | conditioncarindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));
                        }
                        carindex = nextcarindex;
                        break;

                    case 3:
                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation);
                        Clones[i].layer = LayerMask.NameToLayer("Car"); 

                        if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));
                        }
                        carindex = nextcarindex;
                        break;

                    case 4:
                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation);
                        LastCar = Clones[i];
                        Clones[i].layer = LayerMask.NameToLayer("Car");

                        if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));
                        }
                        carindex = nextcarindex;
                        break;
                } // End Switch                
            }// End For-loop carspawning

            yield return new WaitUntil(() => Mathf.RoundToInt(LastCar.transform.position.x) == Mathf.RoundToInt(participant.transform.position.x));  // Wait with returning to the beginning of the subroutine until the last car has passed the location of the participant
            yield return new WaitForSeconds(5);                                                                                                      // Wait five more seconds before starting the subroutine all over
        }
    } // End of CarSpawning in the TestScene


    // Session 2: Actual experiment - This subroutine is triggered when initiating the tRIAL scene via the main menu
    IEnumerator Trial_Varying_Distances()
    {
        yield return new WaitForSeconds(cooldownInitial); // Small cooldown/delay to ensure that car spawning starts after some seconds. Sometimes Unity was a bit laggy/buggy and weird things with the spawning of the cars occurred. Therefore this pause was implemented

        // This following switch function contains the order of the conditions presented to participants. This switch function can be replaced by, for example, a function reading the order of presentation from a csv file.
        // Since an executable version of the experiment was made to reduce the computational costs on the dekstop, the order of presentation was hardcoded in this script.
        switch (ParticipantNumber)                        
        {         
            case 1:
                list_distances = new int[] { 9, 17, 11, 12, 7, 10, 5, 6, 16, 2, 8, 4, 3, 14, 13, 18, 1, 15 }; break;
            case 2:
                list_distances = new int[] { 12, 2, 14, 15, 10, 13, 8, 9, 1, 5, 11, 7, 6, 17, 16, 3, 4, 18 }; break;
            case 3:
                list_distances = new int[] { 17, 7, 1, 2, 15, 18, 13, 14, 6, 10, 16, 12, 11, 4, 3, 8, 9, 5 }; break;
            case 4:
                list_distances = new int[] { 4, 12, 6, 7, 2, 5, 18, 1, 11, 15, 3, 17, 16, 9, 8, 13, 14, 10 }; break;
            case 5:
                list_distances = new int[] { 8, 16, 10, 11, 6, 9, 4, 5, 15, 1, 7, 3, 2, 13, 12, 17, 18, 14 }; break;
            case 6:
                list_distances = new int[] { 16, 6, 18, 1, 14, 17, 12, 13, 5, 9, 15, 11, 10, 3, 2, 7, 8, 4 }; break;
            case 7:
                list_distances = new int[] { 1, 9, 3, 4, 17, 2, 15, 16, 8, 12, 18, 14, 13, 6, 5, 10, 11, 7 }; break;
            case 8:
                list_distances = new int[] { 7, 15, 9, 10, 5, 8, 3, 4, 14, 18, 6, 2, 1, 12, 11, 16, 17, 13 }; break;
            case 9:
                list_distances = new int[] { 10, 18, 12, 13, 8, 11, 6, 7, 17, 3, 9, 5, 4, 15, 14, 1, 2, 16 }; break;
            case 10:
                list_distances = new int[] { 18, 8, 2, 3, 16, 1, 14, 15, 7, 11, 17, 13, 12, 5, 4, 9, 10, 6 }; break;
            case 11:
                list_distances = new int[] { 14, 4, 16, 17, 12, 15, 10, 11, 3, 7, 13, 9, 8, 1, 18, 5, 6, 2 }; break;
            case 12:
                list_distances = new int[] { 13, 3, 15, 16, 11, 14, 9, 10, 2, 6, 12, 8, 7, 18, 17, 4, 5, 1 }; break;
            case 13:
                list_distances = new int[] { 11, 1, 13, 14, 9, 12, 7, 8, 18, 4, 10, 6, 5, 16, 15, 2, 3, 17 }; break;
            case 14:
                list_distances = new int[] { 15, 5, 17, 18, 13, 16, 11, 12, 4, 8, 14, 10, 9, 2, 1, 6, 7, 3 }; break;
            case 15:
                list_distances = new int[] { 2, 10, 4, 5, 18, 3, 16, 17, 9, 13, 1, 15, 14, 7, 6, 11, 12, 8 }; break;
            case 16:
                list_distances = new int[] { 6, 14, 8, 9, 4, 7, 2, 3, 13, 17, 5, 1, 18, 11, 10, 15, 16, 12 }; break;
            case 17:
                list_distances = new int[] { 5, 13, 7, 8, 3, 6, 1, 2, 12, 16, 4, 18, 17, 10, 9, 14, 15, 11 }; break;
            case 18:
                list_distances = new int[] { 3, 11, 5, 6, 1, 4, 17, 18, 10, 14, 2, 16, 15, 8, 7, 12, 13, 9 }; break;
            case 19:
                list_distances = new int[] { 16, 11, 12, 3, 15, 5, 13, 1, 17, 6, 10, 2, 7, 4, 14, 8, 9, 18 }; break;
            case 20:
                list_distances = new int[] { 2, 15, 16, 7, 1, 9, 17, 5, 3, 10, 14, 6, 11, 8, 18, 12, 13, 4 }; break;
            case 21:
                list_distances = new int[] { 11, 6, 7, 16, 10, 18, 8, 14, 12, 1, 5, 15, 2, 17, 9, 3, 4, 13 }; break;
            case 22:
                list_distances = new int[] { 5, 18, 1, 10, 4, 12, 2, 8, 6, 13, 17, 9, 14, 11, 3, 15, 16, 7 }; break;
            case 23:
                list_distances = new int[] { 18, 13, 14, 5, 17, 7, 15, 3, 1, 8, 12, 4, 9, 6, 16, 10, 11, 2 }; break;
            case 24:
                list_distances = new int[] { 14, 9, 10, 1, 13, 3, 11, 17, 15, 4, 8, 18, 5, 2, 12, 6, 7, 16 }; break;
            case 25:
                list_distances = new int[] { 15, 10, 11, 2, 14, 4, 12, 18, 16, 5, 9, 1, 6, 3, 13, 7, 8, 17 }; break;
            case 26:
                list_distances = new int[] { 9, 4, 5, 14, 8, 16, 6, 12, 10, 17, 3, 13, 18, 15, 7, 1, 2, 11 }; break;
            case 27:
                list_distances = new int[] { 10, 5, 6, 15, 9, 17, 7, 13, 11, 18, 4, 14, 1, 16, 8, 2, 3, 12 }; break;
            case 28:
                list_distances = new int[] { 8, 3, 4, 13, 7, 15, 5, 11, 9, 16, 2, 12, 17, 14, 6, 18, 1, 10 }; break;
            case 29:
                list_distances = new int[] { 3, 16, 17, 8, 2, 10, 18, 6, 4, 11, 15, 7, 12, 9, 1, 13, 14, 5 }; break;
            case 30:
                list_distances = new int[] { 4, 17, 18, 9, 3, 11, 1, 7, 5, 12, 16, 8, 13, 10, 2, 14, 15, 6 }; break;
            case 31:
                list_distances = new int[] { 1, 14, 15, 6, 18, 8, 16, 4, 2, 9, 13, 5, 10, 7, 17, 11, 12, 3 }; break;
            case 32:
                list_distances = new int[] { 7, 2, 3, 12, 6, 14, 4, 10, 8, 15, 1, 11, 16, 13, 5, 17, 18, 9 }; break;
            case 33:
                list_distances = new int[] { 6, 1, 2, 11, 5, 13, 3, 9, 7, 14, 18, 10, 15, 12, 4, 16, 17, 8 }; break;
            case 34:
                list_distances = new int[] { 13, 8, 9, 18, 12, 2, 10, 16, 14, 3, 7, 17, 4, 1, 11, 5, 6, 15 }; break;
        }

        // The following switch statement reads the independent variables that correspond to the number in the list 'list_distances' of the participant.
        // For example, participant 1 will see in the first trial that the third car will have 40 meters intervehicle distance to its predecessor, will have a text eHMI and will yield. This corresponds to case 9 in the following list.
        for (int j = 0; j < nr_conditions; j++)
        {
            TrialEnd = false;  // Start of new Trial
            Recording = false; // Ensure the end of recording any data            
            switch (list_distances[j])
            {
                case 1:
                    variable_distance = 20; layer = 12; conditioncarindex = UnityEngine.Random.Range(0, 2); ; break; // 20 M intervehicle distance Yielding Car 
                case 2:
                    variable_distance = 30; layer = 12; conditioncarindex = UnityEngine.Random.Range(0, 2); ; break; // 30 M intervehicle distance Yielding Car 
                case 3:
                    variable_distance = 30; layer = 12; conditioncarindex = UnityEngine.Random.Range(0, 2); ; break; // 40 M intervehicle distance Yielding Car
                case 4:
                    variable_distance = 20; layer = 9; conditioncarindex = UnityEngine.Random.Range(0, 2); ; break;  // 20 M intervehicle distance Non-Yielding Car
                case 5:
                    variable_distance = 30; layer = 9; conditioncarindex = UnityEngine.Random.Range(0, 2); ; break;  // 30 M intervehicle distance Non-Yielding Car
                case 6:
                    variable_distance = 40; layer = 9; conditioncarindex = UnityEngine.Random.Range(0, 2); ; break;  // 40 M intervehicle distance Non-Yielding Car 
                case 7:
                    variable_distance = 20; layer = 12; conditioncarindex = UnityEngine.Random.Range(2, 4); ; break; // 20 M intervehicle distance Yielding Car with Text
                case 8:
                    variable_distance = 30; layer = 12; conditioncarindex = UnityEngine.Random.Range(2, 4); ; break; // 30 M intervehicle distance Yielding Car with Text
                case 9:
                    variable_distance = 40; layer = 12; conditioncarindex = UnityEngine.Random.Range(2, 4); ; break; // 40 M intervehicle distance Yielding Car with Text
                case 10:
                    variable_distance = 20; layer = 9; conditioncarindex = UnityEngine.Random.Range(2, 4); ; break;  // 20 M intervehicle distance Non-Yielding Car with Text
                case 11:
                    variable_distance = 30; layer = 9; conditioncarindex = UnityEngine.Random.Range(2, 4); ; break;  // 30 M intervehicle distance Non-Yielding Car with Text
                case 12:
                    variable_distance = 40; layer = 9; conditioncarindex = UnityEngine.Random.Range(2, 4); ; break;  // 40 M intervehicle distance Non-Yielding Car with Text
                case 13:
                    variable_distance = 20; layer = 12; conditioncarindex = UnityEngine.Random.Range(4, 6); ; break; // 20 M intervehicle distance Yielding Car with Lights
                case 14:
                    variable_distance = 30; layer = 12; conditioncarindex = UnityEngine.Random.Range(4, 6); ; break; // 30 M intervehicle distance Yielding Car with Lights
                case 15:
                    variable_distance = 40; layer = 12; conditioncarindex = UnityEngine.Random.Range(4, 6); ; break; // 40 M intervehicle distance Yielding Car with Lights
                case 16:
                    variable_distance = 20; layer = 9; conditioncarindex = UnityEngine.Random.Range(4, 6); ; break;  // 20 M intervehicle distance Non-Yielding Car with Lights
                case 17:
                    variable_distance = 30; layer = 9; conditioncarindex = UnityEngine.Random.Range(4, 6); ; break;  // 30 M intervehicle distance Non-Yielding Car with Lights
                case 18:
                    variable_distance = 40; layer = 9; conditioncarindex = UnityEngine.Random.Range(4, 6); ; break;  // 40 M intervehicle distance Non-Yielding Car with Lights
            } // End Switch

            // Knowing the independent variables assigned to the third car that is spawned in each trial from the previous list, the following loop spawns the five cars in each trial.
            // It works via the same principle as the spawning loop in the test scene.
            carindex = UnityEngine.Random.Range(0, 6);
            for (int i = 0; i < cars_per_block; i++)
            {                
                nextcarindex = UnityEngine.Random.Range(0, 6);
                float CarSpawnPosition = Mathf.RoundToInt(transform.position.z);

                switch (i)
                {
                    case 0:

                        if (carindex == (1) | carindex == (3) | carindex == (5))
                        {
                            SpawnPos = transform.position - new Vector3(0f, 0.06f, 0f);
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4))
                        {
                            SpawnPos = transform.position;
                        }

                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation);

                        if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));                            
                        }
                        else if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {                            
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));                            
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {                            
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {                            
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));                            
                        }
                        carindex = nextcarindex;
                        break;
                        
                    case 1:
                        nextcarindex = conditioncarindex;

                        if (carindex == (1) | carindex == (3) | carindex == (5))
                        {
                            SpawnPos = transform.position - new Vector3(0f, 0.06f, 0f);
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4))
                        {
                            SpawnPos = transform.position;
                        }

                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation); 
                                            
                        if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (variable_distance + LengthSmart)));                            
                        }
                        else if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (variable_distance + LengthTruck)));                         
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (variable_distance + LengthSmart)));                         
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (variable_distance + LengthTruck)));                           
                        }
                        carindex = conditioncarindex;
                        break;

                    case 2:

                        if (conditioncarindex == (1) | conditioncarindex == (3) | conditioncarindex == (5))
                        {
                            SpawnPosCondition = transform.position - new Vector3(0f, 0.06f, 0f);
                        }
                        else if (conditioncarindex == (0) | conditioncarindex == (2) | conditioncarindex == (4))
                        {
                            SpawnPosCondition = transform.position;
                        }

                        Clones[i] = Instantiate(car[conditioncarindex], SpawnPosCondition, this.transform.rotation); //Instantiates the Vehicle of Interest                              
                        
                        // Checks the layer assigned to the third car from the list of independent variables. If the layer is 9, the car doesn't yield. If it is 12, it will yield.
                        switch (layer)
                        {
                            case 9:
                                Clones[i].layer = LayerMask.NameToLayer("Car"); break; 
                            case 12:
                                Clones[i].layer = LayerMask.NameToLayer("Yielding"); break;
                        }
                        
                        if (conditioncarindex == (1) | conditioncarindex == (3) | conditioncarindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));                            
                        }
                        else if (conditioncarindex == (1) | conditioncarindex == (3) | conditioncarindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));                           
                        }
                        else if (conditioncarindex == (0) | conditioncarindex == (2) | conditioncarindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));                            
                        }
                        else if (conditioncarindex == (0) | conditioncarindex == (2) | conditioncarindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));                            
                        }
                        carindex = nextcarindex;
                        break;

                    case 3:

                        if (carindex == (1) | carindex == (3) | carindex == (5))
                        {
                            SpawnPos = transform.position - new Vector3(0f, 0.06f, 0f);
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4))
                        {
                            SpawnPos = transform.position;
                        }

                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation);

                        // Checks the layer assigned to the fourth car from the list of independent variables. If the layer is 9, the car doesn't yield. If it is 12, it will yield.
                        switch (layer)
                        {
                            case 9:
                                Clones[i].layer = LayerMask.NameToLayer("Car"); break;
                            case 12:
                                Clones[i].layer = LayerMask.NameToLayer("YieldingSecond"); break;
                        }

                        if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));                            
                        }
                        else if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));                            
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));                            
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));                            
                        }
                        carindex = nextcarindex;
                        break;

                    case 4:

                        if (carindex == (1) | carindex == (3) | carindex == (5))
                        {
                            SpawnPos = transform.position - new Vector3(0f, 0.06f, 0f);
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4))
                        {
                            SpawnPos = transform.position;
                        }

                        Clones[i] = Instantiate(car[carindex], SpawnPos, this.transform.rotation);
                        LastCar = Clones[i];
                        // Checks the layer assigned to the fifth car from the list of independent variables. If the layer is 9, the car doesn't yield. If it is 12, it will yield.
                        switch (layer)
                        {
                            case 9:
                                Clones[i].layer = LayerMask.NameToLayer("Car"); break;
                            case 12:
                                Clones[i].layer = LayerMask.NameToLayer("YieldingThird"); break;
                        }

                        if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Smart, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));                            
                        }
                        else if (carindex == (1) | carindex == (3) | carindex == (5) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Smart, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));                            
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (1) | nextcarindex == (3) | nextcarindex == (5))   // First Truck, Next Smart
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthSmart)));                            
                        }
                        else if (carindex == (0) | carindex == (2) | carindex == (4) && nextcarindex == (0) | nextcarindex == (2) | nextcarindex == (4))   // First Truck, Next Truck
                        {
                            yield return new WaitUntil(() => Mathf.RoundToInt(Clones[i].transform.position.z) == Mathf.RoundToInt(CarSpawnPosition - (fixed_distance + LengthTruck)));                            
                        }
                        carindex = nextcarindex;
                        break;
                } // End Switch                
            }// End For-loop carspawning
            yield return new WaitUntil(() => Mathf.RoundToInt(LastCar.transform.position.x) == Mathf.RoundToInt(participant.transform.position.x));
            yield return new WaitForSeconds(5);
            

            // Showing in logwindow in the Unity Editor how far the experiment has progressed.
            Debug.Log("Finished testing " + (j + 1) + " out of 18 blocks.");
            trialnumber = trialnumber + 1;
            if (j == 4 || j == 9 || j == 14 || j == 17)
            {
                TrialEnd = true;
                Recording = false;
                Debug.Log("Pause has started, you have 10 seconds before experiment continues.");
                yield return new WaitForSeconds(pauseExperiment);
                Debug.Log("Pause is ending in 10 seconds, please put on VR-glasses to continue experiment.");
                yield return new WaitForSeconds(pauseExperimentStartUp);
            }
        } // End For-Loop Conditions
        Debug.Log("Experiment is finished!");
    } // End of Iteration     

    private void OnGUI()
    {
        if (TrialEnd == true)
        {
            // If five trials have passed, a UI will prompt that pauses the VR environment
            windowRect = GUI.Window(0, windowRect, EndMyTrial, "Trial End");
            paused = true;
        }
    }
    // UI that is prompted. Contains a button to unpause the VR environment
    void EndMyTrial(int windowID)
    {
        if (GUI.Button(new Rect(25, 20, 250, 20), "Please Take Off The Oculus."))
        {
            trialbutton = true;
        }
    }

    // Checks if a pause has been initiated. If so, the time progress is set to 0.
    void CheckPause()
    {
        if (paused == true)
        {
            Time.timeScale = 0.0f;
        }

        if (crossbutton == true | crashbutton == true | trialbutton == true) // If UI button is pressed, the VR environment is unpaused.
        {
            Time.timeScale = 1.0f;
            Accident = false;                                                // Not used
            TrialEnd = false;               
            crashbutton = false;                                             // Not used
            trialbutton = false;
            paused = false;
        }
    }

    // Checks to see if the data obtained in Unity needs to be written to a text file.
    void CheckRecording()
    {
        if (Recording == true)
        {
            WriteToLog();
        }
    }

    // This function specifies which data is written into the text file
    void WriteToLog()
    {
        if (TrialEnd != true | Accident != true | crossbutton != true)                                                   // Writes continuously until the trial has ended
        {
            string path = string.Format("{0}{1}{2}{3}{4}", FilePath, ParticipantNumber, "_", trialnumber, ".txt");       // File name     
            if (File.Exists(path))                                                                                       // This if statement is used when the text file already exists
            {
                using (TextWriter sw = new StreamWriter(path, true))
                {
                    writeIteration++;                                                                                    // Iteration number
                    sw.WriteLine(String.Format("*{0}*", writeIteration));
                    for (int numcars = 0; numcars < Clones.Length; numcars++)                                            // Writes the location and orientation of every moving car present in the VR environment
                    {
                        if (Clones[numcars] != null)
                        {
                            //Define what information you want to extract.
                            info = String.Format("{0};{1};{2};{3}", Clones[numcars].tag, Clones[numcars].name, Clones[numcars].transform.position, Clones[numcars].transform.eulerAngles);

                            // Clean up formatting.
                            info = info.Replace(",", ";");
                            info = info.Replace("(", "[");
                            info = info.Replace(")", "]");

                            data = String.Format("{0};{1}", Time.time, info);
                            sw.WriteLine(data);
                        }
                    }
                }
            }
            else using (StreamWriter sw = File.CreateText(path))                                                        // This else statement is used when the text file doesn't exist. Same contents as the previous 'if' statement 
                {
                    writeIteration++;
                    sw.WriteLine(String.Format("*{0}*", writeIteration));
                    for (int numcars = 0; numcars < Clones.Length; numcars++)
                    {
                        if (Clones[numcars] != null)
                        {
                            //Define what information you want to extract.
                            info = String.Format("{0};{1};{2};{3}", Clones[numcars].tag, Clones[numcars].name, Clones[numcars].transform.position, Clones[numcars].transform.eulerAngles);

                            // Clean up formatting.
                            info = info.Replace(",", ";");
                            info = info.Replace("(", "[");
                            info = info.Replace(")", "]");

                            data = String.Format("{0};{1}", Time.time, info);
                            sw.WriteLine(data);
                        }
                    }              
                }
        }

        if (TrialEnd == true | Accident == true | crossbutton == true)                                                       // Writes one time at the end of the trial. Originally used to store TTC data, but TTC wasn't used in the report.
        {
            string path = string.Format("{0}{1}{2}{3}{4}", FilePath, ParticipantNumber, "_", trialnumber, ".txt");
            //string path = string.Format("{0}{1}{2}{3}{4}", SavedDataPath, ParticipantNumber, "_", trialnumber, ".txt");
            if (File.Exists(path))
            {
                using (TextWriter sw = new StreamWriter(path, true))
                {
                    writeIteration++;
                    sw.WriteLine(String.Format("*{0}*", writeIteration));
                    for (int numcars = 0; numcars < Clones.Length; numcars++)
                    {
                        if (Clones[numcars] != null)
                        {
                            //Define what information you want to extract.
                            info = String.Format("{0};{1};{2};{3}", Clones[numcars].tag, Clones[numcars].name, Clones[numcars].transform.position, Clones[numcars].transform.eulerAngles);

                            // Clean up formatting.
                            info = info.Replace(",", ";");
                            info = info.Replace("(", "[");
                            info = info.Replace(")", "]");

                            data = String.Format("{0};{1}", Time.time, info);
                            sw.WriteLine(data);
                        }
                    }                   
                }
            }
            else using (StreamWriter sw = File.CreateText(path))
                {
                    writeIteration++;
                    sw.WriteLine(String.Format("*{0}*", writeIteration));
                    for (int numcars = 0; numcars < Clones.Length; numcars++)
                    {
                        if (Clones[numcars] != null)
                        {
                            //Define what information you want to extract.
                            info = String.Format("{0};{1};{2};{3}", Clones[numcars].tag, Clones[numcars].name, Clones[numcars].transform.position, Clones[numcars].transform.eulerAngles);

                            // Clean up formatting.
                            info = info.Replace(",", ";");
                            info = info.Replace("(", "[");
                            info = info.Replace(")", "]");

                            data = String.Format("{0};{1}", Time.time, info);
                            sw.WriteLine(data);
                        }
                    }                    
                }
        }
    }
}

