using System;
using UnityEngine;


public static class ExtensionMethods
{
    public static void ZeroOut(this Transform input)
    {
        input.position = Vector3.zero;
        input.rotation = Quaternion.identity;
    }


    public static Vector3 Flatten(this Vector3 input)
    {
        return new Vector3(input.x, 0, input.z);
    }


    /// <summary>
    ///     Find by tag the transform of (sub)child in a given parent, recursively.
    ///     Adapted from: https://forum.unity.com/threads/solved-find-a-child-by-name-searching-all-subchildren.40684/
    /// </summary>
    public static Transform FindChildByTag(this Transform transform, string tag)
    {
        if (transform.tag.Equals(tag))
        {
            return transform;
        }

        foreach (Transform child in transform)
        {
            var result = FindChildByTag(child, tag);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }


    public static void PlayClip(this AudioSource source, AudioClip clip)
    {
        source.clip = clip;
        source.Play();
    }
    
    
    /// <summary>
    ///     This rounds a float to specified decimals: otherwise issues to round .5 values (e.g. 3.5 to 4, or 6.555 to 6.56)
    /// </summary>
    /// <param name="originalValue"></param>
    /// <param name="decimals"></param>
    /// <returns></returns>
    public static float RoundCorrectly(this float originalValue, int decimals)
    {
        // Standard Rounding gave problems when rounding values like 0.5 / 1.12125 / 2.45 etc to 1 / 1.1213 / 2.5 respectively
        // Value needs to be cast to decimal, and MidpointRounding needs to be set to AwayFromZero to fix that issue:
        var originalAsDecimal = (decimal) originalValue; // Float needs to be cast to decimal. Does not work properly when using double.
        const MidpointRounding midwayRounding = MidpointRounding.AwayFromZero; // As per: https://stackoverflow.com/questions/37290845/incorrect-result-of-math-round-function-in-vb-net  /// And: https://docs.microsoft.com/en-us/dotnet/api/system.midpointrounding?redirectedfrom=MSDN&view=net-6.0

        var rounded = (float) Math.Round(originalAsDecimal, decimals, midwayRounding);

        return rounded;
    }
}