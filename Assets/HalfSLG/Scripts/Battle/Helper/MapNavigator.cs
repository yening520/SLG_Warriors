﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SLGame
{
    public class MapNavigator
        : NormalSingleton<MapNavigator>, IGameBase
    {
        private class NavigationData
        {
            public bool open = true;

            public int F;
            public int G;
            public int H;

            public GridUnit thisGrid;
            public NavigationData preGrid;

            public NavigationData()
            {
                Reset();
            }

            public void Reset()
            {
                open = true;

                F = 0;
                G = 0;
                H = 0;

                //清空关联
                if (thisGrid != null)
                {
                    thisGrid.tempRef = null;
                    thisGrid = null;
                }

                preGrid = null;
            }
        }

        //池
        private int curUsedIdx = 0;
        private List<NavigationData> navigationDataPool = null;

        private NavigationData GetEmptyNavigationData(GridUnit _thisGrid, NavigationData _preGrid, int _G, int _H)
        {
            //优先从池子里取出
            NavigationData nd = null;
            if (curUsedIdx < navigationDataPool.Count)
            {
                nd = navigationDataPool[curUsedIdx];
            }
            else
            {
                nd = new NavigationData();
                navigationDataPool.Add(nd);
            }

            ++curUsedIdx;

            nd.thisGrid = _thisGrid;
            nd.preGrid = _preGrid;
            nd.G = _G;
            nd.H = _H;
            nd.F = _G + _H;
            nd.open = true;
            nd.thisGrid.tempRef = nd;

            return nd;
        }

        private void ResetPool()
        {
            for (int i = 0; i < curUsedIdx; ++i)
            {
                navigationDataPool[i].Reset();
            }
            curUsedIdx = 0;
        }

        /// <summary>
        /// 导航至某个位置
        /// </summary>
        /// <returns>The navigate.</returns>
        /// <param name="battleMap">地图</param>
        /// <param name="from">起始格子</param>
        /// <param name="to">目标格子</param>
        /// <param name="path">保存导航路径</param>
        /// <param name="searched">搜索过的路径</param>
        /// <param name="mobility">步数限制(移动区域半径)</param>
        /// <param name="stopDistance">距离目标的停止距离</param>
        /// <param name="containsTargetGrid">路径是否包含目标</param>
        public bool Navigate(
            BattleMap battleMap,
            GridUnit from,
            GridUnit to,
            List<GridUnit> path,
            List<GridUnit> searched = null,
            int mobility = -1,
            int stopDistance = 0)
        {
            //没有设置地图
            if (battleMap == null)
                return false;

            if (path != null)
                path.Clear();

            if (searched != null)
                searched.Clear();

            //这种情况基本上也就不用寻路了吧...
            if (to.runtimePasses == 0
                && stopDistance <= 1
                && from.Distance(to) > 1)
                return false;

            //本来就在停止距离内
            if (from.Distance(to) <= stopDistance)
                return true;

            int tryTimes = battleMap.GridCount;

            List<NavigationData> opening = new List<NavigationData>();

            opening.Add(GetEmptyNavigationData(from, null, 0, from.Distance(to)));

            int retry = 0;
            bool catched = false;

            //当前探索方向
            int curDir = 0;
            //上次探索方向
            int lastDir = 0;
            //每次检测方向的次数
            int checkTimes = 0;

            //判断是否需要遍历open列表
            NavigationData gift = null;

            //距离最近的格子(接下来要移动的)
            NavigationData next_0 = null;
            //距离次近的格子
            NavigationData next_1 = null;

            int minStep = EGameConstL.Infinity;

            while (retry <= tryTimes && !catched)
            {
                ++retry;
                //从open中查找最近的节点
                if (gift != null)
                {
                    next_0 = gift;
                    gift = null;
                }
                else if (next_1 != null)
                {
                    next_0 = next_1;
                    next_1 = null;
                }
                else
                {
                    minStep = EGameConstL.Infinity;
                    if (opening.Count == 0)
                    {
                        break;
                    }

                    for (int i = opening.Count - 1; i >= 0; --i)
                    {
                        if (!opening[i].open)
                        {
                            opening.RemoveAt(i);
                        }
                        else if (opening[i].F < minStep)
                        {
                            next_0 = opening[i];
                            minStep = next_0.F;
                        }
                        else if (next_1 == null && next_0 != null && opening[i].F == next_0.F)
                        {
                            next_1 = opening[i];
                        }
                    }
                }

                //标志为已关闭
                next_0.open = false;

                //放入已搜索中
                if (searched != null)
                {
                    searched.Add(next_0.thisGrid);
                }

                checkTimes = 6;
                curDir = lastDir;
                //遍历最近节点的周围6个节点，依次放入close中
                int roads = next_0.thisGrid.NavigationPassable ? 63 : next_0.thisGrid.roadPasses;
                while (checkTimes > 0)
                {
                    //沿着当前探索方向继续探索
                    if ((roads & (1 << curDir)) != 0)
                    {
                        //获取该路通向的下一个item
                        GridUnit sibling = battleMap.GetGridByDir(next_0.thisGrid.row, next_0.thisGrid.column, curDir);
                        if (sibling == null)
                        {
                            //没路
                            ++curDir;
                            curDir = (curDir > 5) ? 0 : curDir;
                            --checkTimes;
                            continue;
                        }
                        //如果这个不能移动
                        else if (sibling.GridType == GridType.Obstacle && !sibling.NavigationPassable)
                        {
                            //没路
                            ++curDir;
                            curDir = (curDir > 5) ? 0 : curDir;
                            --checkTimes;
                            continue;
                        }
                        //如果这个格子有战斗单位且可以战斗
                        else if (sibling.battleUnit != null && sibling.battleUnit.CanAction && !sibling.NavigationPassable)
                        {
                            //无法通过
                            ++curDir;
                            curDir = (curDir > 5) ? 0 : curDir;
                            --checkTimes;
                            continue;
                        }
                        //如果这个item就是目标或者已经进入了停止距离
                        else if ((stopDistance > 0 && sibling.Distance(to) <= stopDistance) || sibling.Equals(to))
                        {
                            catched = true;
                            if (path != null)
                            {
                                path.Add(sibling);

                                NavigationData current = next_0;
                                while (current != null)
                                {
                                    if (current.thisGrid != from)
                                    {
                                        path.Add(current.thisGrid);
                                    }
                                    current = current.preGrid;
                                }
                                //翻转一下
                                path.Reverse();
                            }
                            break;
                        }
                        else
                        {
                            //尝试判断这个是否为closed
                            NavigationData nd = sibling.tempRef == null ? null : (NavigationData)(sibling.tempRef);
                            if (nd == null)
                            {
                                //这个格子没有探索过，新建并添加
                                nd = GetEmptyNavigationData(sibling, next_0, next_0.G + 1, sibling.Distance(to));
                                //这个格子不错哦
                                if (nd.F <= next_0.F && gift == null)
                                {
                                    //保存礼物
                                    gift = nd;
                                    //记录下次起始的更新方向
                                    lastDir = curDir;
                                }
                                //比第二目标好
                                else if (next_1 != null && nd.F < next_1.F)
                                {
                                    //替换第二目标
                                    next_1 = nd;
                                    opening.Add(nd);
                                }
                                else
                                {
                                    //已经设置了礼物，因此只能放入opening列表中，以后再更新了呢
                                    opening.Add(nd);
                                }
                            }
                            else
                            {
                                //只处理没有被探索过的格子
                                if (nd.open)
                                {
                                    //已经在Open列表中了
                                    if ((next_0.G + 1) < nd.G)
                                    {
                                        //比原来的近，应该不可能
                                        nd.G = next_0.G + 1;
                                        nd.H = sibling.Distance(to);
                                        nd.F = nd.G + nd.H;
                                        nd.preGrid = next_0;
                                        nd.thisGrid = sibling;
                                    }
                                    //这个格子不错哦
                                    if (nd.F <= next_0.F && gift == null)
                                    {
                                        gift = nd;
                                        //保存当前探索方向
                                        lastDir = curDir;
                                    }
                                    else if (next_1 != null && nd.F < next_1.F)
                                    {
                                        //替换第二目标
                                        next_1 = nd;
                                    }
                                }
                            }
                        }
                    }
                    ++curDir;
                    curDir = (curDir > 5) ? 0 : curDir;
                    --checkTimes;
                }
            }

            opening.Clear();

            //重置池子
            ResetPool();

            //有步数限制
            if (catched
                && path != null
                && mobility > 0)
            {
                for (int i = 0; i < path.Count; ++i)
                {
                    if (path[i].Distance(from) > mobility)
                    {
                        path.RemoveRange(i, path.Count - i);
                        break;
                    }
                }
            }

            return catched;
        }

        class PathTraceData : IComparable<PathTraceData>
        {
            public float MovePointRemainder;
            public GridUnit currentGridUnit;
            public PathTraceData(float v, GridUnit gridUnit)
            {
                MovePointRemainder = v;
                currentGridUnit = gridUnit;
            }

            int IComparable<PathTraceData>.CompareTo(PathTraceData other)
            {
                if (MovePointRemainder > other.MovePointRemainder) return 1;
                if (MovePointRemainder < other.MovePointRemainder) return -1;
                return 0;
            }
        }

        //寻找某点到另一点的路径
        public bool NewNavigate(
            BattleUnit battleUnit,
            BattleMap battleMap,
            GridUnit from,
            GridUnit to,
            List<GridUnit> path,
            int stopDistance = 0)
        {
            PathTraceData[,] flag = new PathTraceData[battleMap.mapWidth, battleMap.mapHeight];
            PriorityQueue<PathTraceData> checkList = new PriorityQueue<PathTraceData>();
            PathTraceData currentUnit = new PathTraceData(battleUnit.battleUnitAttribute.mobility, from);
            flag[from.column, from.row] = currentUnit;
            bool isFind = false;
            checkList.Push(currentUnit);
            while (checkList.Count > 0)
            {
                currentUnit = checkList.Pop();
                if (currentUnit.currentGridUnit.Distance(to) <= stopDistance)
                {
                    isFind = true;
                    break;
                }
                List<GridUnit> gridUnitList = battleMap.GetNearbyGrid(currentUnit.currentGridUnit);
                foreach (GridUnit gridUnit in gridUnitList)
                {
                    float MovePointRemainder = currentUnit.MovePointRemainder - currentUnit.currentGridUnit.m_GridAttribute.m_CrossCost;
                    //绝对障碍不可通过
                    if (gridUnit.GridType == GridType.Obstacle) continue;
                    //不能跳过去，高度太高
                    if (gridUnit.m_GridAttribute.m_Height - currentUnit.currentGridUnit.m_GridAttribute.m_Height > battleUnit.battleUnitAttribute.springPower) continue;
                    //体积太大穿不过去
                    if (gridUnit.m_GridAttribute.m_MaxPassVolume < battleUnit.battleUnitAttribute.volume) continue;
                    //剩余移动点数不足
                    //if (MovePointRemainder <= 0) continue;
                    //有敌人挡着（暂时还没判是否是友方）
                    if (gridUnit.battleUnit != null && gridUnit.battleUnit.CanAction && !gridUnit.NavigationPassable) continue;

                    if (flag[gridUnit.column, gridUnit.row] == null || flag[gridUnit.column, gridUnit.row].MovePointRemainder < MovePointRemainder)
                    {
                        flag[gridUnit.column, gridUnit.row] = currentUnit;
                        checkList.Push(new PathTraceData(MovePointRemainder, gridUnit));
                    }
                }
            }

            if (!isFind) return false;
            path.Clear();
            Stack<GridUnit> temp = new Stack<GridUnit>();
            while (!currentUnit.currentGridUnit.Equals(from))
            {
                if (currentUnit.MovePointRemainder >= 0)
                {
                    temp.Push(currentUnit.currentGridUnit);
                }
                currentUnit = flag[currentUnit.currentGridUnit.column, currentUnit.currentGridUnit.row];
            }
            while (temp.Count != 0)
            {
                path.Add(temp.Pop());
            }
            return true;
        }

        //查找所有可以到达的格子
        public List<GridUnit> GetAccessibleGrids(BattleUnit battleUnit, BattleMap battleMap)
        {
            List<GridUnit> AccessibleGrids = new List<GridUnit>();
            bool[,] flag = new bool[battleMap.mapWidth, battleMap.mapHeight];
            flag[battleUnit.mapGrid.column, battleUnit.mapGrid.row] = true;
            Queue<PathTraceData> checkList = new Queue<PathTraceData>();
            PathTraceData currentUnit = new PathTraceData(battleUnit.battleUnitAttribute.mobility, battleUnit.mapGrid);
            checkList.Enqueue(currentUnit);
            while (checkList.Count > 0)
            {
                currentUnit = checkList.Dequeue();
                List<GridUnit> gridUnitList = battleMap.GetNearbyGrid(currentUnit.currentGridUnit);
                foreach (GridUnit gridUnit in gridUnitList)
                {
                    float MovePointRemainder = currentUnit.MovePointRemainder - currentUnit.currentGridUnit.m_GridAttribute.m_CrossCost;
                    //绝对障碍不可通过
                    if (gridUnit.GridType == GridType.Obstacle) continue;
                    //不能跳过去，高度太高
                    if (gridUnit.m_GridAttribute.m_Height - currentUnit.currentGridUnit.m_GridAttribute.m_Height > battleUnit.battleUnitAttribute.springPower) continue;
                    //体积太大穿不过去
                    if (gridUnit.m_GridAttribute.m_MaxPassVolume < battleUnit.battleUnitAttribute.volume) continue;
                    //剩余移动点数不足
                    if (MovePointRemainder < 0) continue;
                    //有敌人挡着（暂时还没判是否是友方）
                    if (gridUnit.battleUnit != null && gridUnit.battleUnit.CanAction && !gridUnit.NavigationPassable) continue;

                    if (!flag[gridUnit.column, gridUnit.row])
                    {
                        flag[gridUnit.column, gridUnit.row] = true;
                        AccessibleGrids.Add(gridUnit);
                        checkList.Enqueue(new PathTraceData(MovePointRemainder, gridUnit));
                    }
                }
            }

            return AccessibleGrids;
        }

        //查找一定范围内的敌人
        public List<BattleUnit> SearchBattleUnits(GridUnit centerGridUnit, int range)
        {
            List<BattleUnit> battleUnits = new List<BattleUnit>();
            BattleMap battleMap = centerGridUnit.battleMap;
            bool[,] flag = new bool[battleMap.mapWidth, battleMap.mapHeight];
            flag[centerGridUnit.column, centerGridUnit.row] = true;
            Queue<GridUnit> checkList = new Queue<GridUnit>();
            GridUnit currentUnit = centerGridUnit;
            checkList.Enqueue(currentUnit);
            while (checkList.Count > 0)
            {
                currentUnit = checkList.Dequeue();
                List<GridUnit> gridUnitList = battleMap.GetNearbyGrid(currentUnit);
                foreach (GridUnit gridUnit in gridUnitList)
                {
                    if (range < centerGridUnit.Distance(gridUnit)) continue;
                    if (!flag[gridUnit.column, gridUnit.row])
                    {
                        flag[gridUnit.column, gridUnit.row] = true;
                        if (gridUnit.battleUnit != null) battleUnits.Add(gridUnit.battleUnit);
                        checkList.Enqueue(gridUnit);
                    }
                }
            }
            return battleUnits;
        }

        public void Init(params object[] args)
        {
            //初始化一定数量的导航数据
            navigationDataPool = new List<NavigationData>(EGameConstL.WorldMapMaxTryTimes);
            for (int i = 0; i < EGameConstL.WorldMapMaxTryTimes; ++i)
            {
                navigationDataPool.Add(new NavigationData());
            }

            BattleManager.Instance.MgrLog("Map navigator inited.");
        }

        public string Desc()
        {
            return "Map navigatior";
        }
    }
}