using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 属性提升区投放组件。
/// </summary>
public class AttributeBoostCardDropZone : MonoBehaviour, IDropHandler
{
    private CardBuildPanel _panel;

    public void Initialize(CardBuildPanel panel)
    {
        _panel = panel;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_panel == null)
        {
            return;
        }

        GameObject draggedGo = eventData.pointerDrag;
        if (draggedGo == null)
        {
            return;
        }

        CardDragItem dragItem = draggedGo.GetComponent<CardDragItem>();
        if (dragItem == null)
        {
            return;
        }

        _panel.HandleCardDrop(dragItem, CardZoneType.AttributeBoost);
    }
}