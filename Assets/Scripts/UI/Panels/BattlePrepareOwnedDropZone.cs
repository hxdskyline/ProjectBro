using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 战前准备界面的持有区投放组件。
/// </summary>
public class BattlePrepareOwnedDropZone : MonoBehaviour, IDropHandler
{
    private BattlePreparePanel _panel;

    public void Initialize(BattlePreparePanel panel)
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

        PrepareCardDragItem dragItem = draggedGo.GetComponent<PrepareCardDragItem>();
        if (dragItem == null)
        {
            return;
        }

        _panel.HandleCardDrop(dragItem, PrepareCardZoneType.Owned);
    }
}