using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 环形战场UI - 管理三圈层（内/中/外）的视觉表示
/// 圈层用不同颜色的环形区域表示，单位按重量级自动分配到对应圈层区域
/// </summary>
public class BattlefieldRing : MonoBehaviour
{
    [Header("战场配置")]
    [SerializeField] private float _innerRadius = 1.5f;      // 内圈半径
    [SerializeField] private float _middleRadius = 3.0f;     // 中圈半径
    [SerializeField] private float _outerRadius = 4.5f;      // 外圈半径
    [SerializeField] private float _billboardOffset = 1.5f;  // 看板偏移量

    [Header("颜色配置")]
    [SerializeField] private Color _innerColor = new Color(0.2f, 0.6f, 0.8f, 0.3f);   // 内圈颜色（蓝色）
    [SerializeField] private Color _middleColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);  // 中圈颜色（绿色）
    [SerializeField] private Color _outerColor = new Color(0.8f, 0.6f, 0.2f, 0.3f);   // 外圈颜色（橙色）
    [SerializeField] private Color _playerBillboardColor = new Color(0.2f, 0.4f, 0.8f, 0.8f);  // 我方看板颜色
    [SerializeField] private Color _enemyBillboardColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);   // 敌方看板颜色

    [Header("UI元素")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _battlefieldRoot;

    // 圈层UI元素
    private RectTransform _innerRing;
    private RectTransform _middleRing;
    private RectTransform _outerRing;
    private RectTransform _innerArea;
    private RectTransform _middleArea;
    private RectTransform _outerArea;

    // 看板UI元素
    private RectTransform _playerBillboard;
    private RectTransform _enemyBillboard;
    private Text _playerBillboardHpText;
    private Text _enemyBillboardHpText;
    private Image _playerBillboardHpBar;
    private Image _enemyBillboardHpBar;

    // 单位槽位
    private List<RectTransform> _innerSlots = new List<RectTransform>();
    private List<RectTransform> _middleSlots = new List<RectTransform>();
    private List<RectTransform> _outerSlots = new List<RectTransform>();

    // 当前布局的单位
    private Dictionary<int, RectTransform> _unitSlotMap = new Dictionary<int, RectTransform>();

    /// <summary>
    /// 圈层类型
    /// </summary>
    public enum RingType
    {
        Inner,  // 内圈 - 轻量级
        Middle, // 中圈 - 轻量级+中量级
        Outer   // 外圈 - 重量级
    }

    /// <summary>
    /// 初始化环形战场
    /// </summary>
    public void Initialize(Canvas canvas, RectTransform parent)
    {
        _canvas = canvas;
        _battlefieldRoot = parent;

        CreateBattlefieldUI();
        CreateBillboards();
        CreateSlots();
    }

    /// <summary>
    /// 创建战场UI
    /// </summary>
    private void CreateBattlefieldUI()
    {
        // 创建外圈（最底层）
        _outerArea = CreateRingArea("OuterArea", _outerRadius, _outerColor);
        _outerRing = CreateRingBorder("OuterRing", _outerRadius, Color.white);

        // 创建中圈
        _middleArea = CreateRingArea("MiddleArea", _middleRadius, _middleColor);
        _middleRing = CreateRingBorder("MiddleRing", _middleRadius, Color.white);

        // 创建内圈（最顶层）
        _innerArea = CreateRingArea("InnerArea", _innerRadius, _innerColor);
        _innerRing = CreateRingBorder("InnerRing", _innerRadius, Color.white);
    }

    /// <summary>
    /// 创建圈层区域
    /// </summary>
    private RectTransform CreateRingArea(string name, float radius, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_battlefieldRoot, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(radius * 2, radius * 2);
        rect.anchoredPosition = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return rect;
    }

    /// <summary>
    /// 创建圈层边框
    /// </summary>
    private RectTransform CreateRingBorder(string name, float radius, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_battlefieldRoot, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(radius * 2, radius * 2);
        rect.anchoredPosition = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = false;

        // 使用边框材质（简化版：只显示边框线）
        // 实际项目中可能需要自定义shader或使用LineRenderer
        return rect;
    }

    /// <summary>
    /// 创建看板
    /// </summary>
    private void CreateBillboards()
    {
        // 我方看板（左侧）
        _playerBillboard = CreateBillboardUI("PlayerBillboard", new Vector2(-(_outerRadius + _billboardOffset), 0), _playerBillboardColor);
        _playerBillboardHpText = CreateBillboardHpText(_playerBillboard, "PlayerHpText");
        _playerBillboardHpBar = CreateBillboardHpBar(_playerBillboard, "PlayerHpBar");

        // 敌方看板（右侧）
        _enemyBillboard = CreateBillboardUI("EnemyBillboard", new Vector2(_outerRadius + _billboardOffset, 0), _enemyBillboardColor);
        _enemyBillboardHpText = CreateBillboardHpText(_enemyBillboard, "EnemyHpText");
        _enemyBillboardHpBar = CreateBillboardHpBar(_enemyBillboard, "EnemyHpBar");
    }

    /// <summary>
    /// 创建看板UI
    /// </summary>
    private RectTransform CreateBillboardUI(string name, Vector2 position, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(_battlefieldRoot, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2.0f, 3.0f);
        rect.anchoredPosition = position;

        Image image = go.GetComponent<Image>();
        image.color = color;

        CanvasGroup canvasGroup = go.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0.5f; // 初始休眠状态

        return rect;
    }

    /// <summary>
    /// 创建看板HP文本
    /// </summary>
    private Text CreateBillboardHpText(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0.3f);
        rect.sizeDelta = Vector2.zero;

        Text text = go.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 12;
        text.color = Color.white;
        text.text = "HP: 10000";

        return text;
    }

    /// <summary>
    /// 创建看板HP条
    /// </summary>
    private Image CreateBillboardHpBar(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.3f);
        rect.anchorMax = new Vector2(0.9f, 0.35f);
        rect.sizeDelta = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = Color.green;

        return image;
    }

    /// <summary>
    /// 创建单位槽位
    /// </summary>
    private void CreateSlots()
    {
        // 内圈槽位（4个，轻量级）
        _innerSlots = CreateRingSlots(RingType.Inner, 4, _innerRadius * 0.7f);

        // 中圈槽位（6个，轻量级+中量级）
        _middleSlots = CreateRingSlots(RingType.Middle, 6, _middleRadius * 0.85f);

        // 外圈槽位（4个，重量级）
        _outerSlots = CreateRingSlots(RingType.Outer, 4, _outerRadius * 0.9f);
    }

    /// <summary>
    /// 创建圈层槽位
    /// </summary>
    private List<RectTransform> CreateRingSlots(RingType ringType, int count, float radius)
    {
        List<RectTransform> slots = new List<RectTransform>();

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i;
            Vector2 position = GetPositionOnCircle(radius, angle);

            GameObject go = new GameObject($"Slot_{ringType}_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_battlefieldRoot, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0.8f, 0.8f);
            rect.anchoredPosition = position;

            Image image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.2f);
            image.raycastTarget = true;

            slots.Add(rect);
        }

        return slots;
    }

    /// <summary>
    /// 获取圆周上的位置
    /// </summary>
    private Vector2 GetPositionOnCircle(float radius, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        float x = radius * Mathf.Cos(angleRadians);
        float y = radius * Mathf.Sin(angleRadians);
        return new Vector2(x, y);
    }

    /// <summary>
    /// 根据重量级获取允许的圈层
    /// </summary>
    public List<RingType> GetAllowedRings(string weightClass)
    {
        List<RingType> allowed = new List<RingType>();

        switch (weightClass.ToLower())
        {
            case "heavy":
                allowed.Add(RingType.Inner);
                break;
            case "medium":
                allowed.Add(RingType.Middle);
                break;
            case "light":
                allowed.Add(RingType.Outer);
                break;
        }

        return allowed;
    }

    /// <summary>
    /// 获取指定圈层的槽位
    /// </summary>
    public List<RectTransform> GetSlots(RingType ringType)
    {
        switch (ringType)
        {
            case RingType.Inner: return _innerSlots;
            case RingType.Middle: return _middleSlots;
            case RingType.Outer: return _outerSlots;
            default: return new List<RectTransform>();
        }
    }

    /// <summary>
    /// 将单位放置到槽位
    /// </summary>
    public bool PlaceUnit(int unitId, RingType ringType, int slotIndex)
    {
        List<RectTransform> slots = GetSlots(ringType);
        if (slotIndex < 0 || slotIndex >= slots.Count) return false;

        // 检查槽位是否已被占用
        foreach (var kvp in _unitSlotMap)
        {
            if (kvp.Value == slots[slotIndex])
                return false;
        }

        _unitSlotMap[unitId] = slots[slotIndex];
        return true;
    }

    /// <summary>
    /// 移除单位
    /// </summary>
    public void RemoveUnit(int unitId)
    {
        _unitSlotMap.Remove(unitId);
    }

    /// <summary>
    /// 获取单位所在的槽位
    /// </summary>
    public RectTransform GetUnitSlot(int unitId)
    {
        RectTransform slot;
        _unitSlotMap.TryGetValue(unitId, out slot);
        return slot;
    }

    /// <summary>
    /// 更新看板状态
    /// </summary>
    public void UpdateBillboardState(bool isPlayerBillboard, bool isActive, float currentHp, float maxHp)
    {
        RectTransform billboard = isPlayerBillboard ? _playerBillboard : _enemyBillboard;
        Text hpText = isPlayerBillboard ? _playerBillboardHpText : _enemyBillboardHpText;
        Image hpBar = isPlayerBillboard ? _playerBillboardHpBar : _enemyBillboardHpBar;

        if (billboard == null) return;

        // 更新激活状态
        CanvasGroup canvasGroup = billboard.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = isActive ? 1f : 0.5f;
        }

        // 更新HP显示
        if (hpText != null)
        {
            hpText.text = $"HP: {currentHp:F0}/{maxHp:F0}";
        }

        if (hpBar != null)
        {
            float hpPercent = maxHp > 0 ? currentHp / maxHp : 0;
            hpBar.fillAmount = hpPercent;

            // 根据HP百分比改变颜色
            if (hpPercent > 0.6f)
                hpBar.color = Color.green;
            else if (hpPercent > 0.3f)
                hpBar.color = Color.yellow;
            else
                hpBar.color = Color.red;
        }
    }

    /// <summary>
    /// 高亮显示可放置区域
    /// </summary>
    public void HighlightValidSlots(string weightClass, bool highlight)
    {
        List<RingType> allowedRings = GetAllowedRings(weightClass);

        foreach (RingType ring in allowedRings)
        {
            List<RectTransform> slots = GetSlots(ring);
            foreach (var slot in slots)
            {
                Image image = slot.GetComponent<Image>();
                if (image != null)
                {
                    image.color = highlight ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 1f, 1f, 0.2f);
                }
            }
        }
    }

    /// <summary>
    /// 清除所有高亮
    /// </summary>
    public void ClearAllHighlights()
    {
        foreach (var slots in new[] { _innerSlots, _middleSlots, _outerSlots })
        {
            foreach (var slot in slots)
            {
                Image image = slot.GetComponent<Image>();
                if (image != null)
                {
                    image.color = new Color(1f, 1f, 1f, 0.2f);
                }
            }
        }
    }

    /// <summary>
    /// 清理战场
    /// </summary>
    public void Cleanup()
    {
        _unitSlotMap.Clear();

        // 清理所有子对象
        for (int i = _battlefieldRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(_battlefieldRoot.GetChild(i).gameObject);
        }
    }
}
