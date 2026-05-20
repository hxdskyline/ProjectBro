using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 命运面板 - 祈愿玩法界面
    /// 展示3个祈愿档次供玩家选择，然后从祝福池中抽取奖励
    /// </summary>
    public class FatePanel : MonoBehaviour
    {
        private const string PanelName = "命运";

        [Header("UI 组件")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private Text _catFoodText;

        [Header("档次选择")]
        [SerializeField] private RectTransform _tierContainer;
        [SerializeField] private GameObject _tierCardPrefab;

        [Header("祝福选择")]
        [SerializeField] private RectTransform _blessingContainer;
        [SerializeField] private GameObject _blessingCardPrefab;

        [Header("按钮")]
        [SerializeField] private Button _skipButton;

        private FateSystem _fateSystem;
        private DataManager _dataManager;
        private PrayerTier _selectedTier;
        private List<PrayerReward> _currentRewards;

        private System.Action<PrayerReward> _onRewardSelected;
        private System.Action _onSkipped;

        private void Awake()
        {
            _fateSystem = new FateSystem();
            _dataManager = GameManager.Instance?.DataManager;
        }

        private void Start()
        {
            if (_skipButton != null)
            {
                _skipButton.onClick.AddListener(OnSkipClicked);
            }
        }

        /// <summary>
        /// 显示命运面板
        /// </summary>
        public void Show(System.Action<PrayerReward> onRewardSelected, System.Action onSkipped = null)
        {
            _onRewardSelected = onRewardSelected;
            _onSkipped = onSkipped;

            UpdateUI();
            ShowTierSelection();
            gameObject.SetActive(true);
        }

        private void UpdateUI()
        {
            if (_titleText != null)
                _titleText.text = PanelName;

            if (_hintText != null)
                _hintText.text = "选择一个祈愿档次";

            if (_catFoodText != null)
            {
                int catFood = _dataManager?.PlayerData?.catFood ?? 0;
                _catFoodText.text = $"木天蓼叶: {catFood}";
            }
        }

        private void ShowTierSelection()
        {
            ClearContainer(_tierContainer);
            ClearContainer(_blessingContainer);

            // 创建3个档次卡片
            CreateTierCard(PrayerTier.Free, "免费祈愿", "0 木天蓼叶", "可免费参与");
            CreateTierCard(PrayerTier.Normal, "普通祈愿", "300 木天蓼叶", "消耗木天蓼叶");
            CreateTierCard(PrayerTier.Grand, "盛大祈愿", "600 木天蓼叶", "消耗木天蓼叶");

            if (_tierContainer != null)
                _tierContainer.gameObject.SetActive(true);
            if (_blessingContainer != null)
                _blessingContainer.gameObject.SetActive(false);
        }

        private void CreateTierCard(PrayerTier tier, string name, string costText, string description)
        {
            if (_tierCardPrefab == null || _tierContainer == null) return;

            GameObject cardObj = Instantiate(_tierCardPrefab, _tierContainer);
            FateTierCard card = cardObj.GetComponent<FateTierCard>();

            if (card != null)
            {
                bool canSelect = _fateSystem.CanSelectTier(tier);
                card.Initialize(tier, name, costText, description, canSelect, OnTierSelected);
            }
        }

        private void OnTierSelected(PrayerTier tier)
        {
            _selectedTier = tier;

            // 执行祈愿
            _currentRewards = _fateSystem.PerformPrayer(tier);

            // 显示祝福选择
            ShowBlessingSelection();
        }

        private void ShowBlessingSelection()
        {
            if (_currentRewards == null || _currentRewards.Count == 0)
            {
                // 没有奖励，直接完成
                Complete();
                return;
            }

            ClearContainer(_blessingContainer);

            // 创建祝福卡片
            foreach (var reward in _currentRewards)
            {
                CreateBlessingCard(reward);
            }

            if (_tierContainer != null)
                _tierContainer.gameObject.SetActive(false);
            if (_blessingContainer != null)
                _blessingContainer.gameObject.SetActive(true);

            if (_hintText != null)
                _hintText.text = "选择一个祝福";
        }

        private void CreateBlessingCard(PrayerReward reward)
        {
            if (_blessingCardPrefab == null || _blessingContainer == null) return;

            GameObject cardObj = Instantiate(_blessingCardPrefab, _blessingContainer);
            FateBlessingCard card = cardObj.GetComponent<FateBlessingCard>();

            if (card != null)
            {
                card.Initialize(reward, OnBlessingSelected);
            }
        }

        private void OnBlessingSelected(PrayerReward reward)
        {
            _onRewardSelected?.Invoke(reward);
            Complete();
        }

        private void OnSkipClicked()
        {
            _onSkipped?.Invoke();
            Complete();
        }

        private void Complete()
        {
            gameObject.SetActive(false);
        }

        private void ClearContainer(RectTransform container)
        {
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }
    }

    /// <summary>
    /// 命运档次卡片
    /// </summary>
    public class FateTierCard : MonoBehaviour
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _descriptionText;
        [SerializeField] private Button _selectButton;
        [SerializeField] private Image _bgImage;

        private PrayerTier _tier;
        private System.Action<PrayerTier> _onSelectCallback;

        public void Initialize(PrayerTier tier, string name, string costText, string description, bool canSelect, System.Action<PrayerTier> onSelect)
        {
            _tier = tier;
            _onSelectCallback = onSelect;

            if (_nameText != null) _nameText.text = name;
            if (_costText != null) _costText.text = costText;
            if (_descriptionText != null) _descriptionText.text = description;

            if (_selectButton != null)
            {
                _selectButton.interactable = canSelect;
                _selectButton.onClick.AddListener(OnClicked);
            }

            // 设置颜色
            if (_bgImage != null)
            {
                switch (tier)
                {
                    case PrayerTier.Free:
                        _bgImage.color = new Color(0.7f, 0.9f, 0.7f); // 浅绿色
                        break;
                    case PrayerTier.Normal:
                        _bgImage.color = new Color(0.7f, 0.7f, 0.9f); // 浅蓝色
                        break;
                    case PrayerTier.Grand:
                        _bgImage.color = new Color(0.9f, 0.9f, 0.7f); // 浅金色
                        break;
                }
            }
        }

        private void OnClicked()
        {
            _onSelectCallback?.Invoke(_tier);
        }
    }

    /// <summary>
    /// 命运祝福卡片
    /// </summary>
    public class FateBlessingCard : MonoBehaviour
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descriptionText;
        [SerializeField] private Image _bgImage;
        [SerializeField] private Button _selectButton;

        private PrayerReward _reward;
        private System.Action<PrayerReward> _onSelectCallback;

        public void Initialize(PrayerReward reward, System.Action<PrayerReward> onSelect)
        {
            _reward = reward;
            _onSelectCallback = onSelect;

            if (_nameText != null) _nameText.text = reward.displayName;
            if (_descriptionText != null) _descriptionText.text = reward.description;

            if (_selectButton != null)
            {
                _selectButton.onClick.AddListener(OnClicked);
            }

            // 设置颜色
            if (_bgImage != null)
            {
                switch (reward.rewardType)
                {
                    case PrayerRewardType.TempStatBoost:
                        _bgImage.color = new Color(0.7f, 0.9f, 0.7f); // 浅绿色
                        break;
                    case PrayerRewardType.PermanentStatBoost:
                        _bgImage.color = new Color(0.7f, 0.7f, 0.9f); // 浅蓝色
                        break;
                    case PrayerRewardType.PercentStatBoost:
                        _bgImage.color = new Color(0.9f, 0.9f, 0.7f); // 浅金色
                        break;
                    case PrayerRewardType.CatFood:
                        _bgImage.color = new Color(0.9f, 0.8f, 0.6f); // 浅橙色
                        break;
                    default:
                        _bgImage.color = Color.white;
                        break;
                }
            }
        }

        private void OnClicked()
        {
            _onSelectCallback?.Invoke(_reward);
        }
    }
}
