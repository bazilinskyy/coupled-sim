using UnityEngine;


public class RotateWithGaze : MonoBehaviour
{
    public GameObject demoObject;
    public float forceAmount;
    public Material flatMaterial;
    public Material objectMaterial;
    private MeshRenderer mr;
    private Rigidbody rb;


    private void Start()
    {
        rb = GetComponentInChildren<Rigidbody>();
        mr = demoObject.GetComponent<MeshRenderer>();
    }


    private void Update()
    {
        // Check if object is rotating and change material accordingly
        if (!rb.IsSleeping())
        {
            mr.material = objectMaterial;
        }
        else
        {
            mr.material = flatMaterial;
        }
    }


    // Rotates object hit with gaze tracking raycast
    public void RayHit()
    {
        rb.AddTorque(Vector3.up * forceAmount * Time.deltaTime, ForceMode.Force);
    }
}