using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class polySpawner : MonoBehaviour
{
 
    public List<GameObject> pedestrianPrefabList;
    public int pedestriansToSpawn;
    public float randomTargetRange;
    public Vector3 RandomNavmeshLocation(float range)
    {
        float randomX = Random.Range(-range, range);
        float randomZ = Random.Range(-range, range);
        string message = "range(" + randomX.ToString() + "," + randomZ.ToString() + ')';
        //Debug.Log(message);
        Vector3 randomDirection = new Vector3(randomX,0.6f, randomZ);
        randomDirection += transform.position;
        NavMeshHit hit;
        Vector3 finalPosition = Vector3.zero;
        if (NavMesh.SamplePosition(randomDirection, out hit, range, 1))
        {
            finalPosition = hit.position;
        }
        return finalPosition;

    }

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Spawn());
    }

    // Update is called once per frame
    IEnumerator Spawn()
    {
        int count = 0;
        while (count < pedestriansToSpawn)
        {
            GameObject pedestrianPrefab = pedestrianPrefabList[Random.Range(0, pedestrianPrefabList.Count)];
            
            GameObject obj = Instantiate(pedestrianPrefab);
            Vector3 targetPos = RandomNavmeshLocation(randomTargetRange);
            //obj.transform.parent = null;
            obj.transform.position = targetPos;
            

            yield return new WaitForEndOfFrame();

            count++;
        }
    }
}

