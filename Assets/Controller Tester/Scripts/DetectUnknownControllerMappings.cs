using UnityEngine;
using UnityEngine.UI;
using System.Collections;


public class DetectUnknownControllerMappings : MonoBehaviour {

    //axes
    public Text axis1Value;
    public Text axis2Value;
    public Text axis3Value;
    public Text axis4Value;
    public Text axis5Value;
    public Text axis6Value;
    public Text axis7Value;
    public Text axis8Value;
    public Text axis9Value;
    public Text axis10Value;
    public Text axis11Value;
    public Text axis12Value;
    public Text axis13Value;
    public Text axis14Value;
    public Text axis15Value;
    public Text axis16Value;
    public Text axis17Value;
    public Text axis18Value;
    public Text axis19Value;
    public Text axis20Value;
	public Text axis21Value;
	public Text axis22Value;
	public Text axis23Value;
	public Text axis24Value;
	public Text axis25Value;
	public Text axis26Value;
	public Text axis27Value;
	public Text axis28Value;

    //buttons
    public Text button0Value;
    public Text button1Value;
    public Text button2Value;
    public Text button3Value;
    public Text button4Value;
    public Text button5Value;
    public Text button6Value;
    public Text button7Value;
    public Text button8Value;
    public Text button9Value;
    public Text button10Value;
    public Text button11Value;
    public Text button12Value;
    public Text button13Value;
    public Text button14Value;
    public Text button15Value;
    public Text button16Value;
    public Text button17Value;
    public Text button18Value;
    public Text button19Value;

	
	// Update is called once per frame
	void Update () {

        
        //axis

        if (Input.GetAxis("Axis 1") > 0f)
            axis1Value.text = "positive";
        else if (Input.GetAxis("Axis 1") < 0f)
            axis1Value.text = "negative";
        else
            axis1Value.text = "";

        if (Input.GetAxis("Axis 2") > 0f)
            axis2Value.text = "positive";
        else if (Input.GetAxis("Axis 2") < 0f)
            axis2Value.text = "negative";
        else
            axis2Value.text = "";

        if (Input.GetAxis("Axis 3") > 0f)
            axis3Value.text = "positive";
        else if (Input.GetAxis("Axis 3") < 0f)
            axis3Value.text = "negative";
        else
            axis3Value.text = "";

        if (Input.GetAxis("Axis 4") > 0f)
            axis4Value.text = "positive";
        else if (Input.GetAxis("Axis 4") < 0f)
            axis4Value.text = "negative";
        else
            axis4Value.text = "";

        if (Input.GetAxis("Axis 5") > 0f)
            axis5Value.text = "positive";
        else if (Input.GetAxis("Axis 5") < 0f)
            axis5Value.text = "negative";
        else
            axis5Value.text = "";

        if (Input.GetAxis("Axis 6") > 0f)
            axis6Value.text = "positive";
        else if (Input.GetAxis("Axis 6") < 0f)
            axis6Value.text = "negative";
        else
            axis6Value.text = "";

        if (Input.GetAxis("Axis 7") > 0f)
            axis7Value.text = "positive";
        else if (Input.GetAxis("Axis 7") < 0f)
            axis7Value.text = "negative";
        else
            axis7Value.text = "";

        if (Input.GetAxis("Axis 8") > 0f)
            axis8Value.text = "positive";
        else if (Input.GetAxis("Axis 8") < 0f)
            axis8Value.text = "negative";
        else
            axis8Value.text = "";

        if (Input.GetAxis("Axis 9") > 0f)
            axis9Value.text = "positive";
        else if (Input.GetAxis("Axis 9") < 0f)
            axis9Value.text = "negative";
        else
            axis9Value.text = "";

        if (Input.GetAxis("Axis 10") > 0f)
            axis10Value.text = "positive";
        else if (Input.GetAxis("Axis 10") < 0f)
            axis10Value.text = "negative";
        else
            axis10Value.text = "";

        if (Input.GetAxis("Axis 11") > 0f)
            axis11Value.text = "positive";
        else if (Input.GetAxis("Axis 11") < 0f)
            axis11Value.text = "negative";
        else
            axis11Value.text = "";

        if (Input.GetAxis("Axis 12") > 0f)
            axis12Value.text = "positive";
        else if (Input.GetAxis("Axis 12") < 0f)
            axis12Value.text = "negative";
        else
            axis12Value.text = "";

        if (Input.GetAxis("Axis 13") > 0f)
            axis13Value.text = "positive";
        else if (Input.GetAxis("Axis 13") < 0f)
            axis13Value.text = "negative";
        else
            axis13Value.text = "";

        if (Input.GetAxis("Axis 14") > 0f)
            axis14Value.text = "positive";
        else if (Input.GetAxis("Axis 14") < 0f)
            axis14Value.text = "negative";
        else
            axis14Value.text = "";

        if (Input.GetAxis("Axis 15") > 0f)
            axis15Value.text = "positive";
        else if (Input.GetAxis("Axis 15") < 0f)
            axis15Value.text = "negative";
        else
            axis15Value.text = "";

        if (Input.GetAxis("Axis 16") > 0f)
            axis16Value.text = "positive";
        else if (Input.GetAxis("Axis 16") < 0f)
            axis16Value.text = "negative";
        else
            axis16Value.text = "";

        if (Input.GetAxis("Axis 17") > 0f)
            axis17Value.text = "positive";
        else if (Input.GetAxis("Axis 17") < 0f)
            axis17Value.text = "negative";
        else
            axis17Value.text = "";

        if (Input.GetAxis("Axis 18") > 0f)
            axis18Value.text = "positive";
        else if (Input.GetAxis("Axis 18") < 0f)
            axis18Value.text = "negative";
        else
            axis18Value.text = "";

        if (Input.GetAxis("Axis 19") > 0f)
            axis19Value.text = "positive";
        else if (Input.GetAxis("Axis 19") < 0f)
            axis19Value.text = "negative";
        else
            axis19Value.text = "";

        if (Input.GetAxis("Axis 20") > 0f)
            axis20Value.text = "positive";
        else if (Input.GetAxis("Axis 20") < 0f)
            axis20Value.text = "negative";
        else
            axis20Value.text = "";

		if (Input.GetAxis("Axis 21") > 0f)
			axis21Value.text = "positive";
		else if (Input.GetAxis("Axis 21") < 0f)
			axis21Value.text = "negative";
		else
			axis21Value.text = "";
		
		if (Input.GetAxis("Axis 22") > 0f)
			axis22Value.text = "positive";
		else if (Input.GetAxis("Axis 22") < 0f)
			axis22Value.text = "negative";
		else
			axis22Value.text = "";
		
		if (Input.GetAxis("Axis 23") > 0f)
			axis23Value.text = "positive";
		else if (Input.GetAxis("Axis 23") < 0f)
			axis23Value.text = "negative";
		else
			axis23Value.text = "";
		
		if (Input.GetAxis("Axis 24") > 0f)
			axis24Value.text = "positive";
		else if (Input.GetAxis("Axis 24") < 0f)
			axis24Value.text = "negative";
		else
			axis24Value.text = "";
		
		if (Input.GetAxis("Axis 25") > 0f)
			axis25Value.text = "positive";
		else if (Input.GetAxis("Axis 25") < 0f)
			axis25Value.text = "negative";
		else
			axis25Value.text = "";
		
		if (Input.GetAxis("Axis 26") > 0f)
			axis26Value.text = "positive";
		else if (Input.GetAxis("Axis 26") < 0f)
			axis26Value.text = "negative";
		else
			axis26Value.text = "";
		
		if (Input.GetAxis("Axis 27") > 0f)
			axis27Value.text = "positive";
		else if (Input.GetAxis("Axis 27") < 0f)
			axis27Value.text = "negative";
		else
			axis27Value.text = "";
		
		if (Input.GetAxis("Axis 28") > 0f)
			axis28Value.text = "positive";
		else if (Input.GetAxis("Axis 28") < 0f)
			axis28Value.text = "negative";
		else
			axis28Value.text = "";


        //buttons
        if (Input.GetKey(KeyCode.JoystickButton0) == true)
            button0Value.text = "pressed";
        else
            button0Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton1) == true)
            button1Value.text = "pressed";
        else
            button1Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton2) == true)
            button2Value.text = "pressed";
        else
            button2Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton3) == true)
            button3Value.text = "pressed";
        else
            button3Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton4) == true)
            button4Value.text = "pressed";
        else
            button4Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton5) == true)
            button5Value.text = "pressed";
        else
            button5Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton6) == true)
            button6Value.text = "pressed";
        else
            button6Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton7) == true)
            button7Value.text = "pressed";
        else
            button7Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton8) == true)
            button8Value.text = "pressed";
        else
            button8Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton9) == true)
            button9Value.text = "pressed";
        else
            button9Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton10) == true)
            button10Value.text = "pressed";
        else
            button10Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton11) == true)
            button11Value.text = "pressed";
        else
            button11Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton12) == true)
            button12Value.text = "pressed";
        else
            button12Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton13) == true)
            button13Value.text = "pressed";
        else
            button13Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton14) == true)
            button14Value.text = "pressed";
        else
            button14Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton15) == true)
            button15Value.text = "pressed";
        else
            button15Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton16) == true)
            button16Value.text = "pressed";
        else
            button16Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton17) == true)
            button17Value.text = "pressed";
        else
            button17Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton18) == true)
            button18Value.text = "pressed";
        else
            button18Value.text = "";

        if (Input.GetKey(KeyCode.JoystickButton19) == true)
            button19Value.text = "pressed";
        else
            button19Value.text = "";


	
	}
}
