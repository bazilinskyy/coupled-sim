using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraPositioner : MonoBehaviour
{
    public Transform headPosition;
    public Transform waitingRoom;

    private Transform parent;
    private Vector3 correction;
    private void Awake()
    {
        parent = transform.parent;
    }
    public void SetCameraPosition(CameraPosition camPos, MyCameraType camType)
    {
        //if (correction == Vector3.zero) { correction = parent.position - transform.position; }
        Debug.Log($"Moving camera to {camPos} with correction: {correction.ToString()}...");

        //Set appropraite parent
        if (camPos == CameraPosition.Car) { transform.SetParent(parent); }
        if (camPos == CameraPosition.WaitingRoom) { transform.SetParent(waitingRoom); }

        

        //SetAnimatorParam position and rotation
        if (camType == MyCameraType.Normal) 
        {
            if(camPos == CameraPosition.Car) { transform.position = headPosition.position + correction; transform.rotation = headPosition.rotation; }
            if (camPos == CameraPosition.WaitingRoom) { transform.position = waitingRoom.position + correction; transform.rotation = waitingRoom.rotation; }
        }
        if (camType == MyCameraType.Varjo)
        {
            
            if (camPos == CameraPosition.Car) { transform.position = headPosition.position + correction; transform.rotation = headPosition.rotation; }
            if (camPos == CameraPosition.WaitingRoom) { transform.position = waitingRoom.position + correction; transform.rotation = waitingRoom.rotation; }
        }
    }
}

public enum CameraPosition
{
    Car,
    WaitingRoom,
}
public enum MyCameraType
{
    Normal,
    Varjo,
}
