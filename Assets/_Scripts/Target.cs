using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Target : MonoBehaviour
{

    public Waypoint waypoint;
    public TargetDifficulty setDifficulty = TargetDifficulty.easy;

    public Material easy;
    public Material medium;
    public Material hard;

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
        Material material = easy;
        //Adjust the setDiofficulty attribute
        setDifficulty = difficulty;

        //Get appropriate material
        if (difficulty == TargetDifficulty.easy) { material = easy; }
        else if(difficulty == TargetDifficulty.medium) { material = medium; }
        else if(difficulty == TargetDifficulty.hard) { material = hard; }
        
        GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    public void SetDetected()
    {
        detected = true;
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
