using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class colorChange : HMI
{
 
    IEnumerator Go(float time)
    {
        yield return new WaitForSeconds(time);

        { rend.material.SetColor("_Color", Color.yellow); }
        { rend.material.mainTexture = texture; }
    }

    IEnumerator Yield(float time)
    {
        yield return new WaitForSeconds(time);

        { rend.material.SetColor("_Color", Color.white); }
        { rend.material.mainTexture = textureYield; }
    }

    IEnumerator Disable(float time)
    {
        yield return new WaitForSeconds(time);

        // { rend.material.SetColor("_Color", Color.red); }
        // rend.material.mainTexture = noTexture;
        rend.material.mainTexture = null;
        rend.material.color = originalColor;

    }

    Renderer rend;
    Texture2D texture;
    Texture2D textureYield;
    Texture2D noTexture;
    private Color originalColor;

    public static int HMI_On;

    public static int Yielding;



    // private HMI HMI;

    void Start()
    {


        rend = GetComponent<Renderer>();
        //texture = (Texture2D)Resources.Load("eHMI_yellow");
        //textureYield = (Texture2D)Resources.Load("eHMI_white");

        texture = (Texture2D)Resources.Load("Nice_pattern_yellow");
        textureYield = (Texture2D)Resources.Load("Nice_pattern_white");

        noTexture = (Texture2D)Resources.Load("noTexture");
        originalColor = rend.material.color;

        //  StartCoroutine(ExecuteAfterTime(5, textureYield));
        if (HMI_On == 1)
        {
            if (Yielding == 1)
            {
                StartCoroutine(Go(16.14f));
                StartCoroutine(Yield(8.64f));
                StartCoroutine(Disable(21.28f));
            }
            else
            {
                StartCoroutine(Go(8.64f));
                StartCoroutine(Disable(11.78f));
            }
        }
    }



    // Start is called before the first frame update
    //void Start()
    //{
    //    rend = GetComponent<Renderer>();
    //    texture = (Texture2D)Resources.Load("eHMI_yellow");
    //    textureYield = (Texture2D)Resources.Load("eHMI_white");
    //   StartCoroutine(ExecuteAfterTime(5));
    //}

    // Update is called once per frame


    // public override void Display(HMIState state)
    
    
    //void Update()
    //{
    //    if (HMI_On == 1)
    //    {
    //        if (Input.GetKeyDown(KeyCode.Alpha1))
    //        // ( rend.material.SetColor("_Color", Color.yellow); 
    //        // rend.material.mainTexture = texture; );
    //        {
    //            StartCoroutine(Go(0));
    //        }

    //        else if (Input.GetKeyDown(KeyCode.Alpha2))
    //        // ( rend.material.SetColor("_Color", Color.white); 
    //        //  rend.material.mainTexture = textureYield; );
    //        {
    //            StartCoroutine(Yield(0));
    //        }

    //        else if (Input.GetKeyDown(KeyCode.Alpha3))
    //        // ( rend.material.SetColor("_Color", Color.red); 
    //        //  rend.material.mainTexture = noTexture; );
    //        {
    //            StartCoroutine(Disable(0));
    //        }
    //    }
    //    //{
    //    //     base.Display(state);
    //    //  switch (state)
    //    //{
    //    //     case HMIState.STOP:
    //    //StartCoroutine(ExecuteAfterTime(0, texture));
    //    //         break;
    //    //  case HMIState.WALK:
    //    //StartCoroutine(ExecuteAfterTime(0, textureYield));
    //    //         break;
    //    //  default:
    //    //{ rend.material.SetColor("_Color", Color.red); }
    //    //{ rend.material.mainTexture = textureYield; }
    //    //         break;
    //    //}

    //}


    //// if (Input.GetButtonUp("Fire1"))
    //// { rend.material.SetColor("_Color", Color.yellow); }

    ////t += Time.deltaTime / Duration;

    ////if (t == Duration)
    ////{
    ////    rend.material.SetColor("_Color", Color.yellow);
    ////}

    //// public override void Display(HMIState state)
}



