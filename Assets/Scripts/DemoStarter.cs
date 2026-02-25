using UnityEngine;

/// <summary>
/// Demo启动器 - 场景启动时初始化游戏
/// </summary>
public class DemoStarter : MonoBehaviour
{
    private void Start()
    {
        // 加载游戏数据
        GameManager.Instance.LoadGame();

        // 显示主界面
        GameManager.Instance.UIManager.ShowPanel<MainPanel>("MainPanel", UIManager.UILayer.Normal);

        Debug.Log("[DemoStarter] Game demo started!");
    }
}