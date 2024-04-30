using UnityEngine;


/// <summary>
///     From: https://www.brechtos.com/tagselectorattribute/
///     Usage:
///     [TagSelector] public string TagFilter = "";
///     [TagSelector] public string[] TagFilterArray = new string[] { };
/// </summary>
public class TagSelectorAttribute : PropertyAttribute
{
    public bool UseDefaultTagFieldDrawer = false;
}