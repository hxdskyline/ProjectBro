using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapSystem
{
    /// <summary>
    /// 节点类型
    /// </summary>
    public enum MapNodeType
    {
        Battle,      // 普通战斗
        Elite,       // 精英战斗
        Shop,        // 商店（猫市）
        Event,       // 随机事件
        HotSpring,   // 温泉（回血50%或永久强化50%二选一）
        Boss         // Boss关
    }

    /// <summary>
    /// 地图节点状态
    /// </summary>
    public enum MapNodeState
    {
        Locked,      // 未到达（暗色+锁定）
        Available,   // 当前可选（高亮+呼吸动画）
        Visited      // 已通过（灰色+勾号）
    }

    /// <summary>
    /// 地图节点数据
    /// </summary>
    [Serializable]
    public class MapNode
    {
        public int id;                    // 节点唯一ID
        public int column;                // 列号（0~14）
        public int row;                   // 行号
        public MapNodeType nodeType;      // 节点类型
        public List<int> nextNodeIds;     // 连接到的下一层节点ID列表
        public MapNodeState state;        // 节点状态
        public int battleNumber;          // 关卡编号（1~50）

        public MapNode()
        {
            id = -1;
            column = 0;
            row = 0;
            nodeType = MapNodeType.Battle;
            nextNodeIds = new List<int>();
            state = MapNodeState.Locked;
            battleNumber = 1;
        }

        public MapNode(int id, int column, int row, MapNodeType nodeType, int battleNumber)
        {
            this.id = id;
            this.column = column;
            this.row = row;
            this.nodeType = nodeType;
            this.nextNodeIds = new List<int>();
            this.state = MapNodeState.Locked;
            this.battleNumber = battleNumber;
        }
    }

    /// <summary>
    /// 地图连线数据
    /// </summary>
    [Serializable]
    public class MapEdge
    {
        public int fromNodeId;    // 起始节点ID
        public int toNodeId;      // 目标节点ID

        public MapEdge(int from, int to)
        {
            fromNodeId = from;
            toNodeId = to;
        }
    }

    /// <summary>
    /// 地图数据 - 包含所有节点和连线
    /// </summary>
    [Serializable]
    public class MapData
    {
        public List<MapNode> nodes;
        public List<MapEdge> edges;
        public int regionId;        // 地区ID（1~3）

        public MapData()
        {
            nodes = new List<MapNode>();
            edges = new List<MapEdge>();
            regionId = 1;
        }

        /// <summary>
        /// 获取指定列的所有节点
        /// </summary>
        public List<MapNode> GetNodesAtColumn(int column)
        {
            var result = new List<MapNode>();
            foreach (var node in nodes)
            {
                if (node.column == column)
                    result.Add(node);
            }
            return result;
        }

        /// <summary>
        /// 获取指定节点
        /// </summary>
        public MapNode GetNode(int nodeId)
        {
            foreach (var node in nodes)
            {
                if (node.id == nodeId)
                    return node;
            }
            return null;
        }

        /// <summary>
        /// 获取当前可选的节点列表
        /// </summary>
        public List<MapNode> GetAvailableNodes()
        {
            var result = new List<MapNode>();
            foreach (var node in nodes)
            {
                if (node.state == MapNodeState.Available)
                    result.Add(node);
            }
            return result;
        }

        /// <summary>
        /// 标记节点为已访问
        /// </summary>
        public void MarkNodeVisited(int nodeId)
        {
            var node = GetNode(nodeId);
            if (node != null)
            {
                node.state = MapNodeState.Visited;
            }
        }

        /// <summary>
        /// 更新可用节点（从当前节点出发可达的下一层节点）
        /// </summary>
        public void UpdateAvailableNodes(int currentNodeId)
        {
            // 先把所有Available节点改为Locked
            foreach (var node in nodes)
            {
                if (node.state == MapNodeState.Available)
                    node.state = MapNodeState.Locked;
            }

            // 找到当前节点，标记其下一层节点为Available
            var current = GetNode(currentNodeId);
            if (current != null)
            {
                foreach (int nextId in current.nextNodeIds)
                {
                    var nextNode = GetNode(nextId);
                    if (nextNode != null && nextNode.state != MapNodeState.Visited)
                    {
                        nextNode.state = MapNodeState.Available;
                    }
                }
            }
        }
    }
}
