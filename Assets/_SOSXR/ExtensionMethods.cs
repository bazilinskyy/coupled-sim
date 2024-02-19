using UnityEngine;


public static class ExtensionMethods
{
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
}