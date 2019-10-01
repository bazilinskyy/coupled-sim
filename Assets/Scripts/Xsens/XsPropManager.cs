using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace xsens
{
    /// <summary>
    /// This class allows us to update all of our spawned props at runtime
    /// </summary>
    public class XsPropManager : MonoBehaviour
    {
        public GameObject gun;
        public GameObject sword;

        public XsProp.XsPropType currentType;
        public XsLiveAnimator.XsBodyAnimationSegment currentSegment;

        //Set our proptype to noProp and turn off all child objects
        public void Start()
        {
            currentType = XsProp.XsPropType.noProp;
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
        }

        //sets our current bone segment to the correct one. Called in XsLiveAnimator.
        public void SetCurrentSegment(XsLiveAnimator.XsBodyAnimationSegment segment)
        {
            currentSegment = segment;
        }

        //swaps out prop type, allowing us to turn on the correct game object. Called in XsLiveAnimator.
        public void SwapPropType(XsProp.XsPropType type)
        {
            currentType = type;
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }

            switch (type)
            {
                case XsProp.XsPropType.noProp:
                    break;
                case XsProp.XsPropType.gun:
                    gun.gameObject.SetActive(true);
                    break;
                case XsProp.XsPropType.sword:
                    sword.gameObject.SetActive(true);
                    break;
            }
        }
    }
}
