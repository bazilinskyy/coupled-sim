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


    // Start is called before the first frame update
    void Start()
    {
        this.SetUnDetected();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

/*    private void OnBecameInvisible()
    {
        this.gameObject.GetComponent<MeshRenderer>().enabled = false;
    }

    private void OnBecameVisible()
    {
        this.gameObject.GetComponent<MeshRenderer>().enabled = true;
    }
*/
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
        
        this.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    public void SetDetected()
    {
        this.detected = true;
    }

    public void SetUnDetected()
    {
        this.detected = false;
    }

    public bool IsDetected()
    {
        return this.detected;
    }
}
