using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace xsens
{
    /// <summary>
    /// This class is used to hold our prop settings on the XsLiveAnimator
    /// </summary>
    [System.Serializable]
    public class XsProp
    {
        //Enum of availible props
        //Users can potentially add props here as long as they also update the XsPropManager and the prop prefab found in xsens/resources
        public enum XsPropType { noProp, gun, sword }

        //Current Prop Type
        public XsPropType type;
        //Body segment the bone will be tied to
        public XsLiveAnimator.XsBodyAnimationSegment segment;

        //Spawns a gameobject that holds all of our props and allows us to hotswap at runtime
        public GameObject SpawnProp()
        {
            return (GameObject)Resources.Load("prop");
        }
    }
}
