using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MapSystem;

namespace TribeSystem.UI
{
    /// <summary>
    /// 地图选择界面 - 显示分支路径地图，玩家选择下一关
    /// </summary>
    public class MapPanel : UIPanel
    {
        private const string PanelName = "选关";

        [Header("UI 组件")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _currentLevelText;
        [SerializeField] private Text _regionText;
        [SerializeField] private ScrollRect _mapScrollView;
        [SerializeField] private RectTransform _nodesContainer;
        [SerializeField] private RectTransform _edgesContainer;

        [Header("预制体引用")]
        [SerializeField] private GameObject _nodePrefab;
        [SerializeField] private GameObject _edgePrefab;
        [SerializeField] private GameObject _tipsPanelPrefab;

        [Header("布局参数")]
        [SerializeField] private float _columnSpacing = 150f;
        [SerializeField] private float _rowSpacing = 100f;
        [SerializeField] private float _edgeWidth = 3f;

        private MapData _currentMapData;
        private int _currentNodeId = -1;
        private int _currentRegion = 1;
        private int _currentBattleNumber = 1;

        private List<GameObject> _nodeObjects = new List<GameObject>();
        private List<GameObject> _edgeObjects = new List<GameObject>();
        private MapNodeTips _tipsPanel;

        private System.Action<int, MapNodeType> _onNodeSelected;

        private void Awake()
        {
            EnsureUIComponents();
        }

        /// <summary>
        /// 显示地图面板
        /// </summary>
        public void ShowMap(MapData mapData, int currentNodeId, System.Action<int, MapNodeType> onNodeSelected)
        {
            _currentMapData = mapData;
            _currentNodeId = currentNodeId;
            _onNodeSelected = onNodeSelected;

            // 更新UI文本
            UpdateUI();

            // 清空旧节点和连线
            ClearMap();

            // 生成节点和连线
            GenerateMapVisuals();

            // 滚动到当前列
            ScrollToColumn(mapData.GetNode(currentNodeId)?.column ?? 0);

            Show();
        }

        private void UpdateUI()
        {
            if (_titleText != null)
                _titleText.text = PanelName;

            if (_currentLevelText != null)
                _currentLevelText.text = $"第 {_currentBattleNumber} 关";

            if (_regionText != null)
                _regionText.text = $"地区 {_currentRegion}";
        }

        private void ClearMap()
        {
            foreach (var obj in _nodeObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _nodeObjects.Clear();

            foreach (var obj in _edgeObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _edgeObjects.Clear();
        }

        private void GenerateMapVisuals()
        {
            if (_currentMapData == null) return;

            // 先生成连线（在节点下方）
            foreach (var edge in _currentMapData.edges)
            {
                CreateEdgeVisual(edge);
            }

            // 再生成节点
            foreach (var node in _currentMapData.nodes)
            {
                CreateNodeVisual(node);
            }
        }

        private void CreateNodeVisual(MapNode node)
        {
            if (_nodePrefab == null || _nodesContainer == null) return;

            GameObject nodeObj = Instantiate(_nodePrefab, _nodesContainer);
            _nodeObjects.Add(nodeObj);

            // 设置位置
            RectTransform rt = nodeObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                float x = node.column * _columnSpacing;
                float y = (node.row - 2) * _rowSpacing; // 居中：row=2为中间
                rt.anchoredPosition = new Vector2(x, y);
            }

            // 设置节点外观
            MapNodeUI nodeUI = nodeObj.GetComponent<MapNodeUI>();
            if (nodeUI != null)
            {
                nodeUI.Initialize(node, OnNodeClicked);
            }
        }

        private void CreateEdgeVisual(MapEdge edge)
        {
            if (_edgePrefab == null || _edgesContainer == null) return;

            MapNode fromNode = _currentMapData.GetNode(edge.fromNodeId);
            MapNode toNode = _currentMapData.GetNode(edge.toNodeId);

            if (fromNode == null || toNode == null) return;

            GameObject edgeObj = Instantiate(_edgePrefab, _edgesContainer);
            _edgeObjects.Add(edgeObj);

            // 设置连线位置和旋转
            RectTransform rt = edgeObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 fromPos = new Vector2(fromNode.column * _columnSpacing, (fromNode.row - 2) * _rowSpacing);
                Vector2 toPos = new Vector2(toNode.column * _columnSpacing, (toNode.row - 2) * _rowSpacing);

                Vector2 midpoint = (fromPos + toPos) / 2f;
                rt.anchoredPosition = midpoint;

                float distance = Vector2.Distance(fromPos, toPos);
                rt.sizeDelta = new Vector2(distance, _edgeWidth);

                float angle = Mathf.Atan2(toPos.y - fromPos.y, toPos.x - fromPos.x) * Mathf.Rad2Deg;
                rt.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        private void OnNodeClicked(MapNode node)
        {
            if (node == null) return;

            // 显示Tips面板
            ShowTips(node);
        }

        private void ShowTips(MapNode node)
        {
            if (_tipsPanelPrefab == null) return;

            // 如果已有Tips面板，先销毁
            if (_tipsPanel != null)
            {
                Destroy(_tipsPanel.gameObject);
            }

            GameObject tipsObj = Instantiate(_tipsPanelPrefab, transform);
            _tipsPanel = tipsObj.GetComponent<MapNodeTips>();

            if (_tipsPanel != null)
            {
                _tipsPanel.Initialize(node, OnEnterNode);
            }
        }

        private void OnEnterNode(MapNode node)
        {
            // 关闭Tips面板
            if (_tipsPanel != null)
            {
                Destroy(_tipsPanel.gameObject);
                _tipsPanel = null;
            }

            // 回调节点选择
            _onNodeSelected?.Invoke(node.id, node.nodeType);
        }

        private void ScrollToColumn(int column)
        {
            if (_mapScrollView != null)
            {
                float targetX = column * _columnSpacing;
                // 滚动到目标位置
                _mapScrollView.horizontalNormalizedPosition = targetX / (_nodesContainer.rect.width - _mapScrollView.viewport.rect.width);
            }
        }

        private void EnsureUIComponents()
        {
            if (_titleText == null)
                _titleText = transform.Find("Title")?.GetComponent<Text>();
            if (_currentLevelText == null)
                _currentLevelText = transform.Find("CurrentLevel")?.GetComponent<Text>();
            if (_regionText == null)
                _regionText = transform.Find("Region")?.GetComponent<Text>();
            if (_mapScrollView == null)
                _mapScrollView = transform.Find("MapScrollView")?.GetComponent<ScrollRect>();
            if (_nodesContainer == null)
                _nodesContainer = transform.Find("MapScrollView/NodesContainer") as RectTransform;
            if (_edgesContainer == null)
                _edgesContainer = transform.Find("MapScrollView/EdgesContainer") as RectTransform;
        }
    }

    /// <summary>
    /// 地图节点UI组件
    /// </summary>
    public class MapNodeUI : MonoBehaviour
    {
        [SerializeField] private Image _nodeImage;
        [SerializeField] private Text _nodeText;
        [SerializeField] private Image _iconImage;
        [SerializeField] private GameObject _visitedMark;
        [SerializeField] private GameObject _lockMark;

        private MapNode _node;
        private System.Action<MapNode> _onClickCallback;

        public void Initialize(MapNode node, System.Action<MapNode> onClickCallback)
        {
            _node = node;
            _onClickCallback = onClickCallback;

            UpdateVisual();
            SetupButton();
        }

        private void UpdateVisual()
        {
            if (_node == null) return;

            // 设置节点颜色
            Color nodeColor = Color.white;
            switch (_node.state)
            {
                case MapNodeState.Locked:
                    nodeColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 暗色
                    break;
                case MapNodeState.Available:
                    nodeColor = Color.white; // 高亮
                    break;
                case MapNodeState.Visited:
                    nodeColor = new Color(0.7f, 0.7f, 0.7f, 0.8f); // 灰色
                    break;
            }

            if (_nodeImage != null)
                _nodeImage.color = nodeColor;

            // 设置图标
            if (_iconImage != null)
            {
                // 根据节点类型设置不同颜色
                switch (_node.nodeType)
                {
                    case MapNodeType.Battle:
                        _nodeImage.color = new Color(0.8f, 0.8f, 0.8f);
                        break;
                    case MapNodeType.Elite:
                        _nodeImage.color = new Color(1f, 0.6f, 0.6f); // 红色
                        break;
                    case MapNodeType.Shop:
                        _nodeImage.color = new Color(0.6f, 1f, 0.6f); // 绿色
                        break;
                    case MapNodeType.Event:
                        _nodeImage.color = new Color(0.6f, 0.6f, 1f); // 蓝色
                        break;
                    case MapNodeType.Boss:
                        _nodeImage.color = new Color(1f, 0.3f, 0.3f); // 深红色
                        break;
                }
            }

            // 设置标记
            if (_visitedMark != null)
                _visitedMark.SetActive(_node.state == MapNodeState.Visited);
            if (_lockMark != null)
                _lockMark.SetActive(_node.state == MapNodeState.Locked);

            // 设置文本
            if (_nodeText != null)
            {
                _nodeText.text = _node.nodeType.ToString();
            }
        }

        private void SetupButton()
        {
            Button button = GetComponent<Button>();
            if (button != null)
            {
                button.interactable = (_node.state == MapNodeState.Available);
                button.onClick.AddListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            if (_node != null && _node.state == MapNodeState.Available)
            {
                _onClickCallback?.Invoke(_node);
            }
        }
    }

    /// <summary>
    /// 地图节点Tips面板
    /// </summary>
    public class MapNodeTips : MonoBehaviour
    {
        [SerializeField] private Text _nodeNameText;
        [SerializeField] private Text _nodeTypeText;
        [SerializeField] private Text _enemyInfoText;
        [SerializeField] private Text _rewardInfoText;
        [SerializeField] private Button _enterButton;

        private MapNode _node;
        private System.Action<MapNode> _onEnterCallback;

        public void Initialize(MapNode node, System.Action<MapNode> onEnterCallback)
        {
            _node = node;
            _onEnterCallback = onEnterCallback;

            UpdateUI();
            SetupButton();
        }

        private void UpdateUI()
        {
            if (_node == null) return;

            if (_nodeNameText != null)
                _nodeNameText.text = GetNodeName();

            if (_nodeTypeText != null)
                _nodeTypeText.text = GetNodeTypeName();

            if (_enemyInfoText != null)
                _enemyInfoText.text = GetEnemyInfo();

            if (_rewardInfoText != null)
                _rewardInfoText.text = GetRewardInfo();
        }

        private string GetNodeName()
        {
            switch (_node.nodeType)
            {
                case MapNodeType.Battle: return "普通战斗";
                case MapNodeType.Elite: return "精英战斗";
                case MapNodeType.Shop: return "猫市";
                case MapNodeType.Event: return "随机事件";
                case MapNodeType.Boss: return "Boss战";
                default: return "未知";
            }
        }

        private string GetNodeTypeName()
        {
            return $"第 {_node.battleNumber} 关";
        }

        private string GetEnemyInfo()
        {
            if (_node.nodeType == MapNodeType.Shop || _node.nodeType == MapNodeType.Event)
                return "无敌人";

            // TODO: 根据街头情报等级显示敌人信息
            return "敌人信息";
        }

        private string GetRewardInfo()
        {
            switch (_node.nodeType)
            {
                case MapNodeType.Battle: return "普通奖励";
                case MapNodeType.Elite: return "高额奖励";
                case MapNodeType.Shop: return "可购买物品";
                case MapNodeType.Event: return "特殊奖励";
                case MapNodeType.Boss: return "Boss专属奖励";
                default: return "";
            }
        }

        private void SetupButton()
        {
            if (_enterButton != null)
            {
                _enterButton.onClick.AddListener(OnEnterClicked);
            }
        }

        private void OnEnterClicked()
        {
            _onEnterCallback?.Invoke(_node);
        }
    }
}
