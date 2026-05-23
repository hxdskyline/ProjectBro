using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 领导力格子UI - 显示N个格子（N=领导力值）
/// 占用的格子高亮显示，拖拽上阵时占格子，拖回时清格子
/// </summary>
public class LeadershipSlotUI : MonoBehaviour
{
    [Header("UI配置")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _slotsRoot;

    [Header("格子配置")]
    [SerializeField] private float _slotSize = 40f;
    [SerializeField] private float _slotSpacing = 5f;
    [SerializeField] private Color _emptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private Color _occupiedSlotColor = new Color(0.2f, 0.6f, 0.8f, 0.9f);
    [SerializeField] private Color _lockedSlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    // 格子列表
    private List<RectTransform> _slots = new List<RectTransform>();
    private List<Image> _slotImages = new List<Image>();

    // 当前使用的格子数
    private int _usedSlots = 0;
    private int _totalSlots = 0;

    /// <summary>
    /// 初始化领导力格子
    /// </summary>
    public void Initialize(Canvas canvas, RectTransform parent, int maxLeadership)
    {
        _canvas = canvas;
        _slotsRoot = parent;
        _totalSlots = maxLeadership;

        CreateSlots();
    }

    /// <summary>
    /// 创建格子
    /// </summary>
    private void CreateSlots()
    {
        // 清理旧格子
        ClearSlots();

        // 计算总宽度
        float totalWidth = _totalSlots * _slotSize + (_totalSlots - 1) * _slotSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < _totalSlots; i++)
        {
            // 创建格子
            GameObject slotGo = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
            slotGo.transform.SetParent(_slotsRoot, false);

            RectTransform slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(_slotSize, _slotSize);
            slotRect.anchoredPosition = new Vector2(startX + i * (_slotSize + _slotSpacing), 0);

            Image slotImage = slotGo.GetComponent<Image>();
            slotImage.color = _emptySlotColor;

            _slots.Add(slotRect);
            _slotImages.Add(slotImage);
        }

        Debug.Log($"[LeadershipSlotUI] 创建 {_totalSlots} 个领导力格子");
    }

    /// <summary>
    /// 清理格子
    /// </summary>
    private void ClearSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }

        _slots.Clear();
        _slotImages.Clear();
    }

    /// <summary>
    /// 更新格子状态
    /// </summary>
    public void UpdateSlots(int usedCount, int maxCount)
    {
        _usedSlots = usedCount;
        _totalSlots = maxCount;

        // 如果格子数量变化，重新创建
        if (_slots.Count != _totalSlots)
        {
            CreateSlots();
        }

        // 更新每个格子的颜色
        for (int i = 0; i < _slots.Count; i++)
        {
            if (i < _usedSlots)
            {
                // 已占用
                _slotImages[i].color = _occupiedSlotColor;
            }
            else
            {
                // 空闲
                _slotImages[i].color = _emptySlotColor;
            }
        }
    }

    /// <summary>
    /// 高亮显示可放置的格子
    /// </summary>
    public void HighlightAvailableSlots(bool highlight)
    {
        for (int i = _usedSlots; i < _slots.Count; i++)
        {
            if (highlight)
            {
                _slotImages[i].color = new Color(0f, 1f, 0f, 0.8f); // 绿色高亮
            }
            else
            {
                _slotImages[i].color = _emptySlotColor;
            }
        }
    }

    /// <summary>
    /// 占用一个格子
    /// </summary>
    public bool OccupySlot()
    {
        if (_usedSlots >= _totalSlots)
        {
            Debug.LogWarning("[LeadershipSlotUI] 没有空闲格子");
            return false;
        }

        _usedSlots++;
        UpdateSlots(_usedSlots, _totalSlots);
        return true;
    }

    /// <summary>
    /// 释放一个格子
    /// </summary>
    public bool ReleaseSlot()
    {
        if (_usedSlots <= 0)
        {
            Debug.LogWarning("[LeadershipSlotUI] 没有已占用的格子");
            return false;
        }

        _usedSlots--;
        UpdateSlots(_usedSlots, _totalSlots);
        return true;
    }

    /// <summary>
    /// 获取当前使用格子数
    /// </summary>
    public int GetUsedSlots()
    {
        return _usedSlots;
    }

    /// <summary>
    /// 获取总格子数
    /// </summary>
    public int GetTotalSlots()
    {
        return _totalSlots;
    }

    /// <summary>
    /// 获取空闲格子数
    /// </summary>
    public int GetFreeSlots()
    {
        return _totalSlots - _usedSlots;
    }

    /// <summary>
    /// 检查是否可以占用格子
    /// </summary>
    public bool CanOccupySlot()
    {
        return _usedSlots < _totalSlots;
    }
}
