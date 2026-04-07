using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗管理器 - 管理战斗逻辑（暂时仅作为占位符）
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

    public System.Action<bool> BattleEnded;

    public bool IsInBattle => _isInBattle;
    public int LevelId => _levelId;

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

    public void StartBattle()
    {
        if (_isInBattle)
        {
            return;
        }

        _isInBattle = true;
        Debug.Log("[BattleManager] Battle started");

        BuildDemoFighters();
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
        _battleCoroutine = StartCoroutine(DemoBattleLoop());
    }

    public void EndBattle(bool victory)
    {
        if (!_isInBattle)
        {
            return;
        }

        _isInBattle = false;

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

    private void OnDestroy()
    {
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
        ClearOldAvatars();
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
            firstPlayerStats = $" | Leader: ATK={s.Attack} DEF={s.Defense} HP={s.MaxHp} SPD={s.MoveSpeed:F1}";
        }
        string firstEnemyStats = "";
        if (_enemyFighters.Length > 0 && _enemyFighters[0] != null)
        {
            var s = _enemyFighters[0].StaticAttributes;
            firstEnemyStats = $" | Enemy: ATK={s.Attack} DEF={s.Defense} HP={s.MaxHp} SPD={s.MoveSpeed:F1}";
        }

        Debug.Log($"[BattleSummary] {(victory ? "WIN" : "LOSE")} | " +
            $"Player: {pAlive}/{_playerFighters.Length} alive, {pTotalHp}/{pMaxHp} HP{firstPlayerStats} | " +
            $"Enemy: {eAlive}/{_enemyFighters.Length} alive, {eTotalHp}/{eMaxHp} HP{firstEnemyStats}");
    }
}