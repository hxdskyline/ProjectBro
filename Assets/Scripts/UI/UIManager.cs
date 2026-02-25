using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI管理器 - 负责UI面板的加载、显示、隐藏和销毁（使用 Addressable）
/// </summary>
public class UIManager : MonoBehaviour
{
    private Canvas _mainCanvas;
    private Dictionary<string, UIPanel> _activePanels = new Dictionary<string, UIPanel>();

    public enum UILayer
    {
        Background = 0,
        Normal = 1,
        Top = 2,
        PopUp = 3,
        Alert = 4
    }

    public void Initialize()
    {
        // 查找或创建主Canvas
        _mainCanvas = FindObjectOfType<Canvas>();
        if (_mainCanvas == null)
        {
            GameObject canvasGo = new GameObject("MainCanvas");
            _mainCanvas = canvasGo.AddComponent<Canvas>();
            _mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGo.AddComponent<GraphicRaycaster>();
        }

        Debug.Log("[UIManager] Initialized");
    }

    /// <summary>
    /// 显示面板
    /// </summary>
    public T ShowPanel<T>(string panelAddress, UILayer layer = UILayer.Normal) where T : UIPanel
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            _activePanels[panelAddress].Show();
            return _activePanels[panelAddress] as T;
        }

        // 使用 Addressable 加载预制体
        GameObject panelPrefab = GameManager.Instance.ResourceManager.LoadPrefab(panelAddress);
        if (panelPrefab == null)
        {
            Debug.LogError($"[UIManager] Panel prefab not found: {panelAddress}");
            return null;
        }

        GameObject panelInstance = Instantiate(panelPrefab, GetUILayerTransform(layer));
        panelInstance.name = panelPrefab.name;

        T panel = panelInstance.GetComponent<T>();
        if (panel == null)
        {
            panel = panelInstance.AddComponent<T>();
        }

        panel.Initialize();
        panel.Show();

        _activePanels[panelAddress] = panel;
        Debug.Log($"[UIManager] Panel shown: {panelAddress}");

        return panel;
    }

    /// <summary>
    /// 异步显示面板
    /// </summary>
    public void ShowPanelAsync<T>(string panelAddress, UILayer layer, System.Action<T> onComplete) where T : UIPanel
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            _activePanels[panelAddress].Show();
            onComplete?.Invoke(_activePanels[panelAddress] as T);
            return;
        }

        GameManager.Instance.ResourceManager.LoadPrefabAsync(panelAddress, (prefab) =>
        {
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] Panel prefab not found: {panelAddress}");
                onComplete?.Invoke(null);
                return;
            }

            GameObject panelInstance = Instantiate(prefab, GetUILayerTransform(layer));
            panelInstance.name = prefab.name;

            T panel = panelInstance.GetComponent<T>();
            if (panel == null)
            {
                panel = panelInstance.AddComponent<T>();
            }

            panel.Initialize();
            panel.Show();

            _activePanels[panelAddress] = panel;
            Debug.Log($"[UIManager] Panel shown async: {panelAddress}");

            onComplete?.Invoke(panel);
        });
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void HidePanel(string panelAddress)
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            _activePanels[panelAddress].Hide();
            Debug.Log($"[UIManager] Panel hidden: {panelAddress}");
        }
    }

    /// <summary>
    /// 关闭并销毁面板
    /// </summary>
    public void ClosePanel(string panelAddress)
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            UIPanel panel = _activePanels[panelAddress];
            panel.Close();
            _activePanels.Remove(panelAddress);
            Destroy(panel.gameObject);

            // 释放资源
            GameManager.Instance.ResourceManager.UnloadResource(panelAddress);

            Debug.Log($"[UIManager] Panel closed: {panelAddress}");
        }
    }

    /// <summary>
    /// 获取UI层级的Transform
    /// </summary>
    private Transform GetUILayerTransform(UILayer layer)
    {
        Transform layerTransform = _mainCanvas.transform.Find(layer.ToString());
        if (layerTransform == null)
        {
            GameObject layerGo = new GameObject(layer.ToString());
            layerGo.transform.SetParent(_mainCanvas.transform, false);
            RectTransform rectTransform = layerGo.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = layerGo.AddComponent<RectTransform>();
            }
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            layerTransform = layerGo.transform;
        }
        return layerTransform;
    }

    /// <summary>
    /// 获取已显示的面板
    /// </summary>
    public T GetPanel<T>(string panelAddress) where T : UIPanel
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            return _activePanels[panelAddress] as T;
        }
        return null;
    }

    /// <summary>
    /// 关闭所有面板
    /// </summary>
    public void CloseAllPanels()
    {
        List<string> panelNames = new List<string>(_activePanels.Keys);
        foreach (string panelName in panelNames)
        {
            ClosePanel(panelName);
        }
    }
}