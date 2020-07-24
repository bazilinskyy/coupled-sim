using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CustomPeople : MonoBehaviour
{

    public AvatarMask mask;

    public GameObject slider;

    public Transform sliderParent;

    public List<Slider> sliders = new List<Slider>(0);

    [System.Serializable]
    public class CustomBodyElement
    {
        public string name;
        public Transform bodyElement;
    }

    public List<CustomBodyElement> customBodyElement = new List<CustomBodyElement>();

    // Use this for initialization
    void Start()
    {
        for (int i = 0; i < mask.transformCount; i++)
        {
            if (mask.GetTransformActive(i))
            {
                var newCustomBodyElement = new CustomBodyElement();
                newCustomBodyElement.name = mask.GetTransformPath(i);
                newCustomBodyElement.bodyElement = transform.Find(mask.GetTransformPath(i));
                if (!newCustomBodyElement.bodyElement.GetComponent<SkinnedMeshRenderer>())
                {
                    customBodyElement.Add(newCustomBodyElement);
                }
            }
        }

        for (int i = 0; i < customBodyElement.Count; i++)
        {
            var newSlider = Instantiate(slider, sliderParent);
            //newSlider.GetComponentInChildren<Text>().text = customBodyElement[i].name;
            sliders.Add(newSlider.GetComponent<Slider>());
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < customBodyElement.Count; i++)
        {
            if (customBodyElement[i].bodyElement != null)
                customBodyElement[i].bodyElement.transform.localScale = Vector3.one * (sliders[i].value + 1f);
        }
    }
}
