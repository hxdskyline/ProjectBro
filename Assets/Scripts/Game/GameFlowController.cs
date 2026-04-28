using UnityEngine;
using System;
using TribeSystem;
using TribeSystem.UI;

/// <summary>
/// 游戏流程控制器 - 管理整个游戏的流程和状态转换
/// 负责协调各个游戏阶段：初始选择 → 族群构筑 → 战斗 → 回合推进 → 游戏结束
/// </summary>
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    /// <summary>
    /// 游戏状态枚举
    /// </summary>
    public enum GameState
    {
        Uninitialized,      // 未初始化
        InitialSelection,   // 初始族群选择
        RoundPreparation,   // 回合准备（族群构筑、招募、祭祀、商店）
        BattlePhase,        // 战斗阶段
        GameOver            // 游戏结束
    }

    // 事件系统
    public event Action<GameState> OnGameStateChanged;
    public event Action<int> OnRoundChanged;
    public event Action OnGameStarted;
    public event Action OnGameEnded;

    private GameState _currentState = GameState.Uninitialized;
    private GameManager _gameManager;
    private UIManager _uiManager;
    private DataManager _dataManager;
    private TribeBuildPanel _tribeBuildPanel;
    private RoundManager _roundManager;

    private int _currentRound = 1;
    private bool _isGameStarted = false;

    public GameState CurrentState => _currentState;
    public int CurrentRound => _currentRound;
    public bool IsGameStarted => _isGameStarted;

    private void Awake()
    {
        // 单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[GameFlowController] Awake - initializing references");
    }

    private void Start()
    {
        // 在Start时获取引用，确保GameManager已完全初始化
        Debug.Log("[GameFlowController] Start - getting references");

        _gameManager = GameManager.Instance;
        Debug.Log($"[GameFlowController] GameManager.Instance: {(_gameManager != null ? "Found" : "NULL")}");

        if (_gameManager == null)
        {
            Debug.LogError("[GameFlowController] CRITICAL: GameManager.Instance is null!");
            Debug.LogError("[GameFlowController] Make sure GameManager is in the scene and initialized");
            return;
        }

        _uiManager = _gameManager.UIManager;
        Debug.Log($"[GameFlowController] UIManager: {(_uiManager != null ? "Found" : "NULL")}");

        _dataManager = _gameManager.DataManager;
        Debug.Log($"[GameFlowController] DataManager: {(_dataManager != null ? "Found" : "NULL")}");

        _roundManager = new RoundManager();

        if (_uiManager == null)
        {
            Debug.LogError("[GameFlowController] UIManager not found!");
            return;
        }

        if (_dataManager == null)
        {
            Debug.LogError("[GameFlowController] DataManager not found!");
            return;
        }

        Debug.Log("[GameFlowController] Start - all references initialized successfully");
    }

    /// <summary>
    /// 初始化游戏流程控制器
    /// 由GameInitializer在系统初始化后调用
    /// </summary>
    public void Initialize()
    {
        Debug.Log("[GameFlowController] 初始化游戏流程控制器");

        // 确保引用已初始化（如果Start()还没执行）
        if (_dataManager == null)
        {
            Debug.Log("[GameFlowController] 引用未初始化，现在初始化...");
            _gameManager = GameManager.Instance;
            _uiManager = _gameManager?.UIManager;
            _dataManager = _gameManager?.DataManager;

            if (_gameManager == null)
            {
                Debug.LogError("[GameFlowController] CRITICAL: GameManager not found!");
                return;
            }

            if (_dataManager == null)
            {
                Debug.LogError("[GameFlowController] DataManager not found - cannot initialize game flow");
                return;
            }

            if (_uiManager == null)
            {
                Debug.LogError("[GameFlowController] UIManager not found - cannot initialize game flow");
                return;
            }

            Debug.Log("[GameFlowController] 引用初始化完成");
        }

        // 从存档加载当前回合
        int savedRound = _dataManager.GetCurrentRound();
        _roundManager.SetRound(savedRound);
        _currentRound = savedRound;

        // 检查是否需要初始族群选择
        bool isNewGame = _roundManager.CurrentRound == 1 &&
                        (_dataManager.GetTribes() == null ||
                         _dataManager.GetTribes().Count == 0);

        if (isNewGame)
        {
            StartInitialTribeSelection();
        }
        else
        {
            EnterGameRound();
        }
    }

    /// <summary>
    /// 开始初始族群选择
    /// </summary>
    private void StartInitialTribeSelection()
    {
        Debug.Log("[GameFlowController] 开始初始族群选择");
        Debug.Log($"[GameFlowController] _uiManager = {(_uiManager != null ? "存在" : "NULL")}");
        ChangeGameState(GameState.InitialSelection);

        if (_uiManager == null)
        {
            Debug.LogError("[GameFlowController] UIManager not found");
            return;
        }

        // 显示初始族群选择面板
        var panel = _uiManager.ShowPanel<InitialTribeSelectionPanel>(
            "ui/tribebuild/initialtribeselectionpanel",
            UIManager.UILayer.Normal
        );

        if (panel != null)
        {
            // 监听选择完成事件
            panel.OnSelectionComplete += OnInitialTribeSelectionComplete;
            Debug.Log("[GameFlowController] InitialTribeSelectionPanel 已显示");
        }
        else
        {
            Debug.LogError("[GameFlowController] Failed to show InitialTribeSelectionPanel");
        }
    }

    /// <summary>
    /// 初始族群选择完成
    /// </summary>
    private void OnInitialTribeSelectionComplete()
    {
        Debug.Log("[GameFlowController] 初始族群选择完成");
        Debug.Log($"[GameFlowController] 当前状态 - _uiManager: {(_uiManager != null ? "存在" : "NULL")}, GameManager: {(GameManager.Instance != null ? "存在" : "NULL")}");

        OnGameStarted?.Invoke();
        _isGameStarted = true;
        EnterGameRound();
    }

    /// <summary>
    /// 进入游戏回合 - 显示TribeBuildPanel
    /// </summary>
    public void EnterGameRound()
    {
        // 确保从DataManager加载最新的回合数
        if (_dataManager != null)
        {
            int savedRound = _dataManager.GetCurrentRound();
            _roundManager.SetRound(savedRound);
            _currentRound = savedRound;
        }

        Debug.Log($"[GameFlowController] 进入第 {_currentRound} 回合");
        Debug.Log($"[GameFlowController] _uiManager = {(_uiManager != null ? "存在" : "NULL")}");
        Debug.Log($"[GameFlowController] GameManager.Instance = {(GameManager.Instance != null ? "存在" : "NULL")}");
        Debug.Log($"[GameFlowController] GameManager.Instance.UIManager = {(GameManager.Instance?.UIManager != null ? "存在" : "NULL")}");

        // 如果_uiManager是null，尝试从GameManager重新获取
        if (_uiManager == null)
        {
            Debug.LogWarning("[GameFlowController] _uiManager为null，尝试从GameManager重新获取...");
            _uiManager = GameManager.Instance?.UIManager;
        }

        ChangeGameState(GameState.RoundPreparation);

        if (_uiManager == null)
        {
            Debug.LogError("[GameFlowController] UIManager not found - GameManager.UIManager也是null");
            return;
        }

        // 显示族群构筑主界面
        _tribeBuildPanel = _uiManager.ShowPanel<TribeBuildPanel>(
            "ui/tribebuild/tribebuildpanel",
            UIManager.UILayer.Normal
        );

        if (_tribeBuildPanel != null)
        {
            Debug.Log("[GameFlowController] TribeBuildPanel 已显示");
        }
        else
        {
            Debug.LogError("[GameFlowController] Failed to show TribeBuildPanel");
        }
    }

    /// <summary>
    /// 开始战斗阶段
    /// 由TribeBuildPanel或BattlePreparePanel调用
    /// </summary>
    public void EnterBattlePhase()
    {
        Debug.Log("[GameFlowController] 进入战斗阶段");
        ChangeGameState(GameState.BattlePhase);
    }

    /// <summary>
    /// 战斗结束回调（由TribeBuildPanel调用，表示即将推进回合）
    /// </summary>
    public void OnBattleEnded(bool victory)
    {
        if (victory)
        {
            Debug.Log("[GameFlowController] 战斗胜利，准备推进回合");
            // TribeBuildPanel会处理AdvanceRound()，这里只更新状态
            // 如果这是最后一回合，TribeBuildPanel会调用NotifyGameEnded()
        }
        else
        {
            Debug.Log("[GameFlowController] 战斗失败，返回准备阶段");
            ChangeGameState(GameState.RoundPreparation);
        }
    }

    /// <summary>
    /// 游戏即将结束的通知（由TribeBuildPanel调用）
    /// </summary>
    public void NotifyGameEnding()
    {
        Debug.Log("[GameFlowController] 收到游戏结束通知");
        // TribeBuildPanel会处理最后的UI显示（ShowGameClearScreen）
        // 这里只更新内部状态
    }

    /// <summary>
    /// 触发回合改变事件
    /// </summary>
    public void RaiseRoundChanged(int newRound)
    {
        OnRoundChanged?.Invoke(newRound);
    }

    /// <summary>
    /// 推进到下一回合
    /// </summary>
    private void AdvanceRound()
    {
        // 结束当前回合
        _roundManager.EndRound();
        _currentRound = _roundManager.CurrentRound;

        // 同步到存档
        if (_dataManager != null)
        {
            _dataManager.SetCurrentRound(_currentRound);
        }

        OnRoundChanged?.Invoke(_currentRound);

        // 检查游戏是否结束
        if (_roundManager.IsGameOver)
        {
            EndGame();
        }
        else
        {
            // 进入下一回合
            EnterGameRound();
        }
    }

    /// <summary>
    /// 游戏结束
    /// </summary>
    private void EndGame()
    {
        Debug.Log("[GameFlowController] ===== 游戏结束 =====");
        ChangeGameState(GameState.GameOver);

        OnGameEnded?.Invoke();

        // 显示通关界面
        if (_uiManager != null)
        {
            var victoryPanel = _uiManager.ShowPanel<VictoryPanel>(
                "ui/victorypanel",
                UIManager.UILayer.Top
            );

            if (victoryPanel != null)
            {
                Debug.Log("[GameFlowController] VictoryPanel 已显示");
            }
        }
    }

    /// <summary>
    /// 改变游戏状态
    /// </summary>
    private void ChangeGameState(GameState newState)
    {
        if (_currentState == newState) return;

        GameState oldState = _currentState;
        _currentState = newState;

        Debug.Log($"[GameFlowController] 游戏状态转换: {oldState} → {newState}");
        OnGameStateChanged?.Invoke(_currentState);
    }

    /// <summary>
    /// 获取当前回合的事件列表
    /// </summary>
    public RoundEventType[] GetCurrentRoundEvents()
    {
        return _roundManager.GetRoundEvents().ToArray();
    }

    /// <summary>
    /// 获取当前回合描述
    /// </summary>
    public string GetCurrentRoundDescription()
    {
        return _roundManager.GetRoundDescription();
    }

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GameFlowController] 重新开始游戏");

        _roundManager.Reset();
        _currentRound = 1;
        _isGameStarted = false;

        if (_dataManager != null)
        {
            // 重新加载玩家数据（会创建新的玩家数据）
            _dataManager.LoadPlayerData();
            _dataManager.SetCurrentRound(1);
            // 清空本局饰品
            _dataManager.ClearRunAccessories();
        }

        // 重新进行初始族群选择
        StartInitialTribeSelection();
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void ReturnToMainMenu()
    {
        Debug.Log("[GameFlowController] 返回主菜单");

        _roundManager.Reset();
        _currentRound = 1;
        _isGameStarted = false;
        ChangeGameState(GameState.Uninitialized);

        if (_uiManager != null)
        {
            _uiManager.ShowPanel<MainPanel>("ui/mainpanel", UIManager.UILayer.Normal); // 统一使用小写地址
        }
    }
}
