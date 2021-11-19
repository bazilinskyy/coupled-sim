namespace TurnTheGameOn.ArrowWaypointer
{
	using UnityEngine;
	using System.Collections;
	public class SmoothFollow : MonoBehaviour
	{
		public Transform target; // The target we are following
		public float distance = 10.0f; // The distance in the x-z plane to the target
		public float height = 5.0f; // the height we want the camera to be above the target
		public float rotationDamping;
		public float heightDamping;

		void LateUpdate()
		{
			if (!target) return;
			
			var wantedRotationAngle = target.eulerAngles.y;
			var wantedHeight = target.position.y + height; // Calculate the current rotation angles

			var currentRotationAngle = transform.eulerAngles.y;
			var currentHeight = transform.position.y;

			currentRotationAngle = Mathf.LerpAngle(currentRotationAngle, wantedRotationAngle, rotationDamping * Time.deltaTime); // Damp the rotation around the y-axis
			currentHeight = Mathf.Lerp(currentHeight, wantedHeight, heightDamping * Time.deltaTime); // Damp the height

			var currentRotation = Quaternion.Euler(0, currentRotationAngle, 0); // Convert the angle into a rotation

			transform.position = target.position;
			transform.position -= currentRotation * Vector3.forward * distance; // Set the position of the camera on the x-z plane to: distance meters behind the target

			transform.position = new Vector3(transform.position.x ,currentHeight , transform.position.z); // Set the height of the camera

			transform.LookAt(target); // Always look at the target
		}

	}
}