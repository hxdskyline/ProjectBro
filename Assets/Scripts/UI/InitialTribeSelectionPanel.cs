using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TribeSystem;

namespace TribeSystem.UI
{
    /// <summary>
    /// 初始族群选择面板 - 六选一
    /// 点击卡片直接选择族群，无需确认
    /// </summary>
    public class InitialTribeSelectionPanel : UIPanel
    {
        private const string PanelName = "初始族群选择";

        [Header("UI 组件")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private RectTransform _tribesContainer;
        [SerializeField] private GameObject _tribeItemPrefab;

        // 事件
        public event Action OnSelectionComplete;

        public override void Initialize()
        {
            base.Initialize();
            Debug.Log("[InitialTribeSelectionPanel] 初始化");

            EnsureUIComponents();
            InitializeTribeButtons();
        }

        private void EnsureUIComponents()
        {
            if (_titleText == null)
                _titleText = transform.Find("Title")?.GetComponent<Text>();
            if (_hintText == null)
                _hintText = transform.Find("Hint")?.GetComponent<Text>();
            if (_tribesContainer == null)
                _tribesContainer = transform.Find("TribesContainer") as RectTransform;

            if (_titleText != null)
                _titleText.text = PanelName;
            if (_hintText != null)
                _hintText.text = "选择1个族群作为初始战力";
        }

        private void InitializeTribeButtons()
        {
            if (_tribesContainer == null) return;

            foreach (Transform child in _tribesContainer)
                Destroy(child.gameObject);

            foreach (TribeType type in System.Enum.GetValues(typeof(TribeType)))
            {
                if (type == TribeType.None) continue;

                GameObject cardGo = Instantiate(_tribeItemPrefab, _tribesContainer);
                cardGo.name = $"Tribe_{type}";

                var card = cardGo.GetComponent<InitialTribeEventOptionCard>();
                if (card != null)
                {
                    card.Setup(type, selectedType =>
                    {
                        CreateAndSaveInitialTribe(selectedType);
                        OnSelectionComplete?.Invoke();
                        gameObject.SetActive(false);
                    });
                }
            }
        }

        private void CreateAndSaveInitialTribe(TribeType tribeType)
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var config = TribeConfigLoader.Instance.GetTribeConfig(tribeType);
            if (config == null) return;

            var tribe = new TribeRecord
            {
                tribeId = dataManager.GetTribes()?.Count ?? 0,
                tribeType = tribeType,
                leader = new LeaderData
                {
                    leaderId = UnityEngine.Random.Range(1000, 9999),
                    name = $"{config.tribeName}族长",
                    baseAttack = config.leaderBaseStats.attack,
                    baseDefense = config.leaderBaseStats.defense,
                    baseHp = config.leaderBaseStats.hp,
                    baseMoveSpeed = config.leaderBaseStats.moveSpeed,
                    command = config.leaderBaseStats.command,
                    skillIds = new List<int>(),
                    permanentBuffs = new PermanentBuffs()
                },
                cats = new List<CatData>(),
                isActive = true
            };

            for (int i = 0; i < config.initialCatCount; i++)
            {
                var cat = CatData.CreateWithQuality(CatQuality.White, tribeType);
                tribe.cats.Add(cat);
            }

            dataManager.AddTribe(tribe);
            dataManager.SavePlayerData();
            Debug.Log($"[InitialTribeSelectionPanel] 初始族群 {tribeType} 已保存");
        }
    }
}
