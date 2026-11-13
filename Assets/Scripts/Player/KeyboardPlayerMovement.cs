using UnityEngine;

/// <summary>
/// Keyboard and mouse control of the participant's pedestrian (screen mode, without a motion
/// suit): W/S/A/D or arrows walk, Shift runs, Q/E turn, and holding the right mouse button looks
/// around (mouse left/right turns, up/down tilts the head). The body plays the walk animation at
/// the walking speed.
/// </summary>
public class KeyboardPlayerMovement : MonoBehaviour
{
    Rigidbody _rb;
    [SerializeField]
    [Tooltip("Walking speed (m/s).")]
    float _speed = 1.4f;
    [SerializeField]
    [Tooltip("Running speed with Shift (m/s).")]
    float _runSpeed = 3.5f;
    [SerializeField]
    [Tooltip("Turning speed with Q/E (degrees/s).")]
    float _rotateSpeed = 100;
    [Tooltip("Degrees per mouse step.")]
    public float MouseSensitivity = 2f;
    [Tooltip("Look around only while the right mouse button is held (else the mouse always turns the view).")]
    public bool LookWithRightMouseButton = true;
    [Tooltip("Walk animation for the body (the AI pedestrians' controller with a Speed blend), used when the avatar has none.")]
    public RuntimeAnimatorController WalkAnimation;

    Animator _animator;
    Transform _camera;
    float _pitch, _yawInput, _walkClipSpeed = 1.4f;
    Vector3 _velocity;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null && _animator.runtimeAnimatorController == null && WalkAnimation != null)
        {
            _animator.runtimeAnimatorController = WalkAnimation;
            _animator.applyRootMotion = false;
            foreach (var clip in WalkAnimation.animationClips)
                if (clip.name.ToLower().Contains("walk") && clip.averageSpeed.magnitude > 0.3f)
                    _walkClipSpeed = clip.averageSpeed.magnitude * (_animator.isHuman ? _animator.humanScale : 1f);
        }
        var cam = GetComponentInChildren<Camera>(true);
        _camera = cam != null ? cam.transform : null;
    }

    void Update()
    {
        if (!LookWithRightMouseButton || Input.GetMouseButton(1))
        {
            _yawInput += Input.GetAxis("Mouse X") * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * MouseSensitivity, -70f, 70f);
        }
    }

    void LateUpdate()
    {
        if (_camera != null && _camera.gameObject.activeInHierarchy)
        {
            var e = _camera.localEulerAngles;
            _camera.localRotation = Quaternion.Euler(_pitch, e.y, e.z);
        }
    }

    void FixedUpdate()
    {
        var input = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.z += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.z -= 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1;
        bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        // walking backwards and sideways is slower
        var target = Vector3.ClampMagnitude(input, 1f) * (run ? _runSpeed : _speed);
        if (target.z < 0f) target.z *= 0.6f;
        target.x *= 0.8f;
        // people speed up and slow down within about half a second
        _velocity = Vector3.MoveTowards(_velocity, target, 4f * Time.fixedDeltaTime);

        float rot = 0;
        if (Input.GetKey(KeyCode.Q)) rot -= _rotateSpeed;
        else if (Input.GetKey(KeyCode.E)) rot += _rotateSpeed;
        float yaw = rot * Time.fixedDeltaTime + _yawInput;
        _yawInput = 0f;
        _rb.MovePosition(_rb.position + _rb.rotation * _velocity * Time.fixedDeltaTime);
        _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0, yaw, 0));

        if (_animator != null && _animator.runtimeAnimatorController == WalkAnimation && WalkAnimation != null)
        {
            float speed = _velocity.magnitude;
            _animator.SetFloat("Speed", Mathf.Clamp01(speed / _walkClipSpeed));
            _animator.speed = speed > _walkClipSpeed ? Mathf.Min(2.5f, speed / _walkClipSpeed) : 1f;
        }
    }
}
