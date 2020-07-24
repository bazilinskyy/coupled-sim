using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(PedestrianObject))]
[ExecuteInEditMode]
[CanEditMultipleObjects]
public class PedestrianObjectEditor : Editor 
{
	PedestrianObject   PedestrianObject;
	PedestrianSystem   PedestrianSystem;
	
	void Awake()
	{
		PedestrianObject = (PedestrianObject)target;
	}
	
	public override void OnInspectorGUI () 
	{
		DrawDefaultInspector();
		
		if(!PedestrianObject)
			return;
		
		if(!PedestrianSystem)
		{
			GameObject obj = GameObject.Find ("PedestrianSystem");
			if(obj && obj.GetComponent<PedestrianSystem>())
				PedestrianSystem = obj.GetComponent<PedestrianSystem>();
		}
		
	}
}
