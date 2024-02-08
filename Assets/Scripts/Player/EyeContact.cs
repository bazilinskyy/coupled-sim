using UnityEngine;
using UnityEngine.Serialization;


public class EyeContact : MonoBehaviour
{
    public GameObject[] Peds;

    public Rigidbody CarRigidbody;
    public AICar aiCar;
    public Transform PlayerHead;
    public float MaxTrackingDistance; //this distance is wrt to car
    public float MinTrackingDistance; //this distance is wrt to car
    public float MaxHeadRotation; //this is wrt drivers head
    [FormerlySerializedAs("EnableTracking")]
    public bool Tracking;
    [HideInInspector]
    public bool TrackingWhileYielding;

    private float targetAngle;
    private float headPosition => CarRigidbody.transform.InverseTransformPoint(PlayerHead.position).z;
    private float maxTrackingDistanceHead => MaxTrackingDistance - headPosition; //this distance is wrt to drivers head
    private float minTrackingDistanceHead => MinTrackingDistance - headPosition; //this distance is wrt to drivers head

    public Transform TargetPed { get; private set; }


    private void Start()
    {
        Peds = GameObject.FindGameObjectsWithTag("Pedestrian");
        aiCar = CarRigidbody.GetComponent<AICar>();
    }


    private void FixedUpdate()
    {
        var yielding = aiCar.state == AICar.CarState.STOPPED;
        var eyeContactTracking = yielding ? TrackingWhileYielding : Tracking;

        var minDist = float.MaxValue;

        if (eyeContactTracking)
        {
            foreach (var ped in Peds)
            {
                var distance = FlatDistance(PlayerHead.position, ped.transform.position);

                if (minDist > distance)
                {
                    minDist = distance;
                    TargetPed = ped.transform;
                }

                Debug.Log("Car to pedestrian flat distance " + FlatDistance(CarRigidbody.transform.position, ped.transform.position));
            }
        }
        else
        {
            TargetPed = null;
        }

        if ((minDist > maxTrackingDistanceHead || minDist < minTrackingDistanceHead) && !yielding)
        {
            TargetPed = null;
        }

        if (TargetPed != null)
        {
            var sourcePos = PlayerHead.position;
            var targetPos = TargetPed.transform.position;
            var direction = targetPos - sourcePos;
            direction = new Vector3(direction.x, 0.0f, direction.z);

            var targetRay = new Ray(sourcePos, direction);
            targetAngle = Vector3.Angle(transform.forward, targetRay.direction);

            if (targetAngle > MaxHeadRotation)
            {
                TargetPed = null;
            }
        }
    }


    private float FlatDistance(Vector3 pos1, Vector3 pos2)
    {
        var diffVector = pos1 - pos2;
        diffVector.y = 0;
        diffVector.z = 0;
        var distance = diffVector.magnitude;

        return distance;
    }
}