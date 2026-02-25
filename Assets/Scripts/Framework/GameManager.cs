using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 游戏总管理器 - 负责整个游戏的生命周期管理
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;

    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    // 各个管理器的引用
    private ResourceManager _resourceManager;
    private UIManager _uiManager;
    private LevelManager _levelManager;
    private AudioManager _audioManager;
    private DataManager _dataManager;
    private SceneManager _sceneManager;

    public ResourceManager ResourceManager => _resourceManager;
    public UIManager UIManager => _uiManager;
    public LevelManager LevelManager => _levelManager;
    public AudioManager AudioManager => _audioManager;
    public DataManager DataManager => _dataManager;
    public SceneManager SceneManager => _sceneManager;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeManagers();
    }

    private void InitializeManagers()
    {
        Debug.Log("[GameManager] Initializing Game Framework...");

        // 初始化资源管理器（必须首先初始化）
        _resourceManager = gameObject.AddComponent<ResourceManager>();
        _resourceManager.Initialize();

        // 初始化数据管理器
        _dataManager = gameObject.AddComponent<DataManager>();
        _dataManager.Initialize();

        // 初始化音频管理器
        _audioManager = gameObject.AddComponent<AudioManager>();
        _audioManager.Initialize();

        // 初始化场景管理器
        _sceneManager = gameObject.AddComponent<SceneManager>();
        _sceneManager.Initialize();

        // 初始化UI管理器
        _uiManager = gameObject.AddComponent<UIManager>();
        _uiManager.Initialize();

        // 初始化关卡管理器
        _levelManager = gameObject.AddComponent<LevelManager>();
        _levelManager.Initialize();

        Debug.Log("[GameManager] Game Framework Initialized Successfully!");
    }

    public void LoadGame()
    {
        Debug.Log("[GameManager] Loading Game...");
        _dataManager.LoadPlayerData();

        // 显示主界面
        _uiManager.ShowPanel<MainPanel>("MainPanel", UIManager.UILayer.Normal);
    }

    public void SaveGame()
    {
        Debug.Log("[GameManager] Saving Game...");
        _dataManager.SavePlayerData();
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }
}