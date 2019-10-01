using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PimpZ4 : MonoBehaviour, IPimpColor {
    public float R=0;
    public float G=0;
    public float B=0;
    public string[] bodyParts;
    private GameObject[] gameobjects;
    private Color[] colors = new Color[5] { Color.black, Color.white, Color.gray, Color.blue, Color.red };
    //private float[] colors_probabilities = new float[5] { 0.25f, 0.25f, 0.25f, 0.20f, 0.05f };
   // private float[] colors_probabilities = new float[5] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };
    private void initialize()
    {
        bodyParts = new string[21];
        bodyParts[0] = "z4/body 14";
        bodyParts[1] = "z4/body 10";
        bodyParts[2] = "z4/body 11";
        bodyParts[3] = "z4/door1/body07";
        bodyParts[4] = "z4/Object04";
        bodyParts[5] = "z4/body 8";
        bodyParts[6] = "z4/door1/body06";
        bodyParts[7] = "z4/body";
        bodyParts[8] = "z4/body 12";
        bodyParts[9] = "z4/body 13";
        bodyParts[10] = "z4/door1/body05";
        bodyParts[11] = "z4/Group04/body 17";
        bodyParts[12] = "z4/body 1";
        bodyParts[13] = "z4/body 15";
        bodyParts[14] = "z4/body03/body 18";
        bodyParts[15] = "z4/body 2";
        bodyParts[16] = "z4/body 16";
        bodyParts[17] = "z4/body 16";
        bodyParts[18] = "z4/body 3";
        bodyParts[19] = "z4/body 6";
        bodyParts[20] = "z4/body 4";

        //Update the references
        gameobjects = new GameObject[bodyParts.Length];

        for (int i = 0; i < bodyParts.Length; i++)
        {
            Transform t = transform.Find(bodyParts[i]);
            if (t != null)
            {
                gameobjects[i] = t.gameObject;
                //gameobjects[i].GetComponent<MeshRenderer>().material = new Material(gameobjects[i].GetComponent<MeshRenderer>().material);
            }
            else
            {
                //TODO
                Debug.LogWarning("Could not find " + bodyParts[i]);
                gameobjects[i] = null;
            }
        }
    }
	// Use this for initialization
	void Start () {
        Debug.Log("Start");
        applyColor(new Color(R,G,B));
	}
    void IPimpColor.setColor(Color color)
    {
        applyColor(color);
    }

    public void setColorLocally()
    {
        int value = (int) Mathf.Ceil(Random.Range(1f, 100f));
        int index = 0;
        if(value <= 20)//25) //Black
        {
            index = 0;
        } else if(value<=40)//50) //White
        {
            index = 1;
        } else if (value <=60)//75) //Gray
        {
            index = 2;
        } else if (value <= 80)//95) //Blue
        {
            index = 3;
        }
        else //Red
        {
            index = 4;
        }
        Debug.Log("Selected value="+value+" and index="+index+" name="+colors[index].ToString());
        applyColor(colors[index]);
    }

    private void applyColor(Color color)
    {
        initialize();
        if (gameobjects != null)
        {
            for (int i = 0; i < gameobjects.Length; i++)
            {
                gameobjects[i].GetComponent<MeshRenderer>().material = new Material(gameobjects[i].GetComponent<MeshRenderer>().material);
                gameobjects[i].GetComponent<MeshRenderer>().material.color = color;
            }
            R = color.r;
            G = color.g;
            B = color.b;
        }
        else
        {
            Debug.LogWarning("Not possible to change the color");
        }
        
        
    }
}
