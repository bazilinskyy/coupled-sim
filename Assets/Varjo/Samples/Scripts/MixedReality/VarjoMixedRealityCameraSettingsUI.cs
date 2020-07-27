using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Varjo;

public class VarjoMixedRealityCameraSettingsUI : MonoBehaviour {

    public VarjoMixedRealityCameraSettings settings;

    public Dropdown exposureTimeMode;
    public Dropdown exposureTimeValue;
    public Dropdown ISOValueMode;
    public Dropdown ISOValue;
    public Dropdown whiteBalanceMode;
    public Dropdown whiteBalanceValue;
    public Dropdown flickerCompensationMode;
    public Dropdown flickerCompensationValue;
    public Dropdown sharpnessMode;
    public Dropdown sharpnessValue;

    void Start () {
        UpdateModes();
        UpdateValues();
	}

    public void UpdateModes()
    {
        exposureTimeMode.ClearOptions();
        exposureTimeMode.AddOptions(ModeOptionStrings(settings.exposureTimeModeOptions));
        exposureTimeMode.value = OptionIndex(settings.exposureTimeModeOptions, settings.exposureTimeMode);

        ISOValueMode.ClearOptions();
        ISOValueMode.AddOptions(ModeOptionStrings(settings.ISOValueModeOptions));
        ISOValueMode.value = OptionIndex(settings.ISOValueModeOptions, settings.ISOValueMode);

        whiteBalanceMode.ClearOptions();
        whiteBalanceMode.AddOptions(ModeOptionStrings(settings.whiteBalanceModeOptions));
        whiteBalanceMode.value = OptionIndex(settings.whiteBalanceModeOptions, settings.whiteBalanceMode);

        flickerCompensationMode.ClearOptions();
        flickerCompensationMode.AddOptions(ModeOptionStrings(settings.flickerCompensationModeOptions));
        flickerCompensationMode.value = OptionIndex(settings.flickerCompensationModeOptions, settings.flickerCompensationMode);

        sharpnessMode.ClearOptions();
        sharpnessMode.AddOptions(ModeOptionStrings(settings.sharpnessModeOptions));
        sharpnessMode.value = OptionIndex(settings.sharpnessModeOptions, settings.sharpnessMode);
    }

    public void UpdateValues()
    {
        exposureTimeValue.ClearOptions();
        exposureTimeValue.AddOptions(ValueOptionStrings(settings.exposureTimeOptions));
        exposureTimeValue.value = OptionIndex(settings.exposureTimeOptions, settings.exposureTime);
        exposureTimeValue.interactable = settings.exposureTimeMode.Equals(VarjoCameraPropertyMode.Manual);

        ISOValue.ClearOptions();
        ISOValue.AddOptions(ValueOptionStrings(settings.ISOValueOptions));
        ISOValue.value = OptionIndex(settings.ISOValueOptions, settings.ISOValue);
        ISOValue.interactable = settings.ISOValueMode.Equals(VarjoCameraPropertyMode.Manual);

        whiteBalanceValue.ClearOptions();
        whiteBalanceValue.AddOptions(ValueOptionStrings(settings.whiteBalanceOptions));
        whiteBalanceValue.value = OptionIndex(settings.whiteBalanceOptions, settings.whiteBalance);
        whiteBalanceValue.interactable = settings.whiteBalanceMode.Equals(VarjoCameraPropertyMode.Manual);

        flickerCompensationValue.ClearOptions();
        flickerCompensationValue.AddOptions(ValueOptionStrings(settings.flickerCompensationOptions));
        flickerCompensationValue.value = OptionIndex(settings.flickerCompensationOptions, settings.flickerCompensation);
        flickerCompensationValue.interactable = settings.flickerCompensationMode.Equals(VarjoCameraPropertyMode.Manual);

        sharpnessValue.ClearOptions();
        sharpnessValue.AddOptions(ValueOptionStrings(settings.sharpnessOptions));
        sharpnessValue.value = OptionIndex(settings.sharpnessOptions, settings.sharpness);
        sharpnessValue.interactable = settings.sharpnessMode.Equals(VarjoCameraPropertyMode.Manual);
    }

    public void SetExposureTimeMode()
    {
        SetMode(VarjoCameraPropertyType.ExposureTime, (VarjoCameraPropertyMode)settings.exposureTimeModeOptions[exposureTimeMode.value]);
    }

    public void SetISOValueMode()
    {
        SetMode(VarjoCameraPropertyType.ISOValue, (VarjoCameraPropertyMode)settings.ISOValueModeOptions[ISOValueMode.value]);
    }

    public void SetWhiteBalanceMode()
    {
        SetMode(VarjoCameraPropertyType.WhiteBalance, (VarjoCameraPropertyMode)settings.whiteBalanceModeOptions[whiteBalanceMode.value]);
    }

    public void SetFlickerCompensationMode()
    {
        SetMode(VarjoCameraPropertyType.FlickerCompensation, (VarjoCameraPropertyMode)settings.flickerCompensationModeOptions[flickerCompensationMode.value]);
    }

    public void SetSharpnessMode()
    {
        SetMode(VarjoCameraPropertyType.Sharpness, (VarjoCameraPropertyMode)settings.sharpnessModeOptions[sharpnessMode.value]);
    }

    public void SetMode(VarjoCameraPropertyType type, VarjoCameraPropertyMode mode)
    {
        settings.SetPropertyMode(type, mode);
        UpdateValues();
    }

    public void SetExposureTimeValue()
    {
        SetValue(VarjoCameraPropertyType.ExposureTime, (VarjoCameraPropertyValue)settings.exposureTimeOptions[exposureTimeValue.value]);
    }

    public void SetISOValue()
    {
        SetValue(VarjoCameraPropertyType.ISOValue, (VarjoCameraPropertyValue)settings.ISOValueOptions[ISOValue.value]);
    }

    public void SetWhiteBalanceValue()
    {
        SetValue(VarjoCameraPropertyType.WhiteBalance, (VarjoCameraPropertyValue)settings.whiteBalanceOptions[whiteBalanceValue.value]);
    }

    public void SetFlickerCompensationValue()
    {
        SetValue(VarjoCameraPropertyType.FlickerCompensation, (VarjoCameraPropertyValue)settings.flickerCompensationOptions[flickerCompensationValue.value]);
    }

    public void SetSharpnessValue()
    {
        SetValue(VarjoCameraPropertyType.Sharpness, (VarjoCameraPropertyValue)settings.sharpnessOptions[sharpnessValue.value]);
    }

    public void SetValue(VarjoCameraPropertyType type, VarjoCameraPropertyValue value)
    {
        settings.SetPropertyValue(type, value);
    }

    public void ResetExposureTime()
    {
        ResetValue(VarjoCameraPropertyType.ExposureTime);
    }

    public void ResetISOValue()
    {
        ResetValue(VarjoCameraPropertyType.ISOValue);
    }

    public void ResetWhiteBalance()
    {
        ResetValue(VarjoCameraPropertyType.WhiteBalance);
    }

    public void ResetFlickerCompensation()
    {
        ResetValue(VarjoCameraPropertyType.FlickerCompensation);
    }

    public void ResetSharpness()
    {
        ResetValue(VarjoCameraPropertyType.Sharpness);
    }

    public void ResetValue(VarjoCameraPropertyType type)
    {
        settings.ResetProperty(type);
        UpdateModes();
        UpdateValues();
    }

    public void ResetAll()
    {
        settings.ResetAll();
        UpdateModes();
        UpdateValues();
    }

    private List<string> ModeOptionStrings(VarjoCameraPropertyMode[] modes)
    {
        List<string> result = new List<string>();
        foreach (var mode in modes)
        {
            result.Add(((VarjoCameraPropertyMode)mode).ToString());
        }
        return result;
    }

    private List<string> ValueOptionStrings(int[] options)
    {
        List<string> result = new List<string>();
        foreach (var option in options)
        {
            result.Add(option.ToString());
        }
        return result;
    }

    private List<string> ValueOptionStrings(VarjoCameraPropertyValue[] options)
    {
        List<string> result = new List<string>();
        VarjoCameraPropertyDataType type = options[0].type;
        foreach (var option in options)
        {
            switch(type)
            {
                case VarjoCameraPropertyDataType.Bool:
                    result.Add(option.boolValue.ToString());
                    break;
                case VarjoCameraPropertyDataType.Double:
                    result.Add(option.doubleValue.ToString());
                    break;
                case VarjoCameraPropertyDataType.Int:
                    result.Add(option.intValue.ToString());
                    break;
                default:
                    break;
            }
        }
        return result;
    }

    private int OptionIndex(VarjoCameraPropertyValue[] values, VarjoCameraPropertyValue value)
    {
        return Array.IndexOf(values, value);
    }

    private int OptionIndex(VarjoCameraPropertyMode[] modes, VarjoCameraPropertyMode mode)
    {
        return Array.IndexOf(modes, mode);
    }
}
