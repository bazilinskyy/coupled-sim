using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
public class PedestrianNode : MonoBehaviour 
{
	public  List<PedestrianNode>        m_nodes              = new List<PedestrianNode>();
	public  bool                        m_waitAtNode         = false;
	public  int                         m_pathID             = 1;

	void Awake () 
	{
		#if !UNITY_EDITOR
			if(Application.isPlaying)
				if(renderer)
					renderer.enabled = false;
		#endif

		CleanupNodes();
	}
	
	void Start () 
	{
		
	}

	public void AddNode ( PedestrianNode a_node ) 
	{
		if(NodeExists(a_node))
			return;

		m_nodes.Add( a_node );
	}

	public bool NodeExists( PedestrianNode a_node )
	{
		for(int nIndex = 0; nIndex < m_nodes.Count; nIndex++)
		{
			if(m_nodes[nIndex] == a_node)
				return true;
		}

		return false;
	}

	public void RemoveNode( PedestrianNode a_node )
	{
		m_nodes.Remove( a_node );
	}

	public void RemoveAllNodes()
	{
		m_nodes.Clear();
	}

	public PedestrianNode NextNode( PedestrianObject a_obj )
	{
		switch(a_obj.m_pathingStatus)
		{
		case PedestrianObject.PathingStatus.RANDOM:
		{
			if( m_nodes.Count > 0)
			{
				int count = 0;
				List<PedestrianNode> m_tmpNodes = new List<PedestrianNode>();

				for(int nIndex = 0; nIndex < m_nodes.Count; nIndex++)
					m_tmpNodes.Add(m_nodes[nIndex]);

				while(count < m_tmpNodes.Count)
				{
					count++;
					PedestrianNode node = m_tmpNodes[Random.Range(0, m_tmpNodes.Count)];

					if(node && !a_obj.HasVisitedNode( node ))
						return node;
					else
					{
						m_tmpNodes.Remove( node );
						count = 0;
					}
				}
			}
		}
			break;
		}

		return null;
	}

	public void SpawnNode( Vector3 a_pos, bool a_isConnected = true )
	{
		PedestrianNode node     = Instantiate(PedestrianSystem.Instance.m_nodePrefab) as PedestrianNode;
		node.transform.parent   = PedestrianSystem.Instance.transform;
		node.transform.position = a_pos;

		if(a_isConnected)
			AddNode(node);
	}

	public void CleanupNodes()
	{
		for(int nIndex = m_nodes.Count - 1; nIndex >= 0; nIndex--)
		{
			if(!m_nodes[nIndex])
				m_nodes.RemoveAt(nIndex);
		}
	}
	
	void OnDrawGizmos()
	{
		#if !UNITY_EDITOR
			return;
		#else
			if(PedestrianSystem.Instance && !PedestrianSystem.Instance.m_showGizmos)
				return;
		#endif

		if(PedestrianSystem.Instance && !PedestrianSystem.Instance.m_showGizmos)
			return;
		
		float scaleFactorCube   = 0.15f;
		float scaleFactorSphere = 0.225f;
		for(int nIndex = 0; nIndex < m_nodes.Count; nIndex++)
		{
			PedestrianNode connectedNode = m_nodes[nIndex];
			if(connectedNode)
			{
				Vector3 offset = new Vector3(0.0f, 0.1f, 0.0f);
				Gizmos.color = Color.white;
				Gizmos.DrawLine( transform.position + offset, connectedNode.transform.position + offset );
				
				Vector3 dir = transform.position - connectedNode.transform.position;
				//					Gizmos.color = Color.white;
				//					Gizmos.DrawCube( (transform.position - (dir.normalized * ((dir.magnitude / 2) + scaleFactorSphere))) + offset, new Vector3(scaleFactorCube * 1.4f, scaleFactorCube * 1.4f, scaleFactorCube * 1.4f) );
				Gizmos.color = Color.yellow;
				Gizmos.DrawCube( (transform.position - (dir.normalized * ((dir.magnitude / 2) + scaleFactorSphere))) + offset, new Vector3(scaleFactorCube, scaleFactorCube, scaleFactorCube) );
				Gizmos.color = Color.white;
				Gizmos.DrawSphere( (transform.position - (dir.normalized * (dir.magnitude / 2))) + offset, scaleFactorSphere );
			}
		}
	}
}
