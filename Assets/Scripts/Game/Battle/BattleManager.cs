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
        private BattleFighter _artifactLeaderFighter;
        private int _artifactAtkPerDeadCat;
        private int _artifactLeaderLastDeadCount;
        private LeaderSkillExecutor _leaderSkillExecutor;
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

            // 应用地形/天气 BUFF 到玩家单位（通过运行时修正体系）
            ApplyTerrainWeatherBuffs();

            // 应用饰品属性加成
            ApplyAccessoryBuffs();

            // 应用光环 buff（从 leader/cat 的 ActiveBuffs 传播到 RuntimeAttributes）
            ApplyAuraBuffs();

            // 恢复 Persistent buff（饱食层等）到 RuntimeAttributes
            BattleBuffService.RestorePersistentBuffsToRuntime(_playerFighters);

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

            // 初始化首领技能执行器（需要 _simulation 提供 CorpseManager / SummonManager）
            InitLeaderSkillExecutor();
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

            // 将战斗内 Persistent buff 同步回 LeaderData（饱食层等）
            BattleBuffService.SyncPersistentBuffsToLeaderData(_playerFighters);

            // 清除所有战斗内 buff（BattleOnly 类型）
            BuffService.ClearAllBattleBuffs();

            // 清理尸体和召唤物
            _simulation?.CorpseManager?.Clear();
            _simulation?.SummonManager?.Clear();

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
                // 动态更新奇物：每死一只小猫族长+攻击
                UpdateArtifactLeaderBuff();
                // 首领技能 tick
                if (_leaderSkillExecutor != null)
                    _leaderSkillExecutor.Tick(Time.deltaTime, _playerFighters, _enemyFighters);
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
            sr.sortingOrder = -100;

            var handle = Addressables.LoadAssetAsync<Sprite>("ui/sprite/addcat/greenbg");
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
            _artifactLeaderFighter = null;
            _artifactAtkPerDeadCat = 0;
            _artifactLeaderLastDeadCount = 0;
            _leaderSkillExecutor = null;
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
        /// 将 EquipmentRecord 中的饰品属性加成应用到所有玩家单位
        /// </summary>
        private void ApplyAccessoryBuffs()
        {
            if (_playerFighters == null || _playerFighters.Length == 0)
                return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var equipments = dataManager.PlayerData?.runEquipments;
            if (equipments == null || equipments.Count == 0)
                return;

            // 汇总所有装备的效果
            float atkBonus = 0f, defBonus = 0f, hpBonus = 0f, spdBonus = 0f;
            int leaderSpdFlatBonus = 0;

            foreach (var equip in equipments)
            {
                if (equip.effects == null) continue;
                foreach (var eff in equip.effects)
                {
                    // 使用 gameEffectType 区分特殊效果
                    if (eff.gameEffectType >= 0)
                    {
                        switch ((TribeSystem.GameEffect)eff.gameEffectType)
                        {
                            case TribeSystem.GameEffect.AttackPercent:  atkBonus += eff.value; break;
                            case TribeSystem.GameEffect.DefensePercent: defBonus += eff.value; break;
                            case TribeSystem.GameEffect.HpPercent:      hpBonus += eff.value; break;
                            case TribeSystem.GameEffect.SpeedPercent:   spdBonus += eff.value; break;
                            case TribeSystem.GameEffect.LeaderSpeedFlat: leaderSpdFlatBonus += Mathf.RoundToInt(eff.value); break;
                            case TribeSystem.GameEffect.LeaderAttackPerDeadCat: _artifactAtkPerDeadCat += Mathf.RoundToInt(eff.value); break;
                            case TribeSystem.GameEffect.AllPercent:
                                atkBonus += eff.value;
                                defBonus += eff.value;
                                hpBonus += eff.value;
                                spdBonus += eff.value;
                                break;
                        }
                    }
                    else
                    {
                        // 兼容没有 gameEffectType 的旧数据，按 StatType 映射
                        if (eff.isPercent)
                        {
                            switch (eff.statType)
                            {
                                case TribeSystem.StatType.Attack:  atkBonus += eff.value; break;
                                case TribeSystem.StatType.Defense: defBonus += eff.value; break;
                                case TribeSystem.StatType.Hp:      hpBonus += eff.value; break;
                                case TribeSystem.StatType.MoveSpeed: spdBonus += eff.value; break;
                            }
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
                attrs.AttackPercentBuff  += atkBonus;
                attrs.DefensePercentBuff += defBonus;
                attrs.HpPercentBuff      += hpBonus;
                attrs.SpeedPercentBuff   += spdBonus;

                // 族长速度固定值加成
                if (fighter.IsLeader)
                {
                    if (leaderSpdFlatBonus != 0)
                        attrs.SpeedFlatBuff += leaderSpdFlatBonus;
                    // 记录族长引用（用于奇物动态加成）
                    if (_artifactAtkPerDeadCat > 0 && _artifactLeaderFighter == null)
                        _artifactLeaderFighter = fighter;
                }

                attrs.Recalculate();
            }

            if (atkBonus != 0 || defBonus != 0 || hpBonus != 0 || spdBonus != 0 || leaderSpdFlatBonus != 0)
            {
                Debug.Log($"[BattleManager] Applied accessory BUFFs from runEquipments: ATK+{atkBonus:P0} DEF+{defBonus:P0} HP+{hpBonus:P0} SPD+{spdBonus:P0} LeaderSpd+{leaderSpdFlatBonus}");
            }
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
        /// 初始化首领技能执行器
        /// </summary>
        private void InitLeaderSkillExecutor()
        {
            if (_playerFighters == null) return;

            // 找到族长
            BattleFighter leader = null;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                if (_playerFighters[i] != null && _playerFighters[i].IsLeader)
                {
                    leader = _playerFighters[i];
                    break;
                }
            }
            if (leader == null) return;

            _leaderSkillExecutor = new LeaderSkillExecutor(leader, leader.TribeType);
            _leaderSkillExecutor.SetManagers(_simulation.CorpseManager, _simulation.SummonManager);

            // 加载技能配置
            var configText = LoadLeaderSkillConfig();
            if (configText != null)
            {
                var config = JsonUtility.FromJson<LeaderSkillConfigTable>(configText);
                _leaderSkillExecutor.LoadSkills(config);
            }
        }

        private string LoadLeaderSkillConfig()
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Tables/leader_skill_config.json");
            try
            {
                if (System.IO.File.Exists(path))
                    return System.IO.File.ReadAllText(path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BattleManager] 加载首领技能配置失败: {e.Message}");
            }
            return null;
        }



        /// <summary>
        /// 动态更新奇物效果：每有一只死去的小猫，族长增加攻击力
        /// </summary>
        private void UpdateArtifactLeaderBuff()
        {
            if (_artifactLeaderFighter == null || _artifactAtkPerDeadCat <= 0)
                return;
            if (_artifactLeaderFighter.IsDead || _artifactLeaderFighter.IsRemoved)
                return;

            // 统计已死亡的小猫数量
            int deadCatCount = 0;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || f == _artifactLeaderFighter) continue;
                if (f.IsLeader) continue; // 跳过族长
                if (f.IsDead || f.IsDying || f.IsRemoved) deadCatCount++;
            }

            // 数量没变化则跳过
            if (deadCatCount == _artifactLeaderLastDeadCount)
                return;

            // 回退旧的 buff，应用新的
            UnitRuntimeAttributes attrs = _artifactLeaderFighter.RuntimeAttributes;
            if (attrs == null) return;

            attrs.AttackFlatBuff -= _artifactLeaderLastDeadCount * _artifactAtkPerDeadCat;
            _artifactLeaderLastDeadCount = deadCatCount;
            attrs.AttackFlatBuff += deadCatCount * _artifactAtkPerDeadCat;
            attrs.Recalculate();

            Debug.Log($"[BattleManager] {_artifactLeaderFighter.Name} 奇物(亡猫之力)更新: {deadCatCount}只小猫死亡，+{deadCatCount * _artifactAtkPerDeadCat}攻击");
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

            // 找到橘猫族长
            BattleFighter orangeLeader = null;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || !f.IsAlive || !f.IsLeader) continue;
                if (f.TribeType != TribeType.Orange) continue;
                if (f.RuntimeAttributes == null) continue;
                orangeLeader = f;
                break;
            }
            if (orangeLeader == null) return;

            // 应用饱食层（Persistent buff，自动叠加）
            for (int k = 0; k < newKills; k++)
            {
                int prevMaxHp = orangeLeader.RuntimeAttributes.MaxHp;
                orangeLeader.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFullnessStack(60f, 4f));
                orangeLeader.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFullnessAtkStack(4f));
                orangeLeader.RuntimeAttributes.Recalculate();
                orangeLeader.RuntimeAttributes.CurrentHp += orangeLeader.RuntimeAttributes.MaxHp - prevMaxHp;
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
