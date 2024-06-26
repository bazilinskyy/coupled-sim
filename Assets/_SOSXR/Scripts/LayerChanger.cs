using UnityEngine;

public class LayerChanger : MonoBehaviour
{
    [SerializeField] private GameObject objectToLayerChange;
    [SerializeField] private int originalLayer = 0;
    [SerializeField] private int setToLayer;

    public GameObject ObjectToLayerChange
    {
        get => objectToLayerChange;
        set => objectToLayerChange = value;
    }

    public int OriginalLayer
    {
        get => originalLayer;
        set => originalLayer = value;
    }

    public int SetToLayer
    {
        get => setToLayer;
        set => setToLayer = value;
    }

    private void Awake()
    {
        if (ObjectToLayerChange == null)
        {
            ObjectToLayerChange = gameObject;
        }

        GetLayer();
    }

    public void GetLayer()
    {
        OriginalLayer = ObjectToLayerChange.layer;
    }

    public void SetLayer()
    {
        ObjectToLayerChange.layer = SetToLayer; // Set to the specific layer
        Debug.Log("I changed the layer to " + ObjectToLayerChange.layer);
    }
}