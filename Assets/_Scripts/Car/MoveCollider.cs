using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCollider : MonoBehaviour
{
    public void LeftArrow() { GetComponent<BoxCollider>().center.Set(2.5f,0,0); }
    public void RightArrow() { GetComponent<BoxCollider>().center.Set(-2.5f, 0, 0); }
    public void CenterArrow() { GetComponent<BoxCollider>().center.Set(0, 0, 0); }

}
