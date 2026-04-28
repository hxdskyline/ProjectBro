using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TribeSystem;

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
    private BattleFighter _cowLeaderFighter;
    private int _cowLeaderLastCatCount;
    private int _cowAttackPerCat;
    private BattleFighter _artifactLeaderFighter;
    private int _artifactAtkPerDeadCat;
    private int _artifactLeaderLastDeadCount;

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

        // 应用天生特殊 buff
        ApplyInnateBuffs();

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
            UpdateCowLeaderBuff();
            // 动态更新奇物：每死一只小猫族长+攻击
            UpdateArtifactLeaderBuff();

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

    private void ClearBattlefield()
    {
        _simulation = null;
        _playerFighters = null;
        _enemyFighters = null;
        _cowLeaderFighter = null;
        _cowLeaderLastCatCount = 0;
        _cowAttackPerCat = 0;
        _artifactLeaderFighter = null;
        _artifactAtkPerDeadCat = 0;
        _artifactLeaderLastDeadCount = 0;
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

            // 族长速度固定值加成（有天生buff的是族长）
            if (fighter.InnateBuffs != null && fighter.InnateBuffs.Count > 0)
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
    /// 应用族长天生特殊 buff 效果
    /// </summary>
    private void ApplyInnateBuffs()
    {
        if (_playerFighters == null || _playerFighters.Length == 0)
            return;

        for (int i = 0; i < _playerFighters.Length; i++)
        {
            BattleFighter fighter = _playerFighters[i];
            if (fighter == null || fighter.InnateBuffs == null || fighter.InnateBuffs.Count == 0)
                continue;

            UnitRuntimeAttributes attrs = fighter.RuntimeAttributes;
            if (attrs == null) continue;

            foreach (var buff in fighter.InnateBuffs)
            {
                switch (buff.effectType)
                {
                    case TribeSystem.InnateEffectType.DamageReduce:
                        // 受到伤害 -value
                        attrs.DamageReceiveFlatBuff -= Mathf.RoundToInt(buff.effectValue);
                        Debug.Log($"[BattleManager] {fighter.Name} 天生buff: {buff.displayName}（伤害-{Mathf.RoundToInt(buff.effectValue)}）");
                        break;

                    case TribeSystem.InnateEffectType.AttackPerDefeatedCat:
                        // 每有一只被击败的本族小猫，+value 攻击力（动态更新）
                        _cowLeaderFighter = fighter;
                        _cowAttackPerCat = Mathf.RoundToInt(buff.effectValue);
                        _cowLeaderLastCatCount = -1; // 强制首次更新
                        break;

                    case TribeSystem.InnateEffectType.DoubleHit:
                        // value% 概率造成双倍伤害
                        fighter.HasDoubleHit = true;
                        Debug.Log($"[BattleManager] {fighter.Name} 天生buff: {buff.displayName}（{buff.effectValue * 100}%双倍伤害）");
                        break;

                    case TribeSystem.InnateEffectType.SpeedFlat:
                        // 速度 +value
                        attrs.SpeedFlatBuff += Mathf.RoundToInt(buff.effectValue);
                        attrs.Recalculate();
                        Debug.Log($"[BattleManager] {fighter.Name} 天生buff: {buff.displayName}（速度+{Mathf.RoundToInt(buff.effectValue)}）");
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 动态更新奶牛族长的薄葬 buff（根据被击败的小猫数量）
    /// </summary>
    private void UpdateCowLeaderBuff()
    {
        if (_cowLeaderFighter == null || _cowLeaderFighter.IsDead || _cowLeaderFighter.IsRemoved)
            return;

        // 统计本族被击败的小猫数量（IsDead 或 IsRemoved）
        int defeatedCatCount = 0;
        for (int i = 0; i < _playerFighters.Length; i++)
        {
            var f = _playerFighters[i];
            if (f == null || f == _cowLeaderFighter) continue;
            if (f.TribeType != _cowLeaderFighter.TribeType) continue;
            if (f.InnateBuffs != null && f.InnateBuffs.Count > 0) continue; // 跳过族长
            if (f.IsDead || f.IsRemoved) defeatedCatCount++;
        }

        // 数量没变化则跳过
        if (defeatedCatCount == _cowLeaderLastCatCount)
            return;

        // 回退旧的 buff，应用新的
        UnitRuntimeAttributes attrs = _cowLeaderFighter.RuntimeAttributes;
        if (attrs == null) return;

        attrs.AttackFlatBuff -= _cowLeaderLastCatCount * _cowAttackPerCat;
        _cowLeaderLastCatCount = defeatedCatCount;
        attrs.AttackFlatBuff += defeatedCatCount * _cowAttackPerCat;
        attrs.Recalculate();

        Debug.Log($"[BattleManager] {_cowLeaderFighter.Name} 薄葬更新: {defeatedCatCount}只小猫被击败，+{defeatedCatCount * _cowAttackPerCat}攻击");
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
            if (f.InnateBuffs != null && f.InnateBuffs.Count > 0) continue; // 跳过族长
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
    /// 生成子弹（狸花远程攻击）
    /// </summary>
    private void SpawnBullet(BulletData data)
    {
        if (data.Attacker == null || data.Target == null) return;

        GameObject bulletGo = new GameObject("Bullet");
        bulletGo.transform.position = data.Attacker.Transform.position;
        bulletGo.transform.SetParent(transform);

        var bullet = bulletGo.AddComponent<BattleBullet>();
        bullet.Setup(data.Target, data.Damage, data.IsCritical);
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