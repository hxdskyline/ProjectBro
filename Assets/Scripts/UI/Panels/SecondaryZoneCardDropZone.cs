using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 当前打开的二级区域投放组件。
/// </summary>
public class SecondaryZoneCardDropZone : MonoBehaviour, IDropHandler
{
    private CardBuildPanel _panel;

    public void Initialize(CardBuildPanel panel)
    {
        _panel = panel;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_panel == null || !_panel.TryGetActiveSecondaryZone(out CardZoneType zoneType))
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

        _panel.HandleCardDrop(dragItem, zoneType);
    }
}