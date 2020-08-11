using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderOtherRoadUsers : MonoBehaviour
{
    //Render distance of other road-users
    public bool renderPedestrians = true;
    public bool renderVehicles = true;
    public int renderDistanceOthers = 50;
    
    public bool showGizmo =true;

    private GameObject[] pedestrianList;
    private GameObject[] vehicleList;

    // Start is called before the first frame update
    void Start()
    {
        GetGameObjectsOther();
    }

    // Update is called once per frame
    void Update()
    {
        RenderOthers();
    }


    private void OnDrawGizmos()
    {
        if (showGizmo)
        {
            Color color = Color.yellow; color.a = .2f;
            Gizmos.color = color;
            Gizmos.DrawSphere(transform.position, renderDistanceOthers);
        }
    }

    
    void GetGameObjectsOther()
    {
        pedestrianList = GameObject.FindGameObjectsWithTag("Pedestrians");
        vehicleList = GameObject.FindGameObjectsWithTag("Vehicles");

        Debug.Log($"Found {pedestrianList.Length} pedestrians");
        Debug.Log($"Found {vehicleList.Length} vehicles");
    }
    void RenderOthers()
    {

        if (renderPedestrians)
        {
            foreach (GameObject obj in pedestrianList)
            {
                float distance = Vector3.Magnitude(obj.transform.position - this.transform.position);

                //Too far -->  skip
                if (distance >= renderDistanceOthers * 1.1) { continue; }

                bool inFront = InFrontOfCar(obj.transform.position);


                string pedestrianName = obj.name.Split(' ')[0];
                Transform geo = obj.transform.Find("Geometry");
                Transform child = geo.Find(pedestrianName);

                if (inFront && distance <= renderDistanceOthers)
                {
                    child.gameObject.SetActive(true);
                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            };
        }
        if (renderVehicles)
        {
            RenderObjects(vehicleList);
        }
    }

    private bool InFrontOfCar(Vector3 pos)
    {
        bool inFront;

        // plane equation is A(x-a) + B(y-b) + C(z-c) = 0
        // Where normal vector = <A,B,Z>
        // pos = (x,y,z,)
        // a point on the plane Q= (a,b,c)

        //We start from a little behind the car to make sure we dont see anything when heads are turned
        int metersBehindCar = 5;
        Vector3 planePosition = transform.position - transform.forward * metersBehindCar;
        float sign = Vector3.Dot(transform.forward, (pos - planePosition));
        if (sign >= 0) { inFront = true; }
        else { inFront = false; }

        return inFront;
    }

    void RenderObjects(GameObject[] objectList)
    {
        foreach (GameObject obj in objectList)
        {
            float distance = Vector3.Magnitude(obj.transform.position - this.transform.position);
            if (distance <= renderDistanceOthers) { obj.SetActive(true); }
            else { obj.SetActive(false); }
        }
    }
}
