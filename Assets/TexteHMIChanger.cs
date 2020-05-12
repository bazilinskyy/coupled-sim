using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TexteHMIChanger : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField]
    SpriteRenderer _renderer;
    [SerializeField]
    Sprite stop;
    [SerializeField]
    Sprite walk;
    [SerializeField]
    Sprite disabled;

    SpriteRenderer rend;

    public static int HMI_On;
    public static int Yielding;


    void Start()
    {
        rend = GetComponent<SpriteRenderer>();
        if (HMI_On == 1)
        {
            rend.sprite = disabled;

            if (Yielding == 1)
            {
                StartCoroutine(Go(16.14f));
                StartCoroutine(Yield(8.64f));
                StartCoroutine(Disable(21.78f));
            }
            else
            {
                StartCoroutine(Go(8.64f));
                StartCoroutine(Disable(11.78f));
            }
        }
    }

   
    IEnumerator Go(float time)
    {
        yield return new WaitForSeconds(time);

        rend.sprite = stop;
    }

    IEnumerator Yield(float time)
    {
        yield return new WaitForSeconds(time);

        rend.sprite = walk;
    }

    IEnumerator Disable(float time)
    {
        yield return new WaitForSeconds(time);
        rend.sprite = disabled;

    }
}
