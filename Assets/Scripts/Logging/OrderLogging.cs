using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class OrderLogging : MonoBehaviour
{
    List<int> _block;
    int _participantNr;

    public void BeginLog(List<int> block, int participantNr)
    {
        Debug.LogError(PersistentManager.Instance.LogOrder);
        if (PersistentManager.Instance.LogOrder == true)
        {
            _block = block;
            _participantNr = participantNr;

            // Create logging directory
            if (!Directory.Exists("ExperimentListLogs"))
            {
                Directory.CreateDirectory("ExperimentListLogs");
            }
            string filePath = getPath();
            StreamWriter writer = new StreamWriter(filePath);

            // Header
            string head = "ExperimentList,ParticipantNr";
            writer.WriteLine(head);

            // Convert int list to string
            string.Join<int>(",", _block);
            for (int i = 0; i < _block.Count; i++)
            {
                if (i < _block.Count) writer.Write(_block[i]);
                writer.Write(",");
                writer.Write(_participantNr);
                writer.Write(System.Environment.NewLine);
            }
            writer.Flush();
            writer.Close();
            PersistentManager.Instance.LogOrder = false;
        }
    }

    private string getPath()
    {
        string temp = Application.dataPath;
        string root = temp.Remove(temp.Length - 7);
        string dirName = "/ExperimentListLogs/";
        // Define filename
        string filename = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + "_Participant_" + _participantNr + ".csv";
#if UNITY_EDITOR
        return root + dirName + filename;
#elif UNITY_ANDROID
        return Application.persistentDataPath+filename;
#elif UNITY_IPHONE
        return Application.persistentDataPath+"/"+filename;
#else
        return Application.dataPath +"/"+filename;
#endif
    }
}
