using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;

// high level client networking script
// - handles messages recieved from the host 
// - displays client GUI
// - sends local player position updates
public class CheckID : MonoBehaviour
{
    public GameObject myObject;
    public int netID;

    public void SetMyObject(GameObject obj)
    {
        CmdSetMyObject(obj.GetComponent<NetworkIdentity>().netId);
    }

    public void CmdSetMyObject(NetworkInstanceId objectId)
    {
        myObject = NetworkServer.FindLocalObject(objectId);
        RpcSetMyObject(objectId);
        netID = int.Parse(objectId.ToString());
    }

    public void RpcSetMyObject(NetworkInstanceId objectId)
    {
        myObject = ClientScene.FindLocalObject(objectId);
    }
}