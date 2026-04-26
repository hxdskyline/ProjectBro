using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TribeSystem.UI
{
    public enum TribeCardAttrIndex
    {
        Attack = 0,
        Defense = 1,
        Speed = 2,
        Hp = 3,
        Command = 4
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

        [Header("Buff栏")]
        [SerializeField] private RectTransform _buffBarRoot;
        [SerializeField] private RectTransform _buffEntryPrefab;

        private TribeRecord _tribe;
        private CatData _selectedCat;
        private bool _isDeployed;
        private int _currentVariant = 1;
        private TribeType _tribeType;
        private AsyncOperationHandle<Sprite> _portraitHandle;
        private TerrainType _currentTerrain;
        private WeatherType _currentWeather;

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
            _selectedCat = null;
            _isDeployed = isDeployed;
            _currentTerrain = terrain;
            _currentWeather = weather;

            _tribeType = tribe.tribeType;
            _currentVariant = 1;
            LoadPortrait(_tribeType, _currentVariant);

            UpdateTexts();
        }

        /// <summary>
        /// 设置为显示小猫属性
        /// </summary>
        public void SetupForCat(CatData cat, TribeRecord tribe)
        {
            _tribe = tribe;
            _selectedCat = cat;
            _tribeType = tribe.tribeType;
            _currentVariant = 1;
            LoadPortrait(_tribeType, _currentVariant);
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
            LoadPortrait(_tribeType, _currentVariant);
        }

        private void LoadPortrait(TribeType tribeType, int variant)
        {
            if (_portraitImage == null) return;

            string address = GetTribePortraitAddress(tribeType, variant);
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

        private string GetTribePortraitAddress(TribeType tribeType, int variant)
        {
            switch (tribeType)
            {
                case TribeType.Tabby: return $"avatartemp/lihua{variant}";
                case TribeType.Orange: return $"avatartemp/daju{variant}";
                case TribeType.Cow: return $"avatartemp/nainiu{variant}";
                case TribeType.Siamese: return $"avatartemp/xianluo{variant}";
                default: return null;
            }
        }

        private void UpdateTexts()
        {
            if (_tribe == null) return;

            // 族群名称
            if (_nameText != null)
            {
                _nameText.text = GetTribeTypeName(_tribe.tribeType);
            }

            // 小猫数量
            if (_countText != null)
            {
                _countText.text = $"小猫: {_tribe.GetCatCount()} 只";
            }

            // Big卡属性节点
            if (_attrItemNums != null && _attrItemNums.Length >= 5)
            {
                if (_selectedCat != null)
                {
                    // 显示小猫属性
                    var config = TribeConfigLoader.Instance?.GetTribeConfig(_tribe.tribeType);
                    if (config != null)
                    {
                        var catBaseStats = new LeaderStats(
                            config.catBaseStats.attack, config.catBaseStats.defense,
                            config.catBaseStats.hp, config.catBaseStats.speed, 0);
                        var catStats = TribeStatsCalculator.CalculateCatStats(_selectedCat, catBaseStats, _tribe?.leader?.permanentBuffs);
                        SetAttr(TribeCardAttrIndex.Attack, catStats.attack.ToString());
                        SetAttr(TribeCardAttrIndex.Defense, catStats.defense.ToString());
                        SetAttr(TribeCardAttrIndex.Speed, catStats.speed.ToString());
                        SetAttr(TribeCardAttrIndex.Hp, catStats.hp.ToString());
                        SetAttr(TribeCardAttrIndex.Command, "-");
                    }
                }
                else if (_tribe.leader != null)
                {
                    // 显示族长属性（含buff的最终值）
                    var finalStats = TribeStatsCalculator.CalculateLeaderStats(_tribe.leader, _tribe.moodId);
                    SetAttr(TribeCardAttrIndex.Attack, finalStats.attack.ToString());
                    SetAttr(TribeCardAttrIndex.Defense, finalStats.defense.ToString());
                    SetAttr(TribeCardAttrIndex.Speed, finalStats.speed.ToString());
                    SetAttr(TribeCardAttrIndex.Hp, finalStats.hp.ToString());
                    SetAttr(TribeCardAttrIndex.Command, finalStats.command.ToString());
                }
            }

            // Buff栏
            RebuildBuffBar();
        }

        private void SetAttr(TribeCardAttrIndex index, string value)
        {
            int i = (int)index;
            if (_attrItemNums != null && i >= 0 && i < _attrItemNums.Length && _attrItemNums[i] != null)
                _attrItemNums[i].text = value;
        }

        private int _buffEntryCount;

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

            var leader = _tribe.leader;
            if (leader != null)
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

                // 1. 永久buff
                AddPermanentBuffEntries(leader.permanentBuffs, font);

                // 2. 临时buff
                if (leader.temporaryBuff != null && leader.temporaryBuff.IsActive())
                    AddTemporaryBuffEntry(leader.temporaryBuff, font);

                // 3. 天生特殊buff（specialBuffs 中 visible=true 的条目）
                AddInnateBuffEntries(leader.permanentBuffs, font);
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

        private void AddPermanentBuffEntries(PermanentBuffs pb, Font font)
        {
            if (pb == null) return;

            if (pb.attackVisible && (pb.attackBonus != 0 || pb.attackPercent != 0f))
                CreateBuffEntry("atk_icon", "攻击强化", FormatStatBuff("攻击", pb.attackBonus, pb.attackPercent), font, new Color(0.9f, 0.3f, 0.2f, 0.8f));
            if (pb.defenseVisible && (pb.defenseBonus != 0 || pb.defensePercent != 0f))
                CreateBuffEntry("def_icon", "防御强化", FormatStatBuff("防御", pb.defenseBonus, pb.defensePercent), font, new Color(0.3f, 0.5f, 0.9f, 0.8f));
            if (pb.hpVisible && (pb.hpBonus != 0 || pb.hpPercent != 0f))
                CreateBuffEntry("hp_icon", "生命强化", FormatStatBuff("生命", pb.hpBonus, pb.hpPercent), font, new Color(0.2f, 0.8f, 0.3f, 0.8f));
            if (pb.speedVisible && (pb.speedBonus != 0 || pb.speedPercent != 0f))
                CreateBuffEntry("spd_icon", "速度强化", FormatStatBuff("速度", pb.speedBonus, pb.speedPercent), font, new Color(0.8f, 0.6f, 0.1f, 0.8f));
            if (pb.commandVisible && (pb.commandBonus != 0 || pb.commandPercent != 0f))
                CreateBuffEntry("cmd_icon", "统御强化", FormatStatBuff("统御", pb.commandBonus, pb.commandPercent), font, new Color(0.6f, 0.3f, 0.8f, 0.8f));
        }

        private void AddTemporaryBuffEntry(TemporaryBuff tb, Font font)
        {
            if (tb == null) return;

            var lines = new System.Collections.Generic.List<string>();
            if (tb.attackPercent != 0f) lines.Add($"攻击 {(tb.attackPercent > 0 ? "+" : "")}{Mathf.RoundToInt(tb.attackPercent * 100)}%");
            if (tb.defensePercent != 0f) lines.Add($"防御 {(tb.defensePercent > 0 ? "+" : "")}{Mathf.RoundToInt(tb.defensePercent * 100)}%");
            if (tb.hpPercent != 0f) lines.Add($"生命 {(tb.hpPercent > 0 ? "+" : "")}{Mathf.RoundToInt(tb.hpPercent * 100)}%");
            if (tb.speedPercent != 0f) lines.Add($"速度 {(tb.speedPercent > 0 ? "+" : "")}{Mathf.RoundToInt(tb.speedPercent * 100)}%");
            lines.Add($"剩余 {tb.duration} 回合");
            CreateBuffEntry("temp_icon", "限时加成", string.Join("\n", lines.ToArray()), font, new Color(0.9f, 0.7f, 0.1f, 0.8f));
        }

        private void AddTerrainWeatherBuffEntry(TerrainWeatherBuff twBuff, Font font)
        {
            CreateBuffEntry("env_icon", "环境修正", twBuff.GetDescription(), font, new Color(0.4f, 0.7f, 0.5f, 0.8f));
        }

        private void AddInnateBuffEntries(PermanentBuffs pb, Font font)
        {
            if (pb == null || pb.specialBuffs == null) return;
            Color[] colors = {
                new Color(0.9f, 0.3f, 0.2f, 0.8f), // 0红
                new Color(0.3f, 0.5f, 0.9f, 0.8f), // 1蓝
                new Color(0.2f, 0.8f, 0.3f, 0.8f), // 2绿
                new Color(0.9f, 0.7f, 0.1f, 0.8f), // 3金
                new Color(0.6f, 0.3f, 0.8f, 0.8f)  // 4紫
            };
            foreach (var buff in pb.specialBuffs)
            {
                if (!buff.visible) continue;
                int ci = Mathf.Clamp(buff.iconColorIndex, 0, colors.Length - 1);
                CreateBuffEntry($"{buff.buffId}_icon", buff.displayName, buff.description, font, colors[ci]);
            }
        }

        private string FormatStatBuff(string statName, int flatBonus, float percentBonus)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (flatBonus != 0) parts.Add($"{(flatBonus > 0 ? "+" : "")}{flatBonus}");
            if (percentBonus != 0f) parts.Add($"{(percentBonus > 0 ? "+" : "")}{Mathf.RoundToInt(percentBonus * 100)}%");
            return $"{statName} {string.Join(" ", parts.ToArray())}";
        }

        /// <summary>
        /// 实例化buff条目预制体，填充图标、名称、描述
        /// </summary>
        private void CreateBuffEntry(string iconName, string buffName, string description, Font font, Color iconColor)
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
        }

        private void OnDestroy()
        {
            if (_portraitHandle.IsValid())
            {
                Addressables.Release(_portraitHandle);
            }
        }

        private string GetTribeTypeName(TribeType type)
        {
            switch (type)
            {
                case TribeType.Tabby: return "狸花";
                case TribeType.Orange: return "大橘";
                case TribeType.Cow: return "奶牛";
                case TribeType.Siamese: return "暹罗";
                default: return type.ToString();
            }
        }
    }
}
