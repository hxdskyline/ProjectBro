using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 族群详细信息弹窗 - 点击 TribeCard 时显示
    /// </summary>
    public class TribeDetailTips : MonoBehaviour
    {
        [Header("UI 组件")]
        [SerializeField] private Text _tribeNameText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _leaderNameText;
        [SerializeField] private Text _leaderStatsText;
        [SerializeField] private RectTransform _catsListContainer;
        [SerializeField] private Button _restButton;
        [SerializeField] private Button _deployButton;
        [SerializeField] private Text _deployButtonText;

        private TribeRecord _tribe;
        private bool _isDeployed;
        private Action<TribeRecord> _onRestClicked;
        private Action<TribeRecord, bool> _onDeployChanged;

        private void Awake()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }

            if (_restButton != null)
            {
                _restButton.onClick.AddListener(OnRestButtonClicked);
            }

            if (_deployButton != null)
            {
                _deployButton.onClick.AddListener(OnDeployButtonClicked);
            }
        }

        /// <summary>
        /// 显示族群详细信息
        /// </summary>
        public void Show(TribeRecord tribe, bool isDeployed, Action<TribeRecord> onRestClicked, Action<TribeRecord, bool> onDeployChanged)
        {
            _tribe = tribe;
            _isDeployed = isDeployed;
            _onRestClicked = onRestClicked;
            _onDeployChanged = onDeployChanged;

            UpdateContent();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void UpdateContent()
        {
            if (_tribe == null) return;

            // 族群名称
            if (_tribeNameText != null)
            {
                _tribeNameText.text = GetTribeTypeName(_tribe.tribeType) + "族";
            }

            // 族长信息
            if (_leaderNameText != null)
            {
                _leaderNameText.text = _tribe.leader?.name ?? "无族长";
            }

            // 族长属性
            if (_leaderStatsText != null && _tribe.leader != null)
            {
                var leader = _tribe.leader;
                _leaderStatsText.text = $"攻击: {leader.baseAttack}\n" +
                                        $"防御: {leader.baseDefense}\n" +
                                        $"血量: {leader.baseHp}\n" +
                                        $"速度: {leader.baseSpeed}\n" +
                                        $"统御: {leader.command}";

                // 显示休息状态
                if (leader.restTurns > 0)
                {
                    _leaderStatsText.text += $"\n\n<color=#ff5555>休息中: {leader.restTurns}回合</color>";
                }
            }

            // 上阵按钮状态
            if (_deployButton != null)
            {
                bool canDeploy = !_tribe.IsLeaderResting();
                _deployButton.interactable = canDeploy;
            }

            if (_deployButtonText != null)
            {
                _deployButtonText.text = _isDeployed ? "下阵" : "上阵";
            }

            // 休息按钮状态
            if (_restButton != null)
            {
                _restButton.interactable = !_tribe.IsLeaderResting();
            }

            // 更新小猫列表
            UpdateCatsList();
        }

        private void UpdateCatsList()
        {
            if (_catsListContainer == null || _tribe?.cats == null) return;

            // 清空现有列表
            for (int i = _catsListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_catsListContainer.GetChild(i).gameObject);
            }

            // 统计各品质小猫数量
            Dictionary<CatQuality, int> qualityCounts = new Dictionary<CatQuality, int>();
            foreach (var cat in _tribe.cats)
            {
                if (qualityCounts.ContainsKey(cat.quality))
                {
                    qualityCounts[cat.quality]++;
                }
                else
                {
                    qualityCounts[cat.quality] = 1;
                }
            }

            // 生成小猫统计
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var kvp in qualityCounts)
            {
                GameObject catGo = new GameObject("CatItem", typeof(RectTransform), typeof(Text));
                catGo.transform.SetParent(_catsListContainer, false);

                RectTransform catRect = catGo.GetComponent<RectTransform>();
                catRect.sizeDelta = new Vector2(150f, 20f);

                Text catText = catGo.GetComponent<Text>();
                catText.font = font;
                catText.fontSize = 12;
                catText.alignment = TextAnchor.MiddleLeft;
                catText.color = GetQualityColor(kvp.Key);
                catText.text = $"{GetQualityName(kvp.Key)}: {kvp.Value} 只";
            }
        }

        private void OnRestButtonClicked()
        {
            if (_tribe != null)
            {
                _onRestClicked?.Invoke(_tribe);
                Hide();
            }
        }

        private void OnDeployButtonClicked()
        {
            if (_tribe != null)
            {
                _onDeployChanged?.Invoke(_tribe, !_isDeployed);
                Hide();
            }
        }

        private Color GetQualityColor(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White: return new Color(0.9f, 0.9f, 0.9f, 1f);
                case CatQuality.Blue: return new Color(0.3f, 0.5f, 0.9f, 1f);
                case CatQuality.Purple: return new Color(0.6f, 0.3f, 0.8f, 1f);
                case CatQuality.Gold: return new Color(1f, 0.6f, 0.2f, 1f);
                default: return Color.white;
            }
        }

        private string GetQualityName(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White: return "白色";
                case CatQuality.Blue: return "蓝色";
                case CatQuality.Purple: return "紫色";
                case CatQuality.Gold: return "金色";
                default: return quality.ToString();
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
