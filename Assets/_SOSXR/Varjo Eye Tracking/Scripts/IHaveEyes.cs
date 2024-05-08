using UnityEngine;


/// <summary>
/// Implement this interface at VarjoEyeTracking when we're ready for eyeball madness.
/// </summary>
public interface IHaveEyes
{
    public Transform LeftEyeTransform { get; set; }
    public Transform RightEyeTransform { get; set; }
}