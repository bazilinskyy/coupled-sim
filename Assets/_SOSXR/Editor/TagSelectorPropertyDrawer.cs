using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;


/// <summary>
///     Original by DYLAN ENGELMAN http://jupiterlighthousestudio.com/custom-inspectors-unity/
///     Altered by Brecht Lecluyse https://www.brechtos.com
///     From: https://www.brechtos.com/tagselectorattribute/
/// </summary>
[CustomPropertyDrawer(typeof(TagSelectorAttribute))]
public class TagSelectorPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);

            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        if (attribute is TagSelectorAttribute {UseDefaultTagFieldDrawer: true} attrib)
        {
            property.stringValue = EditorGUI.TagField(position, label, property.stringValue);
        }
        else
        {
            //generate the taglist + custom tags
            var tagList = new List<string> {"<NoTag>"};
            tagList.AddRange(InternalEditorUtility.tags);
            var propertyString = property.stringValue;
            var index = -1;

            if (propertyString != "")
            {
                //check if there is an entry that matches the entry and get the index
                //we skip index 0 as that is a special custom case
                for (var i = 1; i < tagList.Count; i++)
                {
                    if (tagList[i] == propertyString)
                    {
                        index = i;

                        break;
                    }
                }
            }
            else
            {
                //The tag is empty
                index = 0; //first index is the special <notag> entry
            }

            //Draw the popup box with the current selected index
            index = EditorGUI.Popup(position, label.text, index, tagList.ToArray());

            //Adjust the actual string value of the property based on the selection
            if (index == 0)
            {
                property.stringValue = "";
            }
            else if (index >= 1)
            {
                property.stringValue = tagList[index];
            }
            else
            {
                property.stringValue = "";
            }
        }

        EditorGUI.EndProperty();
    }
}