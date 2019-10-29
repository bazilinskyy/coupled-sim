using UnityEngine;
using UnityEngine.UI;
using System;

// Initially this script was ment to obtain data from the participants performance (i.e., successful crossings without any crashes etc.) 
// This script was never used in the end.
public class PlayerController : MonoBehaviour
{

    public float speed;
    public Text countText;
    public Text winText;
    public GameObject HalfwayCrossing;
    public GameObject Marker;
    private Rigidbody rb;
    private float count;

    

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        count = 0;
        SetCountText();
        winText.text = "";
    }

    void FixedUpdate()
    {
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);

        rb.AddForce(movement * speed);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("HalfwayCrossing"))
        {
            count = count + 1f;
            SetCountText();
        }
        else if (other.gameObject.CompareTag("Car"))
        {
            count = 0;
            SetCountText();
        }
     }

    void SetCountText()
    {
        countText.text = "Count: " + count.ToString();
    }


}