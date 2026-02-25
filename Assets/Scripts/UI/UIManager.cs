using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI管理器 - 负责UI面板的加载、显示、隐藏和销毁
/// </summary>
public class UIManager : MonoBehaviour
{
    private Canvas _mainCanvas;
    private Dictionary<string, UIPanel> _activePanels = new Dictionary<string, UIPanel>();

    // UI Bundle 名称
    private const string UI_BUNDLE_NAME = "ui";

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
    public T ShowPanel<T>(string panelName, UILayer layer = UILayer.Normal) where T : UIPanel
    {
        if (_activePanels.ContainsKey(panelName))
        {
            _activePanels[panelName].Show();
            return _activePanels[panelName] as T;
        }

        // 从 AssetBundle 加载预制体
        GameObject panelPrefab = GameManager.Instance.ResourceManager.LoadPrefab(panelName);
        if (panelPrefab == null)
        {
            Debug.LogError($"[UIManager] Panel prefab not found: {panelName}");
            return null;
        }

        GameObject panelInstance = Instantiate(panelPrefab, GetUILayerTransform(layer));
        panelInstance.name = panelName;

        T panel = panelInstance.GetComponent<T>();
        if (panel == null)
        {
            panel = panelInstance.AddComponent<T>();
        }

        panel.Initialize();
        panel.Show();

        _activePanels[panelName] = panel;
        Debug.Log($"[UIManager] Panel shown: {panelName}");

        return panel;
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void HidePanel(string panelName)
    {
        if (_activePanels.ContainsKey(panelName))
        {
            _activePanels[panelName].Hide();
            Debug.Log($"[UIManager] Panel hidden: {panelName}");
        }
    }

    /// <summary>
    /// 关闭并销毁面板
    /// </summary>
    public void ClosePanel(string panelName)
    {
        if (_activePanels.ContainsKey(panelName))
        {
            UIPanel panel = _activePanels[panelName];
            panel.Close();
            _activePanels.Remove(panelName);
            Destroy(panel.gameObject);
            Debug.Log($"[UIManager] Panel closed: {panelName}");
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
    public T GetPanel<T>(string panelName) where T : UIPanel
    {
        if (_activePanels.ContainsKey(panelName))
        {
            return _activePanels[panelName] as T;
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