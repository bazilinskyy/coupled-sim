using UnityEngine;


public class CopyPositionAndRotationOfTaggedTarget : MonoBehaviour
{
    [SerializeField] private Transform m_taggedTransform;
    [TagSelector] [SerializeField] private string m_tagToSearchFor;
    [Tooltip("When this is enabled, it will go to the root GameObject (its ancestor), and then go down all their children to find a tagged object. This can be useful for when working with networked systems, in which multiples of the same prefabs could be spawned, each having their own version of the tagged object.\n" +
             "If this is disabled, it will search throughout the scene for a tagged transform")]
    [SerializeField] private bool m_onlySearchInAncestralHierarchy = true;


    private bool FoundTaggedTransform()
    {
        if (m_taggedTransform != null)
        {
            return true;
        }

        if (m_onlySearchInAncestralHierarchy)
        {
            m_taggedTransform = transform.root.FindChildByTag(m_tagToSearchFor); // Go to the root GameObject, then search back downwards until you find something with this tag.
        }
        else
        {
            var go = GameObject.FindWithTag(m_tagToSearchFor);

            if (go != null)
            {
                m_taggedTransform = go.transform;
            }
        }

        if (m_taggedTransform == null)
        {
            Debug.LogWarningFormat("SOSXR: We don't have anything in our scene with the tag {0}, are you sure that it is defined?", m_tagToSearchFor);
        }

        return m_taggedTransform != null;
    }


    private void CopyPositionAndRotation()
    {
        transform.position = m_taggedTransform.position;
        transform.rotation = m_taggedTransform.rotation;
    }


    private void Update()
    {
        if (FoundTaggedTransform())
        {
            CopyPositionAndRotation();
        }
    }
}