using UnityEngine;


[ExecuteAlways]
public class GizmoDrawer_Cube : MonoBehaviour
{
    [SerializeField] private Vector3 m_size = new(0.25f, 0.25f, 0.25f);
    [SerializeField] private Color32 m_color = Color.blue;


    private void Start()
    {
        m_color = Random.ColorHSV();
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = m_color;
        Gizmos.DrawCube(transform.position, m_size);
    }
}