using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class characterNavigationController : MonoBehaviour
{

    public Vector3 destination;
    public bool reachedDestination;
    public float movementSpeed = 1;
    public float rotationSpeed = 10;
    public float stopDistance = .1f;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update() {
        if (transform.position != destination)
        {
            Vector3 destinationDirection = destination - transform.position;
            destinationDirection.y = 0;
            float desintationDistance = destinationDirection.magnitude;

            if (desintationDistance >= stopDistance)
            {
                reachedDestination = false;
                Quaternion targetRotation = Quaternion.LookRotation(destinationDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                transform.Translate(Vector3.forward * movementSpeed * Time.deltaTime);
            }
            else
            {
                reachedDestination = true;
            }

            //velocity = (transform.position - lastPosition) / Time.deltaTime;
        }

    }
    public void SetDestination(Vector3 destination)
    {
        this.destination = destination;
        reachedDestination = false;
    }
}
