using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TribeSystem;

namespace TribeSystem.UI
{
    /// <summary>
    /// 温泉界面 - 显示回血50%或永久强化50%二选一
    /// </summary>
    public class HotSpringPanel : UIPanel
    {
        private const string PanelName = "温泉";

        [Header("UI 组件")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _descriptionText;
        [SerializeField] private Button _healButton;
        [SerializeField] private Button _boostButton;
        [SerializeField] private Text _healButtonText;
        [SerializeField] private Text _boostButtonText;

        [Header("单位选择")]
        [SerializeField] private RectTransform _unitListContainer;
        [SerializeField] private GameObject _unitButtonPrefab;

        private DataManager _dataManager;
        private HealthPersistenceSystem _healthSystem;
        private System.Action _onComplete;

        private FighterData _selectedUnit;
        private bool _isHealMode = true;

        private void Awake()
        {
            _dataManager = GameManager.Instance?.DataManager;
            _healthSystem = new HealthPersistenceSystem();
        }

        /// <summary>
        /// 显示温泉界面
        /// </summary>
        public void ShowHotSpring(System.Action onComplete)
        {
            _onComplete = onComplete;

            if (_titleText != null)
                _titleText.text = PanelName;

            if (_descriptionText != null)
                _descriptionText.text = "选择一个选项：\n1. 回复所有我方单位50%生命值\n2. 永久提升指定单位全属性50%";

            // 设置按钮文字
            if (_healButtonText != null)
                _healButtonText.text = "回复生命 (50%)";

            if (_boostButtonText != null)
                _boostButtonText.text = "永久强化 (50%)";

            // 绑定按钮事件
            if (_healButton != null)
                _healButton.onClick.AddListener(OnHealClick);

            if (_boostButton != null)
                _boostButton.onClick.AddListener(OnBoostClick);

            // 显示单位列表（用于强化选择）
            ShowUnitList();

            Show();
        }

        private void ShowUnitList()
        {
            if (_unitListContainer == null || _unitButtonPrefab == null) return;

            // 清空旧列表
            foreach (Transform child in _unitListContainer)
            {
                Destroy(child.gameObject);
            }

            var playerData = _dataManager?.PlayerData;
            if (playerData == null || playerData.tribes == null) return;

            // 获取所有可选单位
            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    // 跳过有满目疮痍debuff的单位
                    if (unit.hasWoundsDebuff) continue;

                    CreateUnitButton(unit);
                }
            }
        }

        private void CreateUnitButton(FighterData unit)
        {
            var buttonObj = Instantiate(_unitButtonPrefab, _unitListContainer);
            var buttonText = buttonObj.GetComponentInChildren<Text>();

            if (buttonText != null)
            {
                // 获取兵种名称
                var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
                string unitName = fighterConfig?.fighterName ?? $"单位 {unit.fighterId}";
                buttonText.text = $"{unitName} (HP: {unit.currentHp}/{unit.staticHp})";
            }

            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => OnUnitSelected(unit));
            }
        }

        private void OnUnitSelected(FighterData unit)
        {
            _selectedUnit = unit;
            Debug.Log($"[HotSpringPanel] 选择单位: {unit.fighterId}");
        }

        private void OnHealClick()
        {
            Debug.Log("[HotSpringPanel] 选择回复生命");

            // 回复所有友方单位50%生命值
            _healthSystem.HealAllAlliesPercent(0.5f);

            // 保存数据
            _dataManager?.SavePlayerData();

            // 关闭面板
            Close();

            // 通知完成
            _onComplete?.Invoke();
        }

        private void OnBoostClick()
        {
            if (_selectedUnit == null)
            {
                Debug.LogWarning("[HotSpringPanel] 请先选择一个单位");
                return;
            }

            Debug.Log($"[HotSpringPanel] 选择永久强化单位: {_selectedUnit.fighterId}");

            // 永久提升指定单位全属性50%
            _selectedUnit.staticAttack *= 1.5f;
            _selectedUnit.staticDefense *= 1.5f;
            _selectedUnit.staticHp *= 1.5f;
            _selectedUnit.currentHp = _selectedUnit.staticHp; // 强化后回满血

            // 保存数据
            _dataManager?.SavePlayerData();

            // 关闭面板
            Close();

            // 通知完成
            _onComplete?.Invoke();
        }

        private void OnDestroy()
        {
            if (_healButton != null)
                _healButton.onClick.RemoveListener(OnHealClick);

            if (_boostButton != null)
                _boostButton.onClick.RemoveListener(OnBoostClick);
        }
    }
}
