using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public static class HexAdventureMapGenerator
    {
        private const int RouteCount = 4;
        private const int MiddleFloorCount = 8;

        public static HexMapData Generate()
        {
            var map = new HexMapData();
            var nodeLookup = new Dictionary<(int floor, int lane), HexMapNodeData>();

            var startNode = CreateOrGetNode(nodeLookup, map, 0, 1, HexMapNodeType.Start);
            startNode.uiPosition = GetNodePosition(0, 1.5f);
            map.startNodeId = startNode.id;

            var routes = new List<List<HexMapNodeData>>();
            for (int routeIndex = 0; routeIndex < RouteCount; routeIndex++)
            {
                var route = new List<HexMapNodeData>();
                int lane = routeIndex;
                var previousNode = startNode;
                for (int floor = 1; floor <= MiddleFloorCount; floor++)
                {
                    if (floor > 1)
                        lane = Mathf.Clamp(lane + Random.Range(-1, 2), 0, RouteCount - 1);

                    var node = CreateOrGetNode(nodeLookup, map, floor, lane, PickNodeTypeForFloor(floor));
                    node.uiPosition = GetNodePosition(floor, lane);
                    Connect(previousNode, node);
                    route.Add(node);
                    previousNode = node;
                }

                routes.Add(route);
            }

            var bossNode = CreateOrGetNode(nodeLookup, map, MiddleFloorCount + 1, 1, HexMapNodeType.Boss);
            bossNode.uiPosition = GetNodePosition(MiddleFloorCount + 1, 1.5f);
            map.bossNodeId = bossNode.id;

            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                if (route.Count > 0)
                    Connect(route[^1], bossNode);
            }

            return map;
        }

        private static HexMapNodeData CreateOrGetNode(
            Dictionary<(int floor, int lane), HexMapNodeData> nodeLookup,
            HexMapData map,
            int floor,
            float lane,
            HexMapNodeType nodeType)
        {
            int laneKey = Mathf.RoundToInt(lane * 10f);
            if (nodeLookup.TryGetValue((floor, laneKey), out var existingNode))
            {
                if (existingNode.nodeType != HexMapNodeType.Start && existingNode.nodeType != HexMapNodeType.Boss)
                    existingNode.nodeType = PromoteNodeType(existingNode.nodeType, nodeType);
                return existingNode;
            }

            var node = new HexMapNodeData
            {
                id = $"node_{floor}_{laneKey}",
                floorIndex = floor,
                laneIndex = laneKey,
                nodeType = nodeType,
            };
            nodeLookup[(floor, laneKey)] = node;
            map.nodes.Add(node);
            return node;
        }

        private static HexMapNodeType PromoteNodeType(HexMapNodeType currentType, HexMapNodeType incomingType)
        {
            if (currentType == incomingType)
                return currentType;

            if (currentType == HexMapNodeType.EliteBattle || incomingType == HexMapNodeType.EliteBattle)
                return HexMapNodeType.EliteBattle;
            if (currentType == HexMapNodeType.Shop || incomingType == HexMapNodeType.Shop)
                return HexMapNodeType.Shop;
            if (currentType == HexMapNodeType.Rest || incomingType == HexMapNodeType.Rest)
                return HexMapNodeType.Rest;
            if (currentType == HexMapNodeType.Event || incomingType == HexMapNodeType.Event)
                return HexMapNodeType.Event;
            return HexMapNodeType.SmallBattle;
        }

        private static void Connect(HexMapNodeData from, HexMapNodeData to)
        {
            if (from == null || to == null)
                return;

            if (!from.outgoingNodeIds.Contains(to.id))
                from.outgoingNodeIds.Add(to.id);
        }

        private static HexMapNodeType PickNodeTypeForFloor(int floor)
        {
            if (floor <= 2)
                return HexMapNodeType.SmallBattle;

            int roll = Random.Range(0, 100);
            if (floor == MiddleFloorCount)
                return roll < 60 ? HexMapNodeType.Rest : HexMapNodeType.EliteBattle;
            if (roll < 42)
                return HexMapNodeType.SmallBattle;
            if (roll < 58)
                return HexMapNodeType.Event;
            if (roll < 72)
                return HexMapNodeType.Rest;
            if (roll < 86)
                return HexMapNodeType.Shop;
            return HexMapNodeType.EliteBattle;
        }

        private static Vector2 GetNodePosition(int floor, float lane)
        {
            const float xSpacing = 260f;
            const float ySpacing = 142f;
            const float centerX = 0f;
            const float baseY = -300f;
            return new Vector2(centerX + (lane - 1.5f) * xSpacing, baseY + floor * ySpacing);
        }
    }
}
