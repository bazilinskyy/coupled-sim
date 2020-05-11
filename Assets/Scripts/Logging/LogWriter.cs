// Adapted from Koen de Clercq's Log Writer - February 2019.
// Adapted from Leo 

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;


public class LogWriter : MonoBehaviour
{
    public int practise;
    private string info;
    private string data;
    private string ovr;
    private string stime;
    //float[] timerAi = new float[10];
    //float timeri;
    Boolean thebutton = false;
    public GameObject CarSpawner;
    private float Speed;
    private float Acceleration;
    private float distanceTravelled = 0f;
    private float lastPosition;
    private int layer;
    private int trial;
    private int eHMI;
    //private int ParticipantNumber = StoredParticipantNumber.ParticipantNumber;

    // System time variables
    private Time SystemTime;                                                                                 // Call the system time
    private int SystemTimeHour;                                                                              // Create variable time in hours
    private int SystemTimeMinute;                                                                            // Create variable time in minutes
    private int SystemTimeSecond;                                                                            // Create variable time in seconds
    private int SystemTimeMillisecond;                                                                       // Create variable time in milliseconds

    // Sound variables
    public float RmsValue;
    public float DbValue;
    public float PitchValue;
    private const int QSamples = 256;
    private const float RefValue = 0.1f;
    private const float Threshold = 0f;
    float[] _samples;
    private float[] _spectrum;
    private float _fSample;

    // Directory related variables
    string path = string.Format("{0}{1:yyyyMMddHHmmssfff}{2}", @"C:\Users\rstle\Desktop\Unity_Leo\SavedData\", DateTime.Now, "_MainScene.txt");       // Filename of the textfile

    // Define which tags of objects to trace.
    private string[] tagsTrace = {
            "Car",
            //"eHMI",
            //"MainCamera",
            "PartCamera" //,
            //"Active",
            //"Non_active"
        };

    // Initialisation
    void Start()
    {
        using (StreamWriter sw = File.CreateText(path))
        {
            sw.WriteLine(@"simulation_time;system_time;tag;name;pos_x;pos_y;pos_z;euler_x;euler_y;euler_z;speed;acc;travel;layer;eHMI_num;trail;db;rms;pitch");
        }
        Debug.Log("Main Scene LogWriter loaded");
        _samples = new float[QSamples];
        _spectrum = new float[QSamples];
        _fSample = AudioSettings.outputSampleRate;
    }

    void Update()
    {
        CarSpawner = GameObject.Find("CarSpawner");
        layer = CarSpawner.GetComponent<CarSpawner>().layer;
        trial = CarSpawner.GetComponent<CarSpawner>().trial;
        eHMI = CarSpawner.GetComponent<CarSpawner>().eHMI;
        SystemTimeHour = System.DateTime.Now.Hour;                                                              // Assign system time hours
        SystemTimeMinute = System.DateTime.Now.Minute;                                                          // Assign system time minutes
        SystemTimeSecond = System.DateTime.Now.Second;                                                          // Assign system time seconds
        SystemTimeMillisecond = System.DateTime.Now.Millisecond;                                                // Assign system time milliseconds           
        //Debug.Log("System time:" + SystemTimeHour + ":" + SystemTimeMinute + ":" + SystemTimeSecond + "." + SystemTimeMillisecond);         // Display in console system time to be logged
        GameObject[] Car = GameObject.FindGameObjectsWithTag("Merc");
        foreach (GameObject Merc in Car)
        {
            Acceleration = (Merc.transform.GetComponent<Rigidbody>().velocity.magnitude - (Speed / 3.6f)) / Time.fixedDeltaTime;
            Speed = Merc.transform.GetComponent<Rigidbody>().velocity.magnitude * 3.6f;
            distanceTravelled += Mathf.Abs((Merc.transform.GetComponent<Rigidbody>().position.magnitude) - lastPosition);
            lastPosition = Merc.transform.GetComponent<Rigidbody>().position.magnitude;
        }
        AnalyzeSound();
    }

    void AnalyzeSound()
    {
        AudioListener.GetOutputData(_samples, 0);                   // fill array with samples
        int i;
        float sum = 0;
        for (i = 0; i < QSamples; i++)
        {
            sum += _samples[i] * _samples[i];                       // sum squared samples
        }
        RmsValue = Mathf.Sqrt(sum / QSamples);                      // rms = square root of average
        DbValue = 20 * Mathf.Log10(RmsValue / RefValue);            // calculate dB

        if (DbValue < -160)                                         // clamp it to -160dB min
        {
            DbValue = -160;                                         // get sound spectrum
        }

        AudioListener.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        float maxV = 0;
        var maxN = 0;
        for (i = 0; i < QSamples; i++)
        { // find max 
            if (!(_spectrum[i] > maxV) || !(_spectrum[i] > Threshold))
                continue;

            maxV = _spectrum[i];
            maxN = i; // maxN is the index of max
        }
        float freqN = maxN; // pass the index to a float variable
        if (maxN > 0 && maxN < QSamples - 1)
        { // interpolate index using neighbours
            var dL = _spectrum[maxN - 1] / _spectrum[maxN];
            var dR = _spectrum[maxN + 1] / _spectrum[maxN];
            freqN += 0.5f * (dR * dR - dL * dL);
        }
        PitchValue = freqN * (_fSample / 2) / QSamples; // convert index to frequency
    }

    void LateUpdate()
    {
        WriteToLog();
    }

    // Update is called according to LateUpdate().
    void WriteToLog()
    {

        using (StreamWriter sw = File.AppendText(path))
        {
            for (int i = 0; i < tagsTrace.Length; i++)

            {
                if (GameObject.FindGameObjectsWithTag(tagsTrace[i]).Length != 0)
                {

                    GameObject[] objects = GameObject.FindGameObjectsWithTag(tagsTrace[i]);

                    foreach (GameObject obj in objects)
                    {
                        // Define what information you want to extract.
                        if (i == 0 && layer < 18 && layer >= 8)     //non yielding
                        {
                            info = String.Format("{0};{1};{2};{3};{4};{5};{6}", "Carny", obj.name, obj.transform.position.ToString("f3"), obj.transform.eulerAngles.ToString("f2"), Speed.ToString("f3"), Acceleration.ToString("f3"), distanceTravelled.ToString("f3"));
                        }
                        else if (i == 0 && layer >= 18 && layer < 28)  //yielding
                        {
                            info = String.Format("{0};{1};{2};{3};{4};{5};{6}", "Cary", obj.name, obj.transform.position.ToString("f3"), obj.transform.eulerAngles.ToString("f2"), Speed.ToString("f3"), Acceleration.ToString("f3"), distanceTravelled.ToString("f3"));
                        }
                        //else if (i == 1 && layer < 18 && layer >= 8)     //non yielding
                        //{
                        //    info = String.Format("{0};{1};{2};{3};{4}", "eHMIy", obj.name, obj.transform.position.ToString("f3"), obj.transform.eulerAngles.ToString("f2"), Speed.ToString("f3"), Acceleration.ToString("f3"), distanceTravelled.ToString("f3"));
                        //}
                        //else if (i == 1 && layer >= 18 && layer < 28)  //yielding
                        //{
                        //    info = String.Format("{0};{1};{2};{3};{4}", "eHMIny", obj.name, obj.transform.position.ToString("f3"), obj.transform.eulerAngles.ToString("f2"), Speed.ToString("f3"), Acceleration.ToString("f3"), distanceTravelled.ToString("f3"));
                        //}
                        else
                        {
                            info = String.Format("{0};{1};{2};{3};{4};{5};{6}", obj.tag, obj.name, obj.transform.position.ToString("f3"), obj.transform.eulerAngles.ToString("f2"), 0f, 0f, 0f);
                        }


                        // Clean up formatting.
                        info = info.Replace(", ", ";");
                        info = info.Replace("(", "");
                        info = info.Replace(")", "");

                        stime = String.Format("{0}", SystemTimeHour + ":" + SystemTimeMinute + ":" + SystemTimeSecond + "." + SystemTimeMillisecond);
                        data = String.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8}", Time.time, stime, info, layer, eHMI, trial, DbValue, RmsValue, PitchValue); // In Matlab we add here the Varjo logging data
                        data = data.Replace(",", ".");
                        sw.WriteLine(data);
                    }
                }
            }

            // Read Input on Oculus Remote.
            //OVRInput.Update();
            //ovr = String.Format("{0};{1};{2}", Time.time, "Input", OVRInput.Get(OVRInput.Button.One));
            //ovr = String.Format("{0};{1};{2}", Time.time, "Input", Input.GetMouseButtonDown(0));


            // the F5 / escape button has been disabled on the r400 Logitech
            if (Input.GetKey(KeyCode.PageDown) == true || Input.GetKey(KeyCode.PageUp) == true || Input.GetKey(KeyCode.Period) == true)
            {
                thebutton = true;
            }
            else
            {
                thebutton = false;
            }
            ovr = String.Format("{0};{1};{2};{3}", Time.time, stime, "Input", thebutton);
            ovr = ovr.Replace(",", ".");
            //ovr = String.Format("{0};{1};{2};{3};{4};", Time.time, "Input", thebutton, "Partno", ParticipantNumber);
            /* ovr = String.Format("{0};{1};{2}", Time.time, "Input", Input.GetKey(KeyCode.PageUp));
            ovr = String.Format("{0};{1};{2}", Time.time, "Input", Input.GetKey(KeyCode.Period));
            ovr = String.Format("{0};{1};{2}", Time.time, "Input", Input.GetKey(KeyCode.F5));
            ovr = String.Format("{0};{1};{2}", Time.time, "Input", Input.GetKey(KeyCode.Escape));
            */

            // Activate during practise session (write value of 1) and deactivate during final experiments (write value of 0).
            if (practise == 1)
            {
                //Debug.Log(OVRInput.Get(OVRInput.Button.One)); 
                Debug.Log(thebutton);
            }

            sw.WriteLine(ovr);
        }
    }
}