using UnityEngine;


public class BecomeChildUnderTaggedTarget : MonoBehaviour
{
    [TagSelector] [SerializeField] private string m_tagToSearchFor;
    private Transform _taggedObject;


    private void FindObjectWithTag()
    {
        if (_taggedObject != null)
        {
            return;
        }

        _taggedObject = transform.root.FindChildByTag(m_tagToSearchFor); // Go to the root GameObject, then search back downwards until you find something with this tag.

        if (_taggedObject == null)
        {
            Debug.LogWarningFormat("SOSXR: We don't have anything in our scene with the tag {0}, are you sure that it is defined?", m_tagToSearchFor);
        }
    }


    private void BecomeChildOf()
    {
        if (_taggedObject == null)
        {
            return;
        }

        transform.position = _taggedObject.position;
        transform.rotation = _taggedObject.rotation;
    }


    private void Update()
    {
        if (_taggedObject == null)
        {
            FindObjectWithTag();

            return;
        }

        BecomeChildOf();
        // enabled = false;
    }
}