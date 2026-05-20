using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapSystem
{
    /// <summary>
    /// 地图生成器 - 生成纺锤形分支路径地图
    /// 结构：1起点 → 扩展分支 → 合并收缩 → 1个Boss点
    /// </summary>
    public class MapGenerator
    {
        private const int DEFAULT_LAYERS = 15;         // 默认层数
        private const int MIN_NODES_PER_LAYER = 2;     // 每层最少节点
        private const int MAX_NODES_PER_LAYER = 4;     // 每层最多节点
        private const int HOT_SPRING_LAYERS = 2;       // 温泉数量
        private const int FIRST_FORK_LAYER = 1;        // 第1层后3分叉

        private int _nodeIdCounter = 0;
        private System.Random _random;
        private MapConfig _config;

        public MapGenerator(int seed = -1, MapConfig config = null)
        {
            if (seed < 0)
                _random = new System.Random();
            else
                _random = new System.Random(seed);

            _config = config ?? MapConfig.GetDefault();
        }

        /// <summary>
        /// 生成一个大关的地图（纺锤形结构）
        /// </summary>
        public MapData GenerateRegionMap(int regionId, int startBattleNumber)
        {
            MapData mapData = new MapData();
            mapData.regionId = regionId;
            _nodeIdCounter = 0;

            int totalLayers = _config.layersPerRegion;

            // 第0层：单一起点（战斗）
            int startNode = CreateNode(mapData, 0, 1, MapNodeType.Battle, startBattleNumber);
            List<int> previousLayerNodes = new List<int> { startNode };

            // 第1层：必然3分叉
            List<int> currentLayerNodes = CreateFork(mapData, 1, previousLayerNodes, 3, startBattleNumber + 1);
            previousLayerNodes = currentLayerNodes;

            // 第2层到第totalLayers-2层：纺锤形扩展和收缩
            for (int layer = 2; layer < totalLayers - 1; layer++)
            {
                int battleNumber = startBattleNumber + layer;
                currentLayerNodes = GenerateLayer(mapData, layer, previousLayerNodes, battleNumber);
                previousLayerNodes = currentLayerNodes;
            }

            // 最后一层：Boss关（固定单节点）
            int bossNode = CreateNode(mapData, totalLayers - 1, 1, MapNodeType.Boss, startBattleNumber + totalLayers - 1);
            foreach (int nodeId in previousLayerNodes)
            {
                CreateEdge(mapData, nodeId, bossNode);
            }

            return mapData;
        }

        /// <summary>
        /// 生成整局地图（3个大关）
        /// </summary>
        public List<MapData> GenerateFullMap()
        {
            var maps = new List<MapData>();

            int battleNumber = 1;
            for (int region = 1; region <= 3; region++)
            {
                maps.Add(GenerateRegionMap(region, battleNumber));
                battleNumber += _config.layersPerRegion;
            }

            return maps;
        }

        /// <summary>
        /// 生成中间层（纺锤形核心逻辑）
        /// </summary>
        private List<int> GenerateLayer(MapData mapData, int layer, List<int> previousLayerNodes, int battleNumber)
        {
            List<int> currentLayerNodes = new List<int>();

            // 确定本层目标节点数（纺锤形：中间多，两头少）
            int targetNodeCount = CalculateTargetNodeCount(layer, _config.layersPerRegion);

            // 确定节点类型
            MapNodeType nodeType = GetNodeType(layer, battleNumber);

            if (previousLayerNodes.Count == 1)
            {
                // 前一层只有1个节点，需要分叉
                int forkCount = Mathf.Min(targetNodeCount, MAX_NODES_PER_LAYER);
                currentLayerNodes = CreateFork(mapData, layer, previousLayerNodes, forkCount, battleNumber);
            }
            else if (previousLayerNodes.Count >= targetNodeCount)
            {
                // 前一层节点多于目标，需要合线
                currentLayerNodes = MergeNodes(mapData, layer, previousLayerNodes, targetNodeCount, battleNumber);
            }
            else
                // 前一层节点少于目标，需要分叉
            {
                currentLayerNodes = ExpandNodes(mapData, layer, previousLayerNodes, targetNodeCount, battleNumber);
            }

            return currentLayerNodes;
        }

        /// <summary>
        /// 计算目标节点数（纺锤形曲线）
        /// </summary>
        private int CalculateTargetNodeCount(int layer, int totalLayers)
        {
            // 纺锤形：从第1层开始扩展，中间达到最大，然后收缩
            // 使用正弦曲线模拟：nodes = min + (max - min) * sin(π * layer / (totalLayers - 1))
            float progress = (float)layer / (totalLayers - 1);
            float sinValue = Mathf.Sin(Mathf.PI * progress);
            int targetCount = Mathf.RoundToInt(MIN_NODES_PER_LAYER + (MAX_NODES_PER_LAYER - MIN_NODES_PER_LAYER) * sinValue);
            return Mathf.Clamp(targetCount, MIN_NODES_PER_LAYER, MAX_NODES_PER_LAYER);
        }

        /// <summary>
        /// 合并节点（多个前层节点合并为更少的当前层节点）
        /// </summary>
        private List<int> MergeNodes(MapData mapData, int layer, List<int> previousLayerNodes, int targetCount, int battleNumber)
        {
            List<int> currentLayerNodes = new List<int>();
            MapNodeType nodeType = GetNodeType(layer, battleNumber);

            // 将前层节点分组，每组连接到一个当前层节点
            int nodesPerGroup = Mathf.CeilToInt((float)previousLayerNodes.Count / targetCount);

            for (int i = 0; i < targetCount; i++)
            {
                int startIdx = i * nodesPerGroup;
                int endIdx = Mathf.Min(startIdx + nodesPerGroup, previousLayerNodes.Count);

                if (startIdx >= previousLayerNodes.Count)
                    break;

                // 创建当前层节点
                int row = i + 1;
                int nodeId = CreateNode(mapData, layer, row, nodeType, battleNumber);
                currentLayerNodes.Add(nodeId);

                // 连接前层节点到当前层节点
                for (int j = startIdx; j < endIdx; j++)
                {
                    CreateEdge(mapData, previousLayerNodes[j], nodeId);
                }
            }

            return currentLayerNodes;
        }

        /// <summary>
        /// 扩展节点（少量前层节点扩展为更多当前层节点）
        /// </summary>
        private List<int> ExpandNodes(MapData mapData, int layer, List<int> previousLayerNodes, int targetCount, int battleNumber)
        {
            List<int> currentLayerNodes = new List<int>();
            MapNodeType nodeType = GetNodeType(layer, battleNumber);

            // 每个前层节点至少连接1个当前层节点，多余的随机分配
            int extraNodes = targetCount - previousLayerNodes.Count;
            List<int> nodesToCreate = new List<int>();

            // 基础：每个前层节点1个当前层节点
            for (int i = 0; i < previousLayerNodes.Count; i++)
            {
                nodesToCreate.Add(1);
            }

            // 分配额外节点
            for (int i = 0; i < extraNodes; i++)
            {
                int randomIdx = _random.Next(0, nodesToCreate.Count);
                nodesToCreate[randomIdx]++;
            }

            // 创建节点并连接
            int currentRow = 1;
            for (int i = 0; i < previousLayerNodes.Count; i++)
            {
                for (int j = 0; j < nodesToCreate[i]; j++)
                {
                    int nodeId = CreateNode(mapData, layer, currentRow, nodeType, battleNumber);
                    currentLayerNodes.Add(nodeId);
                    CreateEdge(mapData, previousLayerNodes[i], nodeId);
                    currentRow++;
                }
            }

            return currentLayerNodes;
        }

        /// <summary>
        /// 创建分叉节点
        /// </summary>
        private List<int> CreateFork(MapData mapData, int layer, List<int> fromNodes, int forkCount, int battleNumber)
        {
            List<int> forkNodes = new List<int>();
            MapNodeType nodeType = GetNodeType(layer, battleNumber);

            for (int i = 0; i < forkCount; i++)
            {
                int row = i + 1;
                int nodeId = CreateNode(mapData, layer, row, nodeType, battleNumber);
                forkNodes.Add(nodeId);

                // 每个前一层节点都连接到所有分叉节点
                foreach (int fromNode in fromNodes)
                {
                    CreateEdge(mapData, fromNode, nodeId);
                }
            }

            return forkNodes;
        }

        /// <summary>
        /// 创建节点
        /// </summary>
        private int CreateNode(MapData mapData, int layer, int row, MapNodeType nodeType, int battleNumber)
        {
            int nodeId = _nodeIdCounter++;
            MapNode node = new MapNode(nodeId, layer, row, nodeType, battleNumber);
            mapData.nodes.Add(node);
            return nodeId;
        }

        /// <summary>
        /// 创建连线
        /// </summary>
        private void CreateEdge(MapData mapData, int fromNodeId, int toNodeId)
        {
            // 检查是否已存在
            foreach (var edge in mapData.edges)
            {
                if (edge.fromNodeId == fromNodeId && edge.toNodeId == toNodeId)
                    return;
            }

            MapEdge edgeObj = new MapEdge(fromNodeId, toNodeId);
            mapData.edges.Add(edgeObj);

            // 更新节点的nextNodeIds
            var fromNode = mapData.GetNode(fromNodeId);
            if (fromNode != null && !fromNode.nextNodeIds.Contains(toNodeId))
            {
                fromNode.nextNodeIds.Add(toNodeId);
            }
        }

        /// <summary>
        /// 根据层号和关卡编号确定节点类型
        /// </summary>
        private MapNodeType GetNodeType(int layer, int battleNumber)
        {
            // Boss关在最后一层，已在GenerateRegionMap中处理
            if (layer == _config.layersPerRegion - 1)
                return MapNodeType.Boss;

            // 温泉固定在第5层和第10层
            if (_config.hotSpringLayers.Contains(layer))
                return MapNodeType.HotSpring;

            // 精英关在固定位置
            if (_config.eliteLayers.Contains(layer))
                return MapNodeType.Elite;

            // 商店和事件随机分布
            double roll = _random.NextDouble();
            if (roll < 0.15)
                return MapNodeType.Shop;
            else if (roll < 0.30)
                return MapNodeType.Event;
            else
                return MapNodeType.Battle;
        }
    }

    /// <summary>
    /// 地图配置（从JSON读取）
    /// </summary>
    [Serializable]
    public class MapConfig
    {
        public int layersPerRegion = 15;                    // 每地区层数
        public int minNodesPerLayer = 2;                    // 每层最少节点
        public int maxNodesPerLayer = 4;                    // 每层最多节点
        public List<int> hotSpringLayers = new List<int> { 5, 10 };  // 温泉固定层
        public List<int> eliteLayers = new List<int> { 5, 10 };      // 精英固定层
        public float shopChance = 0.15f;                    // 商店出现概率
        public float eventChance = 0.15f;                   // 事件出现概率

        /// <summary>
        /// 获取默认配置
        /// </summary>
        public static MapConfig GetDefault()
        {
            return new MapConfig();
        }

        /// <summary>
        /// 从JSON加载配置
        /// </summary>
        public static MapConfig LoadFromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<MapConfig>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapConfig] 加载配置失败: {e.Message}");
                return GetDefault();
            }
        }
    }
}
