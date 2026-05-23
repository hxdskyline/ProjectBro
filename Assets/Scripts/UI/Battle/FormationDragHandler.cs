using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 拖拽布阵系统 - 处理单位拖拽到环形战场的逻辑
/// 支持绿色高亮表示可放置，红色高亮表示不可放置
/// </summary>
public class FormationDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("拖拽配置")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private BattlefieldRing _battlefieldRing;

    [Header("高亮颜色")]
    [SerializeField] private Color _validHighlightColor = new Color(0f, 1f, 0f, 0.5f);   // 绿色
    [SerializeField] private Color _invalidHighlightColor = new Color(1f, 0f, 0f, 0.5f); // 红色
    [SerializeField] private Color _normalSlotColor = new Color(1f, 1f, 1f, 0.2f);       // 正常槽位颜色

    // 拖拽状态
    private bool _isDragging = false;
    private RectTransform _draggedUnit;
    private CanvasGroup _draggedCanvasGroup;
    private Vector2 _originalPosition;
    private RectTransform _originalParent;

    // 单位信息
    private int _unitId;
    private string _weightClass;

    // 高亮的槽位
    private List<RectTransform> _highlightedSlots = new List<RectTransform>();

    /// <summary>
    /// 拖拽完成事件
    /// </summary>
    public System.Action<int, BattlefieldRing.RingType, int> OnDropCompleted;

    /// <summary>
    /// 初始化拖拽处理器
    /// </summary>
    public void Initialize(Canvas canvas, BattlefieldRing battlefieldRing)
    {
        _canvas = canvas;
        _battlefieldRing = battlefieldRing;
    }

    /// <summary>
    /// 设置单位信息
    /// </summary>
    public void SetUnitInfo(long unitId, string weightClass)
    {
        _unitId = (int)unitId;
        _weightClass = weightClass;
    }

    /// <summary>
    /// 开始拖拽
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_battlefieldRing == null) return;

        _isDragging = true;
        _draggedUnit = GetComponent<RectTransform>();
        _draggedCanvasGroup = GetComponent<CanvasGroup>();

        if (_draggedCanvasGroup != null)
        {
            _draggedCanvasGroup.blocksRaycasts = false;
        }

        // 记录原始位置
        _originalPosition = _draggedUnit.anchoredPosition;
        _originalParent = _draggedUnit.parent as RectTransform;

        // 高亮可放置区域
        HighlightValidSlots();
    }

    /// <summary>
    /// 拖拽中
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || _draggedUnit == null || _canvas == null) return;

        // 更新拖拽位置
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint);

        _draggedUnit.position = _canvas.transform.TransformPoint(localPoint);
    }

    /// <summary>
    /// 结束拖拽（未放置到有效位置）
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        _isDragging = false;

        // 清除高亮
        ClearHighlights();

        // 恢复原始位置
        if (_draggedUnit != null)
        {
            _draggedUnit.SetParent(_originalParent);
            _draggedUnit.anchoredPosition = _originalPosition;
        }

        if (_draggedCanvasGroup != null)
        {
            _draggedCanvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// 放置到目标位置
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (!_isDragging) return;

        // 查找目标槽位
        RectTransform targetSlot = GetSlotUnderPointer(eventData);
        if (targetSlot == null) return;

        // 检查是否可以放置
        BattlefieldRing.RingType targetRing = GetRingTypeFromSlot(targetSlot);
        List<BattlefieldRing.RingType> allowedRings = _battlefieldRing.GetAllowedRings(_weightClass);

        if (!allowedRings.Contains(targetRing))
        {
            Debug.LogWarning($"[FormationDragHandler] 单位 {_unitId} 不能放置到 {targetRing} 圈层");
            return;
        }

        // 计算槽位索引
        int slotIndex = GetSlotIndex(targetSlot, targetRing);
        if (slotIndex < 0) return;

        // 放置单位
        if (_battlefieldRing.PlaceUnit(_unitId, targetRing, slotIndex))
        {
            // 更新单位位置到槽位
            _draggedUnit.SetParent(targetSlot);
            _draggedUnit.anchorMin = Vector2.zero;
            _draggedUnit.anchorMax = Vector2.one;
            _draggedUnit.sizeDelta = Vector2.zero;
            _draggedUnit.anchoredPosition = Vector2.zero;

            // 触发完成事件
            OnDropCompleted?.Invoke(_unitId, targetRing, slotIndex);

            Debug.Log($"[FormationDragHandler] 单位 {_unitId} 放置到 {targetRing} 圈层 槽位 {slotIndex}");
        }
    }

    /// <summary>
    /// 高亮可放置的槽位
    /// </summary>
    private void HighlightValidSlots()
    {
        ClearHighlights();

        if (_battlefieldRing == null || string.IsNullOrEmpty(_weightClass)) return;

        List<BattlefieldRing.RingType> allowedRings = _battlefieldRing.GetAllowedRings(_weightClass);

        foreach (BattlefieldRing.RingType ring in allowedRings)
        {
            List<RectTransform> slots = _battlefieldRing.GetSlots(ring);
            foreach (var slot in slots)
            {
                Image image = slot.GetComponent<Image>();
                if (image != null)
                {
                    image.color = _validHighlightColor;
                    _highlightedSlots.Add(slot);
                }
            }
        }
    }

    /// <summary>
    /// 清除高亮
    /// </summary>
    private void ClearHighlights()
    {
        foreach (var slot in _highlightedSlots)
        {
            if (slot != null)
            {
                Image image = slot.GetComponent<Image>();
                if (image != null)
                {
                    image.color = _normalSlotColor;
                }
            }
        }
        _highlightedSlots.Clear();
    }

    /// <summary>
    /// 获取指针下的槽位
    /// </summary>
    private RectTransform GetSlotUnderPointer(PointerEventData eventData)
    {
        // 使用GraphicRaycaster检测UI元素
        GraphicRaycaster raycaster = _canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null) return null;

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(eventData, results);

        foreach (var result in results)
        {
            RectTransform rect = result.gameObject.GetComponent<RectTransform>();
            if (rect != null && result.gameObject.name.Contains("Slot_"))
            {
                return rect;
            }
        }

        return null;
    }

    /// <summary>
    /// 从槽位获取圈层类型
    /// </summary>
    private BattlefieldRing.RingType GetRingTypeFromSlot(RectTransform slot)
    {
        string slotName = slot.gameObject.name;

        if (slotName.Contains("Inner"))
            return BattlefieldRing.RingType.Inner;
        else if (slotName.Contains("Middle"))
            return BattlefieldRing.RingType.Middle;
        else if (slotName.Contains("Outer"))
            return BattlefieldRing.RingType.Outer;

        return BattlefieldRing.RingType.Inner;
    }

    /// <summary>
    /// 获取槽位索引
    /// </summary>
    private int GetSlotIndex(RectTransform slot, BattlefieldRing.RingType ringType)
    {
        List<RectTransform> slots = _battlefieldRing.GetSlots(ringType);
        return slots.IndexOf(slot);
    }
}
