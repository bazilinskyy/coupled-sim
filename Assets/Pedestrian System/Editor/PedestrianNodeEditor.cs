using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(PedestrianNode))]
[ExecuteInEditMode]
[CanEditMultipleObjects]
public class PedestrianNodeEditor : Editor 
{
	PedestrianNode     PedestrianNode;
	PedestrianSystem   PedestrianSystem;
	
	void Awake()
	{
		PedestrianNode = (PedestrianNode)target;
		PedestrianNode.CleanupNodes();
	}
	
	public override void OnInspectorGUI () 
	{
		DrawDefaultInspector();
		
		if(!PedestrianNode)
			return;

		if(!PedestrianSystem)
		{
			GameObject obj = GameObject.Find ("PedestrianSystem");
			if(obj && obj.GetComponent<PedestrianSystem>())
				PedestrianSystem = obj.GetComponent<PedestrianSystem>();
		}

		if(!PedestrianSystem || !PedestrianSystem.Instance)
			return;

		GUILayout.Space(20.0f);
		
		GUILayout.BeginHorizontal("box");

		if(GUILayout.Button("Return to Pedestrian System"))
		{
			if (PedestrianSystem && PedestrianSystem.Instance)
			{
				Selection.activeObject = PedestrianSystem;
			}
		}

		GUILayout.EndHorizontal();


		GUILayout.BeginHorizontal("box");
		
		if(PedestrianSystem.TextureIconAnchor)
		{
			if(GUILayout.Button(PedestrianSystem.TextureIconAnchor))
			{
				PedestrianSystem.Instance.SetPedestrianNode( PedestrianSystem.Tooltip.ANCHOR, PedestrianNode );
				//Selection.activeObject = PedestrianSystem.Instance;
			}
		}
		else
		{
			if(GUILayout.Button("Select as Anchor", GUILayout.Height(49)))
			{
				PedestrianSystem.Instance.SetPedestrianNode( PedestrianSystem.Tooltip.ANCHOR, PedestrianNode );
				//Selection.activeObject = PedestrianSystem.Instance;
			}
		}

		if(PedestrianSystem.TextureIconEdit)
		{
			if(GUILayout.Button(PedestrianSystem.TextureIconEdit))
			{
				PedestrianSystem.Instance.SetPedestrianNode( PedestrianSystem.Tooltip.EDIT, PedestrianNode );
				//Selection.activeObject = PedestrianSystem.Instance;
			}
		}
		else
		{
			if(GUILayout.Button("Select as Edit", GUILayout.Height(49)))
			{
				PedestrianSystem.Instance.SetPedestrianNode( PedestrianSystem.Tooltip.EDIT, PedestrianNode );
				//Selection.activeObject = PedestrianSystem.Instance;
			}
		}
		
		GUILayout.EndHorizontal();
		bool guiChanged = false;

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
		
		if(useAnchorNode && useEditNode &&
		   (useAnchorNode == PedestrianNode || useEditNode == PedestrianNode))
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

		GUILayout.BeginHorizontal("box");
		if(!PedestrianNode.GetComponent<PedestrianVehicleCheckTrigger>())
		{
			if(GUILayout.Button("Enable Pedestrian Crossing"))
			{
				PedestrianNode.gameObject.AddComponent<PedestrianVehicleCheckTrigger>();
				return;
			}
		}
		else
		{
			if(GUILayout.Button("Disable Pedestrian Crossing"))
			{
				if(PedestrianNode.GetComponent<PedestrianVehicleCheckTrigger>())
					DestroyImmediate(PedestrianNode.GetComponent<PedestrianVehicleCheckTrigger>());
				if(PedestrianNode.GetComponent<Rigidbody>())
					DestroyImmediate(PedestrianNode.GetComponent<Rigidbody>());
				if(PedestrianNode.GetComponent<Collider>())
					DestroyImmediate(PedestrianNode.GetComponent<Collider>());

				Debug.LogError("PEDESTRIAN SYSTEM -> The 'Missing Reference Exception' error below can be ignored");
				return;
			}
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal("box");
		if(GUILayout.Button("Delete Node"))
		{
			DestroyImmediate(PedestrianNode.gameObject);
			return;
		}
		GUILayout.EndHorizontal();

		if(PedestrianNode.m_nodes.Count > 0)
		{
			GUILayout.BeginHorizontal("box");
			if(GUILayout.Button("Remove All Links"))
			{
				PedestrianNode.RemoveAllNodes();
				EditorUtility.SetDirty(PedestrianNode);
			}
			GUILayout.EndHorizontal();

			for(int pIndex = 0; pIndex < PedestrianNode.m_nodes.Count; pIndex++)
			{
				GUILayout.BeginHorizontal("box");
				if(GUILayout.Button("Reveal ->", GUILayout.Height(49)))
				{
					PedestrianSystem.ClearCLRevealObjsFrom();
					PedestrianSystem.ClearCLRevealObjsTo();
					PedestrianSystem.AddToCLRevealObjsFrom(PedestrianNode.transform);
					PedestrianSystem.AddToCLRevealObjsTo(PedestrianNode.m_nodes[pIndex].transform);
				}
				if(PedestrianSystem.TextureIconRemoveAll)
				{
					if(GUILayout.Button(PedestrianSystem.TextureIconRemoveAll))
					{
						PedestrianNode.RemoveNode(PedestrianNode.m_nodes[pIndex]);
						EditorUtility.SetDirty(PedestrianNode);
						PedestrianSystem.ClearCLRevealObjsFrom();
						PedestrianSystem.ClearCLRevealObjsTo();
					}
				}
				GUILayout.EndHorizontal();
			}
		}

		/*
		if(PedestrianSystem.Instance.EditNode && PedestrianNode.NodeExists(PedestrianSystem.Instance.EditNode))
		{
			if(PedestrianSystem.TextureIconRemoveEdit)
			{
				if(GUILayout.Button(PedestrianSystem.TextureIconRemoveEdit))
					PedestrianNode.RemoveNode(PedestrianSystem.Instance.EditNode);
			}
			else
			{
				if(GUILayout.Button("Remove Edit Link"))
					PedestrianNode.RemoveNode(PedestrianSystem.Instance.EditNode);
			}
		}

		if(PedestrianSystem.Instance.AnchorNode && PedestrianNode.NodeExists(PedestrianSystem.Instance.AnchorNode))
		{
			if(PedestrianSystem.TextureIconRemoveAnchor)
			{
				if(GUILayout.Button(PedestrianSystem.TextureIconRemoveAnchor))
					PedestrianNode.RemoveNode(PedestrianSystem.Instance.AnchorNode);
			}
			else
			{
				if(GUILayout.Button("Remove Anchor Link"))
					PedestrianNode.RemoveNode(PedestrianSystem.Instance.AnchorNode);
			}
		}
		*/


		if(PedestrianSystem.Instance.PreviousAnchorNode == PedestrianNode)
			PedestrianSystem.Instance.PositionTooltip(PedestrianSystem.Tooltip.ANCHOR, PedestrianNode.gameObject);
		else if(PedestrianSystem.Instance.PreviousEditNode == PedestrianNode)
			PedestrianSystem.Instance.PositionTooltip(PedestrianSystem.Tooltip.EDIT, PedestrianNode.gameObject);

		if(guiChanged)
		{
			EditorUtility.SetDirty(useAnchorNode);
			EditorUtility.SetDirty(useEditNode);
		}

		if(GUI.changed)
			EditorUtility.SetDirty(PedestrianNode);
	}
}
