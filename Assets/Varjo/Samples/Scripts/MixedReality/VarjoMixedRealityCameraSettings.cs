using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

public class VarjoMixedRealityCameraSettings : MonoBehaviour {

    [Header("Camera Settings")]
    [Header("Exposure Time")]
    public VarjoCameraPropertyValue exposureTime;
    public VarjoCameraPropertyValue[] exposureTimeOptions;
    [NonSerialized]
    public VarjoCameraPropertyMode exposureTimeMode;
    [NonSerialized]
    public VarjoCameraPropertyMode[] exposureTimeModeOptions;

    [Header("ISO Value")]
    public VarjoCameraPropertyValue ISOValue;
    public VarjoCameraPropertyValue[] ISOValueOptions;
    [NonSerialized]
    public VarjoCameraPropertyMode ISOValueMode;
    [NonSerialized]
    public VarjoCameraPropertyMode[] ISOValueModeOptions;

    [Header("White Balance")]
    public VarjoCameraPropertyValue whiteBalance;
    public VarjoCameraPropertyValue[] whiteBalanceOptions;
    [NonSerialized]
    public VarjoCameraPropertyMode whiteBalanceMode;
    [NonSerialized]
    public VarjoCameraPropertyMode[] whiteBalanceModeOptions;

    [Header("Flicker Compensation")]
    public VarjoCameraPropertyValue flickerCompensation;
    public VarjoCameraPropertyValue[] flickerCompensationOptions;
    [NonSerialized]
    public VarjoCameraPropertyMode flickerCompensationMode;
    [NonSerialized]
    public VarjoCameraPropertyMode[] flickerCompensationModeOptions;

    private void OnEnable()
    {
        UpdateValues();
        UpdateModes();
        UpdateOptions();
        UpdateModeOptions();
    }

    public void UpdateValues()
    {
        exposureTime = GetPropertyValue(VarjoCameraPropertyType.ExposureTime);
        ISOValue = GetPropertyValue(VarjoCameraPropertyType.ISOValue);
        whiteBalance = GetPropertyValue(VarjoCameraPropertyType.WhiteBalance);
        flickerCompensation = GetPropertyValue(VarjoCameraPropertyType.FlickerCompensation);
    }

    public void UpdateModes()
    {
        exposureTimeMode = GetPropertyMode(VarjoCameraPropertyType.ExposureTime);
        ISOValueMode = GetPropertyMode(VarjoCameraPropertyType.ISOValue);
        whiteBalanceMode = GetPropertyMode(VarjoCameraPropertyType.WhiteBalance);
        flickerCompensationMode = GetPropertyMode(VarjoCameraPropertyType.FlickerCompensation);
    }

    public void UpdateOptions()
    {
        exposureTimeOptions = GetPropertyValues(VarjoCameraPropertyType.ExposureTime);
        ISOValueOptions = GetPropertyValues(VarjoCameraPropertyType.ISOValue);
        whiteBalanceOptions = GetPropertyValues(VarjoCameraPropertyType.WhiteBalance);
        flickerCompensationOptions = GetPropertyValues(VarjoCameraPropertyType.FlickerCompensation);
    }

    public void UpdateModeOptions()
    {
        exposureTimeModeOptions = GetPropertyModes(VarjoCameraPropertyType.ExposureTime);
        ISOValueModeOptions = GetPropertyModes(VarjoCameraPropertyType.ISOValue);
        whiteBalanceModeOptions = GetPropertyModes(VarjoCameraPropertyType.WhiteBalance);
        flickerCompensationModeOptions = GetPropertyModes(VarjoCameraPropertyType.FlickerCompensation);
    }

    public void SetPropertyMode(VarjoCameraPropertyType type, VarjoCameraPropertyMode mode)
    {
        VarjoPluginMR.LockCameraConfig();
        VarjoPluginMR.SetCameraPropertyMode(type, mode);
        UpdateModes();
        VarjoPluginMR.UnlockCameraConfig();
    }

    public void SetPropertyValue(VarjoCameraPropertyType type, VarjoCameraPropertyValue value)
    {
        VarjoPluginMR.LockCameraConfig();
        VarjoPluginMR.SetCameraPropertyValue(type, value);
        UpdateValues();
        VarjoPluginMR.UnlockCameraConfig();
    }

    public void ResetProperty(VarjoCameraPropertyType type)
    {
        VarjoPluginMR.LockCameraConfig();
        VarjoPluginMR.ResetCameraProperty(type);
        UpdateValues();
        UpdateModes();
        VarjoPluginMR.UnlockCameraConfig();
    }

    public void ResetAll()
    {
        VarjoPluginMR.LockCameraConfig();
        VarjoPluginMR.ResetCameraProperties();
        UpdateValues();
        UpdateModes();
        VarjoPluginMR.UnlockCameraConfig();
    }

    VarjoCameraPropertyMode GetPropertyMode(VarjoCameraPropertyType type)
    {
        VarjoCameraPropertyMode mode;
        if (VarjoPluginMR.GetCameraPropertyMode(type, out mode))
        {
            return mode;
        }
        return VarjoCameraPropertyMode.Off;
    }

    VarjoCameraPropertyValue GetPropertyValue(VarjoCameraPropertyType type)
    {
        VarjoCameraPropertyValue value;
        if (VarjoPluginMR.GetCameraPropertyValue(type, out value))
        {
            return value;
        }
        return default(VarjoCameraPropertyValue);
    }

    VarjoCameraPropertyValue[] GetPropertyValues(VarjoCameraPropertyType type)
    {
        List<VarjoCameraPropertyValue> list = new List<VarjoCameraPropertyValue>();
        VarjoCameraPropertyValue[] values = new VarjoCameraPropertyValue[VarjoPluginMR.GetCameraPropertyValueCount(type)];
        if (VarjoPluginMR.GetCameraPropertyValues(type, out list))
        {
            values = list.ToArray();
        }
        return values;
    }

    VarjoCameraPropertyMode[] GetPropertyModes(VarjoCameraPropertyType type)
    {
        List<VarjoCameraPropertyMode> list = new List<VarjoCameraPropertyMode>();
        VarjoCameraPropertyMode[] modes = new VarjoCameraPropertyMode[VarjoPluginMR.GetCameraPropertyModeCount(type)];
        if (VarjoPluginMR.GetCameraPropertyModes(type, out list))
        {
            modes = list.ToArray();
        }
        return modes;
    }
}
