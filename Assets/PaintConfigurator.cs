using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaintConfigurator : MonoBehaviour
{
    public MeshRenderer [] meshRenderers;

    public void ChangePaint(Color color)
    {
        foreach (var meshRenderer in meshRenderers) {
            meshRenderer.material.color = color;
        }
    }
}
