using UnityEngine;
using System.Collections.Generic;
using System.IO;
using LitJson;

/// <summary>
/// 读表器 - 负责加载和管理游戏数据表（从 StreamingAsset 加载）
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

    // StreamingAsset 中的 Tables 文件夹路径
    private string _tablesPath;

    public void Initialize()
    {
        // 设置 Tables 路径
        _tablesPath = Path.Combine(Application.streamingAssetsPath, "Tables");

        Debug.Log($"[TableReader] Initialized - Tables path: {_tablesPath}");
    }

    /// <summary>
    /// 加载 JSON 数据表
    /// </summary>
    public void LoadTable(string tableName)
    {
        if (_tables.ContainsKey(tableName))
        {
            Debug.LogWarning($"[TableReader] Table already loaded: {tableName}");
            return;
        }

        try
        {
            // 构建完整的表文件路径
            string tableFilePath = Path.Combine(_tablesPath, $"{tableName}.json");

            // 检查文件是否存在
            if (!File.Exists(tableFilePath))
            {
                Debug.LogError($"[TableReader] Table file not found: {tableFilePath}");
                return;
            }

            // 读取文件内容
            string jsonContent = File.ReadAllText(tableFilePath);

            // 解析 JSON
            JsonData jsonData = JsonMapper.ToObject(jsonContent);
            _tables[tableName] = jsonData;

            Debug.Log($"[TableReader] Loaded table: {tableName} from {tableFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TableReader] Error loading table {tableName}: {e.Message}");
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

        // 如果是数组
        if (table.IsArray)
        {
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i]["id"].ToString() == id)
                {
                    return table[i];
                }
            }
        }
        // 如果是字典
        else if (table.IsObject && table.Keys.Contains(id))
        {
            return table[id];
        }

        return null;
    }

    /// <summary>
    /// 获取表中的所有记录
    /// </summary>
    public JsonData GetAllRecords(string tableName)
    {
        return GetTable(tableName);
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

    /// <summary>
    /// 清空所有表缓存
    /// </summary>
    public void ClearAllTables()
    {
        _tables.Clear();
        Debug.Log("[TableReader] All tables cleared");
    }

    /// <summary>
    /// 打印加载的表信息（调试用）
    /// </summary>
    public void PrintTablesInfo()
    {
        Debug.Log($"[TableReader] Loaded tables count: {_tables.Count}");
        foreach (string tableName in _tables.Keys)
        {
            Debug.Log($"  - {tableName}");
        }
    }
}