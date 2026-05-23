using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TribeSystem;

/// <summary>
/// 三区系统UI - 显示待上阵区/上阵区/生产区的单位列表
/// 支持单位在三区间拖拽移动
/// </summary>
public class ZonePanel : MonoBehaviour
{
    [Header("UI配置")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _pendingZoneRoot;    // 待上阵区
    [SerializeField] private RectTransform _battleZoneRoot;     // 上阵区
    [SerializeField] private RectTransform _productionZoneRoot; // 生产区

    [Header("UI模板")]
    [SerializeField] private GameObject _unitCardPrefab;        // 单位卡片预制体

    [Header("颜色配置")]
    [SerializeField] private Color _pendingZoneColor = new Color(0.2f, 0.4f, 0.6f, 0.8f);
    [SerializeField] private Color _battleZoneColor = new Color(0.2f, 0.6f, 0.3f, 0.8f);
    [SerializeField] private Color _productionZoneColor = new Color(0.6f, 0.4f, 0.2f, 0.8f);

    // 三区系统服务
    private TribeZoneService _zoneService;

    // 单位卡片列表
    private List<GameObject> _pendingCards = new List<GameObject>();
    private List<GameObject> _battleCards = new List<GameObject>();
    private List<GameObject> _productionCards = new List<GameObject>();

    // 领导力格子UI
    private LeadershipSlotUI _leadershipSlotUI;

    /// <summary>
    /// 初始化三区UI
    /// </summary>
    public void Initialize(Canvas canvas, TribeZoneService zoneService)
    {
        _canvas = canvas;
        _zoneService = zoneService;

        // 创建区域UI
        CreateZoneUI();

        // 订阅事件
        if (_zoneService != null)
        {
            _zoneService.OnUnitsChanged += RefreshAllZones;
            _zoneService.OnUnitMoved += OnUnitMoved;
        }

        // 刷新所有区域
        RefreshAllZones();
    }

    /// <summary>
    /// 创建区域UI
    /// </summary>
    private void CreateZoneUI()
    {
        // 创建待上阵区
        if (_pendingZoneRoot == null)
        {
            _pendingZoneRoot = CreateZone("PendingZone", "待上阵区", _pendingZoneColor,
                new Vector2(0.02f, 0.7f), new Vector2(0.32f, 0.98f));
        }

        // 创建上阵区
        if (_battleZoneRoot == null)
        {
            _battleZoneRoot = CreateZone("BattleZone", "上阵区", _battleZoneColor,
                new Vector2(0.34f, 0.7f), new Vector2(0.66f, 0.98f));
        }

        // 创建生产区
        if (_productionZoneRoot == null)
        {
            _productionZoneRoot = CreateZone("ProductionZone", "生产区", _productionZoneColor,
                new Vector2(0.68f, 0.7f), new Vector2(0.98f, 0.98f));
        }

        // 添加领导力格子
        CreateLeadershipSlots();
    }

    /// <summary>
    /// 创建区域
    /// </summary>
    private RectTransform CreateZone(string name, string title, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        // 创建区域容器
        GameObject zoneGo = new GameObject(name, typeof(RectTransform), typeof(Image));
        zoneGo.transform.SetParent(transform, false);

        RectTransform zoneRect = zoneGo.GetComponent<RectTransform>();
        zoneRect.anchorMin = anchorMin;
        zoneRect.anchorMax = anchorMax;
        zoneRect.sizeDelta = Vector2.zero;

        Image bg = zoneGo.GetComponent<Image>();
        bg.color = color;

        // 创建标题
        GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(zoneGo.transform, false);

        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.9f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.sizeDelta = Vector2.zero;

        Text titleText = titleGo.GetComponent<Text>();
        titleText.text = title;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 14;
        titleText.color = Color.white;

        // 创建单位列表区域
        GameObject listGo = new GameObject("UnitList", typeof(RectTransform));
        listGo.transform.SetParent(zoneGo.transform, false);

        RectTransform listRect = listGo.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.02f, 0.02f);
        listRect.anchorMax = new Vector2(0.98f, 0.88f);
        listRect.sizeDelta = Vector2.zero;

        // 添加垂直布局
        VerticalLayoutGroup layout = listGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        return listRect;
    }

    /// <summary>
    /// 创建领导力格子
    /// </summary>
    private void CreateLeadershipSlots()
    {
        // 在上阵区下方创建领导力格子
        GameObject leadershipGo = new GameObject("LeadershipSlots", typeof(RectTransform));
        leadershipGo.transform.SetParent(_battleZoneRoot, false);

        RectTransform leadershipRect = leadershipGo.GetComponent<RectTransform>();
        leadershipRect.anchorMin = new Vector2(0f, 0f);
        leadershipRect.anchorMax = new Vector2(1f, 0.15f);
        leadershipRect.sizeDelta = Vector2.zero;

        _leadershipSlotUI = leadershipGo.AddComponent<LeadershipSlotUI>();

        // 获取领导力值
        int maxLeadership = _zoneService?.GetMaxPopulation() ?? 3;
        _leadershipSlotUI.Initialize(_canvas, leadershipRect, maxLeadership);
    }

    /// <summary>
    /// 刷新所有区域
    /// </summary>
    public void RefreshAllZones()
    {
        RefreshPendingZone();
        RefreshBattleZone();
        RefreshProductionZone();

        // 更新领导力格子
        if (_leadershipSlotUI != null && _zoneService != null)
        {
            int usedPopulation = _zoneService.GetTotalPopulation();
            int maxPopulation = _zoneService.GetMaxPopulation();
            _leadershipSlotUI.UpdateSlots(usedPopulation, maxPopulation);
        }
    }

    /// <summary>
    /// 刷新待上阵区
    /// </summary>
    private void RefreshPendingZone()
    {
        ClearZoneCards(_pendingCards);

        if (_zoneService == null || _pendingZoneRoot == null) return;

        List<FighterData> pendingUnits = _zoneService.GetUnitsInZone(UnitZone.Pending);
        foreach (var unit in pendingUnits)
        {
            GameObject card = CreateUnitCard(unit, UnitZone.Pending);
            if (card != null)
            {
                card.transform.SetParent(_pendingZoneRoot, false);
                _pendingCards.Add(card);
            }
        }
    }

    /// <summary>
    /// 刷新上阵区
    /// </summary>
    private void RefreshBattleZone()
    {
        ClearZoneCards(_battleCards);

        if (_zoneService == null || _battleZoneRoot == null) return;

        List<FighterData> battleUnits = _zoneService.GetUnitsInZone(UnitZone.Battle);
        foreach (var unit in battleUnits)
        {
            GameObject card = CreateUnitCard(unit, UnitZone.Battle);
            if (card != null)
            {
                card.transform.SetParent(_battleZoneRoot, false);
                _battleCards.Add(card);
            }
        }
    }

    /// <summary>
    /// 刷新生产区
    /// </summary>
    private void RefreshProductionZone()
    {
        ClearZoneCards(_productionCards);

        if (_zoneService == null || _productionZoneRoot == null) return;

        List<FighterData> productionUnits = _zoneService.GetUnitsInZone(UnitZone.Production);
        foreach (var unit in productionUnits)
        {
            GameObject card = CreateUnitCard(unit, UnitZone.Production);
            if (card != null)
            {
                card.transform.SetParent(_productionZoneRoot, false);
                _productionCards.Add(card);
            }
        }
    }

    /// <summary>
    /// 创建单位卡片
    /// </summary>
    private GameObject CreateUnitCard(FighterData unit, UnitZone currentZone)
    {
        // 如果有预制体，使用预制体
        if (_unitCardPrefab != null)
        {
            GameObject card = Instantiate(_unitCardPrefab);
            SetupUnitCard(card, unit, currentZone);
            return card;
        }

        // 否则动态创建
        GameObject cardGo = new GameObject($"Unit_{unit.id}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        cardGo.transform.SetParent(transform, false);

        RectTransform cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(80f, 60f);

        Image cardBg = cardGo.GetComponent<Image>();
        cardBg.color = GetUnitCardColor(unit);

        // 添加单位名称
        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameGo.transform.SetParent(cardGo.transform, false);

        RectTransform nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.sizeDelta = Vector2.zero;

        Text nameText = nameGo.GetComponent<Text>();
        nameText.text = GetUnitDisplayName(unit);
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.fontSize = 12;
        nameText.color = Color.white;

        // 添加单位品质
        GameObject qualityGo = new GameObject("Quality", typeof(RectTransform), typeof(Text));
        qualityGo.transform.SetParent(cardGo.transform, false);

        RectTransform qualityRect = qualityGo.GetComponent<RectTransform>();
        qualityRect.anchorMin = new Vector2(0f, 0f);
        qualityRect.anchorMax = new Vector2(1f, 0.5f);
        qualityRect.sizeDelta = Vector2.zero;

        Text qualityText = qualityGo.GetComponent<Text>();
        qualityText.text = unit.quality.ToString();
        qualityText.alignment = TextAnchor.MiddleCenter;
        qualityText.fontSize = 10;
        qualityText.color = Color.yellow;

        // 添加拖拽处理器
        FormationDragHandler dragHandler = cardGo.AddComponent<FormationDragHandler>();
        dragHandler.Initialize(_canvas, null); // 需要设置BattlefieldRing

        // 设置单位信息
        dragHandler.SetUnitInfo(unit.id, GetUnitWeightClass(unit));

        return cardGo;
    }

    /// <summary>
    /// 设置单位卡片
    /// </summary>
    private void SetupUnitCard(GameObject card, FighterData unit, UnitZone currentZone)
    {
        // 设置卡片信息
        CardUI cardUI = card.GetComponent<CardUI>();
        if (cardUI != null)
        {
            cardUI.SetUnitData(unit);
        }

        // 添加拖拽处理器
        FormationDragHandler dragHandler = card.GetComponent<FormationDragHandler>();
        if (dragHandler == null)
        {
            dragHandler = card.AddComponent<FormationDragHandler>();
        }

        dragHandler.Initialize(_canvas, null); // 需要设置BattlefieldRing
        dragHandler.SetUnitInfo(unit.id, GetUnitWeightClass(unit));
    }

    /// <summary>
    /// 获取单位卡片颜色
    /// </summary>
    private Color GetUnitCardColor(FighterData unit)
    {
        switch (unit.quality)
        {
            case CatQuality.White: return new Color(0.8f, 0.8f, 0.8f, 0.9f);
            case CatQuality.Blue: return new Color(0.3f, 0.5f, 0.8f, 0.9f);
            case CatQuality.Purple: return new Color(0.6f, 0.3f, 0.8f, 0.9f);
            case CatQuality.Gold: return new Color(0.9f, 0.7f, 0.2f, 0.9f);
            default: return new Color(0.5f, 0.5f, 0.5f, 0.9f);
        }
    }

    /// <summary>
    /// 获取单位显示名称
    /// </summary>
    private string GetUnitDisplayName(FighterData unit)
    {
        var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
        if (fighterConfig != null)
        {
            return fighterConfig.fighterName;
        }

        return $"Unit {unit.fighterId}";
    }

    /// <summary>
    /// 获取单位重量级
    /// </summary>
    private string GetUnitWeightClass(FighterData unit)
    {
        // TODO: 从配置中获取重量级
        // 暂时返回默认值
        return "medium";
    }

    /// <summary>
    /// 清理区域卡片
    /// </summary>
    private void ClearZoneCards(List<GameObject> cards)
    {
        foreach (var card in cards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }
        cards.Clear();
    }

    /// <summary>
    /// 单位移动事件处理
    /// </summary>
    private void OnUnitMoved(long unitId, UnitZone newZone)
    {
        Debug.Log($"[ZonePanel] 单位 {unitId} 移动到 {newZone}");
        RefreshAllZones();
    }

    /// <summary>
    /// 清理
    /// </summary>
    private void OnDestroy()
    {
        if (_zoneService != null)
        {
            _zoneService.OnUnitsChanged -= RefreshAllZones;
            _zoneService.OnUnitMoved -= OnUnitMoved;
        }
    }
}

/// <summary>
/// 单位卡片UI组件
/// </summary>
public class CardUI : MonoBehaviour
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _qualityText;
    [SerializeField] private Image _avatarImage;

    private FighterData _unitData;

    public void SetUnitData(FighterData unit)
    {
        _unitData = unit;

        if (_nameText != null)
        {
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
            _nameText.text = fighterConfig?.fighterName ?? $"Unit {unit.fighterId}";
        }

        if (_qualityText != null)
        {
            _qualityText.text = unit.quality.ToString();
        }
    }
}
