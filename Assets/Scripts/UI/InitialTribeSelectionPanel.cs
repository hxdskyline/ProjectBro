using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TribeSystem;

namespace TribeSystem.UI
{
    /// <summary>
    /// 初始兵种选择面板 - 支持可配置轮数的三选一
    /// 游戏开始时：2轮，第十回合加新兵种：1轮
    /// </summary>
    public class InitialTribeSelectionPanel : UIPanel
    {
        private const string PanelName = "选择初始兵种";
        private const int PickCount = 3;

        [Header("UI 组件")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private RectTransform _tribesContainer;
        [SerializeField] private GameObject _tribeItemPrefab;

        // 事件
        public event Action OnSelectionComplete;

        /// <summary>
        /// 总选择轮数（默认2轮，第十回合可设置为1轮）
        /// </summary>
        public int TotalRounds { get; set; } = 2;

        private List<FighterConfig> _allFighters;
        private int _currentRound = 1;
        private int _firstSelection = -1;
        private List<int> _round1CandidateIds = new List<int>();
        private List<InitialTribeEventOptionCard> _cards = new List<InitialTribeEventOptionCard>();

        public override void Initialize()
        {
            base.Initialize();
            Debug.Log($"[InitialTribeSelectionPanel] 初始化, TotalRounds={TotalRounds}");

            EnsureUIComponents();
            LoadAllFighters();
            ShowRound(1);
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
        }

        private void LoadAllFighters()
        {
            _allFighters = new List<FighterConfig>();

            var allConfigs = TribeConfigLoader.Instance?.GetAllFighterConfigs();
            if (allConfigs == null)
            {
                Debug.LogError("[InitialTribeSelectionPanel] 无法加载兵种配置");
                return;
            }

            // 收集玩家已拥有的 fighterId
            var ownedFighterIds = new HashSet<int>();
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager != null)
            {
                var tribes = dataManager.GetTribes();
                if (tribes != null)
                {
                    foreach (var tribe in tribes)
                    {
                        if (tribe.fighterId > 0)
                            ownedFighterIds.Add(tribe.fighterId);
                    }
                }
            }

            // 排除已拥有的兵种
            foreach (var config in allConfigs)
            {
                if (!ownedFighterIds.Contains(config.fighterId))
                {
                    _allFighters.Add(config);
                }
            }

            Debug.Log($"[InitialTribeSelectionPanel] 加载了 {_allFighters.Count} 个未拥有的兵种（排除了 {ownedFighterIds.Count} 个已拥有的）");
        }

        private void ShowRound(int round)
        {
            _currentRound = round;

            if (_hintText != null)
            {
                if (TotalRounds == 1)
                    _hintText.text = "选择一个新兵种加入你的阵营";
                else
                    _hintText.text = $"第{round}轮选择（{round}/{TotalRounds}）— 从3个兵种中选1个";
            }

            // 清空旧卡片
            if (_tribesContainer != null)
            {
                foreach (Transform child in _tribesContainer)
                    Destroy(child.gameObject);
            }
            _cards.Clear();

            // 随机选3个候选
            List<FighterConfig> candidates = PickRandomCandidates(PickCount);

            // 第1轮时记录候选ID，第2轮排除这3个
            if (round == 1)
            {
                _round1CandidateIds.Clear();
                foreach (var c in candidates)
                    _round1CandidateIds.Add(c.fighterId);
            }

            foreach (var fighter in candidates)
            {
                GameObject cardGo = Instantiate(_tribeItemPrefab, _tribesContainer);
                cardGo.name = $"Fighter_{fighter.fighterId}";

                var card = cardGo.GetComponent<InitialTribeEventOptionCard>();
                if (card != null)
                {
                    card.SetupByFighter(fighter, OnFighterSelected);
                    _cards.Add(card);
                }
            }
        }

        private List<FighterConfig> PickRandomCandidates(int count)
        {
            // 构建候选池（第2轮排除第1轮出现的3个）
            List<FighterConfig> pool = new List<FighterConfig>();
            foreach (var f in _allFighters)
            {
                if (!_round1CandidateIds.Contains(f.fighterId))
                    pool.Add(f);
            }

            // Fisher-Yates 洗牌取前N个
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            int take = Mathf.Min(count, pool.Count);
            return pool.GetRange(0, take);
        }

        private void OnFighterSelected(int fighterId)
        {
            if (_currentRound < TotalRounds)
            {
                // 还有下一轮
                _firstSelection = fighterId;
                Debug.Log($"[InitialTribeSelectionPanel] 第{_currentRound}轮选择: fighterId={fighterId}");
                ShowRound(_currentRound + 1);
            }
            else
            {
                // 最后一轮，完成选择
                Debug.Log($"[InitialTribeSelectionPanel] 第{_currentRound}轮选择: fighterId={fighterId}");

                var dataManager = GameManager.Instance?.DataManager;
                if (dataManager == null) return;

                // 根据轮数创建对应数量的族群
                if (TotalRounds == 1)
                {
                    // 只选了1轮，创建1个族群
                    CreateAndSaveInitialTribe(fighterId, dataManager);
                }
                else
                {
                    // 选了2轮，创建2个族群
                    CreateAndSaveInitialTribe(_firstSelection, dataManager);
                    CreateAndSaveInitialTribe(fighterId, dataManager);
                }

                dataManager.SavePlayerData();
                OnSelectionComplete?.Invoke();
                gameObject.SetActive(false);
            }
        }

        private void CreateAndSaveInitialTribe(int fighterId, DataManager dataManager)
        {
            var fighterConfig = TribeConfigLoader.Instance.GetFighterConfig(fighterId);
            if (fighterConfig == null) return;

            TribeType tribeType = (TribeType)fighterConfig.tribeType;

            var tribe = new TribeRecord
            {
                tribeId = dataManager.GetTribes()?.Count ?? 0,
                fighterId = fighterId,
                tribeType = tribeType,
                leader = new LeaderData
                {
                    leaderId = UnityEngine.Random.Range(1000, 9999),
                    name = fighterConfig.fighterName,
                    baseAttack = fighterConfig.attack,
                    baseDefense = fighterConfig.defense,
                    baseHp = fighterConfig.hp,
                    baseMoveSpeed = fighterConfig.moveSpeed,
                    skillIds = new List<int>(),
                    permanentBuffs = new PermanentBuffs()
                },
                cats = new List<CatData>(),
                isActive = true
            };

            dataManager.AddTribe(tribe);
            Debug.Log($"[InitialTribeSelectionPanel] 添加兵种: {fighterConfig.fighterName} (fighterId={fighterId})");
        }
    }
}
