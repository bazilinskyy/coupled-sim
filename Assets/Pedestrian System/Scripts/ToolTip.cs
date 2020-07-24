using UnityEngine;
using System.Collections;

public class ToolTip : MonoBehaviour 
{
	public  bool m_disableOnStart = true;
	void Start () 
	{
		gameObject.SetActive(!m_disableOnStart);
	}
}
