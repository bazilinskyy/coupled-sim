using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CarMovementLoggerNew_4 : MonoBehaviour 
{
	// sample rate in Hz 
   private float samplingRate = 5f;
   private StreamWriter sw;
   Rigidbody rb;
   private string path;
   private string filename;
   private string logfile;
    float timer;

  private string headers =@"C = [";

   void Start() 
   {
       
       rb = GetComponent<Rigidbody>();
       path=Path.Combine(Application.dataPath,"ExperimentLogs_Pedestrian");
        if (!Directory.Exists(path))
        {        
        Directory.CreateDirectory(path);
        }

        timer = 0f;

        Debug.Log("Starting Log");
        filename = "Vehicle_" + rb + "_upto50" + ".txt";
        logfile = Path.Combine(path, filename);

        using (StreamWriter sw = File.CreateText(logfile))
        {
          sw.WriteLine(headers);
        }

    }

   //void OnTriggerEnter(Collider other)
   //{
   //    if(other.gameObject.CompareTag("start"))
   //    {
   //        Debug.Log("Starting Log");
   //        filename= "Vehicle_" + rb + "_upto50" + ".txt";
   //        logfile=Path.Combine(path,filename);
                                        
   //        using (StreamWriter sw = File.CreateText(logfile))
   //        {
   //            sw.WriteLine(headers);
   //         }
       
   //        InvokeRepeating("StartLog",0,1/samplingRate);
   //    }
       
   //    else if(other.gameObject.CompareTag("end"))
   //    {
   //        EndLog();
   //        Debug.Log("Ending Log");
   //    }
   //}

   public void StartLog()
   {
       if (File.Exists(logfile)==true)
       {
       using (StreamWriter sw = new StreamWriter(logfile,true))
       {
        sw.WriteLine($"{Time.timeSinceLevelLoad},{transform.position.x},{transform.position.y},{transform.position.z}");
       }
       }
   }   

   public void EndLog()
   {
       CancelInvoke();
   }

   void Update()
   {
        {
            timer += Time.deltaTime;
            if (timer <= 23f)
            {
                

                InvokeRepeating("StartLog", 0, 1);

            }
            if (timer>23f)
                {
                EndLog();
            }
        }
    }   

}
