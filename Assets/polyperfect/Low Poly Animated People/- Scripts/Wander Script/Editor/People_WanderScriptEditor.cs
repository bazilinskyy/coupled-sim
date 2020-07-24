using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace PolyPerfect
{
    [CustomEditor(typeof(People_WanderScript))]
    [CanEditMultipleObjects]
    public class People_WanderScriptEditor : Common_WanderScriptEditor { }
}