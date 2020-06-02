// Copyright 2019 Varjo Technologies Oy. All rights reserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

namespace VarjoExample
{
    /// <summary>
    /// When user presses application button or defined key send gaze calibration request.
    /// </summary>
    public class VarjoGazeCalibrationRequest : MonoBehaviour
    {
        public VarjoPlugin.GazeCalibrationParameters[] parameters;

        public enum CalibrationType
        {
            LEGACY,
            FAST
        }

        public enum OutputFilterType
        {
            STANDARD,
            NONE
        }

        [Header("Keyboard key to request calibration")]
        public KeyCode key = KeyCode.Space;

        [Header("Should application button request calibration")]
        public bool useApplicationButton = true;

        [Header("Calibration parameters")]
        public bool useCalibrationParameters = false;
        public CalibrationType calibrationType = CalibrationType.LEGACY;
        public OutputFilterType outputFilterType = OutputFilterType.STANDARD;

        void Update()
        {
            if (VarjoManager.Instance.IsLayerVisible())
            {
                if (Input.GetKeyDown(key))
                {
                    RequestGazeCalibration();
                }
            }

            if(VarjoManager.Instance.GetButtonDown() && useApplicationButton)
            {
                RequestGazeCalibration();
            }
        }

        void RequestGazeCalibration()
        {
            if (!useCalibrationParameters)
            {
                VarjoPlugin.RequestGazeCalibration();
            }
            else
            {
                parameters = new VarjoPlugin.GazeCalibrationParameters[2];

                parameters[0] = new VarjoPlugin.GazeCalibrationParameters();
                parameters[0].key = "GazeCalibrationType";
                parameters[0].value = calibrationType == CalibrationType.LEGACY ? "Legacy" : "Fast";

                parameters[1] = new VarjoPlugin.GazeCalibrationParameters();
                parameters[1].key = "OutputFilterType";
                parameters[1].value = outputFilterType == OutputFilterType.STANDARD ? "Standard" : "None";

                VarjoPlugin.RequestGazeCalibrationWithParameters(parameters);
            }
        }
    }
}
