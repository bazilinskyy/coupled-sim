using UnityEngine;


public class RecenterImmovableChild : MonoBehaviour
{
    [SerializeField] private Transform m_parent;
    [Tooltip("This can be the XR Camera for instance")]
    [SerializeField] private Transform m_immovableChild; 
    [SerializeField] private Transform m_recenterTo;
    [Tooltip("If no RecenterTo Transform has been set, it will search for this Tag")]
    [SerializeField] [TagSelector] private string m_recenterToTag = "Target_XROrigin";
    [SerializeField] private KeyCode m_recenterKey = KeyCode.Keypad0;
    

    [ContextMenu(nameof(RecenterAndFlatten))]
    public void RecenterAndFlatten()
    {
        FindObjectWithTag();
        RecenterPosition(true);
        RecenterRotation();

        Debug.LogFormat("SOSXR: We just ran {0}", nameof(RecenterAndFlatten));
    }


    [ContextMenu(nameof(RecenterWithoutFlatten))]
    public void RecenterWithoutFlatten()
    {
        FindObjectWithTag();
        RecenterPosition(false);
        RecenterRotation();

        Debug.LogFormat("SOSXR: We just ran {0}", nameof(RecenterWithoutFlatten));
    }


    private void FindObjectWithTag()
    {
        if (m_recenterTo != null)
        {
            return;
        }

        m_recenterTo = transform.root.FindChildByTag(m_recenterToTag); // Go to the root GameObject, then search back downwards until you find something with this tag.

        if (m_recenterTo == null)
        {
            Debug.LogWarningFormat("SOSXR: We don't have anything in our scene with the tag {0}, are you sure that it is defined?", m_recenterToTag);
        }
    }


    private void RecenterPosition(bool flatten)
    {
        var distanceDiff = m_recenterTo.transform.position - m_immovableChild.position;
        m_parent.transform.position += distanceDiff;

        if (flatten)
        {
            m_parent.transform.position = m_parent.transform.position.Flatten();
        }
    }


    private void RecenterRotation()
    {
        var rotationAngleY = m_recenterTo.transform.rotation.eulerAngles.y - m_immovableChild.transform.rotation.eulerAngles.y;

        m_parent.transform.Rotate(0, rotationAngleY, 0);
    }


    private void Update()
    {
        if (Input.GetKeyDown(m_recenterKey))
        {
            RecenterWithoutFlatten();
            Debug.Log("SOSXR: RecenterWithoutFlatten via key");
        }
    }
}