using UnityEngine;


[RequireComponent(typeof(OnTriggerEnterEvent))]
[ExecuteAlways]
public class GizmoDrawer_OnTriggerEnterEvent : MonoBehaviour
{
    private OnTriggerEnterEvent _onTriggerEnterEvent;


    private void Awake()
    {
        _onTriggerEnterEvent = GetComponent<OnTriggerEnterEvent>();
    }


    private void OnDrawGizmos()
    {
        DrawObeyingGizmo();
    }


    private void DrawObeyingGizmo()
    {
        var newPosition = transform.position;
        newPosition.y += 2.5f;

        if (_onTriggerEnterEvent.EventToFire.GetPersistentEventCount() > 0)
        {
            Gizmos.DrawIcon(newPosition, "event_red.png", false);
        }
    }
}