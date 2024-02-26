using UnityEngine;


[RequireComponent(typeof(Obey))]
[ExecuteAlways]
public class ObeyGizmoDrawer : MonoBehaviour
{
    private Obey _obey;


    private void Awake()
    {
        _obey = GetComponent<Obey>();
    }


    private void OnDrawGizmos()
    {
        DrawObeyingGizmo();
    }


    private void DrawObeyingGizmo()
    {
        var newPosition = transform.position;
        newPosition.y += 2.5f;

        if (_obey.ObeyTrafficLight)
        {
            Gizmos.DrawIcon(newPosition, "tick.png", false);
        }
        else if (!_obey.ObeyTrafficLight)
        {
            Gizmos.DrawIcon(newPosition, "exclamation_mark.png", false);
        }
    }
}