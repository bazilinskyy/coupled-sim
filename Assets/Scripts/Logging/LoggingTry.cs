using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

public class LoggingTry// : MonoBehaviour
{
    // Data to retrieve
    PlayerSystem _playerSystem;
    AICarSyncSystem _aiCarSystem;

    public LoggingTry(PlayerSystem playerSys, AICarSyncSystem aiCarSystem)
    {
        _playerSystem = playerSys;
        _aiCarSystem = aiCarSystem;
    }

    // Variables to log
    private int trialnr;

    private float car_pos;
    private float car_vel;
    private float car_acc;

    // Create CSV struct
    private List<string[]> rowData = new List<string[]>();

    // Initialization
    public void BeginLog()
    {
        // Creating First row of titles manually.
        string[] rowDataTemp = new string[3];
        rowDataTemp[0] = "Car pos.x";
        rowDataTemp[1] = "Car pos.y";
        rowDataTemp[2] = "Car pos.z";
        rowData.Add(rowDataTemp);

        // You can add up the values in as many cells as you want.
        for (int i = 0; i < 10; i++)
        {
            rowDataTemp = new string[3];
            rowDataTemp[0] = "Sushanta" + i; // name
            rowDataTemp[1] = "" + i; // ID
            rowDataTemp[2] = "$" + UnityEngine.Random.Range(5000, 10000); // Income
            rowData.Add(rowDataTemp);
        }

        string[][] output = new string[rowData.Count][];

        for (int i = 0; i < output.Length; i++)
        {
            output[i] = rowData[i];
        }

        int length = output.GetLength(0);
        string delimiter = ",";

        StringBuilder sb = new StringBuilder();

        for (int index = 0; index < length; index++)
            sb.AppendLine(string.Join(delimiter, output[index]));

        string filePath = getPath();

        StreamWriter outStream = System.IO.File.CreateText(filePath);
        outStream.WriteLine(sb);
        outStream.Close();
    }

    // Create first row 
    void FirstRowCreator()
    {
        // Creating First row of titles manually.
        string[] rowDataTemp = new string[3];
        rowDataTemp[0] = "Car pos.x";
        rowDataTemp[1] = "Car pos.y";
        rowDataTemp[2] = "Car pos.z";
        rowData.Add(rowDataTemp);
    }
    // Following method is used to retrive the relative path as device platform
    private string getPath()
    {
        // Create logging directory
        if (!Directory.Exists("CSV"))
        {
            Directory.CreateDirectory("CSV");
        }
        // Get path
        //string path = string.Format("{0}{1:yyyyMMddHHmmssfff}{2}", @"C:\Users\DhrCS\Documents\Github\coupled-sim\SavedData\", DateTime.Now, "_MainScene.txt");       // Filename of the textfile
        string path = string.Format(@"C:\Users\DhrCS\Documents\Github\coupled-sim\CSV\Saved_data.csv");       // Filename of the textfile
        return path;
        /*#if UNITY_EDITOR
            return Application.dataPath + "/CSV/" + "Saved_data.csv";
        #else
            return Application.dataPath +"/"+"Saved_data.csv";
        #endif*/
    }
}
