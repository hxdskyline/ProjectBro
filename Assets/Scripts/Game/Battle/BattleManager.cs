using UnityEngine;

/// <summary>
/// 战斗管理器 - 管理战斗逻辑（暂时仅作为占位符）
/// </summary>
public class BattleManager : MonoBehaviour
{
    private int _levelId;
    private bool _isInBattle;

    public bool IsInBattle => _isInBattle;
    public int LevelId => _levelId;

    public void Initialize(int levelId)
    {
        _levelId = levelId;
        Debug.Log($"[BattleManager] Initialized for level: {levelId}");
    }

    public void StartBattle()
    {
        _isInBattle = true;
        Debug.Log("[BattleManager] Battle started");

        // TODO: 实现实际的战斗逻辑
        // 包括：
        // - 加载敌方兵种
        // - 初始化玩家兵种
        // - 回合制战斗循环
        // - 胜负判定
    }

    public void EndBattle(bool victory)
    {
        _isInBattle = false;

        if (victory)
        {
            Debug.Log("[BattleManager] Battle ended - Victory!");
        }
        else
        {
            Debug.Log("[BattleManager] Battle ended - Defeat!");
        }
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
}