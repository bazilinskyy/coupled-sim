using UnityEngine;


//debug keyboard controller for pedestrian role
public class KeyboardPlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
    [SerializeField] private float _rotateSpeed = 50;
    private Rigidbody _rb;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }


    private void FixedUpdate()
    {
        var move = default(Vector3);

        if (Input.GetKey(KeyCode.W))
        {
            move.z += _speed;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            move.z -= _speed;
        }

        if (Input.GetKey(KeyCode.A))
        {
            move.x -= _speed;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            move.x += _speed;
        }

        float rot = 0;

        if (Input.GetKey(KeyCode.Q))
        {
            rot -= _rotateSpeed;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            rot += _rotateSpeed;
        }

        _rb.MovePosition(_rb.position + _rb.rotation * move * Time.fixedDeltaTime);
        _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0, rot * Time.fixedDeltaTime, 0));
    }
}