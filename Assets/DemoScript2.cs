using UnityEngine;

public class DemoScript2 : MonoBehaviour
{
    // The object whose rotation we want to match.
    Transform target;

    // Angular speed in degrees per sec.
    float speed = 5;

    void Update()
    {
        // The step size is equal to speed times frame time.
        var step = speed * Time.deltaTime;

        // Rotate our transform a step closer to the target's.
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target.rotation, step);
    }
}
