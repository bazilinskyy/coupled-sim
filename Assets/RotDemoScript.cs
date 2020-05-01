//This example shows the difference between using Space.world and Space.self. Attach this script to a GameObject
//Enable or disable the checkbox in the Inspector before starting (depending on if you want world or self)
//Press play to see the GameObject rotating appropriately. Press space to switch between world and self.

using UnityEngine;

public class RotDemoScript : MonoBehaviour
{
    float m_Speed;
    public bool m_WorldSpace;

    void Start()
    {
        //Set the speed of the rotation
        m_Speed = 20.0f;
        //Rotate the GameObject a little at the start to show the difference between Space and Local
        transform.Rotate(60, 0, 60);
    }

    void Update()
    {
        //Rotate the GameObject in World Space if in the m_WorldSpace state
        if (m_WorldSpace)
            transform.Rotate(Vector3.up * m_Speed * Time.deltaTime, Space.World);
        //Otherwise, rotate the GameObject in local space
        else
            transform.Rotate(Vector3.up * m_Speed * Time.deltaTime, Space.Self);

        //Press the Space button to switch between world and local space states
        if (Input.GetKeyDown(KeyCode.Space))
        {
            //Make the current state switch to the other state
            m_WorldSpace = !m_WorldSpace;
            //Output the Current state to the console
            Debug.Log("World Space : " + m_WorldSpace.ToString());
        }
    }
}