using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Target : MonoBehaviour
{

    public Waypoint waypoint;
    public TargetDifficulty setDifficulty = TargetDifficulty.easy_6;

    public Material easy_6;
    public Material easy_5;
    public Material medium_4;
    public Material medium_3;
    public Material hard_2;
    public Material hard_1;


    public int ID;
    public bool detected = false;

    //Time at which this target was visible
    public float defaultVisibilityTime = -1f;
    public float startTimeVisible = -1f;


    // Start is called before the first frame update
    void Start()
    {
        SetUnDetected();
        startTimeVisible = -1f;
    }
    public void SetDifficulty( TargetDifficulty difficulty)
    {
        //Default
        Material material = easy_6;
        //Adjust the setDiofficulty attribute
        setDifficulty = difficulty;

        //Get appropriate material
        if (difficulty == TargetDifficulty.easy_6) { material = easy_6; }
        else if (difficulty == TargetDifficulty.easy_5) { material = easy_5; }
        else if(difficulty == TargetDifficulty.medium_4) { material = medium_4; }
        else if (difficulty == TargetDifficulty.medium_3) { material = medium_3; }
        else if(difficulty == TargetDifficulty.hard_2) { material = hard_2; }
        else if (difficulty == TargetDifficulty.hard_1) { material = hard_1; }

        GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    public void SetDetected()
    {
        detected = true;
        transform.GetComponent<MeshRenderer>().enabled = false;
    }

    public void SetUnDetected()
    {
        detected = false;
    }

    public bool IsDetected()
    {
        return detected;
    }
}
