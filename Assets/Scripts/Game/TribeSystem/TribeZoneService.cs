using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 单位区域类型
    /// </summary>
    public enum UnitZone
    {
        Pending,    // 待上阵区
        Battle,     // 上阵区
        Production  // 生产区
    }

    /// <summary>
    /// 三区系统服务 - 管理待上阵区/上阵区/生产区的单位流转
    /// </summary>
    public class TribeZoneService
    {
        private DataManager _dataManager;
        private AuraService _auraService;

        // 事件
        public event Action OnUnitsChanged;
        public event Action<int, UnitZone> OnUnitMoved;

        public TribeZoneService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        public void SetAuraService(AuraService auraService)
        {
            _auraService = auraService;
        }

        /// <summary>
        /// 获取指定区域的单位列表
        /// </summary>
        public List<FighterData> GetUnitsInZone(UnitZone zone)
        {
            var result = new List<FighterData>();
            var playerData = _dataManager?.PlayerData;

            if (playerData == null || playerData.tribes == null)
                return result;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units != null)
                {
                    foreach (var unit in tribe.units)
                    {
                        if (unit.zone == zone)
                        {
                            result.Add(unit);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 将单位移动到指定区域
        /// </summary>
        public bool MoveUnit(long unitId, UnitZone targetZone)
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData == null || playerData.tribes == null)
                return false;

            FighterData unit = FindUnit(unitId, playerData);
            if (unit == null)
                return false;

            // 检查移动是否合法
            if (!CanMoveUnit(unit, targetZone))
                return false;

            UnitZone oldZone = unit.zone;
            unit.zone = targetZone;

            // 如果移动到生产区，设置debuff
            if (targetZone == UnitZone.Production)
            {
                ApplyProductionDebuff(unit);
            }

            OnUnitMoved?.Invoke(unitId, targetZone);
            OnUnitsChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// 检查单位是否可以移动到目标区域
        /// </summary>
        public bool CanMoveUnit(FighterData unit, UnitZone targetZone)
        {
            if (unit == null) return false;

            // 生产区不可逆：已进入生产区的单位不能移出
            if (unit.zone == UnitZone.Production)
                return false;

            switch (targetZone)
            {
                case UnitZone.Pending:
                    // 待上阵区的单位可以移回待上阵区
                    return unit.zone == UnitZone.Pending;

                case UnitZone.Battle:
                    // 检查领导力限制
                    return CheckLeadershipLimit(unit);

                case UnitZone.Production:
                    // 任何非生产区单位都可以放入生产区（不可逆）
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 检查领导力限制
        /// </summary>
        private bool CheckLeadershipLimit(FighterData unit)
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData == null) return false;

            int currentPopulation = GetTotalPopulation();
            int maxPopulation = GetMaxPopulation();

            int unitPopulationCost = GetUnitPopulationCost(unit);

            return currentPopulation + unitPopulationCost <= maxPopulation;
        }

        /// <summary>
        /// 获取当前上阵区总人口
        /// </summary>
        public int GetTotalPopulation()
        {
            var units = GetUnitsInZone(UnitZone.Battle);
            int total = 0;

            foreach (var unit in units)
            {
                total += GetUnitPopulationCost(unit);
            }

            return total;
        }

        /// <summary>
        /// 获取最大人口（领导力）
        /// </summary>
        public int GetMaxPopulation()
        {
            // TODO: 从主角属性获取领导力值
            // 目前返回默认值3
            return 3;
        }

        /// <summary>
        /// 获取单位占用人口
        /// </summary>
        public int GetUnitPopulationCost(FighterData unit)
        {
            if (unit == null) return 1;

            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
            if (fighterConfig != null)
            {
                return fighterConfig.populationCost;
            }

            return 1; // 默认占用1人口
        }

        /// <summary>
        /// 查找单位
        /// </summary>
        private FighterData FindUnit(long unitId, PlayerData playerData)
        {
            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units != null)
                {
                    foreach (var unit in tribe.units)
                    {
                        if (unit.id == unitId)
                        {
                            return unit;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 应用生产区debuff（满目疮痍）
        /// </summary>
        private void ApplyProductionDebuff(FighterData unit)
        {
            // 满目疮痍debuff：不能上阵，只能放入生产区
            // 这里只需要设置标记，实际debuff效果在检查CanMoveUnit时处理
            // TODO: 可以添加具体的debuff效果
        }

        /// <summary>
        /// 获取生产区总产出
        /// </summary>
        public int GetProductionOutput()
        {
            var units = GetUnitsInZone(UnitZone.Production);
            int totalOutput = 0;

            foreach (var unit in units)
            {
                totalOutput += GetUnitProductionOutput(unit);
            }

            return totalOutput;
        }

        /// <summary>
        /// 获取单位产出
        /// </summary>
        private int GetUnitProductionOutput(FighterData unit)
        {
            if (unit == null) return 0;

            // 根据单位品质和等级计算产出
            int baseOutput = 10;

            // 品质加成
            switch (unit.quality)
            {
                case CatQuality.White: baseOutput *= 1; break;
                case CatQuality.Blue: baseOutput *= 2; break;
                case CatQuality.Purple: baseOutput *= 3; break;
                case CatQuality.Gold: baseOutput *= 5; break;
            }

            // 等级加成
            baseOutput *= (int)unit.tier;

            return baseOutput;
        }

        /// <summary>
        /// 结算生产区产出（每关结束时调用）
        /// </summary>
        public int SettleProductionOutput()
        {
            int totalOutput = GetProductionOutput();

            if (totalOutput > 0 && _dataManager != null)
            {
                _dataManager.AddCatFood(totalOutput);
                Debug.Log($"[TribeZoneService] 生产区产出: {totalOutput} 木天蓼叶");
            }

            return totalOutput;
        }

        /// <summary>
        /// Boss关：全员上阵（包括生产区单位）
        /// 注意：Boss关结束后，生产区单位保持在上阵区（生产区不可逆）
        /// </summary>
        public void ForceAllUnitsToBattle()
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData == null || playerData.tribes == null)
                return;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units != null)
                {
                    foreach (var unit in tribe.units)
                    {
                        // 将所有单位移动到上阵区（包括生产区）
                        if (unit.zone != UnitZone.Battle)
                        {
                            unit.zone = UnitZone.Battle;
                        }
                    }
                }
            }

            OnUnitsChanged?.Invoke();
        }
    }
}
