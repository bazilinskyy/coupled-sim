using UnityEngine;


public class RotateWithGaze : MonoBehaviour
{
    [SerializeField] private GameObject m_gameObjectToRotate;
    [SerializeField] private float m_forceAmount;
    [SerializeField] private Material m_flatMaterial;
    [SerializeField] private Material m_objectMaterial;

    private MeshRenderer _meshRenderer;
    private Rigidbody _rigidbody;


    private void Awake()
    {
        _rigidbody = GetComponentInChildren<Rigidbody>();
        _meshRenderer = m_gameObjectToRotate.GetComponent<MeshRenderer>();
    }


    private void Update()
    {
        ChangeRotatingMaterial();
    }


    private void ChangeRotatingMaterial()
    {
        if (!_rigidbody.IsSleeping())
        {
            _meshRenderer.material = m_objectMaterial;
        }
        else
        {
            _meshRenderer.material = m_flatMaterial;
        }
    }


    /// <summary>
    ///     Rotates object hit with gaze tracking raycast
    /// </summary>
    public void RayHit()
    {
        _rigidbody.AddTorque(Vector3.up * (m_forceAmount * Time.deltaTime), ForceMode.Force);
    }
}