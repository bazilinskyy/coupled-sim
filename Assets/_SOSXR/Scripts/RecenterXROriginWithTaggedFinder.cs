using UnityEngine;


public class RecenterXROriginWithTaggedFinder : RecenterXROrigin
{
    [SerializeField] [TagSelector] private readonly string m_recenterToTag = "Target_XROrigin";


    public override void RecenterAndFlatten()
    {
        FindObjectWithTag();
        base.RecenterAndFlatten();
    }


    public override void RecenterWithoutFlatten()
    {
        FindObjectWithTag();
        base.RecenterWithoutFlatten();
    }


    private void FindObjectWithTag()
    {
        if (RecenterTo != null)
        {
            return;
        }

        RecenterTo = GameObject.FindWithTag(m_recenterToTag).transform;

        // _recenterTo = transform.root.FindChildByTag(m_recenterToTag); // Go to the root GameObject, then search back downwards until you find something with this tag.

        if (RecenterTo == null)
        {
            Debug.LogWarningFormat("SOSXR: We don't have anything in our scene with the tag {0}, are you sure that it is defined?", m_recenterToTag);
        }
    }
}