using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PedestrianObjectSpawner : MonoBehaviour 
{
	public  List<PedestrianObject> m_objectPrefabs      = new List<PedestrianObject>();
	
	[Range(0.0f, 1.0f)]
	public  float               m_nodeObjectSpawnChance     = 0.0f;

	public  float               m_onStartDelay               = 2.0f;
	public  int                 m_totalToSpawn               = 10;
	private int                 m_totalSpawned               = 0;
	public  float               m_spawnCheckDist             = 0.0f;
	public  float               m_spawnCheckRadius           = 5.0f;
	public  float               m_spawnDelayBetweenTries     = 5.0f;
	public  PedestrianNode      m_startNode                  = null;
	public  bool                m_respawnobjectOnObjectDestroy = true;
	private List<PedestrianObject> m_objectPool              = new List<PedestrianObject>();     

	public PedestrianObject SpawnRandomObject( bool a_ignoreChangeOfSpawning = false )
	{
		if(m_objectPrefabs.Count <= 0)
			return null;

		if(PedestrianSystem.Instance && !PedestrianSystem.Instance.CanSpawn())
			return null;

		float chanceOfSpawn = Random.Range(0.0f, 1.0f);
		
		if(!a_ignoreChangeOfSpawning && chanceOfSpawn > m_nodeObjectSpawnChance)
			return null;
		
		int randIndex = Random.Range(0, m_objectPrefabs.Count);
		
		PedestrianObject obj = Instantiate( m_objectPrefabs[randIndex], transform.position, transform.rotation ) as PedestrianObject;
		obj.Spawn ( transform.position, m_startNode );
		return obj;
	}

	void Awake()
	{
		if(GetComponent<Renderer>())
			GetComponent<Renderer>().enabled = false;
	}

	IEnumerator Start () 
	{
		if(PedestrianSystem.Instance)
			PedestrianSystem.Instance.RegisterObjectSpawner( this );

		if(m_totalToSpawn <= 0)
			yield break;

		for(int sIndex = 0; sIndex < m_totalToSpawn; sIndex++)
		{
			PedestrianObject obj = SpawnRandomObject(true);
			obj.gameObject.SetActive(false);
			m_objectPool.Add(obj);
		}

		yield return new WaitForSeconds(m_onStartDelay);


		while(m_totalSpawned < m_totalToSpawn)
		{
			Collider[] colliderHit = Physics.OverlapSphere( transform.position, m_spawnCheckRadius );

			bool hitObj = false; 
			for(int hIndex = 0; hIndex < colliderHit.Length; hIndex++)
			{
				if(colliderHit[hIndex].transform.GetComponent<PedestrianObject>())
					hitObj = true;
			}

			if(!hitObj)
			{
				if(m_totalSpawned < m_objectPool.Count)
				{
					PedestrianObject obj =  m_objectPool[m_totalSpawned];
					obj.gameObject.SetActive(true);
				}

				m_totalSpawned++;
			}

			yield return new WaitForSeconds(m_spawnDelayBetweenTries);
		}
	}

	public void RespawnObject()
	{
		StartCoroutine( ProcessSpawnOnDeath() );
	}

	IEnumerator ProcessSpawnOnDeath()
	{
		bool hasSpawned = false;
		while(!hasSpawned)
		{
			Collider[] colliderHit = Physics.OverlapSphere( transform.position, m_spawnCheckRadius );
			
			bool hitObj = false; 
			for(int hIndex = 0; hIndex < colliderHit.Length; hIndex++)
			{
				if(colliderHit[hIndex].transform.GetComponent<PedestrianObject>())
					hitObj = true;
			}
			
			if(!hitObj)
			{
				SpawnRandomObject();
				hasSpawned = true;
			}

			if(!hasSpawned)
				yield return new WaitForSeconds(m_spawnDelayBetweenTries);
		}

		yield return null;
	}

	void OnDrawGizmos()
	{
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position + ( transform.forward * m_spawnCheckDist ), m_spawnCheckRadius);
	}
}
