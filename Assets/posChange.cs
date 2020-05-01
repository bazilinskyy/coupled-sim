using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class posChange : MonoBehaviour
{
    public float y;
    float Timestep;
    float steps;
    float deltaY;
    float y_count;
    public static int HMI_On;
    public static int Yielding;
    int special;



    // Start is called before the first frame update
    void Start()
    {

        y = 0.15f;
        Timestep = 0.5f;
        y_count = 0f;

        Vector3 newPosition = transform.position; // We store the current position
        newPosition.y = newPosition.y + 0.13f; // We set an axis, in this case the y axis
        transform.position = newPosition; // We pass it back

        if (HMI_On == 1)
        {
            if (Yielding == 1)
            {
                StartCoroutine(GoTurbo(15.78f));
                StartCoroutine(Yield(8.28f));
                StartCoroutine(Disable(21.28f));
            }
            else
            {
                StartCoroutine(Go(8.28f));
                StartCoroutine(Disable(11.78f));
            }
        }

    }

    IEnumerator Go(float time)
    {
        yield return new WaitForSeconds(time);
        steps = Timestep / Time.deltaTime;
        deltaY = y / steps;

    }

    IEnumerator GoTurbo(float time)
    {
        yield return new WaitForSeconds(time);
        steps = Timestep / Time.deltaTime;
        deltaY = y / steps;

        special = 1;
    }

    IEnumerator Yield(float time)
    {
        yield return new WaitForSeconds(time);
        steps = Timestep / Time.deltaTime;
        deltaY = -y / steps;

        
    }

    IEnumerator Disable(float time)
    {
        yield return new WaitForSeconds(time);
        steps = Timestep / Time.deltaTime;
        deltaY = -y / steps;

       
    }

    // Update is called once per frame
    void Update()
    {
        if ((deltaY != 0))
        {
            Vector3 newPosition = transform.position; // We store the current position

            if (special == 1)
            {
                newPosition.y = newPosition.y + 2*deltaY; // We set a axis, in this case the y axis
            }

            else
            {
                newPosition.y = newPosition.y + deltaY; // We set a axis, in this case the y axis
            }
            transform.position = newPosition; // We pass it back
                                                  // transform.position += new Vector3(1 * Time.deltaTime, 0, 0);

                y_count = y_count + deltaY;

                if (Mathf.Abs(y_count) >= y)
                {
                    y_count = 0;
                    deltaY = 0;
                    special = 0;
                }
        }

        
    }
}
//    {
//        if (HMI_On == 1)
//        {
//            if ((Input.GetKeyDown(KeyCode.Alpha4)))
//            {

//                steps = Timestep / Time.deltaTime;
//                deltaY = y / steps;

//            }

//            if ((Input.GetKeyDown(KeyCode.Alpha5)))
//            {
//                steps = Timestep / Time.deltaTime;
//                deltaY = -y / steps;
//            }

//            if (deltaY != 0)
//            {
//                Vector3 newPosition = transform.position; // We store the current position
//                newPosition.y = newPosition.y + deltaY; // We set a axis, in this case the y axis

//                transform.position = newPosition; // We pass it back
//                                                  // transform.position += new Vector3(1 * Time.deltaTime, 0, 0);

//                y_count = y_count + deltaY;

//                if (Mathf.Abs(y_count) >= y)
//                {
//                    y_count = 0;
//                    deltaY = 0;
//                }
//            }
//        }
//    }
//}
