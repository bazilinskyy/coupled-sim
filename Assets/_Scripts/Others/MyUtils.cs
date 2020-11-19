using System;
using UnityEngine;
using Varjo;
public static class MyUtils
{
	public static GameObject GetPlayer()
    {
        return GameObject.FindGameObjectWithTag("Player");
    }

    public static ExperimentInput GetExperimentInput()
    {
        return GameObject.FindGameObjectWithTag("Player").GetComponent<ExperimentInput>();
    }

    public static Vector3 TransformToWorldAxis(Vector3 gaze, Vector3 gazePosition)
    {
        Vector4 gazeH = new Vector4(gaze.x, gaze.y, gaze.z, 1);
        Vector3 xAxis = VarjoManager.Instance.HeadTransform.right;
        Vector3 yAxis = VarjoManager.Instance.HeadTransform.up;
        Vector3 zAxis = VarjoManager.Instance.HeadTransform.forward;

        float x = Vector4.Dot(new Vector4(xAxis.x, yAxis.x, zAxis.x, gazePosition.x), gazeH);
        float y = Vector4.Dot(new Vector4(xAxis.y, yAxis.y, zAxis.y, gazePosition.y), gazeH);
        float z = Vector4.Dot(new Vector4(xAxis.z, yAxis.z, zAxis.z, gazePosition.z), gazeH);


        return new Vector3(x, y, z);
    }
    public static Vector3 TransformToWorldAxis(double[] gaze, double[] gazePositionDouble)
    {
        Vector4 gazeH = new Vector4((float)gaze[0], (float)gaze[1], (float)gaze[2], 1);
        Vector3 gazePosition = new Vector3((float)gazePositionDouble[0], (float)gazePositionDouble[1], (float)gazePositionDouble[2]);

        Vector3 xAxis = VarjoManager.Instance.HeadTransform.right;
        Vector3 yAxis = VarjoManager.Instance.HeadTransform.up;
        Vector3 zAxis = VarjoManager.Instance.HeadTransform.forward;

        float x = Vector4.Dot(new Vector4(xAxis.x, yAxis.x, zAxis.x, gazePosition.x), gazeH);
        float y = Vector4.Dot(new Vector4(xAxis.y, yAxis.y, zAxis.y, gazePosition.y), gazeH);
        float z = Vector4.Dot(new Vector4(xAxis.z, yAxis.z, zAxis.z, gazePosition.z), gazeH);

        return new Vector3(x, y, z);
    }

}
