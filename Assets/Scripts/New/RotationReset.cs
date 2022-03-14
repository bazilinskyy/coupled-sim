using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationReset : MonoBehaviour
{
    private float timeValue = 0;
    private Quaternion Anchor_xy;

    void Update()
    {
        timeValue += Time.deltaTime;
        if (timeValue > 0.0f)
        {
            Anchor_xy = Quaternion.Euler(new Vector3(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 0f));
            transform.rotation = Anchor_xy;
        }
    }
}
