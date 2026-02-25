using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 资源管理器 - 负责所有资源的加载、缓存和卸载
/// </summary>
public class ResourceManager : MonoBehaviour
{
    private Dictionary<string, Object> _resourceCache = new Dictionary<string, Object>();
    private Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
    
    // 资源路径配置
    private const string RESOURCES_PATH = "Assets/Resources";
    private const string PREFABS_PATH = "Prefabs";
    private const string TEXTURES_PATH = "Textures";
    private const string AUDIO_PATH = "Audio";
    private const string SPRITES_PATH = "Sprites";
    private const string DATA_PATH = "Data";

    public void Initialize()
    {
        Debug.Log("[ResourceManager] Initialized");
    }

    /// <summary>
    /// 加载资源（通用方法）
    /// </summary>
    public T LoadResource<T>(string resourceName) where T : Object
    {
        if (_resourceCache.ContainsKey(resourceName))
        {
            return _resourceCache[resourceName] as T;
        }

        T resource = Resources.Load<T>(resourceName);
        if (resource != null)
        {
            _resourceCache[resourceName] = resource;
            Debug.Log($"[ResourceManager] Loaded resource: {resourceName}");
        }
        else
        {
            Debug.LogError($"[ResourceManager] Failed to load resource: {resourceName}");
        }

        return resource;
    }

    /// <summary>
    /// 加载预制体
    /// </summary>
    public GameObject LoadPrefab(string prefabName)
    {
        if (_prefabCache.ContainsKey(prefabName))
        {
            return _prefabCache[prefabName];
        }

        GameObject prefab = LoadResource<GameObject>($"{PREFABS_PATH}/{prefabName}");
        if (prefab != null)
        {
            _prefabCache[prefabName] = prefab;
        }

        return prefab;
    }

    /// <summary>
    /// 实例化预制体
    /// </summary>
    public GameObject InstantiatePrefab(string prefabName, Transform parent = null)
    {
        GameObject prefab = LoadPrefab(prefabName);
        if (prefab == null)
        {
            Debug.LogError($"[ResourceManager] Prefab not found: {prefabName}");
            return null;
        }

        GameObject instance = Instantiate(prefab, parent);
        instance.name = prefab.name;
        return instance;
    }

    /// <summary>
    /// 加载贴图
    /// </summary>
    public Sprite LoadSprite(string spriteName)
    {
        return LoadResource<Sprite>($"{SPRITES_PATH}/{spriteName}");
    }

    /// <summary>
    /// 加载音频
    /// </summary>
    public AudioClip LoadAudio(string audioName)
    {
        return LoadResource<AudioClip>($"{AUDIO_PATH}/{audioName}");
    }

    /// <summary>
    /// 加载TextAsset（用于JSON等文本数据）
    /// </summary>
    public TextAsset LoadTextAsset(string assetName)
    {
        return LoadResource<TextAsset>($"{DATA_PATH}/{assetName}");
    }

    /// <summary>
    /// 卸载资源
    /// </summary>
    public void UnloadResource(string resourceName)
    {
        if (_resourceCache.ContainsKey(resourceName))
        {
            _resourceCache.Remove(resourceName);
            Resources.UnloadAsset(_resourceCache[resourceName]);
            Debug.Log($"[ResourceManager] Unloaded resource: {resourceName}");
        }
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void ClearCache()
    {
        _resourceCache.Clear();
        _prefabCache.Clear();
        Resources.UnloadUnusedAssets();
        Debug.Log("[ResourceManager] Cache cleared");
    }
}