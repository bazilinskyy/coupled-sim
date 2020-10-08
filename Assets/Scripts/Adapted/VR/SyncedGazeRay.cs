using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyncedGazeRay : MonoBehaviour
{
    public Transform gazeRayOrigin_t;
    public Transform gazeRayDirection_t;
    public GameObject go_Gaze;

    Vector3 gazeRayOrigin;
    Vector3 gazeRayDirection;

    SyncLineDrawer SyncLineDrawer_;

    void FixedUpdate()
    {
        gazeRayOrigin = gazeRayOrigin_t.position;
        gazeRayDirection = gazeRayDirection_t.position;

        //GameObject go_Gaze = gameObject.transform.Find("Gaze").gameObject;


        if (PersistentManager.Instance._visualizeGaze == true)
        {
            //SyncLineDrawer_.DrawLineInGameView(go_Gaze, gazeRayOrigin, gazeRayOrigin + gazeRayDirection * 10.0f, Color.red, 0.04f, false);
            SyncLineDrawer_.DrawLineInGameView(go_Gaze, gazeRayOrigin, gazeRayDirection, Color.cyan, 0.07f, false);
        }
    }
    // Helper function gaze vector visualization
    public struct SyncLineDrawer
    {
        private LineRenderer SyncLineRenderer;

        private void init(GameObject gameObject, bool laser)
        {
            if (SyncLineRenderer == null)
            {
                if (laser == false)
                {
                    // Crosshair
                    SyncLineRenderer = gameObject.AddComponent<LineRenderer>();
                }
                else if (laser == true)
                {
                    // Create empty game object for the linerenderer to add layer. Laser
                    var LaserObject = new GameObject();
                    LaserObject.transform.parent = gameObject.transform.parent;
                    LaserObject.layer = LayerMask.NameToLayer("IgnoreForPassenger");
                    SyncLineRenderer = LaserObject.AddComponent<LineRenderer>();
                }
                //Particles/Additive
                SyncLineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));
            }
        }

        //Draws lines through the provided vertices
        public void DrawLineInGameView(GameObject gameObject, Vector3 start, Vector3 end, Color color, float width, bool laser)
        {
            if (SyncLineRenderer == null)
            {
                init(gameObject, laser);
            }

            //Set material transparancy
            //lineRenderer.material = new Material(Shader.Find("Particles/Additive(Soft)"));
            color.a = 0.2f;

            //Set color
            SyncLineRenderer.startColor = color;
            SyncLineRenderer.endColor = color;

            //Set width
            SyncLineRenderer.startWidth = width;
            SyncLineRenderer.endWidth = width;

            //Set line count which is 2
            SyncLineRenderer.positionCount = 2;

            //Set the postion of both two lines
            SyncLineRenderer.SetPosition(0, start);
            SyncLineRenderer.SetPosition(1, end);
        }

        public void Destroy()
        {
            if (SyncLineRenderer != null)
            {
                UnityEngine.Object.Destroy(SyncLineRenderer.gameObject);
            }
        }
    }
}
