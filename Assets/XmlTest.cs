/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;using Varjo;
using System.Xml.Serialization;
using System.IO;
using System;
public class XmlTest : MonoBehaviour
{
    private string gazeDataFileName = "gazeData.xml";
    private string dataFolder = "Data";
    //singleton pattern
    public static XmlTest ins;
    private GazeContainer gazeData;
    // Update is called once per frame
    private void OnApplicationQuit()
    {
        SaveThis<GazeContainer>(gazeDataFileName, gazeData);
    }

    private void Awake()
    {
        gazeData = new GazeContainer();
    }

    void Update()
    {
        AddEyeTrackingData();
    }

    void SaveThis<T>(string fileName, object data)
    {
        //check if data makes sense
        Type dataType = data.GetType();
        if (!dataType.Equals(typeof(T))) { throw new Exception($"ERROR: data type and given type done match {dataType.FullName} != {typeof(T).FullName}..."); }

        XmlSerializer serializer = new XmlSerializer(typeof(T));
        //overwrites mydata.xml
        string filePath = string.Join("/", saveFolder(), fileName);
        //        string name = typeof(T).FullName;
        Debug.Log($"Saving {fileName} data to {filePath}...");

        FileStream stream = new FileStream(filePath, FileMode.Create);
        serializer.Serialize(stream, data);
        stream.Close();
    }

    private string saveFolder()
    {
        //Save folder will be .../unityproject/Data/subjectName-date/subjectName/navigationName

        string[] assetsFolderArray = Application.dataPath.Split('/'); //Gives .../unityproject/assest

        //emmit unityfolder/assets and keep root folder

        string[] baseFolderArray = new string[assetsFolderArray.Length - 2];
        for (int i = 0; i < (assetsFolderArray.Length - 2); i++) { baseFolderArray[i] = assetsFolderArray[i]; }

        string dateTime = DateTime.Now.ToString("MM-dd_HH-mm");

        string baseFolder = string.Join("/", baseFolderArray);
        string saveFolder = string.Join("/", baseFolder, dataFolder, $"gaze-test-{dateTime}", "test");
        Directory.CreateDirectory(saveFolder);

        return saveFolder;
    }
    private void AddEyeTrackingData()
    {
        VarjoPlugin.GazeData varjo_data = VarjoPlugin.GetGaze();

        if (varjo_data.status == VarjoPlugin.GazeStatus.VALID)
        {
            //Valid gaze data
            MyGazeData data = new MyGazeData();
            data.time = Time.time;
            data.frame = Time.frameCount;

            data.focusDistance = varjo_data.focusDistance;
            data.focusStability = varjo_data.focusStability;

            data.rightPupilSize = varjo_data.rightPupilSize;
            data.forward_right = ConvertToVector3(varjo_data.right.forward);
            data.position_right = ConvertToVector3(varjo_data.right.position);

            data.leftPupilSize = varjo_data.leftPupilSize;
            data.forward_left = ConvertToVector3(varjo_data.left.forward);
            data.position_left = ConvertToVector3(varjo_data.left.position);
            data.forward_combined = ConvertToVector3(varjo_data.gaze.forward);
            data.position_combined = ConvertToVector3(varjo_data.gaze.position);

            data.status = varjo_data.status;
            data.leftCalibrationQuality = VarjoPlugin.GetGazeCalibrationQuality().left;
            data.leftStatus = varjo_data.leftStatus;

            data.rightCalibrationQuality = VarjoPlugin.GetGazeCalibrationQuality().right;
            data.rightStatus = varjo_data.rightStatus;

            gazeData.dataList.Add(data);
        }
        else
        {
            //Invalid gaze data
            MyGazeData data = new MyGazeData();

            data.time = Time.time;
            data.frame = Time.frameCount;

            data.status = varjo_data.status;
            data.leftCalibrationQuality = VarjoPlugin.GetGazeCalibrationQuality().left;
            data.leftStatus = varjo_data.leftStatus;

            data.rightCalibrationQuality = VarjoPlugin.GetGazeCalibrationQuality().right;
            data.rightStatus = varjo_data.rightStatus;

            gazeData.dataList.Add(data);

        }
    }
    private Vector3 ConvertToVector3(double[] varjo_data)
    {
        return new Vector3((float)varjo_data[0], (float)varjo_data[1], (float)varjo_data[2]);
    }
}
*/