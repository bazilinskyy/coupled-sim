using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PolyMorpher : MonoBehaviour
{

    public AvatarMask customPartMask;

    public Vector2 minMax = new Vector2(0.9f, 1.1f);

    public bool MorphOnStart = true;

    [System.Serializable]
    public class CustomBodyElement
    {
        public string name;
        public Transform bodyElement;
        [HideInInspector] public Vector3 startScale;
    }

    public List<CustomBodyElement> customBodyElement = new List<CustomBodyElement>();

    [ContextMenu("Get Body Parts")]
    IEnumerator GetBodyParts()
    {
        customBodyElement.Clear();

        for (int i = 0; i < customPartMask.transformCount; i++)
        {
            if (customPartMask.GetTransformActive(i))
            {
                var newCustomBodyElement = new CustomBodyElement();
                newCustomBodyElement.name = customPartMask.GetTransformPath(i);
                newCustomBodyElement.bodyElement = transform.Find(customPartMask.GetTransformPath(i));

                if (newCustomBodyElement.bodyElement == null)
                {
                    Debug.Log("Could not find");
                    continue;
                }

                else
                {
                    newCustomBodyElement.startScale = Vector3.one;

                    if (newCustomBodyElement.bodyElement.GetComponent<SkinnedMeshRenderer>())
                    {
                        customBodyElement.Add(newCustomBodyElement);
                    }
                }
            }
        }

        yield return null;
    }

    [ContextMenu("Random Size")]
    void RandomSize()
    {
        for (int i = 0; i < customBodyElement.Count; i++)
        {
            if (customBodyElement[i].bodyElement != null)
            {
                var randomSize = Random.Range(minMax.x, minMax.y);
                customBodyElement[i].bodyElement.localScale = Vector3.one * randomSize;
            }
        }
    }

    [ContextMenu("Reset Size")]
    void ResetSize()
    {
        for (int i = 0; i < customBodyElement.Count; i++)
        {
            if (customBodyElement[i].bodyElement != null)
                customBodyElement[i].bodyElement.localScale = customBodyElement[i].startScale;
        }
    }

    void Start()
    {
        if (MorphOnStart)
        {
            StartCoroutine(Morph());
        }
    }

    IEnumerator Morph()
    {
        yield return GetBodyParts();

        if (customBodyElement.Count > 0)
        {
            RandomSize();
        }
    }
}
