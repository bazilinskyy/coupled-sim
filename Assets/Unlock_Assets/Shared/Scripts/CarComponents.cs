using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;




public class CarComponents : MonoBehaviour {

	public bool blink;
	[Header("Lights")]
	public bool frontLightsOn;
	public bool brakeEffectsOn;
	[Space(5)]
	public GameObject 			brakeEffects;
	public GameObject 			frontLightEffects;
	public GameObject 			reverseEffect;
	[Header("Needles")]
	public Transform			SpeedNeedle;
	public Vector2				SpeedNeedleRotateRange	= Vector3.zero; 
	private Vector3 			SpeedEulers				=   Vector3.zero;
	public Transform			RpmNeedle;
	public Vector2				RpmNeedleRotateRange  	= Vector3.zero; 
	private Vector3 			RpmdEulers				=   Vector3.zero;
	public float				_NeedleSmoothing 		= 1.0f;
	public Transform 			steeringWheel;

	private float 				rotateNeedles			= 0.0f;
	[Header("Wheels")]
	public Transform 			wheel_FR;
	public Transform 			wheel_FL;
	[Header("Panel Texts")]
	public Text txtSpeed, txtRPM;
	public Slider sliderRPM;

	private IEnumerator coroutine;


	void Start () {
		blink 			= true;
		frontLightsOn 	= true;
		brakeEffectsOn 	= true;

		if (SpeedNeedle) SpeedEulers = SpeedNeedle.localEulerAngles;
		if (RpmNeedle) RpmdEulers = RpmNeedle.localEulerAngles;

		coroutine = WaitLights(2.0f);
		StartCoroutine(coroutine);

	
	}
	

	void Update () {
		if (blink) {
			TurnOnFrontLights ();
			TurnOnBackLights ();
		}

		if (SpeedNeedle) {

				Vector3 temp = new Vector3 (SpeedEulers.x, SpeedEulers.y, Mathf.Lerp (SpeedNeedleRotateRange.x, SpeedNeedleRotateRange.y, (rotateNeedles)));
				SpeedNeedle.localEulerAngles = Vector3.Lerp (SpeedNeedle.localEulerAngles, temp, Time.deltaTime * _NeedleSmoothing);

		}

		if (RpmNeedle)
		{
			Vector3 temp = new Vector3( RpmdEulers.x,RpmdEulers.y,Mathf.Lerp( RpmNeedleRotateRange.x, RpmNeedleRotateRange.y,	(rotateNeedles)));
			RpmNeedle.localEulerAngles = Vector3.Lerp( RpmNeedle.localEulerAngles, temp, Time.deltaTime * _NeedleSmoothing);
		}

		if (steeringWheel != null) {
			Vector3 eulers = steeringWheel.localRotation.eulerAngles;
			Vector3 wheelsEulers = wheel_FL.localRotation.eulerAngles;
			eulers.z = rotateNeedles * 15.0f;
			wheelsEulers.y = -rotateNeedles * 15.0f;


			steeringWheel.localRotation = Quaternion.Slerp (steeringWheel.localRotation, Quaternion.Euler (eulers), Time.deltaTime * 2.5f);

			wheel_FL.localRotation = Quaternion.Slerp (wheel_FL.localRotation, Quaternion.Euler (wheelsEulers), Time.deltaTime * 2.5f);
			wheel_FR.localRotation = Quaternion.Slerp (wheel_FR.localRotation, Quaternion.Euler (wheelsEulers), Time.deltaTime * 2.5f);



		}

		if (txtSpeed)
			txtSpeed.text = ((int)(rotateNeedles * 100.0f)).ToString ();// + " km/h";
		if (txtRPM) 
		txtRPM.text = ((int)(rotateNeedles * 1000.0f)).ToString ();
		if (sliderRPM)
			sliderRPM.value = (rotateNeedles * 1000.0f);
		
	}

	public void TurnOnFrontLights()
	{
		if (frontLightsOn) {
			frontLightEffects.SetActive (true);
			rotateNeedles += Time.deltaTime;
		} else {
			frontLightEffects.SetActive (false);
			rotateNeedles -= Time.deltaTime;
		}
	}

	public void TurnOnBackLights()
	{
		if (brakeEffectsOn) {
			brakeEffects.SetActive (true);
		} else {
			brakeEffects.SetActive (false);
		}
	}



	private IEnumerator WaitLights(float waitTime) {
		while (true) {
			yield return new WaitForSeconds(waitTime);
			frontLightsOn = !frontLightsOn;
			brakeEffectsOn = !brakeEffectsOn;
		}
	}


}
