using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[CustomEditor(typeof(PedestrianSystem))]
public class PedestrianSystemEditor : Editor 
{
	private PedestrianSystem    PedestrianSystem     { get; set; }

	private string[]            m_prefabLocation;

	private List<PedestrianObject>  m_allAssets      = new List<PedestrianObject>();
	private List<PedestrianObject>  m_lowAssets      = new List<PedestrianObject>();
	private List<PedestrianObject>  m_mediumAssets   = new List<PedestrianObject>();
	private List<PedestrianObject>  m_highAssets     = new List<PedestrianObject>();

	static public  PedestrianNode   AnchorNode       { get; set; }
	static public  PedestrianNode   EditNode         { get; set; }

	static public string  TooltipAnchorLocation      = "Assets/Pedestrian System/Prefabs/Base/ToolTip-Anchor.prefab";
	static public string  TooltipEditLocation        = "Assets/Pedestrian System/Prefabs/Base/ToolTip-Edit.prefab";

	private Transform     EditNodeFolder             { get; set; }
	private string        m_editNodeFolderName       = "Edit Folder";
	private Transform     AnchorNodeFolder           { get; set; }
	private string        m_anchorNodeFolderName     = "Anchor Folder";
	private Transform     ResourceNodeFolder         { get; set; }
	private string        m_resourceNodeFolderName   = "Resource Folder";

	void Awake()
	{
		PedestrianSystem = (PedestrianSystem)target;
		
		if(!PedestrianSystem)
			Debug.LogError("Pedestrian System Error -> \"Pedestrian System\" script must be in the scene. It is missing. Drop in the \"PedestrianSystem\" Prefab to fix this.");

		string sDataPath  = Application.dataPath + "/Pedestrian System/Prefabs/Resources/Active/";
		
		// get the system file paths of all the files in the asset folder
		string[] aFilePaths = Directory.GetFiles(sDataPath);
		
		// enumerate through the list of files loading the assets they represent and getting their type
		
		int count = 0;
		foreach (string sFilePath in aFilePaths) 
		{
			if(sFilePath.Length <= 6)
				continue;
			
			string sAssetPath = sFilePath.Substring(sFilePath.Length - 6, 6);
			
			if(sAssetPath == "prefab")
				count++;
		}
		
		m_prefabLocation = new string[count];
		
		m_highAssets.Clear();
		
		count = 0;
		foreach (string sFilePath in aFilePaths) 
		{
			if(sFilePath.Length <= 6)
				continue;
			
			string sAssetPath = sFilePath.Substring(sFilePath.Length - 6, 6);
			
			if(sAssetPath == "prefab")
			{
				string file = sFilePath.Substring(sDataPath.Length);
				m_prefabLocation[count] = sDataPath + file;
				//Debug.Log("count " + count + " : " + m_prefabLocation[count]);
				
				PedestrianObject asset = AssetDatabase.LoadAssetAtPath(m_prefabLocation[count], typeof(PedestrianObject)) as PedestrianObject;
				
				if(asset)
				{
					if(asset.m_assetFrequency == PedestrianSystem.ObjectFrequency.HIGH)
						m_highAssets.Add(asset);
					else if(asset.m_assetFrequency == PedestrianSystem.ObjectFrequency.MEDIUM)
						m_mediumAssets.Add(asset);
					else if(asset.m_assetFrequency == PedestrianSystem.ObjectFrequency.LOW)
						m_lowAssets.Add(asset);
					
					m_allAssets.Add(asset);
				}
				
				count++;
			}
		}
	}
	
	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector();
		
		if(!PedestrianSystem || !PedestrianSystem.Instance)
			return;

		GUILayout.Space(10.0f);
		GUILayout.BeginVertical("box");

		if(GUILayout.Button("Generate Node", GUILayout.Height(49)))
		{
			if(!PedestrianSystem.Instance.m_nodePrefab)
			{
				Debug.LogError("Node Prefab not set. You can find one in \"Assets\\Pedestrian System\\Prefabs\\Pathing\". Node Generation Cancelled.");
				return;
			}

			PedestrianNode node        = PrefabUtility.InstantiatePrefab(PedestrianSystem.Instance.m_nodePrefab) as PedestrianNode;
			Selection.activeGameObject = node.gameObject;
			node.transform.parent      = PedestrianSystem.Instance.transform;

			if(PedestrianSystem.Instance.m_autoLink)
			{
				if(PedestrianSystem.Instance.m_linkBothDir)
				{
					if(EditNode)
					{
						EditNode.AddNode( node );
						EditorUtility.SetDirty( EditNode );

						node.AddNode( EditNode );
						EditorUtility.SetDirty( node );

						Vector3 pos = EditNode.transform.position;
						pos.x += 4.0f;
						node.transform.position = pos;
					}
					else if(AnchorNode)
					{
						AnchorNode.AddNode( node );
						EditorUtility.SetDirty( AnchorNode );
						
						node.AddNode( AnchorNode );
						EditorUtility.SetDirty( node );

						Vector3 pos = AnchorNode.transform.position;
						pos.x += 4.0f;
						node.transform.position = pos;
					}
				}
				else if(EditNode)
				{
					EditNode.AddNode( node );
					EditorUtility.SetDirty(EditNode);

					Vector3 pos = EditNode.transform.position;
    			    pos.x += 4.0f;
					node.transform.position = pos;
				}
				else if(AnchorNode)
				{
					AnchorNode.AddNode( node );
					EditorUtility.SetDirty(AnchorNode);

					Vector3 pos = AnchorNode.transform.position;
					pos.x += 4.0f;
					node.transform.position = pos;
				}
			}

			if(EditNode)
			{
				if(AnchorNode)
					AnchorNode.transform.parent = PedestrianSystem.transform;

				AnchorNode = EditNode;
				AnchorNode.transform.parent = AnchorNodeFolder.transform;

				EditNode   = node;
				EditNode.transform.parent   = EditNodeFolder.transform;

				PedestrianSystem.Instance.SetPedestrianNode(PedestrianSystem.Tooltip.ANCHOR, AnchorNode);
			}
			else
			{
				EditNode   = node;
				EditNode.transform.parent   = EditNodeFolder.transform;
			}

			PedestrianSystem.Instance.SetPedestrianNode(PedestrianSystem.Tooltip.EDIT, node);
		}

		GUILayout.EndVertical();
		bool guiChanged = false;

		if(EditNode && AnchorNode)
		{
			GUILayout.BeginHorizontal("box");
			if(GUILayout.Button("Go to 'Edit' Piece", GUILayout.Height(49)))
				Selection.activeObject = EditNode.transform;
			
			if(GUILayout.Button("Go to 'Anchor' Piece", GUILayout.Height(49)))
				Selection.activeObject = AnchorNode.transform;
			GUILayout.EndHorizontal();
			
			GUILayout.Space(10.0f);
		}

		PedestrianNode useAnchorNode = null;
		PedestrianNode useEditNode   = null;

		if(PedestrianSystem.Instance.EditNode)
			useEditNode = PedestrianSystem.Instance.EditNode;
		else if(PedestrianSystem.Instance.PreviousEditNode)
			useEditNode = PedestrianSystem.Instance.PreviousEditNode;

		if(PedestrianSystem.Instance.AnchorNode)
			useAnchorNode = PedestrianSystem.Instance.AnchorNode;
		else if(PedestrianSystem.Instance.PreviousAnchorNode)
			useAnchorNode = PedestrianSystem.Instance.PreviousAnchorNode;

		if(useAnchorNode && useEditNode)
		{
			GUILayout.BeginHorizontal("box");
			
			if(PedestrianSystem && PedestrianSystem.TextureIconAnchorToEdit)
			{
				if(GUILayout.Button(PedestrianSystem.TextureIconAnchorToEdit))
				{
					// link Anchor To Edit
					PedestrianSystem.Instance.LinkNode();
					guiChanged = true;
				}
			}
			else
			{
				if(GUILayout.Button("Link Anchor To Edit", GUILayout.Height(49)))
				{
					// link Anchor To Edit
					PedestrianSystem.Instance.LinkNode();
					guiChanged = true;
				}
			}
			
			if(PedestrianSystem.TextureIconEditToAnchor)
			{
				if(GUILayout.Button(PedestrianSystem.TextureIconEditToAnchor))
				{
					// link Edit to Anchor
					PedestrianSystem.Instance.LinkNode( false );
					guiChanged = true;
				}
			}
			else
			{
				if(GUILayout.Button("Link Edit to Anchor", GUILayout.Height(49)))
				{
					// link Edit to Anchor
					PedestrianSystem.Instance.LinkNode( false );
					guiChanged = true;
				}
			}
			GUILayout.EndHorizontal();
		}

//		if(PedestrianSystem.Instance.EditNode)
//		{
//			SetPedestrianNode( PedestrianSystem.Tooltip.EDIT, PedestrianSystem.Instance.EditNode );
//			PedestrianSystem.Instance.SetPedestrianNode( PedestrianSystem.Tooltip.EDIT, null );
//		}
//		
//		if(PedestrianSystem.Instance.AnchorNode)
//		{
//			SetPedestrianNode( PedestrianSystem.Tooltip.ANCHOR, PedestrianSystem.Instance.AnchorNode );
//			PedestrianSystem.Instance.SetPedestrianNode( PedestrianSystem.Tooltip.ANCHOR, null );
//		}

		if(guiChanged)
		{
			EditorUtility.SetDirty(useAnchorNode);
			EditorUtility.SetDirty(useEditNode);
		}
				
		if(EditNodeFolder)
		{
			for(int cIndex = 0; cIndex < EditNodeFolder.childCount; cIndex++)
				EditNodeFolder.GetChild(cIndex).transform.parent = PedestrianSystem.transform;
			
			if(EditNode)
				EditNode.transform.parent   = EditNodeFolder;
		}
		
		if(AnchorNodeFolder)
		{
			for(int cIndex = 0; cIndex < AnchorNodeFolder.childCount; cIndex++)
				AnchorNodeFolder.GetChild(cIndex).transform.parent = PedestrianSystem.transform;
			
			if(AnchorNode)
				AnchorNode.transform.parent = AnchorNodeFolder;
		}

		if(AnchorNode)
			SetPedestrianNode(PedestrianSystem.Tooltip.ANCHOR, AnchorNode);
	
		if(EditNode)
			SetPedestrianNode(PedestrianSystem.Tooltip.EDIT, EditNode);

		if(GUI.changed)
			EditorUtility.SetDirty(PedestrianSystem);
	}

	void OnSceneGUI()
	{
		ProcessFolderChecks();
		//CheckForAnchorNode();
		//CheckForEditNode();

		if(PedestrianSystem && PedestrianSystem.Instance)
		{
			if(!EditNode)
				PedestrianSystem.Instance.ShowTooltip( PedestrianSystem.Tooltip.EDIT, false );
			else
				PedestrianSystem.Instance.ShowTooltip( PedestrianSystem.Tooltip.EDIT, true );
			
			if(!AnchorNode)
				PedestrianSystem.Instance.ShowTooltip( PedestrianSystem.Tooltip.ANCHOR, false );
			else
				PedestrianSystem.Instance.ShowTooltip( PedestrianSystem.Tooltip.ANCHOR, true );
			
			if(!PedestrianSystem.Instance.TooltipAnchor)
			{
				if(ResourceNodeFolder && ResourceNodeFolder.childCount > 1)
				{
					for(int cIndex = 1; cIndex < ResourceNodeFolder.childCount; cIndex++)
					{
						if(ResourceNodeFolder.GetChild(cIndex) && ResourceNodeFolder.GetChild(cIndex).name == "Tooltip-Anchor")
							if(ResourceNodeFolder.GetChild(cIndex).gameObject)
								DestroyImmediate(ResourceNodeFolder.GetChild(cIndex).gameObject);
					}
				}
				
				GameObject tooltip = GameObject.Find ("Tooltip-Anchor");
				if (tooltip)
					PedestrianSystem.Instance.TooltipAnchor = tooltip;
				else
				{
					GameObject toolTip = AssetDatabase.LoadAssetAtPath(TooltipAnchorLocation, typeof(GameObject)) as GameObject;
					PedestrianSystem.Instance.TooltipAnchor = PrefabUtility.InstantiatePrefab(toolTip) as GameObject;
				}
			}
			else
			{
				if(ResourceNodeFolder)
					PedestrianSystem.Instance.TooltipAnchor.transform.parent = ResourceNodeFolder;
			}
			
			if(!PedestrianSystem.Instance.TooltipEdit)
			{
				if(ResourceNodeFolder && ResourceNodeFolder.childCount > 1)
				{
					for(int cIndex = 1; cIndex < ResourceNodeFolder.childCount; cIndex++)
					{
						if(ResourceNodeFolder.GetChild(cIndex) && ResourceNodeFolder.GetChild(cIndex).name == "Tooltip-Edit")
							if(ResourceNodeFolder.GetChild(cIndex).gameObject)
								DestroyImmediate(ResourceNodeFolder.GetChild(cIndex).gameObject);
					}
				}
				
				GameObject tooltip = GameObject.Find ("Tooltip-Edit");
				if (tooltip)
					PedestrianSystem.Instance.TooltipEdit = tooltip;
				else
				{
					GameObject toolTip = AssetDatabase.LoadAssetAtPath(TooltipEditLocation, typeof(GameObject)) as GameObject;
					PedestrianSystem.Instance.TooltipEdit = PrefabUtility.InstantiatePrefab(toolTip) as GameObject;
				}
			}
			else
			{
				if(ResourceNodeFolder)
					PedestrianSystem.Instance.TooltipEdit.transform.parent = ResourceNodeFolder;
			}
			
			if(PedestrianSystem.Instance.EditNode)
			{
				SetPedestrianNode( PedestrianSystem.Tooltip.EDIT, PedestrianSystem.Instance.EditNode );
				PedestrianSystem.Instance.SetPedestrianNode( PedestrianSystem.Tooltip.EDIT, null );
			}
			
			if(PedestrianSystem.Instance.AnchorNode)
			{
				SetPedestrianNode( PedestrianSystem.Tooltip.ANCHOR, PedestrianSystem.Instance.AnchorNode );
				PedestrianSystem.Instance.SetPedestrianNode( PedestrianSystem.Tooltip.ANCHOR, null );
			}
			
			if(PedestrianSystem.Instance.TooltipAnchor)
				PedestrianSystem.Instance.TooltipAnchor.transform.Rotate(Vector3.up, 1.0f); 
			if(PedestrianSystem.Instance.TooltipEdit)
				PedestrianSystem.Instance.TooltipEdit.transform.Rotate(Vector3.up, 1.0f); 
		}
	}

	void ProcessFolderChecks( bool a_avoidCreation = false )
	{
		if(!EditNodeFolder)
		{
			bool foundEditNodeFolder = false;
			for(int cIndex = 0; cIndex < PedestrianSystem.transform.childCount; cIndex++)
			{
				if(PedestrianSystem.transform.GetChild(cIndex).name == m_editNodeFolderName)
				{
					EditNodeFolder      = PedestrianSystem.transform.GetChild(cIndex);
					foundEditNodeFolder = true;
					break;
				}
			}
			
			if(!foundEditNodeFolder && !a_avoidCreation)
			{
				EditNodeFolder = new GameObject().transform;
				EditNodeFolder.name = m_editNodeFolderName;
				EditNodeFolder.transform.parent = PedestrianSystem.transform;
			}
		}

		if(!AnchorNodeFolder)
		{
			bool foundAnchorNodeFolder = false;
			for(int cIndex = 0; cIndex < PedestrianSystem.transform.childCount; cIndex++)
			{
				if(PedestrianSystem.transform.GetChild(cIndex).name == m_anchorNodeFolderName)
				{
					AnchorNodeFolder      = PedestrianSystem.transform.GetChild(cIndex);
					foundAnchorNodeFolder = true;
					break;
				}
			}
			
			if(!foundAnchorNodeFolder && !a_avoidCreation)
			{
				AnchorNodeFolder = new GameObject().transform;
				AnchorNodeFolder.name = m_anchorNodeFolderName;
				AnchorNodeFolder.transform.parent = PedestrianSystem.transform;
			}
		}

		if(!ResourceNodeFolder)
		{
			bool foundResourceNodeFolder = false;
			for(int cIndex = 0; cIndex < PedestrianSystem.transform.childCount; cIndex++)
			{
				if(PedestrianSystem.transform.GetChild(cIndex).name == m_resourceNodeFolderName)
				{
					ResourceNodeFolder      = PedestrianSystem.transform.GetChild(cIndex);
					foundResourceNodeFolder = true;
					break;
				}
			}
			
			if(!foundResourceNodeFolder && !a_avoidCreation)
			{
				ResourceNodeFolder = new GameObject().transform;
				ResourceNodeFolder.name = m_resourceNodeFolderName;
				ResourceNodeFolder.transform.parent = PedestrianSystem.transform;
			}
		}		
	}
	
	void CheckForAnchorNode()
	{
		if(!AnchorNodeFolder)
			return;
		
		for(int cIndex = 0; cIndex < AnchorNodeFolder.childCount; cIndex++)
		{
			if(AnchorNodeFolder.GetChild(cIndex).GetComponent<PedestrianNode>())
			{
				SetPedestrianNode( PedestrianSystem.Tooltip.ANCHOR, AnchorNodeFolder.GetChild(cIndex).GetComponent<PedestrianNode>() );
			}
		}
	}

	void CheckForEditNode()
	{
		if(!EditNodeFolder)
			return;
		
		for(int cIndex = 0; cIndex < EditNodeFolder.childCount; cIndex++)
		{
			if(EditNodeFolder.GetChild(cIndex).GetComponent<PedestrianNode>())
			{
				SetPedestrianNode( PedestrianSystem.Tooltip.EDIT, EditNodeFolder.GetChild(cIndex).GetComponent<PedestrianNode>() );
			}
		}
	}

	public void SetPedestrianNode( PedestrianNode a_piece )
	{
		EditNode   = a_piece;
		AnchorNode = a_piece;
	}
	
	void SetPedestrianNode( PedestrianSystem.Tooltip a_tooltip, PedestrianNode a_obj )
	{
		switch(a_tooltip)
		{
		case PedestrianSystem.Tooltip.ANCHOR:
		{
			AnchorNode = a_obj;
			if(PedestrianSystem.Instance && AnchorNode)
			{
				//PedestrianSystem.Instance.AnchorNode = AnchorNode;
				PedestrianSystem.Instance.PositionTooltip(a_tooltip, AnchorNode.gameObject);
			}
		}
			break;
		case PedestrianSystem.Tooltip.EDIT:
		{
			EditNode = a_obj;
			
			if(PedestrianSystem.Instance && EditNode)
			{
				///PedestrianSystem.Instance.EditNode = EditNode;
				PedestrianSystem.Instance.PositionTooltip(a_tooltip, EditNode.gameObject);
			}
		}
			break;
		}
		
	}
}
