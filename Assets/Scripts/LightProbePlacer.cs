using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//creates grid of lightprobes
[ExecuteInEditMode]
[RequireComponent(typeof(LightProbeGroup))]
public class LightProbePlacer : MonoBehaviour
{
    LightProbeGroup lightProbeGroup;

    public Vector2 mapDimensions;
    public float layerHight;
    public int layersCount;
    public Vector2 probeOffset;

    public void PlaceLightProbes()
    {
#if UNITY_EDITOR
        lightProbeGroup = GetComponent<LightProbeGroup>();
        List<Vector3> positions = new List<Vector3>();

        int xCount = Mathf.CeilToInt(mapDimensions.x / probeOffset.x);
        int yCount = Mathf.CeilToInt(mapDimensions.y / probeOffset.y);

        for(int z = 0; z < layersCount; z++)
        {
            for(int y = 0; y < yCount; y++)
            {
                for(int x = 0; x < xCount; x++)
                {
                    Vector3 checkPos = Vector3.zero;
                    Vector3 probePos = new Vector3(x * probeOffset.x, z * layerHight + 0.30f, y * probeOffset.y);
                    checkPos = transform.parent.TransformPoint(probePos);
                    checkPos = new Vector3(checkPos.x, 1000, checkPos.z);
                    RaycastHit hit;
                    /*
                    if (Physics.Raycast(checkPos, Vector3.down, out hit))
                    {                    
                        probePos = new Vector3(probePos.x, hit.point.y + (layerHight * z) , probePos.z);
                    }
                    
                    if(probePos.y > layersCount * layerHight) 
                    {
                        continue;
                    }
                    else if(probePos.y > layerHight && z != 0)
                    {
                        probePos.y = z * layerHight;
                    }
                    */
                    positions.Add(probePos);
                }
            }
        }

        lightProbeGroup.probePositions = positions.ToArray();

#endif
    }
}
