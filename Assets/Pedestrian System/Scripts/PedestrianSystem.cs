using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
[ExecuteInEditMode]
public class PedestrianSystem : MonoBehaviour 
{
	public static PedestrianSystem Instance { get; set; }

	public        bool                      m_showGizmos     = true;
	public        PedestrianNode            m_nodePrefab     = null;
	public        bool                      m_autoLink       = true;             // enable to automatically link the edit and anchor node together on generate node
	public        bool                      m_linkBothDir    = true;             // enable to link the anchor with the edit node and also the edit with the anchor node

	[Range(0.0f, 5.0f)]
	public        float                     m_globalSpeedVariation       = 0.5f; // used to generate a slight variation of speed each node a object gets to
	
	[Range(0.0f, 5.0f)]
	public        float                     m_globalLanePosVariation     = 0.0f; // used to generate a slight variation of lane position for all object

	public        Texture2D                 TextureIconAnchor = null;
	public        Texture2D                 TextureIconEdit   = null;
	public        Texture2D                 TextureIconAnchorToEdit   = null;
	public        Texture2D                 TextureIconEditToAnchor   = null;
	public        Texture2D                 TextureIconRemoveAnchor   = null;
	public        Texture2D                 TextureIconRemoveEdit     = null;
	public        Texture2D                 TextureIconRemoveAll      = null;

	public        PedestrianNode            AnchorNode       { get; set; }
	public        PedestrianNode            EditNode         { get; set; }
	public        PedestrianNode            PreviousAnchorNode { get; set; }
	public        PedestrianNode            PreviousEditNode   { get; set; }
	public        GameObject                TooltipAnchor    { get; set; }
	public        GameObject                TooltipEdit      { get; set; }

	public  int                             m_objectSpawnCountMax      = -1;   // if -1 then unlimited objects can spawn. If higher than only this amount will ever spawn using the Traffic System spawn options
	public  bool                            m_randomObjectSpawnPerNode  = false; 
	[Range(0, 1)]
	public  float                           m_randomObjectSpawnChancePerNode = 0.0f;
	public  int                             m_numOfObjectsSpawnedPerNode     = 1;

	private       List<Transform>           CLRevealObjectsFrom       = new List<Transform>();
	private       List<Transform>           CLRevealObjectsTo         = new List<Transform>();

	public  List<PedestrianObject>          m_objectPrefabs      = new List<PedestrianObject>();
	private List<PedestrianObjectSpawner>   m_objectSpawners     = new List<PedestrianObjectSpawner>();
	private List<PedestrianObject>          m_spawnedObjects     = new List<PedestrianObject>();
	public  List<PedestrianObject>          GetSpawnedObjects()  { return m_spawnedObjects; }

	public enum Tooltip
	{
		ANCHOR    = 0,
		EDIT      = 1
	}

	public enum ObjectFrequency
	{
		HIGH    = 0,
		MEDIUM  = 1,
		LOW     = 2
	}

	void Awake () 
	{
		if(Instance != null)
		{
			Destroy (this);
			return;
		}

		Instance = this;
	}

	void Start()
	{
		if(Instance != this)
			return;

		if(m_randomObjectSpawnPerNode && Application.isPlaying)
		{
			PedestrianNode[] nodes = GameObject.FindObjectsOfType<PedestrianNode>();
			
			for(int rIndex = 0; rIndex < nodes.Length; rIndex++)
			{
				int perNodeCount = 0;
				while(perNodeCount < m_numOfObjectsSpawnedPerNode)
				{
					float rand = Random.Range(0.0f, 1.0f);
					if(rand <= m_randomObjectSpawnChancePerNode && CanSpawn())
					{
						int randObjectIndex    = Random.Range(0, m_objectPrefabs.Count);
						PedestrianObject obj   = Instantiate( m_objectPrefabs[randObjectIndex], transform.position, transform.rotation ) as PedestrianObject;
						obj.Spawn( nodes[rIndex].transform.position, nodes[rIndex] );
					}

					perNodeCount++;
				}
			}
		}
	}

	public void SetPedestrianNode( Tooltip a_tooltip, PedestrianNode a_obj, bool a_show = true )
	{
		switch(a_tooltip)
		{
		case Tooltip.ANCHOR:
		{
			AnchorNode = a_obj;
			if(a_obj)
				PreviousAnchorNode = a_obj;
			if(AnchorNode)
			{
				ShowTooltip(Tooltip.ANCHOR, a_show);
				PositionTooltip(Tooltip.ANCHOR, AnchorNode.gameObject);
			}
		}
			break;
		case Tooltip.EDIT:
		{
			EditNode = a_obj;
			if(a_obj)
				PreviousEditNode = a_obj;
			if(EditNode)
			{
				ShowTooltip(Tooltip.EDIT, a_show);
				PositionTooltip(Tooltip.EDIT, EditNode.gameObject);
			}
		}
			break;
		}
	}


	public void ShowTooltip( Tooltip a_tooltip, bool a_show )
	{
		switch(a_tooltip)
		{
		case Tooltip.ANCHOR:
		{
			if(TooltipAnchor)
			{
				TooltipAnchor.SetActive( a_show );
			}
		}
			break;
		case Tooltip.EDIT:
		{
			if(TooltipEdit)
			{
				TooltipEdit.SetActive( a_show );
			}
		}
			break;
		}
	}
	
	public void PositionTooltip( Tooltip a_tooltip, GameObject a_obj, bool a_show = true )
	{
		switch(a_tooltip)
		{
		case Tooltip.ANCHOR:
		{
			if(TooltipAnchor)
			{
				ShowTooltip(Tooltip.ANCHOR, a_show);
				TooltipAnchor.transform.position = new Vector3(a_obj.transform.position.x, a_obj.transform.position.y + a_obj.GetComponent<Renderer>().bounds.extents.y + 2.0f, a_obj.transform.position.z);
			}
		}
			break;
		case Tooltip.EDIT:
		{
			if(TooltipEdit)
			{
				ShowTooltip(Tooltip.EDIT, a_show);
				TooltipEdit.transform.position = new Vector3(a_obj.transform.position.x, a_obj.transform.position.y + a_obj.GetComponent<Renderer>().bounds.extents.y + 2.4f, a_obj.transform.position.z);
			}
		}
			break;
		}
	}

	public bool CanSpawn()
	{
		if(m_objectSpawnCountMax <= -1)
			return true;
		
		if(m_spawnedObjects.Count < m_objectSpawnCountMax)
			return true;
		
		return false;
	}

	public void LinkNode(bool a_anchorToEdit = true )
	{
		PedestrianNode useEditNode   = null;
		PedestrianNode useAnchorNode = null;

		if(EditNode)
			useEditNode = EditNode;
		else if(PreviousEditNode)
			useEditNode = PreviousEditNode;
		
		if(AnchorNode)
			useAnchorNode = AnchorNode;
		else if(PreviousAnchorNode)
			useAnchorNode = PreviousAnchorNode;

		if(m_linkBothDir)
		{
			if(useAnchorNode)
				useAnchorNode.AddNode( useEditNode );
			if(useEditNode)
				useEditNode.AddNode( useAnchorNode );
		}
		else if(a_anchorToEdit)
		{
			if(useAnchorNode)
				useAnchorNode.AddNode( useEditNode );
		}
		else
		{
			if(useEditNode)
				useEditNode.AddNode( useAnchorNode );
		}
	}

	public void AddToCLRevealObjsFrom( Transform a_obj )
	{
		bool foundObj = false;

		if(!foundObj)
			CLRevealObjectsFrom.Add(a_obj);
	}
	
	public void ClearCLRevealObjsFrom()
	{
		CLRevealObjectsFrom.Clear();
	}
	
	public void AddToCLRevealObjsTo( Transform a_obj )
	{
		bool foundObj = false;

		if(!foundObj)
			CLRevealObjectsTo.Add(a_obj);
	}
	
	public void ClearCLRevealObjsTo()
	{
		CLRevealObjectsTo.Clear();
	}

	public void RegisterObject( PedestrianObject a_object )
	{
		m_spawnedObjects.Add( a_object );
	}
	
	public void UnRegisterObject( PedestrianObject a_object )
	{
		m_spawnedObjects.Remove( a_object );
		RespawnObject();
	}
	
	public void RegisterObjectSpawner( PedestrianObjectSpawner a_spawner )
	{
		m_objectSpawners.Add( a_spawner );
	}
	
	public void RespawnObject()
	{
		if(m_objectSpawners.Count <= 0)
			return;
		
		PedestrianObjectSpawner spawners = m_objectSpawners[Random.Range(0, m_objectSpawners.Count)];
		spawners.RespawnObject();
	}

	void OnDrawGizmos()
	{
		#if !UNITY_EDITOR
		return;
		#endif
		
		if(CLRevealObjectsFrom.Count > 0 && CLRevealObjectsFrom.Count == CLRevealObjectsTo.Count)
		{
			float scaleFactorCube   = 0.25f;
			float scaleFactorSphere = 0.35f;
			
			for(int rIndex = 0; rIndex < CLRevealObjectsFrom.Count; rIndex++)
			{
				if(CLRevealObjectsFrom[rIndex] == null)
					continue;
				
				if(CLRevealObjectsTo[rIndex] == null)
					continue;
				
				Vector3 offset = new Vector3(0.0f, 0.225f, 0.0f);
				Gizmos.color = Color.red;
				Gizmos.DrawLine( CLRevealObjectsFrom[rIndex].position + offset, CLRevealObjectsTo[rIndex].position + offset );
				
				Vector3 dir = CLRevealObjectsFrom[rIndex].position - CLRevealObjectsTo[rIndex].position;
				Gizmos.color = Color.yellow;
				Gizmos.DrawCube( (CLRevealObjectsFrom[rIndex].position - (dir.normalized * ((dir.magnitude / 2) + scaleFactorSphere))) + offset, new Vector3(scaleFactorCube, scaleFactorCube, scaleFactorCube) );
				Gizmos.color = Color.red;
				Gizmos.DrawSphere( (CLRevealObjectsFrom[rIndex].position - (dir.normalized * (dir.magnitude / 2))) + offset, scaleFactorSphere );
				
				Gizmos.color = Color.red;
				Gizmos.DrawSphere( CLRevealObjectsFrom[rIndex].position + offset, scaleFactorSphere );
				Gizmos.DrawSphere( CLRevealObjectsTo[rIndex].position + offset, scaleFactorSphere );
			}
		}
	}
}
