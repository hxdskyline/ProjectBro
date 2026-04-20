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

        [Header("Small/Big 节点（点击卡片切换）")]
        [SerializeField] private GameObject _smallNode;
        [SerializeField] private GameObject _bigNode;

        [Header("Big卡属性节点 Attr/Item1~5/Num")]
        [SerializeField] private Text[] _attrItemNums; // 攻击、防御、速度、生命、统御

        [Header("Big卡Buff栏")]
        [SerializeField] private RectTransform _buffBarRoot; // BuffScroll/Viewport/BuffContent 的 Content
        [SerializeField] private RectTransform _buffEntryPrefab; // BuffEntry 预制体

        private TribeRecord _tribe;
        private bool _isDeployed;
        private bool _isExpanded;
        private System.Action<TribeCard> _onExpandRequested;
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
            _isDeployed = isDeployed;
            _currentTerrain = terrain;
            _currentWeather = weather;

            // 卡片本身点击 → 切换展开/收起
            Button cardButton = GetComponentInChildren<Button>(true);
            if (cardButton != null)
            {
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(OnCardClicked);
            }

            // 加载头像
            _tribeType = tribe.tribeType;
            _currentVariant = 1;
            LoadPortrait(_tribeType, _currentVariant);

            // 初始为 Small 状态
            SetExpanded(false);

            // 更新文本内容
            UpdateTexts();
        }

        /// <summary>
        /// 设置展开回调（由父级管理同一时间只展开一张卡）
        /// </summary>
        public void SetExpandCallback(System.Action<TribeCard> onExpandRequested)
        {
            _onExpandRequested = onExpandRequested;
        }

        /// <summary>
        /// 切换展开/收起状态
        /// </summary>
        public void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;
            if (_smallNode != null) _smallNode.SetActive(!expanded);
            if (_bigNode != null) _bigNode.SetActive(expanded);

            // 通过 LayoutElement 控制高度，VerticalLayoutGroup 会据此自动布局
            LayoutElement le = GetComponent<LayoutElement>();
            if (le != null)
                le.preferredHeight = expanded ? 350f : 175f;

            if (expanded)
                RebuildBuffBar();
        }

        public bool IsExpanded => _isExpanded;
        public TribeRecord Tribe => _tribe;

        private void OnCardClicked()
        {
            if (_isExpanded) return;
            // 未展开 → 请求展开（父级会收起其他卡）
            _onExpandRequested?.Invoke(this);
        }

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
                case TribeType.Maine: return $"avatartemp/mianyin{variant}";
                case TribeType.Tabby: return $"avatartemp/lihua{variant}";
                case TribeType.Orange: return $"avatartemp/daju{variant}";
                case TribeType.Cow: return $"avatartemp/nainiu{variant}";
                case TribeType.Siamese: return $"avatartemp/xianluo{variant}";
                case TribeType.Ragdoll: return $"avatartemp/buou{variant}";
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
            // Big卡属性节点
            if (_attrItemNums != null && _attrItemNums.Length >= 5 && _tribe.leader != null)
            {
                var leader = _tribe.leader;
                SetAttr(TribeCardAttrIndex.Attack, leader.baseAttack.ToString());
                SetAttr(TribeCardAttrIndex.Defense, leader.baseDefense.ToString());
                SetAttr(TribeCardAttrIndex.Speed, leader.baseSpeed.ToString());
                SetAttr(TribeCardAttrIndex.Hp, leader.baseHp.ToString());
                SetAttr(TribeCardAttrIndex.Command, leader.command.ToString());
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

        private void RebuildBuffBar()
        {
            if (_tribe == null) return;

            if (_buffBarRoot == null)
            {
                EnsureBuffBarRoot();
            }

            if (_buffBarRoot == null) return;

            // 清空现有buff条目
            for (int i = _buffBarRoot.childCount - 1; i >= 0; i--)
                Destroy(_buffBarRoot.GetChild(i).gameObject);

            var leader = _tribe.leader;
            if (leader == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. 永久buff
            AddPermanentBuffEntries(leader.permanentBuffs, font);

            // 2. 临时buff
            if (leader.temporaryBuff != null && leader.temporaryBuff.IsActive())
                AddTemporaryBuffEntry(leader.temporaryBuff, font);

            // 3. 地形天气buff
            TerrainWeatherBuff twBuff = TribeBattleBuffProvider.GetBuff(_tribe.tribeType, _currentTerrain, _currentWeather);
            if (!twBuff.IsNeutral)
                AddTerrainWeatherBuffEntry(twBuff, font);
        }

        private void AddPermanentBuffEntries(PermanentBuffs pb, Font font)
        {
            if (pb == null) return;

            if (pb.attackBonus != 0 || pb.attackPercent != 0f)
                CreateBuffEntry("atk_icon", "攻击强化", FormatStatBuff("攻击", pb.attackBonus, pb.attackPercent), font, new Color(0.9f, 0.3f, 0.2f, 0.8f));
            if (pb.defenseBonus != 0 || pb.defensePercent != 0f)
                CreateBuffEntry("def_icon", "防御强化", FormatStatBuff("防御", pb.defenseBonus, pb.defensePercent), font, new Color(0.3f, 0.5f, 0.9f, 0.8f));
            if (pb.hpBonus != 0 || pb.hpPercent != 0f)
                CreateBuffEntry("hp_icon", "生命强化", FormatStatBuff("生命", pb.hpBonus, pb.hpPercent), font, new Color(0.2f, 0.8f, 0.3f, 0.8f));
            if (pb.speedBonus != 0 || pb.speedPercent != 0f)
                CreateBuffEntry("spd_icon", "速度强化", FormatStatBuff("速度", pb.speedBonus, pb.speedPercent), font, new Color(0.8f, 0.6f, 0.1f, 0.8f));
            if (pb.commandBonus != 0 || pb.commandPercent != 0f)
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

        private string FormatStatBuff(string statName, int flatBonus, float percentBonus)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (flatBonus != 0) parts.Add($"{(flatBonus > 0 ? "+" : "")}{flatBonus}");
            if (percentBonus != 0f) parts.Add($"{(percentBonus > 0 ? "+" : "")}{Mathf.RoundToInt(percentBonus * 100)}%");
            return $"{statName} {string.Join(" ", parts.ToArray())}";
        }

        /// <summary>
        /// 确保 _buffBarRoot 引用有效（从预制体子树中查找 BuffContent）
        /// </summary>
        private void EnsureBuffBarRoot()
        {
            if (_buffBarRoot != null) return;
            // 从 Buff 节点下的 BuffScroll/Viewport/BuffContent 找到 Content
            Transform buffScroll = transform.Find("Buff");
            if (buffScroll == null) buffScroll = _bigNode?.transform.Find("Buff");
            if (buffScroll == null) return;
            var content = buffScroll.Find("BuffScroll/Viewport/BuffContent");
            if (content != null)
                _buffBarRoot = content.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 实例化buff条目预制体，填充图标、名称、描述
        /// </summary>
        private void CreateBuffEntry(string iconName, string buffName, string description, Font font, Color iconColor)
        {
            if (_buffEntryPrefab == null || _buffBarRoot == null) return;

            RectTransform entry = Instantiate(_buffEntryPrefab, _buffBarRoot, false);
            entry.name = $"Buff_{buffName}";

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
                case TribeType.Maine: return "缅因";
                case TribeType.Tabby: return "狸花";
                case TribeType.Orange: return "大橘";
                case TribeType.Cow: return "奶牛";
                case TribeType.Siamese: return "暹罗";
                case TribeType.Ragdoll: return "布偶";
                default: return type.ToString();
            }
        }
    }
}
