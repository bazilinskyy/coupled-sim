using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class SyncObject : MonoBehaviour
{
    public int NetInstanceId;
    public Action<SyncObject, SyncObject> OnCollision;
}
