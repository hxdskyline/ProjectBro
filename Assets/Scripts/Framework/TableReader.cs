using UnityEngine;
using System.Collections.Generic;
using LitJson;

/// <summary>
/// 读表器 - 负责加载和管理游戏数据表
/// </summary>
public class TableReader : MonoBehaviour
{
    private static TableReader _instance;
    
    public static TableReader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TableReader>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("TableReader");
                    _instance = go.AddComponent<TableReader>();
                }
            }
            return _instance;
        }
    }

    // 缓存所有数据表
    private Dictionary<string, JsonData> _tables = new Dictionary<string, JsonData>();

    public void Initialize()
    {
        Debug.Log("[TableReader] Initialized");
    }

    /// <summary>
    /// 加载JSON数据表
    /// </summary>
    public void LoadTable(string tableName)
    {
        if (_tables.ContainsKey(tableName))
        {
            Debug.LogWarning($"[TableReader] Table already loaded: {tableName}");
            return;
        }

        TextAsset jsonFile = GameManager.Instance.ResourceManager.LoadTextAsset($"Tables/{tableName}");
        if (jsonFile == null)
        {
            Debug.LogError($"[TableReader] Failed to load table: {tableName}");
            return;
        }

        try
        {
            JsonData jsonData = JsonMapper.ToObject(jsonFile.text);
            _tables[tableName] = jsonData;
            Debug.Log($"[TableReader] Loaded table: {tableName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TableReader] Error parsing table {tableName}: {e.Message}");
        }
    }

    /// <summary>
    /// 获取数据表
    /// </summary>
    public JsonData GetTable(string tableName)
    {
        if (!_tables.ContainsKey(tableName))
        {
            LoadTable(tableName);
        }

        return _tables.ContainsKey(tableName) ? _tables[tableName] : null;
    }

    /// <summary>
    /// 获取表中的单条记录（按ID）
    /// </summary>
    public JsonData GetRecord(string tableName, string id)
    {
        JsonData table = GetTable(tableName);
        if (table == null)
            return null;

        for (int i = 0; i < table.Count; i++)
        {
            if (table[i]["id"].ToString() == id)
            {
                return table[i];
            }
        }

        return null;
    }

    /// <summary>
    /// 卸载表
    /// </summary>
    public void UnloadTable(string tableName)
    {
        if (_tables.ContainsKey(tableName))
        {
            _tables.Remove(tableName);
            Debug.Log($"[TableReader] Unloaded table: {tableName}");
        }
    }
}