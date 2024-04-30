using UnityEngine;


[ExecuteAlways]
public class GizmoDrawer_WireSphere : MonoBehaviour
{
    [SerializeField] private float m_radius = 0.25f;
    [SerializeField] private Color32 m_color = Color.red;


    private void Start()
    {
        m_color = Random.ColorHSV();
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = m_color;
        Gizmos.DrawWireSphere(transform.position, m_radius);
    }
}