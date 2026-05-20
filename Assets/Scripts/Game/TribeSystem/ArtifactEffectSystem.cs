using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 奇物效果类型
    /// </summary>
    public enum ArtifactEffectType
    {
        AtkFlat,         // 攻击力固定值
        DefFlat,         // 防御力固定值
        HpFlat,          // 生命值固定值
        AtkPercent,      // 攻击力百分比
        DefPercent,      // 防御力百分比
        HpPercent,       // 生命值百分比
        MoveSpeedPercent, // 移动速度百分比
        AtkSpeedPercent,  // 攻击速度百分比
        AllStatsPercent,  // 全属性百分比
        KillHeal,         // 击杀回血
        DamageReduce,     // 减伤
        LowHpBonus,       // 低血量加攻
        KillShield        // 击杀护盾
    }

    /// <summary>
    /// 奇物效果数据
    /// </summary>
    [Serializable]
    public class ArtifactEffectData
    {
        public ArtifactEffectType effectType;
        public float value;
        public string subType;          // 特效子类型
        public string description;      // 效果描述

        public ArtifactEffectData()
        {
            effectType = ArtifactEffectType.AtkFlat;
            value = 0;
            subType = "";
            description = "";
        }
    }

    /// <summary>
    /// 奇物配置
    /// </summary>
    [Serializable]
    public class ArtifactConfig
    {
        public string artifactId;           // 奇物ID
        public string artifactName;         // 奇物名称
        public string description;          // 描述
        public List<ArtifactEffectData> effects; // 效果列表
        public int tier;                    // 等级（1-3）
        public ArtifactRarity rarity;       // 稀有度

        public ArtifactConfig()
        {
            artifactId = "";
            artifactName = "";
            description = "";
            effects = new List<ArtifactEffectData>();
            tier = 1;
            rarity = ArtifactRarity.Common;
        }
    }

    /// <summary>
    /// 奇物稀有度
    /// </summary>
    public enum ArtifactRarity
    {
        Common,     // 普通
        Rare,       // 稀有
        Epic,       // 史诗
        Legendary   // 传说
    }

    /// <summary>
    /// 奇物实例
    /// </summary>
    [Serializable]
    public class ArtifactInstance
    {
        public ArtifactConfig config;
        public bool isActive;

        public ArtifactInstance()
        {
            config = new ArtifactConfig();
            isActive = false;
        }
    }

    /// <summary>
    /// 特效奇物系统 - 管理20个奇物的效果和触发
    /// </summary>
    public class ArtifactEffectSystem
    {
        private List<ArtifactConfig> _artifactConfigs;
        private List<ArtifactInstance> _ownedArtifacts;

        // 事件
        public event Action<ArtifactInstance> OnArtifactEquipped;
        public event Action<ArtifactInstance> OnArtifactUnequipped;
        public event Action<FighterData, float> OnKillHeal;
        public event Action<FighterData, float> OnDamageReduced;
        public event Action<FighterData, float> OnLowHpBonus;
        public event Action<List<FighterData>, float> OnKillShield;

        public ArtifactEffectSystem()
        {
            _artifactConfigs = new List<ArtifactConfig>();
            _ownedArtifacts = new List<ArtifactInstance>();
            InitializeArtifactConfigs();
        }

        /// <summary>
        /// 初始化奇物配置
        /// </summary>
        private void InitializeArtifactConfigs()
        {
            // 纯属性奇物（8个）
            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_AtkFlat40",
                artifactName = "猫拳套",
                description = "全体攻击力+40",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkFlat, value = 40 }
                },
                rarity = ArtifactRarity.Common
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_DefFlat20",
                artifactName = "猫铠甲",
                description = "全体防御力+20",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.DefFlat, value = 20 }
                },
                rarity = ArtifactRarity.Common
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_HpFlat500",
                artifactName = "猫爬架Ⅱ",
                description = "全体生命值+500",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.HpFlat, value = 500 }
                },
                rarity = ArtifactRarity.Common
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_MoveSpeed40",
                artifactName = "猫风铃",
                description = "全体移动速度+40%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.MoveSpeedPercent, value = 0.4f }
                },
                rarity = ArtifactRarity.Common
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_AtkSpd40",
                artifactName = "猫爪手套",
                description = "全体攻击速度+40%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkSpeedPercent, value = 0.4f }
                },
                rarity = ArtifactRarity.Common
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_AtkPct20",
                artifactName = "猫魂之刃",
                description = "全体攻击力+20%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkPercent, value = 0.2f }
                },
                rarity = ArtifactRarity.Common
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_DefPct15",
                artifactName = "猫灵壁障",
                description = "全体防御力+15%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.DefPercent, value = 0.15f }
                },
                rarity = ArtifactRarity.Common
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_HpPct25",
                artifactName = "猫心护符",
                description = "全体生命值+25%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.HpPercent, value = 0.25f }
                },
                rarity = ArtifactRarity.Common
            });

            // 双属性奇物（8个）
            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_Atk25_Hp250",
                artifactName = "猫猎弓",
                description = "攻击力+25，生命值+250",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkFlat, value = 25 },
                    new ArtifactEffectData { effectType = ArtifactEffectType.HpFlat, value = 250 }
                },
                rarity = ArtifactRarity.Rare
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_Def12_Hp300",
                artifactName = "猫重盾",
                description = "防御力+12，生命值+300",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.DefFlat, value = 12 },
                    new ArtifactEffectData { effectType = ArtifactEffectType.HpFlat, value = 300 }
                },
                rarity = ArtifactRarity.Rare
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_Atk20_AtkSpd20",
                artifactName = "猫狂爪",
                description = "攻击力+20，攻击速度+20%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkFlat, value = 20 },
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkSpeedPercent, value = 0.2f }
                },
                rarity = ArtifactRarity.Rare
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_MoveSpeed20_AtkSpd20",
                artifactName = "猫疾风",
                description = "移动速度+20%，攻击速度+20%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.MoveSpeedPercent, value = 0.2f },
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkSpeedPercent, value = 0.2f }
                },
                rarity = ArtifactRarity.Rare
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_AtkPct10_HpPct15",
                artifactName = "猫嗜血",
                description = "攻击力+10%，生命值+15%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkPercent, value = 0.1f },
                    new ArtifactEffectData { effectType = ArtifactEffectType.HpPercent, value = 0.15f }
                },
                rarity = ArtifactRarity.Rare
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_DefPct8_HpPct15",
                artifactName = "猫铁壁",
                description = "防御力+8%，生命值+15%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.DefPercent, value = 0.08f },
                    new ArtifactEffectData { effectType = ArtifactEffectType.HpPercent, value = 0.15f }
                },
                rarity = ArtifactRarity.Rare
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_Atk15_Def10_Hp150",
                artifactName = "猫战甲",
                description = "攻击力+15，防御力+10，生命值+150",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.AtkFlat, value = 15 },
                    new ArtifactEffectData { effectType = ArtifactEffectType.DefFlat, value = 10 },
                    new ArtifactEffectData { effectType = ArtifactEffectType.HpFlat, value = 150 }
                },
                rarity = ArtifactRarity.Rare
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_AllStats5",
                artifactName = "猫王冠Ⅱ",
                description = "全属性+5%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData { effectType = ArtifactEffectType.AllStatsPercent, value = 0.05f }
                },
                rarity = ArtifactRarity.Rare
            });

            // 特效奇物（4个）
            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_KillHeal15",
                artifactName = "猫九命",
                description = "击杀敌人时，回复最大生命值的15%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData
                    {
                        effectType = ArtifactEffectType.KillHeal,
                        value = 0.15f,
                        subType = "KillHeal"
                    }
                },
                rarity = ArtifactRarity.Epic
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_DmgReduce10",
                artifactName = "猫薄荷",
                description = "受到的所有伤害降低10%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData
                    {
                        effectType = ArtifactEffectType.DamageReduce,
                        value = 0.1f,
                        subType = "DamageReduce"
                    }
                },
                rarity = ArtifactRarity.Epic
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_LowHpDmg30",
                artifactName = "猫狂化",
                description = "生命值低于30%时，攻击力+30%",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData
                    {
                        effectType = ArtifactEffectType.LowHpBonus,
                        value = 0.3f,
                        subType = "LowHpBonus"
                    }
                },
                rarity = ArtifactRarity.Epic
            });

            _artifactConfigs.Add(new ArtifactConfig
            {
                artifactId = "Artifact_ShieldOnKill",
                artifactName = "猫守护",
                description = "击杀敌人时，为全体队友施加一个吸收200伤害的护盾，持续5秒",
                effects = new List<ArtifactEffectData>
                {
                    new ArtifactEffectData
                    {
                        effectType = ArtifactEffectType.KillShield,
                        value = 200f,
                        subType = "KillShield"
                    }
                },
                rarity = ArtifactRarity.Epic
            });
        }

        /// <summary>
        /// 获取所有奇物配置
        /// </summary>
        public List<ArtifactConfig> GetAllArtifactConfigs()
        {
            return _artifactConfigs;
        }

        /// <summary>
        /// 根据ID获取奇物配置
        /// </summary>
        public ArtifactConfig GetArtifactConfig(string artifactId)
        {
            foreach (var config in _artifactConfigs)
            {
                if (config.artifactId == artifactId)
                    return config;
            }
            return null;
        }

        /// <summary>
        /// 装备奇物
        /// </summary>
        public bool EquipArtifact(string artifactId)
        {
            var config = GetArtifactConfig(artifactId);
            if (config == null)
                return false;

            // 检查是否已拥有
            foreach (var owned in _ownedArtifacts)
            {
                if (owned.config.artifactId == artifactId)
                {
                    Debug.LogWarning($"[ArtifactEffectSystem] 已拥有奇物: {artifactId}");
                    return false;
                }
            }

            // 检查最大数量限制（5个）
            if (_ownedArtifacts.Count >= 5)
            {
                Debug.LogWarning("[ArtifactEffectSystem] 奇物数量已达上限");
                return false;
            }

            var instance = new ArtifactInstance
            {
                config = config,
                isActive = true
            };

            _ownedArtifacts.Add(instance);
            OnArtifactEquipped?.Invoke(instance);

            return true;
        }

        /// <summary>
        /// 卸载奇物
        /// </summary>
        public bool UnequipArtifact(string artifactId)
        {
            for (int i = _ownedArtifacts.Count - 1; i >= 0; i--)
            {
                if (_ownedArtifacts[i].config.artifactId == artifactId)
                {
                    var instance = _ownedArtifacts[i];
                    _ownedArtifacts.RemoveAt(i);
                    OnArtifactUnequipped?.Invoke(instance);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用所有奇物效果到单位
        /// </summary>
        public void ApplyAllEffects(FighterData unit)
        {
            if (unit == null) return;

            foreach (var artifact in _ownedArtifacts)
            {
                if (!artifact.isActive) continue;

                foreach (var effect in artifact.config.effects)
                {
                    ApplyEffect(unit, effect);
                }
            }
        }

        /// <summary>
        /// 应用单个效果
        /// </summary>
        private void ApplyEffect(FighterData unit, ArtifactEffectData effect)
        {
            switch (effect.effectType)
            {
                case ArtifactEffectType.AtkFlat:
                    unit.staticAttack += effect.value;
                    break;
                case ArtifactEffectType.DefFlat:
                    unit.staticDefense += effect.value;
                    break;
                case ArtifactEffectType.HpFlat:
                    unit.staticHp += effect.value;
                    unit.currentHp += effect.value;
                    break;
                case ArtifactEffectType.AtkPercent:
                    unit.staticAttack *= (1 + effect.value);
                    break;
                case ArtifactEffectType.DefPercent:
                    unit.staticDefense *= (1 + effect.value);
                    break;
                case ArtifactEffectType.HpPercent:
                    unit.staticHp *= (1 + effect.value);
                    unit.currentHp *= (1 + effect.value);
                    break;
                case ArtifactEffectType.MoveSpeedPercent:
                    unit.staticMoveSpeed *= (1 + effect.value);
                    break;
                case ArtifactEffectType.AtkSpeedPercent:
                    unit.staticAttackSpeed *= (1 + effect.value);
                    break;
                case ArtifactEffectType.AllStatsPercent:
                    unit.staticAttack *= (1 + effect.value);
                    unit.staticDefense *= (1 + effect.value);
                    unit.staticHp *= (1 + effect.value);
                    unit.currentHp *= (1 + effect.value);
                    unit.staticMoveSpeed *= (1 + effect.value);
                    unit.staticAttackSpeed *= (1 + effect.value);
                    break;
            }
        }

        /// <summary>
        /// 触发击杀回血效果
        /// </summary>
        public void TriggerKillHeal(FighterData killer)
        {
            if (killer == null) return;

            foreach (var artifact in _ownedArtifacts)
            {
                if (!artifact.isActive) continue;

                foreach (var effect in artifact.config.effects)
                {
                    if (effect.effectType == ArtifactEffectType.KillHeal)
                    {
                        float healAmount = killer.staticHp * effect.value;
                        OnKillHeal?.Invoke(killer, healAmount);
                    }
                }
            }
        }

        /// <summary>
        /// 触发减伤效果
        /// </summary>
        public float GetDamageReduction(FighterData target, float originalDamage)
        {
            if (target == null) return originalDamage;

            float totalReduction = 0;

            foreach (var artifact in _ownedArtifacts)
            {
                if (!artifact.isActive) continue;

                foreach (var effect in artifact.config.effects)
                {
                    if (effect.effectType == ArtifactEffectType.DamageReduce)
                    {
                        totalReduction += effect.value;
                    }
                }
            }

            // 限制最大减伤
            totalReduction = Mathf.Min(totalReduction, 0.5f);

            float reducedDamage = originalDamage * (1 - totalReduction);
            OnDamageReduced?.Invoke(target, originalDamage - reducedDamage);

            return reducedDamage;
        }

        /// <summary>
        /// 触发低血量加攻效果
        /// </summary>
        public float GetLowHpBonus(FighterData unit)
        {
            if (unit == null) return 0;

            float hpPercent = unit.currentHp / unit.staticHp;

            if (hpPercent > 0.3f) return 0;

            float bonus = 0;
            foreach (var artifact in _ownedArtifacts)
            {
                if (!artifact.isActive) continue;

                foreach (var effect in artifact.config.effects)
                {
                    if (effect.effectType == ArtifactEffectType.LowHpBonus)
                    {
                        bonus += effect.value;
                    }
                }
            }

            OnLowHpBonus?.Invoke(unit, bonus);

            return bonus;
        }

        /// <summary>
        /// 触发击杀护盾效果
        /// </summary>
        public void TriggerKillShield(FighterData killer, List<FighterData> allies)
        {
            if (killer == null || allies == null) return;

            float shieldAmount = 0;

            foreach (var artifact in _ownedArtifacts)
            {
                if (!artifact.isActive) continue;

                foreach (var effect in artifact.config.effects)
                {
                    if (effect.effectType == ArtifactEffectType.KillShield)
                    {
                        shieldAmount += effect.value;
                    }
                }
            }

            if (shieldAmount > 0)
            {
                OnKillShield?.Invoke(allies, shieldAmount);
            }
        }

        /// <summary>
        /// 获取已拥有奇物列表
        /// </summary>
        public List<ArtifactInstance> GetOwnedArtifacts()
        {
            return _ownedArtifacts;
        }

        /// <summary>
        /// 获取随机奇物（用于掉落）
        /// </summary>
        public ArtifactConfig GetRandomArtifact(ArtifactRarity? targetRarity = null)
        {
            var candidates = new List<ArtifactConfig>();

            foreach (var config in _artifactConfigs)
            {
                // 排除已拥有的
                bool owned = false;
                foreach (var ownedArtifact in _ownedArtifacts)
                {
                    if (ownedArtifact.config.artifactId == config.artifactId)
                    {
                        owned = true;
                        break;
                    }
                }

                if (owned) continue;

                if (targetRarity == null || config.rarity == targetRarity.Value)
                {
                    candidates.Add(config);
                }
            }

            if (candidates.Count == 0)
                return null;

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            _ownedArtifacts.Clear();
        }
    }
}
