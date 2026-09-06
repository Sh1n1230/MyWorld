using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
public class HoverEventTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UnityEvent onHoverEnter;
    public UnityEvent onHoverExit;
    public void OnPointerEnter(PointerEventData eventData)
    {
        onHoverEnter?.Invoke();
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        onHoverExit?.Invoke();
    }
}