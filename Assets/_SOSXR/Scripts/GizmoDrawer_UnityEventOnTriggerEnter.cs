using UnityEngine;


[RequireComponent(typeof(UnityEventOnTriggerEnter))]
[ExecuteAlways]
public class GizmoDrawer_UnityEventOnTriggerEnter : MonoBehaviour
{
    private UnityEventOnTriggerEnter _onTriggerEnterEvent;


    private void Awake()
    {
        _onTriggerEnterEvent = GetComponent<UnityEventOnTriggerEnter>();
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