using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// HP持久化系统 - 管理战斗后的HP状态和满目疮痍debuff
    /// </summary>
    public class HealthPersistenceSystem
    {
        private DataManager _dataManager;

        // 事件
        public event Action<FighterData> OnUnitWounded;
        public event Action<FighterData> OnUnitRecovered;
        public event Action<FighterData> OnWoundsDebuffApplied;
        public event Action<FighterData> OnWoundsDebuffRemoved;

        public HealthPersistenceSystem()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 战斗结束后处理HP持久化
        /// </summary>
        public void OnBattleEnd(bool isVictory, bool isBossBattle)
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData == null || playerData.tribes == null)
                return;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    // Boss关胜利：所有单位复活
                    if (isBossBattle && isVictory)
                    {
                        ReviveUnit(unit);
                    }
                    else
                    {
                        // 普通情况：HP不回复，0血单位获得debuff
                        ProcessUnitHealth(unit);
                    }
                }
            }
        }

        /// <summary>
        /// 处理单位HP状态
        /// </summary>
        private void ProcessUnitHealth(FighterData unit)
        {
            if (unit == null) return;

            // 如果单位HP为0或以下，应用满目疮痍debuff
            if (unit.currentHp <= 0)
            {
                ApplyWoundsDebuff(unit);
            }
        }

        /// <summary>
        /// 应用满目疮痍debuff
        /// </summary>
        private void ApplyWoundsDebuff(FighterData unit)
        {
            if (unit == null || unit.hasWoundsDebuff) return;

            unit.hasWoundsDebuff = true;
            unit.zone = UnitZone.Production; // 自动移入生产区

            OnWoundsDebuffApplied?.Invoke(unit);
            OnUnitWounded?.Invoke(unit);

            Debug.Log($"[HealthPersistenceSystem] 单位 {unit.id} 获得满目疮痍debuff，移入生产区");
        }

        /// <summary>
        /// 移除满目疮痍debuff
        /// </summary>
        private void RemoveWoundsDebuff(FighterData unit)
        {
            if (unit == null || !unit.hasWoundsDebuff) return;

            unit.hasWoundsDebuff = false;

            OnWoundsDebuffRemoved?.Invoke(unit);
            OnUnitRecovered?.Invoke(unit);

            Debug.Log($"[HealthPersistenceSystem] 单位 {unit.id} 移除满目疮痍debuff");
        }

        /// <summary>
        /// Boss关：复活所有单位
        /// </summary>
        private void ReviveUnit(FighterData unit)
        {
            if (unit == null) return;

            // 恢复满血
            unit.currentHp = unit.staticHp;

            // 移除满目疮痍debuff
            if (unit.hasWoundsDebuff)
            {
                RemoveWoundsDebuff(unit);
            }

            // 如果在生产区，移回待上阵区
            if (unit.zone == UnitZone.Production)
            {
                unit.zone = UnitZone.Pending;
            }
        }

        /// <summary>
        /// 回复单位HP（通过事件或被动技能）
        /// </summary>
        public void HealUnit(FighterData unit, float healAmount)
        {
            if (unit == null) return;

            float oldHp = unit.currentHp;
            unit.currentHp = Mathf.Min(unit.currentHp + healAmount, unit.staticHp);

            // 如果之前是0血，现在回血了，移除debuff
            if (oldHp <= 0 && unit.currentHp > 0 && unit.hasWoundsDebuff)
            {
                RemoveWoundsDebuff(unit);
            }

            Debug.Log($"[HealthPersistenceSystem] 单位 {unit.id} 回复 {healAmount} HP，当前HP: {unit.currentHp}");
        }

        /// <summary>
        /// 回复所有友方单位HP
        /// </summary>
        public void HealAllAllies(float healAmount)
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData == null || playerData.tribes == null)
                return;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    HealUnit(unit, healAmount);
                }
            }
        }

        /// <summary>
        /// 回复所有友方单位百分比HP
        /// </summary>
        public void HealAllAlliesPercent(float percent)
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData == null || playerData.tribes == null)
                return;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    float healAmount = unit.staticHp * percent;
                    HealUnit(unit, healAmount);
                }
            }
        }

        /// <summary>
        /// 检查单位是否有满目疮痍debuff
        /// </summary>
        public bool HasWoundsDebuff(FighterData unit)
        {
            return unit != null && unit.hasWoundsDebuff;
        }

        /// <summary>
        /// 检查单位是否可以上阵
        /// </summary>
        public bool CanDeploy(FighterData unit)
        {
            if (unit == null) return false;

            // 有满目疮痍debuff的单位不能上阵
            if (unit.hasWoundsDebuff) return false;

            // HP为0的单位不能上阵
            if (unit.currentHp <= 0) return false;

            return true;
        }

        /// <summary>
        /// 获取所有受伤单位
        /// </summary>
        public List<FighterData> GetWoundedUnits()
        {
            var result = new List<FighterData>();
            var playerData = _dataManager?.PlayerData;

            if (playerData == null || playerData.tribes == null)
                return result;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    if (unit.currentHp < unit.staticHp)
                    {
                        result.Add(unit);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 获取所有满目疮痍单位
        /// </summary>
        public List<FighterData> GetWoundedDebuffUnits()
        {
            var result = new List<FighterData>();
            var playerData = _dataManager?.PlayerData;

            if (playerData == null || playerData.tribes == null)
                return result;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    if (unit.hasWoundsDebuff)
                    {
                        result.Add(unit);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 获取单位HP百分比
        /// </summary>
        public float GetHpPercent(FighterData unit)
        {
            if (unit == null || unit.staticHp <= 0) return 0;
            return unit.currentHp / unit.staticHp;
        }

        /// <summary>
        /// 保存HP状态
        /// </summary>
        public void SaveHealthState()
        {
            // HP状态已通过FighterData自动保存
            Debug.Log("[HealthPersistenceSystem] HP状态已保存");
        }

        /// <summary>
        /// 加载HP状态
        /// </summary>
        public void LoadHealthState()
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData == null || playerData.tribes == null)
                return;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    // 检查并恢复满目疮痍debuff状态
                    if (unit.currentHp <= 0 && !unit.hasWoundsDebuff)
                    {
                        unit.hasWoundsDebuff = true;
                    }
                }
            }

            Debug.Log("[HealthPersistenceSystem] HP状态已加载");
        }
    }
}
