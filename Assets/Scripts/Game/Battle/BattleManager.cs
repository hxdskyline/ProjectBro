using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using TribeSystem;
using BattleSystem.Fighter;
using BattleSystem.Avatar;
using BattleSystem.Effects;

namespace BattleSystem
{
    /// <summary>
    /// 战斗管理器 - 管理战斗逻辑
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [Header("Demo Avatar Setup")]
        [SerializeField] private GameObject _fighterPrefab;
        [SerializeField] private AvatarAnimationDefinition _playerAvatarDefinition;
        [SerializeField] private AvatarAnimationDefinition _enemyAvatarDefinition;
        [SerializeField] private int _fightersPerCamp = 15;
        [SerializeField] private Vector2 _spawnAreaMin = new Vector2(-6.5f, -3.5f);
        [SerializeField] private Vector2 _spawnAreaMax = new Vector2(6.5f, 3.5f);
        [SerializeField] private float _spawnMinDistance = 1.5f;
        [SerializeField] private int _spawnTryCount = 24;
        [SerializeField] private float _fighterScale = 0.6f;
        [SerializeField] private Color _playerTint = new Color(0.6f, 0.9f, 1f, 1f);
        [SerializeField] private Color _enemyTint = new Color(1f, 0.7f, 0.7f, 1f);
        [SerializeField] private BattleUnitTypeConfig _playerUnitType;
        [SerializeField] private BattleUnitTypeConfig _enemyUnitType;

        [Header("Demo Battle Stats")]
        [SerializeField] private float _attackResolveDelay = 0.45f;
        [SerializeField] private float _attackCooldown = 0.6f;
        [SerializeField] private float _seekDelay = 1.0f;
        [SerializeField] private float _deathDuration = 2.0f;

        private int _levelId;
        private bool _isInBattle;
        private Coroutine _battleCoroutine;
        private BattleFighter[] _playerFighters;
        private BattleFighter[] _enemyFighters;
        private BattleSimulation _simulation;
        private BattleFighterSpawnDefinition[] _playerFighterDefinitions;
        private int _enemyFighterCount;
        private UnitStaticAttributes? _enemyStaticAttributes;
        private TerrainType _currentTerrain = TerrainType.Plain;
        private WeatherType _currentWeather = WeatherType.Sunny;
        private int _artifactAtkPerDeadCat;
        private int _artifactLeaderLastDeadCount;
        private int _lastEnemyDeathCount;

        public System.Action<bool> BattleEnded;

        public bool IsInBattle => _isInBattle;
        public int LevelId => _levelId;
        public BattleFighter[] PlayerFighters => _playerFighters;
        public BattleFighter[] EnemyFighters => _enemyFighters;

        public void Initialize(int levelId)
        {
            _levelId = levelId;
            Debug.Log($"[BattleManager] Initialized for level: {levelId}");
        }

        public void ConfigureDemoAvatars(AvatarAnimationDefinition playerDefinition, AvatarAnimationDefinition enemyDefinition)
        {
            _playerAvatarDefinition = playerDefinition;
            _enemyAvatarDefinition = enemyDefinition;
        }

        public void ConfigureFighterPrefab(GameObject fighterPrefab)
        {
            _fighterPrefab = fighterPrefab;
        }

        public void ConfigurePlayerFighters(BattleFighterSpawnDefinition[] playerFighterDefinitions)
        {
            _playerFighterDefinitions = playerFighterDefinitions;
        }

        public void ConfigureEnemyFighterCount(int enemyFighterCount)
        {
            _enemyFighterCount = Mathf.Max(1, enemyFighterCount);
        }

        public void ConfigureEnemyStats(UnitStaticAttributes stats)
        {
            _enemyStaticAttributes = stats;
        }

        public void ConfigureTerrainWeather(TerrainType terrain, WeatherType weather)
        {
            _currentTerrain = terrain;
            _currentWeather = weather;
        }

        public void StartBattle()
        {
            if (_isInBattle)
            {
                return;
            }

            _isInBattle = true;
            Debug.Log("[BattleManager] Battle started");

            BuildDemoFighters();

            // 初始化奇物动态效果（亡猫之力等）— 从 runEquipments 读取
            InitArtifactEffects();

            // 应用地形/天气 BUFF 到玩家单位（通过运行时修正体系）
            ApplyTerrainWeatherBuffs();

            // 应用词缀 buff（从 ownedAffixes 读取并应用到所有友方单位）
            ApplyAffixBuffs();

            // 应用光环 buff（从 leader/cat 的 ActiveBuffs 传播到 RuntimeAttributes）
            ApplyAuraBuffs();

            // 同步所有 fighter 的 HUD 最大生命值（buff 可能改变了 MaxHp）
            SyncFighterHudMaxHp(_playerFighters);

            // 应用天生特殊 buff
            // 初始化战斗模拟
            _simulation = new BattleSimulation(
                _playerFighters,
                _enemyFighters,
                new BattleSimulationConfig
                {
                    AttackResolveDelay = _attackResolveDelay,
                    AttackCooldown = _attackCooldown,
                    SeekDelay = _seekDelay,
                    DeathDuration = _deathDuration
                });

            BattleSimulation.OnBulletFired += SpawnBullet;
            _battleCoroutine = StartCoroutine(DemoBattleLoop());
        }

        public void EndBattle(bool victory)
        {
            if (!_isInBattle)
            {
                return;
            }

            _isInBattle = false;
            BattleSimulation.OnBulletFired -= SpawnBullet;

            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }

            if (victory)
            {
                Debug.Log("[BattleManager] Battle ended - Victory!");
            }
            else
            {
                Debug.Log("[BattleManager] Battle ended - Defeat!");
            }

            // Battle summary log
            LogBattleSummary(victory);

            // 将战斗内 Persistent buff 同步回 FighterData（饱食层等）
            BattleBuffService.SyncPersistentBuffsToUnits(_playerFighters);

            // 同步战斗后的HP状态回FighterData
            SyncHealthToFighterData(victory);

            // 清除所有战斗内 buff（BattleOnly 类型）
            BuffService.ClearAllBattleBuffs();

            // 清理尸体和召唤物
            _simulation?.CorpseManager?.Clear();
            _simulation?.SummonManager?.Clear();

            // 处理HP持久化（满目疮痍debuff等）
            var campaign = GameManager.Instance?.BattleCampaignRuntime;
            bool isBossBattle = campaign != null && _levelId >= campaign.MaxBattleCount;
            var healthPersistence = new HealthPersistenceSystem();
            healthPersistence.OnBattleEnd(victory, isBossBattle);

            // Ensure settlement UI appears over a clean battlefield.
            ClearBattlefield();

            BattleEnded?.Invoke(victory);
        }

        public void PauseBattle()
        {
            Time.timeScale = 0;
            Debug.Log("[BattleManager] Battle paused");
        }

        public void ResumeBattle()
        {
            Time.timeScale = 1;
            Debug.Log("[BattleManager] Battle resumed");
        }

        public bool TryUseConsumable(ConsumableEffectType effectType)
        {
            if (_simulation == null || !_isInBattle)
            {
                Debug.LogWarning("[BattleManager] Cannot use consumable: not in battle");
                return false;
            }

            _simulation.ApplyConsumable(effectType);
            return true;
        }

        private void OnDestroy()
        {
            BattleSimulation.OnBulletFired -= SpawnBullet;
            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }
        }

        /// <summary>
        /// 同步战斗后的HP状态回FighterData
        /// </summary>
        private void SyncHealthToFighterData(bool victory)
        {
            if (_playerFighters == null) return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            var tribes = dataManager?.PlayerData?.tribes;
            if (tribes == null) return;

            foreach (BattleFighter fighter in _playerFighters)
            {
                if (fighter == null || fighter.RuntimeAttributes == null) continue;

                // 查找对应的FighterData
                FighterData unit = FindUnit(tribes, fighter.TribeType, fighter.FighterId);
                if (unit == null) continue;

                // 同步HP
                if (fighter.IsDead || fighter.IsDying || fighter.IsRemoved)
                {
                    unit.currentHp = 0;
                }
                else
                {
                    unit.currentHp = fighter.RuntimeAttributes.CurrentHp;
                }
            }
        }

        /// <summary>
        /// 查找对应的FighterData
        /// </summary>
        private FighterData FindUnit(List<TribeSystem.TribeRecord> tribes, TribeSystem.TribeType tribeType, int fighterId)
        {
            foreach (var tribe in tribes)
            {
                if (tribe.tribeType != tribeType) continue;
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    if (unit.fighterId == fighterId)
                    {
                        return unit;
                    }
                }
            }
            return null;
        }

        private void BuildDemoFighters()
        {
            if (_playerAvatarDefinition == null || _enemyAvatarDefinition == null)
            {
                Debug.LogWarning("[BattleManager] AvatarAnimationDefinition missing. Please assign player/enemy definitions from BattlePanel.");
            }

            ClearOldAvatars();
            SpawnBattleBackground();

            BattleSpawnResult result = BattleSpawner.Spawn(
                transform,
                new BattleSpawnConfig
                {
                    FighterPrefab = _fighterPrefab,
                    PlayerAvatarDefinition = _playerAvatarDefinition,
                    EnemyAvatarDefinition = _enemyAvatarDefinition,
                    FightersPerCamp = _fightersPerCamp,
                    EnemyFighterCount = _enemyFighterCount > 0 ? _enemyFighterCount : _fightersPerCamp,
                    SpawnAreaMin = _spawnAreaMin,
                    SpawnAreaMax = _spawnAreaMax,
                    SpawnMinDistance = _spawnMinDistance,
                    SpawnTryCount = _spawnTryCount,
                    FighterScale = _fighterScale,
                    PlayerTint = _playerTint,
                    EnemyTint = _enemyTint,
                    PlayerFighterDefinitions = _playerFighterDefinitions,
                    PlayerUnitType = _playerUnitType,
                    EnemyUnitType = _enemyUnitType,
                    EnemyStaticAttributes = _enemyStaticAttributes
                });

            _playerFighters = result.PlayerFighters;
            _enemyFighters = result.EnemyFighters;

            Debug.Log($"[BattleManager] Demo fighters ready. Player={_playerFighters.Length}, Enemy={_enemyFighters.Length}");
        }

        private IEnumerator DemoBattleLoop()
        {
            if (_simulation == null || !_simulation.IsReady)
            {
                Debug.LogError("[BattleManager] Demo fighters are not ready.");
                EndBattle(false);
                yield break;
            }

            while (_isInBattle)
            {
                // 动态更新奶牛族长的猫群之力 buff
                // 天生 buff 的动态更新由 IBuffEffect.OnTick 处理
                // 动态更新奇物：每死一只小猫+攻击
                UpdateArtifactLeaderBuff();
                // 战斗内成长触发
                UpdateBattleGrowth();

                if (_simulation.Tick(Time.deltaTime, out bool playerVictory))
                {
                    EndBattle(playerVictory);
                    yield break;
                }

                yield return null;
            }
        }

        private void ClearOldAvatars()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private void SpawnBattleBackground()
        {
            var go = new GameObject("BattleBackground");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -1000;

            var handle = Addressables.LoadAssetAsync<Sprite>("ui/sprite/common/greenbg");
            handle.Completed += op =>
            {
                if (op.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    sr.sprite = op.Result;
                else
                    Debug.LogWarning("[BattleManager] Failed to load battle background sprite");
            };
        }

        private void ClearBattlefield()
        {
            _simulation = null;
            _playerFighters = null;
            _enemyFighters = null;
            _artifactAtkPerDeadCat = 0;
            _artifactLeaderLastDeadCount = 0;
            _lastEnemyDeathCount = 0;
            ClearOldAvatars();
        }

        /// <summary>
        /// 将地形/天气 BUFF 应用到所有玩家单位的运行时修正属性上
        /// </summary>
        private void ApplyTerrainWeatherBuffs()
        {
            if (_playerFighters == null || _playerFighters.Length == 0)
                return;

            for (int i = 0; i < _playerFighters.Length; i++)
            {
                BattleFighter fighter = _playerFighters[i];
                if (fighter == null || fighter.RuntimeAttributes == null)
                    continue;

                TerrainWeatherBuff buff = TribeBattleBuffProvider.GetBuff(
                    fighter.TribeType, _currentTerrain, _currentWeather);

                if (buff.IsNeutral)
                    continue;

                UnitRuntimeAttributes attrs = fighter.RuntimeAttributes;
                attrs.AttackPercentBuff += buff.attackPercent;
                attrs.DefensePercentBuff += buff.defensePercent;
                attrs.HpPercentBuff += buff.hpPercent;
                attrs.SpeedPercentBuff += buff.speedPercent;
                attrs.Recalculate();
            }

            Debug.Log($"[BattleManager] Applied terrain/weather BUFFs: " +
                $"Terrain={_currentTerrain}, Weather={_currentWeather}");
        }

        /// <summary>
        /// 初始化奇物动态效果（亡猫之力等）— 从 runEquipments 读取特殊效果类型
        /// </summary>
        private void InitArtifactEffects()
        {
            if (_playerFighters == null || _playerFighters.Length == 0)
                return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var equipments = dataManager.PlayerData?.runEquipments;
            if (equipments == null || equipments.Count == 0)
                return;

            foreach (var equip in equipments)
            {
                if (equip.effects == null) continue;
                foreach (var eff in equip.effects)
                {
                    if (eff.gameEffectType < 0) continue;
                    switch ((TribeSystem.GameEffect)eff.gameEffectType)
                    {
                        case TribeSystem.GameEffect.LeaderAttackPerDeadCat:
                            _artifactAtkPerDeadCat += Mathf.RoundToInt(eff.value);
                            break;
                    }
                }
            }

            if (_artifactAtkPerDeadCat > 0)
                Debug.Log($"[BattleManager] InitArtifactEffects: 亡猫之力 atkPerDeadCat={_artifactAtkPerDeadCat}");
        }

        /// <summary>
        /// 应用光环 buff — 从 leader/cat 的 ActiveBuffs 传播到 RuntimeAttributes
        /// 注意：主要的 buff 传递已在 BattleSpawner.CreateFighter 中通过 AuraBuffs 参数完成。
        /// 此方法处理额外的非标准修正（如地形/天气等外部字段）。
        /// </summary>
        private void ApplyAuraBuffs()
        {
            // 光环 buff 已在 BattleSpawner.CreateFighter 中通过 AuraBuffs 参数应用到 RuntimeAttributes
            // 此方法保留用于未来的扩展需求
        }

        /// <summary>
        /// 应用词缀 buff — 从 playerData.ownedAffixes 读取词缀，应用到所有友方单位
        /// </summary>
        private void ApplyAffixBuffs()
        {
            if (_playerFighters == null || _playerFighters.Length == 0)
                return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var ownedAffixes = dataManager.PlayerData?.ownedAffixes;
            if (ownedAffixes == null || ownedAffixes.Count == 0)
            {
                Debug.Log("[BattleManager] ApplyAffixBuffs: ownedAffixes is null or empty, skipping");
                return;
            }

            Debug.Log($"[BattleManager] ApplyAffixBuffs: ownedAffixes count={ownedAffixes.Count}, ids=[{string.Join(",", ownedAffixes)}]");

            // 加载词缀数据
            var allAffixes = LoadAllAffixes();
            if (allAffixes == null || allAffixes.Count == 0)
            {
                Debug.LogWarning("[BattleManager] ApplyAffixBuffs: failed to load affix config");
                return;
            }

            Debug.Log($"[BattleManager] ApplyAffixBuffs: loaded {allAffixes.Count} affixes from config");

            // 汇总所有词缀的效果（只应用 fighterId=0 的通用词缀）
            float atkFlatBonus = 0f, defFlatBonus = 0f, hpFlatBonus = 0f;
            float atkPercentBonus = 0f, defPercentBonus = 0f, hpPercentBonus = 0f, spdPercentBonus = 0f;

            foreach (var affixId in ownedAffixes)
            {
                if (!allAffixes.TryGetValue(affixId, out var affix))
                {
                    Debug.LogWarning($"[BattleManager] ApplyAffixBuffs: affix '{affixId}' not found in config");
                    continue;
                }

                // 只应用通用词缀（fighterId=0），兵种词缀需要在创建 fighter 时单独处理
                if (affix.fighterId != 0)
                {
                    Debug.Log($"[BattleManager] ApplyAffixBuffs: skipping tribe-specific affix '{affixId}' (fighterId={affix.fighterId})");
                    continue;
                }

                Debug.Log($"[BattleManager] ApplyAffixBuffs: applying affix '{affixId}' ({affix.displayName})");

                var affixEffects = affix.ResolveEffects();
                if (affixEffects == null || affixEffects.Count == 0) continue;

                foreach (var eff in affixEffects)
                {
                    if (eff.isPercent)
                    {
                        switch (eff.statType)
                        {
                            case TribeSystem.StatType.Attack: atkPercentBonus += eff.value; break;
                            case TribeSystem.StatType.Defense: defPercentBonus += eff.value; break;
                            case TribeSystem.StatType.Hp: hpPercentBonus += eff.value; break;
                            case TribeSystem.StatType.MoveSpeed: spdPercentBonus += eff.value; break;
                        }
                    }
                    else
                    {
                        switch (eff.statType)
                        {
                            case TribeSystem.StatType.Attack: atkFlatBonus += eff.value; break;
                            case TribeSystem.StatType.Defense: defFlatBonus += eff.value; break;
                            case TribeSystem.StatType.Hp: hpFlatBonus += eff.value; break;
                        }
                    }
                }
            }

            // 应用到所有玩家单位
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                BattleFighter fighter = _playerFighters[i];
                if (fighter == null || fighter.RuntimeAttributes == null)
                    continue;

                UnitRuntimeAttributes attrs = fighter.RuntimeAttributes;
                attrs.AttackFlatBuff += (int)atkFlatBonus;
                attrs.DefenseFlatBuff += (int)defFlatBonus;
                attrs.HpFlatBuff += (int)hpFlatBonus;
                attrs.AttackPercentBuff += atkPercentBonus;
                attrs.DefensePercentBuff += defPercentBonus;
                attrs.HpPercentBuff += hpPercentBonus;
                attrs.SpeedPercentBuff += spdPercentBonus;

                attrs.Recalculate();
            }

            if (atkFlatBonus != 0 || defFlatBonus != 0 || hpFlatBonus != 0 ||
                atkPercentBonus != 0 || defPercentBonus != 0 || hpPercentBonus != 0 || spdPercentBonus != 0)
            {
                Debug.Log($"[BattleManager] Applied affix BUFFs: ATK+{atkFlatBonus}({atkPercentBonus:P0}) DEF+{defFlatBonus}({defPercentBonus:P0}) HP+{hpFlatBonus}({hpPercentBonus:P0}) SPD+{spdPercentBonus:P0}");
            }
        }

        /// <summary>
        /// 同步所有 fighter 的 HUD 最大生命值（buff 可能改变了 MaxHp）
        /// </summary>
        private void SyncFighterHudMaxHp(BattleFighter[] fighters)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f == null || f.Transform == null || f.RuntimeAttributes == null) continue;
                var hud = f.Transform.GetComponent<FighterHUD>();
                if (hud != null)
                {
                    hud.SetMaxHp(f.RuntimeAttributes.MaxHp);
                    hud.UpdateHp(f.RuntimeAttributes.CurrentHp);
                }
            }
        }

        /// <summary>
        /// 从 affix_config.json 加载所有词缀数据
        /// </summary>
        private Dictionary<string, TribeSystem.AffixData> LoadAllAffixes()
        {
            var allAffixes = new Dictionary<string, TribeSystem.AffixData>();
            try
            {
                string configPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Tables/affix_config.json");
                if (!System.IO.File.Exists(configPath))
                {
                    Debug.LogError($"[BattleManager] 词缀配置文件不存在: {configPath}");
                    return allAffixes;
                }

                string json = System.IO.File.ReadAllText(configPath);
                var root = LitJson.JsonMapper.ToObject(json);

                if (root != null && root.Keys.Contains("affixes"))
                {
                    var affixesJson = root["affixes"];
                    for (int i = 0; i < affixesJson.Count; i++)
                    {
                        var item = affixesJson[i];
                        var affix = new TribeSystem.AffixData
                        {
                            affixId = ReadString(item, "affixId", ""),
                            displayName = ReadString(item, "displayName", ""),
                            fighterId = ReadInt(item, "fighterId", 0),
                            buffIds = new List<int>()
                        };

                        // 解析 buffIds
                        if (item.Keys.Contains("buffIds") && item["buffIds"].IsArray)
                        {
                            var buffIdsJson = item["buffIds"];
                            for (int b = 0; b < buffIdsJson.Count; b++)
                            {
                                if (int.TryParse(buffIdsJson[b].ToString(), out int buffId))
                                    affix.buffIds.Add(buffId);
                            }
                        }

                        // 从 buff_config 解析描述
                        affix.description = affix.ResolveDescription();

                        if (!string.IsNullOrEmpty(affix.affixId))
                        {
                            allAffixes[affix.affixId] = affix;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleManager] 加载词缀数据失败: {e.Message}");
            }

            return allAffixes;
        }

        private static string ReadString(LitJson.JsonData json, string key, string defaultValue)
        {
            return json.Keys.Contains(key) ? json[key].ToString() : defaultValue;
        }

        private static int ReadInt(LitJson.JsonData json, string key, int defaultValue)
        {
            return json.Keys.Contains(key) && int.TryParse(json[key].ToString(), out int v) ? v : defaultValue;
        }

        private static float ReadFloat(LitJson.JsonData json, string key, float defaultValue)
        {
            return json.Keys.Contains(key) && float.TryParse(json[key].ToString(), out float v) ? v : defaultValue;
        }

        private static bool ReadBool(LitJson.JsonData json, string key)
        {
            return json.Keys.Contains(key)
                && bool.TryParse(json[key].ToString(), out bool v)
                && v;
        }


        /// <summary>
        /// 动态更新奇物效果：每有一只死去的单位，所有存活单位增加攻击力
        /// </summary>
        private void UpdateArtifactLeaderBuff()
        {
            if (_artifactAtkPerDeadCat <= 0 || _playerFighters == null)
                return;

            // 统计已死亡的单位数量
            int deadCount = 0;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null) continue;
                if (f.IsDead || f.IsDying || f.IsRemoved) deadCount++;
            }

            // 数量没变化则跳过
            if (deadCount == _artifactLeaderLastDeadCount)
                return;

            // 回退旧的 buff，应用新的到所有存活单位
            int delta = deadCount - _artifactLeaderLastDeadCount;
            _artifactLeaderLastDeadCount = deadCount;

            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || f.IsDead || f.IsRemoved) continue;
                UnitRuntimeAttributes attrs = f.RuntimeAttributes;
                if (attrs == null) continue;
                attrs.AttackFlatBuff += delta * _artifactAtkPerDeadCat;
                attrs.Recalculate();
            }

            Debug.Log($"[BattleManager] 奇物(亡猫之力)更新: {deadCount}只单位死亡，+{delta * _artifactAtkPerDeadCat}攻击");
        }

        /// <summary>
        /// 战斗内成长触发：检测敌人死亡，为橘猫族长添加饱食层
        /// </summary>
        private void UpdateBattleGrowth()
        {
            if (_enemyFighters == null || _playerFighters == null) return;

            // 统计当前死亡/已移除的敌人数量
            int enemyDeathCount = 0;
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                if (_enemyFighters[i] != null && (_enemyFighters[i].IsDying || _enemyFighters[i].IsRemoved))
                    enemyDeathCount++;
            }

            if (enemyDeathCount <= _lastEnemyDeathCount) return;
            int newKills = enemyDeathCount - _lastEnemyDeathCount;
            _lastEnemyDeathCount = enemyDeathCount;

            // 找到橘猫单位
            BattleFighter orangeUnit = null;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || !f.IsAlive) continue;
                if (f.TribeType != TribeType.Orange) continue;
                if (f.RuntimeAttributes == null) continue;
                orangeUnit = f;
                break;
            }
            if (orangeUnit == null) return;

            // 应用饱食层（Persistent buff，自动叠加）
            for (int k = 0; k < newKills; k++)
            {
                int prevMaxHp = orangeUnit.RuntimeAttributes.MaxHp;
                orangeUnit.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFullnessStack(60f, 4f));
                orangeUnit.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFullnessAtkStack(4f));
                orangeUnit.RuntimeAttributes.Recalculate();
                orangeUnit.RuntimeAttributes.CurrentHp += orangeUnit.RuntimeAttributes.MaxHp - prevMaxHp;
            }
        }

        /// <summary>
        /// 生成子弹（狸花远程攻击）
        /// </summary>
        private void SpawnBullet(BulletData data)
        {
            if (data.Attacker == null || data.Target == null) return;

            GameObject bulletGo = new GameObject("Bullet");
            bulletGo.transform.position = data.Attacker.Transform.position;
            bulletGo.transform.SetParent(transform);

            var bullet = bulletGo.AddComponent<BattleBullet>();
            bullet.Setup(data.Attacker, data.Target, data.Damage, data.IsCritical);
        }

        private void LogBattleSummary(bool victory)
        {
            if (_playerFighters == null || _enemyFighters == null) return;

            int pAlive = 0, pDead = 0;
            int pTotalHp = 0, pMaxHp = 0;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null) continue;
                pMaxHp += f.StaticAttributes.MaxHp;
                if (f.IsRemoved || f.IsDying)
                {
                    pDead++;
                }
                else
                {
                    pAlive++;
                    pTotalHp += f.CurrentHp;
                }
            }

            int eAlive = 0, eDead = 0;
            int eTotalHp = 0, eMaxHp = 0;
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                var f = _enemyFighters[i];
                if (f == null) continue;
                eMaxHp += f.StaticAttributes.MaxHp;
                if (f.IsRemoved || f.IsDying)
                {
                    eDead++;
                }
                else
                {
                    eAlive++;
                    eTotalHp += f.CurrentHp;
                }
            }

            string firstPlayerStats = "";
            if (_playerFighters.Length > 0 && _playerFighters[0] != null)
            {
                var s = _playerFighters[0].StaticAttributes;
                firstPlayerStats = $" | Leader: ATK={s.Attack} DEF={s.Defense} HP={s.MaxHp} SPD={s.MoveSpeed}";
            }
            string firstEnemyStats = "";
            if (_enemyFighters.Length > 0 && _enemyFighters[0] != null)
            {
                var s = _enemyFighters[0].StaticAttributes;
                firstEnemyStats = $" | Enemy: ATK={s.Attack} DEF={s.Defense} HP={s.MaxHp} SPD={s.MoveSpeed}";
            }

            Debug.Log($"[BattleSummary] {(victory ? "WIN" : "LOSE")} | " +
                $"Player: {pAlive}/{_playerFighters.Length} alive, {pTotalHp}/{pMaxHp} HP{firstPlayerStats} | " +
                $"Enemy: {eAlive}/{_enemyFighters.Length} alive, {eTotalHp}/{eMaxHp} HP{firstEnemyStats}");
        }
    }
}
