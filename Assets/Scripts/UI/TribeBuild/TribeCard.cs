using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    public enum TribeCardAttrIndex
    {
        Attack = 0,
        Defense = 1,
        Speed = 2,
        Hp = 3,
        AttackSpeed = 4
    }

    /// <summary>
    /// 族群卡片组件 - 显示单个族群信息
    /// </summary>
    public class TribeCard : MonoBehaviour
    {
        [Header("UI 组件")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _countText;
        [SerializeField] private Image _portraitImage; // 族群头像

        [Header("UI 节点")]
        [SerializeField] private GameObject _bigNode;

        [Header("属性节点 Attr/Item1~5/Num")]
        [SerializeField] private Text[] _attrItemNums; // 攻击、防御、速度、生命、统御

        [Header("属性节点（用于 hover 事件）")]
        [SerializeField] private RectTransform[] _attrItems; // Attr/Item1~5

        [Header("Buff栏")]
        [SerializeField] private RectTransform _buffBarRoot;
        [SerializeField] private RectTransform _buffEntryPrefab;

        [Header("Tooltip")]
        [SerializeField] private StatTooltip _statTooltipPrefab;

        private TribeRecord _tribe;
        private FighterData _selectedUnit;
        private bool _isDeployed;
        private int _currentVariant = 1;
        private TribeType _tribeType;
        private int _fighterId;
        private AsyncOperationHandle<Sprite> _portraitHandle;
        private TerrainType _currentTerrain;
        private WeatherType _currentWeather;
        private StatTooltip _tooltipInstance;
        private int _buffEntryCount;

        /// <summary>
        /// 设置卡片数据
        /// </summary>
        public void Setup(TribeRecord tribe, bool isDeployed, System.Action<int, bool> onToggleChanged, System.Action<TribeRecord, bool> onShowDetail = null)
        {
            Setup(tribe, isDeployed, onToggleChanged, onShowDetail, TerrainType.Plain, WeatherType.Sunny);
        }

        /// <summary>
        /// 设置卡片数据（带地形天气，用于显示buff）
        /// </summary>
        public void Setup(TribeRecord tribe, bool isDeployed, System.Action<int, bool> onToggleChanged, System.Action<TribeRecord, bool> onShowDetail, TerrainType terrain, WeatherType weather)
        {
            _tribe = tribe;
            _selectedUnit = null;
            _isDeployed = isDeployed;
            _currentTerrain = terrain;
            _currentWeather = weather;

            _tribeType = tribe.tribeType;
            _fighterId = tribe.fighterId;
            _currentVariant = 1;
            LoadPortrait(_fighterId, _currentVariant);

            UpdateTexts();
        }

        /// <summary>
        /// 设置为显示指定单位属性
        /// </summary>
        public void SetupForUnit(FighterData unit, TribeRecord tribe)
        {
            _tribe = tribe;
            _selectedUnit = unit;
            _tribeType = tribe.tribeType;
            _fighterId = tribe.fighterId;
            _currentVariant = 1;
            LoadPortrait(_fighterId, _currentVariant);
            UpdateTexts();
        }

        public TribeRecord Tribe => _tribe;

        /// <summary>
        /// 切换头像 variant
        /// </summary>
        public void SetPortraitVariant(int variant)
        {
            if (_currentVariant == variant) return;
            _currentVariant = variant;
            LoadPortrait(_fighterId, _currentVariant);
        }

        private void LoadPortrait(int fighterId, int variant)
        {
            if (_portraitImage == null) return;

            string address = GetTribePortraitAddress(fighterId, variant);
            if (!string.IsNullOrEmpty(address))
            {
                if (_portraitHandle.IsValid())
                {
                    Addressables.Release(_portraitHandle);
                }
                _portraitHandle = Addressables.LoadAssetAsync<Sprite>(address);
                _portraitHandle.Completed += (op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded && _portraitImage != null)
                    {
                        _portraitImage.sprite = op.Result;
                    }
                };
            }
        }

        private string GetTribePortraitAddress(int fighterId, int variant)
        {
            return TribeConfigLoader.Instance?.GetFighterAvatarAddress(fighterId, variant);
        }

        private void UpdateTexts()
        {
            if (_tribe == null) return;

            // 兵种名称（从 fighter 表获取）
            if (_nameText != null)
            {
                var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(_tribe.fighterId);
                _nameText.text = fighterConfig?.fighterName ?? $"兵种{_tribe.fighterId}";
            }

            // 小猫数量
            if (_countText != null)
            {
                _countText.text = $"小猫: {_tribe.GetCatCount()} 只";
            }

            // Big卡属性节点
            if (_attrItemNums != null && _attrItemNums.Length >= 5)
            {
                if (_selectedUnit != null)
                {
                    // 显示指定单位属性
                    var unitStats = TribeStatsCalculator.CalculateFighterStats(_selectedUnit);
                    SetAttr(TribeCardAttrIndex.Attack, unitStats.attack.ToString());
                    SetAttr(TribeCardAttrIndex.Defense, unitStats.defense.ToString());
                    SetAttr(TribeCardAttrIndex.Hp, unitStats.hp.ToString());
                }
                else if (_tribe.units != null && _tribe.units.Count > 0)
                {
                    // 显示首个单位属性（含buff的最终值）
                    var finalStats = TribeStatsCalculator.CalculateFighterStats(_tribe.units[0], _tribe.moodId);
                    // 奶牛族：猫群之力，每只本族小猫+3攻击力
                    int displayAtk = finalStats.attack;
                    if (_tribe.tribeType == TribeType.Cow)
                    {
                        int unitCount = _tribe.units?.Count ?? 0;
                        displayAtk += unitCount * 3;
                    }
                    SetAttr(TribeCardAttrIndex.Attack, displayAtk.ToString());
                    SetAttr(TribeCardAttrIndex.Defense, finalStats.defense.ToString());
                    SetAttr(TribeCardAttrIndex.Hp, finalStats.hp.ToString());
                }
            }

            // 绑定属性行 hover 事件
            BindAttrHoverEvents();

            // Buff栏
            RebuildBuffBar();
        }

        private void SetAttr(TribeCardAttrIndex index, string value)
        {
            int i = (int)index;
            if (_attrItemNums != null && i >= 0 && i < _attrItemNums.Length && _attrItemNums[i] != null)
                _attrItemNums[i].text = value;
        }

        // ─── Tooltip hover 事件绑定 ─────────────────────────────────

        private void BindAttrHoverEvents()
        {
            if (_attrItems == null) return;

            StatType[] statOrder = { StatType.Attack, StatType.Defense, StatType.MoveSpeed, StatType.Hp, StatType.AttackSpeed };

            for (int i = 0; i < _attrItems.Length && i < statOrder.Length; i++)
            {
                var item = _attrItems[i];
                if (item == null) continue;

                var statType = statOrder[i];

                // 移除旧的 EventTrigger
                var oldTrigger = item.GetComponent<EventTrigger>();
                if (oldTrigger != null) Destroy(oldTrigger);

                var trigger = item.gameObject.AddComponent<EventTrigger>();
                trigger.triggers = new List<EventTrigger.Entry>();

                // PointerEnter
                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                StatType capturedStat = statType;
                RectTransform capturedItem = item;
                enterEntry.callback.AddListener((data) => { OnAttrHoverEnter(capturedStat, capturedItem); });
                trigger.triggers.Add(enterEntry);

                // PointerExit
                var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((data) => { OnAttrHoverExit(); });
                trigger.triggers.Add(exitEntry);
            }
        }

        private void OnAttrHoverEnter(StatType stat, RectTransform anchor)
        {
            if (_selectedUnit != null)
            {
                // 指定单位视图
                var unitStats = TribeStatsCalculator.CalculateFighterStats(_selectedUnit);

                int baseValue = GetBaseValue(_selectedUnit, stat);
                int finalValue = GetFinalValue(unitStats, stat);

                var flatEntries = new List<UnifiedBuff>();
                var percentEntries = new List<UnifiedBuff>();
                foreach (var buff in _selectedUnit.ActiveBuffs)
                {
                    if (buff.statType != stat) continue;
                    if (buff.source == BuffSource.Artifact && buff.sourceId == "Artifact_CatAttackFlat_Global") continue;
                    if (buff.isPercent) percentEntries.Add(buff); else flatEntries.Add(buff);
                }

                // 攻击属性：补上全局奇物加成条目，使 tooltip 公式与 finalValue 一致
                if (stat == StatType.Attack)
                {
                    var globalBonus = GameManager.Instance?.DataManager?.PlayerData?.globalCatAttackFlatBonus ?? 0;
                    if (globalBonus > 0)
                        flatEntries.Add(UnifiedBuff.CreateStatBuff("global_cat_atk_flat", "奇物：苍蝇拍", BuffSource.Artifact, "Artifact_CatAttackFlat_Global", StatType.Attack, false, globalBonus));
                }

                EnsureTooltipInstance();
                if (_tooltipInstance != null)
                    StatTooltip.Show(stat, finalValue, baseValue, flatEntries, percentEntries, anchor);
            }
            else
            {
                // 首个单位视图
                if (_tribe?.units == null || _tribe.units.Count == 0) return;

                var unit = _tribe.units[0];
                int baseValue = GetBaseValue(unit, stat);
                var finalStats = TribeStatsCalculator.CalculateFighterStats(unit, _tribe.moodId);
                int finalValue = GetFinalValue(finalStats, stat);

                var flatEntries = new List<UnifiedBuff>();
                var percentEntries = new List<UnifiedBuff>();
                foreach (var buff in unit.ActiveBuffs)
                {
                    if (buff.statType != stat) continue;
                    if (buff.isPercent) percentEntries.Add(buff); else flatEntries.Add(buff);
                }

                EnsureTooltipInstance();
                if (_tooltipInstance != null)
                    StatTooltip.Show(stat, finalValue, baseValue, flatEntries, percentEntries, anchor);
            }
        }

        private int GetBaseValue(FighterData unit, StatType stat)
        {
            if (unit == null) return 0;
            switch (stat)
            {
                case StatType.Attack: return Mathf.RoundToInt(unit.staticAttack);
                case StatType.Defense: return Mathf.RoundToInt(unit.staticDefense);
                case StatType.Hp: return Mathf.RoundToInt(unit.staticHp);
                case StatType.MoveSpeed: return Mathf.RoundToInt(unit.staticMoveSpeed * 1000);
                case StatType.AttackSpeed: return Mathf.RoundToInt(unit.staticAttackSpeed * 1000);
                default: return 0;
            }
        }

        private int GetFinalValue(FighterStats stats, StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return stats.attack;
                case StatType.Defense: return stats.defense;
                case StatType.Hp: return stats.hp;
                case StatType.MoveSpeed: return Mathf.RoundToInt(stats.moveSpeed * 1000);
                case StatType.AttackSpeed: return Mathf.RoundToInt(stats.attackSpeed * 1000);
                default: return 0;
            }
        }

        private void OnAttrHoverExit()
        {
            StatTooltip.Hide();
        }

        private void EnsureTooltipInstance()
        {
            if (_tooltipInstance == null && _statTooltipPrefab != null)
            {
                _tooltipInstance = Instantiate(_statTooltipPrefab, transform.root);
                _tooltipInstance.gameObject.SetActive(false);
            }
        }

        // ─── Buff 栏
        private void RebuildBuffBar()
        {
            if (_tribe == null) return;
            if (_buffBarRoot == null) return;

            // 移除 Buff 的 UGUI 布局组件，改用代码手动计算
            var csf = _buffBarRoot.GetComponent<ContentSizeFitter>();
            if (csf != null) Object.Destroy(csf);
            var vlg = _buffBarRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) Object.Destroy(vlg);

            // 清空现有buff条目
            for (int i = _buffBarRoot.childCount - 1; i >= 0; i--)
                Destroy(_buffBarRoot.GetChild(i).gameObject);

            // 用计数器追踪新条目索引（Destroy 不会立即移除，childCount 不准）
            _buffEntryCount = 0;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (_selectedUnit != null)
            {
                // 显示指定单位的 buff
                AddUnifiedBuffEntries(_selectedUnit.ActiveBuffs, font);
            }
            else
            {
                if (_tribe.units != null && _tribe.units.Count > 0)
                {
                    var unit = _tribe.units[0];
                    // 显示首个单位的 buff
                    AddUnifiedBuffEntries(unit.ActiveBuffs, font);
                }
            }

            // 根据条目数量设置 Buff 高度
            float entryHeight = _buffEntryPrefab != null ? _buffEntryPrefab.sizeDelta.y : 100f;
            _buffBarRoot.sizeDelta = new Vector2(_buffBarRoot.sizeDelta.x, _buffEntryCount * entryHeight);

            // 刷新 Big 节点布局
            if (_bigNode != null)
            {
                var bigRect = _bigNode.GetComponent<RectTransform>();
                if (bigRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(bigRect);
            }
        }

        /// <summary>
        /// 按来源分组显示 UnifiedBuff 列表（用于 buff 栏）
        /// 同一来源的多个buff合并显示，如"苍蝇拍x2 +40"
        /// </summary>
        private void AddUnifiedBuffEntries(List<UnifiedBuff> buffs, Font font)
        {
            if (buffs == null || buffs.Count == 0) return;

            // 按来源名称分组
            var sourceGroups = new Dictionary<string, List<UnifiedBuff>>();
            var sourceOrder = new List<string>(); // 保持插入顺序
            foreach (var buff in buffs)
            {
                string key = buff.displayName ?? buff.sourceId ?? buff.buffId;
                if (!sourceGroups.ContainsKey(key))
                {
                    sourceGroups[key] = new List<UnifiedBuff>();
                    sourceOrder.Add(key);
                }
                sourceGroups[key].Add(buff);
            }

            Color[] statColors = {
                new Color(0.9f, 0.3f, 0.2f, 0.8f), // 攻击红
                new Color(0.3f, 0.5f, 0.9f, 0.8f), // 防御蓝
                new Color(0.8f, 0.6f, 0.1f, 0.8f), // 速度金
                new Color(0.2f, 0.8f, 0.3f, 0.8f), // 生命绿
                new Color(0.9f, 0.5f, 0.1f, 0.8f), // 攻速橙
                new Color(0.6f, 0.3f, 0.8f, 0.8f)  // 统御紫
            };

            foreach (var sourceName in sourceOrder)
            {
                var group = sourceGroups[sourceName];
                int totalCount = 0;
                // 按属性类型汇总该来源的所有buff
                var statSums = new Dictionary<StatType, (float flat, float pct)>();
                foreach (var b in group)
                {
                    totalCount++;
                    float totalVal = b.value * b.currentStacks;
                    if (!statSums.ContainsKey(b.statType))
                        statSums[b.statType] = (0f, 0f);
                    var s = statSums[b.statType];
                    if (b.isPercent)
                        statSums[b.statType] = (s.flat, s.pct + totalVal);
                    else
                        statSums[b.statType] = (s.flat + totalVal, s.pct);
                }

                // 构建描述文本
                string desc;
                bool isStackable = group[0].stackRule == TribeSystem.BuffStackRule.Stack;
                if (!isStackable && !string.IsNullOrEmpty(group[0].description))
                {
                    // 非叠层 buff：使用原始描述
                    desc = group[0].description;
                }
                else
                {
                    // 叠层 buff 或无描述：从属性自动拼接
                    var descParts = new List<string>();
                    foreach (var sKvp in statSums)
                    {
                        string statName;
                        switch (sKvp.Key)
                        {
                            case StatType.Attack:      statName = "攻击"; break;
                            case StatType.Defense:     statName = "防御"; break;
                            case StatType.MoveSpeed:   statName = "速度"; break;
                            case StatType.Hp:          statName = "生命"; break;
                            case StatType.AttackSpeed: statName = "攻速"; break;
                            default:                   statName = "统御"; break;
                        }
                        descParts.Add(FormatStatBuff(statName, Mathf.RoundToInt(sKvp.Value.flat), sKvp.Value.pct));
                    }
                    desc = string.Join(" ", descParts.ToArray());
                }

                // 显示名称：对可叠加 buff 显示层数，否则显示来源名
                int displayCount = totalCount;
                if (isStackable && group[0].currentStacks > 1)
                    displayCount = group[0].currentStacks;
                string displayName = displayCount > 1 ? $"{sourceName}x{displayCount}" : sourceName;

                // 选择颜色：取第一个buff的属性类型对应颜色
                StatType firstStat = group[0].statType;
                int colorIndex;
                switch (firstStat)
                {
                    case StatType.Attack:      colorIndex = 0; break;
                    case StatType.Defense:     colorIndex = 1; break;
                    case StatType.MoveSpeed:   colorIndex = 2; break;
                    case StatType.Hp:          colorIndex = 3; break;
                    case StatType.AttackSpeed: colorIndex = 4; break;
                    default:                   colorIndex = 5; break;
                }

                CreateBuffEntry($"{sourceName}_icon", displayName, desc,
                    font, statColors[colorIndex], firstStat);
            }
        }

        private void AddTerrainWeatherBuffEntry(TerrainWeatherBuff twBuff, Font font)
        {
            CreateBuffEntry("env_icon", "环境修正", twBuff.GetDescription(), font, new Color(0.4f, 0.7f, 0.5f, 0.8f), null);
        }

        private string FormatStatBuff(string statName, int flatBonus, float percentBonus)
        {
            var parts = new List<string>();
            if (flatBonus != 0) parts.Add($"{(flatBonus > 0 ? "+" : "")}{flatBonus}");
            if (percentBonus != 0f) parts.Add($"{(percentBonus > 0 ? "+" : "")}{Mathf.RoundToInt(percentBonus * 100)}%");
            return $"{statName} {string.Join(" ", parts.ToArray())}";
        }

        /// <summary>
        /// 实例化buff条目预制体，填充图标、名称、描述
        /// </summary>
        private void CreateBuffEntry(string iconName, string buffName, string description, Font font, Color iconColor, StatType? relatedStat)
        {
            if (_buffEntryPrefab == null || _buffBarRoot == null) return;

            RectTransform entry = Instantiate(_buffEntryPrefab, _buffBarRoot, false);
            entry.name = $"Buff_{buffName}";
            entry.anchoredPosition = new Vector2(0f, -_buffEntryCount * _buffEntryPrefab.sizeDelta.y);
            _buffEntryCount++;

            // 图标颜色
            Image iconImg = entry.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
                iconImg.color = iconColor;

            // 名称文本
            Text nameText = entry.Find("Name")?.GetComponent<Text>();
            if (nameText != null)
                nameText.text = buffName;

            // 描述文本
            Text descText = entry.Find("Desc")?.GetComponent<Text>();
            if (descText != null)
                descText.text = description;

            // hover 事件：悬浮时显示该属性的完整来源
            if (relatedStat.HasValue)
            {
                var trigger = entry.gameObject.AddComponent<EventTrigger>();
                trigger.triggers = new List<EventTrigger.Entry>();

                StatType capturedStat = relatedStat.Value;
                RectTransform capturedEntry = entry;

                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((data) => { OnBuffEntryHoverEnter(capturedStat, capturedEntry); });
                trigger.triggers.Add(enterEntry);

                var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((data) => { OnAttrHoverExit(); });
                trigger.triggers.Add(exitEntry);
            }
        }

        private void OnBuffEntryHoverEnter(StatType stat, RectTransform anchor)
        {
            if (_selectedUnit != null)
            {
                // 指定单位视图
                var unitStats = TribeStatsCalculator.CalculateFighterStats(_selectedUnit);

                int baseValue = GetBaseValue(_selectedUnit, stat);
                int finalValue = GetFinalValue(unitStats, stat);

                var flatEntries = new List<UnifiedBuff>();
                var percentEntries = new List<UnifiedBuff>();
                foreach (var buff in _selectedUnit.ActiveBuffs)
                {
                    if (buff.statType != stat) continue;
                    if (buff.source == BuffSource.Artifact && buff.sourceId == "Artifact_CatAttackFlat_Global") continue;
                    if (buff.isPercent) percentEntries.Add(buff); else flatEntries.Add(buff);
                }

                // 攻击属性：补上全局奇物加成条目，使 tooltip 公式与 finalValue 一致
                if (stat == StatType.Attack)
                {
                    var globalBonus = GameManager.Instance?.DataManager?.PlayerData?.globalCatAttackFlatBonus ?? 0;
                    if (globalBonus > 0)
                        flatEntries.Add(UnifiedBuff.CreateStatBuff("global_cat_atk_flat", "奇物：苍蝇拍", BuffSource.Artifact, "Artifact_CatAttackFlat_Global", StatType.Attack, false, globalBonus));
                }

                EnsureTooltipInstance();
                if (_tooltipInstance != null)
                    StatTooltip.Show(stat, finalValue, baseValue, flatEntries, percentEntries, anchor);
            }
            else
            {
                // 首个单位视图
                if (_tribe?.units == null || _tribe.units.Count == 0) return;

                var unit = _tribe.units[0];
                int baseValue = GetBaseValue(unit, stat);
                var finalStats = TribeStatsCalculator.CalculateFighterStats(unit, _tribe.moodId);
                int finalValue = GetFinalValue(finalStats, stat);

                var flatEntries = new List<UnifiedBuff>();
                var percentEntries = new List<UnifiedBuff>();
                foreach (var buff in unit.ActiveBuffs)
                {
                    if (buff.statType != stat) continue;
                    if (buff.isPercent) percentEntries.Add(buff); else flatEntries.Add(buff);
                }

                EnsureTooltipInstance();
                if (_tooltipInstance != null)
                    StatTooltip.Show(stat, finalValue, baseValue, flatEntries, percentEntries, anchor);
            }
        }

        private void OnDestroy()
        {
            if (_portraitHandle.IsValid())
            {
                Addressables.Release(_portraitHandle);
            }
        }
    }
}
