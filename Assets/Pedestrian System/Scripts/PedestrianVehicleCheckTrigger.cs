// Enable this if you have the Road and Traffic System package installed
//#define TRAFFIC_SYSTEM

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent (typeof (Rigidbody))]
[RequireComponent (typeof (BoxCollider))]
[ExecuteInEditMode]
public class PedestrianVehicleCheckTrigger : MonoBehaviour 
{
	public  PedestrianNode     m_node                             = null;        // the node that holds this vehilecheck script
	#if TRAFFIC_SYSTEM
	private int                m_vehiclesWithinReach              = 0;
	#endif

	void Awake()
	{
		if(!m_node && GetComponent<PedestrianNode>())
			m_node = GetComponent<PedestrianNode>();

		if(GetComponent<Rigidbody>())
			GetComponent<Rigidbody>().useGravity = false;

		if(GetComponent<Collider>())
			GetComponent<Collider>().isTrigger = true;
	}

	void OnTriggerEnter( Collider a_obj )
	{
		if(!m_node)
			return;

		#if TRAFFIC_SYSTEM
		TrafficSystemVehicle vehicle = null;

		if(a_obj.transform.GetComponent<TrafficSystemVehicle>())
			vehicle = a_obj.transform.GetComponent<TrafficSystemVehicle>();

		if(vehicle)
			m_vehiclesWithinReach++;

		m_node.m_waitAtNode = true;
		#endif
	}

	void OnTriggerExit( Collider a_obj )
	{
		if(!m_node)
			return;

		#if TRAFFIC_SYSTEM
		TrafficSystemVehicle vehicle = null;
		
		if(a_obj.transform.GetComponent<TrafficSystemVehicle>())
			vehicle = a_obj.transform.GetComponent<TrafficSystemVehicle>();
		
		if(vehicle)
			m_vehiclesWithinReach--;

		if(m_vehiclesWithinReach <= 0)
			m_node.m_waitAtNode = false;
		#endif
	}
}
