using UnityEngine;
using UnityEngine.EventSystems;

// This script is used to control the tooltip or mouse pointer in game so the game knows when a button has been pressed.

class TooltipController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    GameObject target;

    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        target.SetActive(true);
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        target.SetActive(false);
    }
}