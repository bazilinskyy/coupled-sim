/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class WaypointNavigator : MonoBehaviour
{
    characterNavigationController controller;
    public Waypoint currentWaypoint;
    int direction;


    private void Awake()
    {
        controller = GetComponent<characterNavigationController>();
    }

    void Start()
    {
        direction = Mathf.RoundToInt(Random.Range(0f, 1f));
        controller.SetDestination(currentWaypoint.GetPosition());
        transform.rotation = currentWaypoint.transform.rotation;

        if (direction == 0) 
        { 
            transform.forward = -currentWaypoint.transform.forward;
        }
        else if (direction == 1)
        {
            transform.forward = currentWaypoint.transform.forward;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (controller.reachedDestination)
        {
            bool shouldBranch = false;
            if (currentWaypoint.branches != null && currentWaypoint.branches.Count > 0)
            {
                shouldBranch = Random.Range(0f, 1f) <= currentWaypoint.branchRatio ? true : false;
            }

            if (shouldBranch)
            {
                currentWaypoint = currentWaypoint.branches[Random.Range(0, currentWaypoint.branches.Count - 1)];
            }
            else
            {
                if (direction == 0)
                {
                    if(currentWaypoint.nextWaypoint!= null) 
                    {
                        currentWaypoint = currentWaypoint.nextWaypoint;
                    }
                    else
                    {
                        currentWaypoint = currentWaypoint.previousWaypoint;
                        direction = 1;
                    }

                    

                }

                else if (direction == 1)
                {
                    if (currentWaypoint.previousWaypoint != null)
                    {
                        currentWaypoint = currentWaypoint.previousWaypoint;
                    }
                    else
                    {
                        currentWaypoint = currentWaypoint.nextWaypoint;
                        direction = 0;
                    }
                    
                }
            }
           
        controller.SetDestination(currentWaypoint.GetPosition());
        }
            
    }
}
*/