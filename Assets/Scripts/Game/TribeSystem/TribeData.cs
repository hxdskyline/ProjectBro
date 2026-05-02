using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 单位等级（Tier1=一级兵，Tier2=二级兵，Tier3=三级兵）
    /// </summary>
    public enum UnitTier
    {
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3
    }

    // ═══════════════════════════════════════════════════════════
    //  玩家状态 — 族群实例、族长、小猫
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 族群记录
    /// </summary>
    [Serializable]
    public class TribeRecord
    {
        public int tribeId;
        public TribeType tribeType;
        public LeaderData leader;
        public List<CatData> cats;
        public string moodId;
        public bool isActive;

        public TribeRecord()
        {
            tribeId = -1;
            tribeType = TribeType.Tabby;
            leader = new LeaderData();
            cats = new List<CatData>();
            moodId = null;
            isActive = true;
        }

        /// <summary>
        /// 获取小猫总数
        /// </summary>
        public int GetCatCount()
        {
            return cats != null ? cats.Count : 0;
        }

        /// <summary>
        /// 检查族长是否在休息
        /// </summary>
        public bool IsLeaderResting()
        {
            return leader != null && leader.restTurns > 0;
        }
    }

    /// <summary>
    /// 族长数据
    /// </summary>
    [Serializable]
    public class LeaderData
    {
        public int leaderId;
        public string name;
        public int baseAttack;
        public int baseDefense;
        public int baseHp;
        public float baseMoveSpeed;
        public int command;
        public List<int> skillIds;
        public PermanentBuffs permanentBuffs;
        public TemporaryBuff temporaryBuff;
        public int restTurns;

        // ── 统一 buff 运行时列表（不序列化，加载存档时从 buffEntries 转换） ──
        [NonSerialized] private List<UnifiedBuff> _activeBuffs;
        public List<UnifiedBuff> ActiveBuffs
        {
            get
            {
                if (_activeBuffs == null) _activeBuffs = new List<UnifiedBuff>();
                return _activeBuffs;
            }
        }

        public LeaderData()
        {
            leaderId = -1;
            name = "族长";
            baseAttack = 100;
            baseDefense = 80;
            baseHp = 1000;
            baseMoveSpeed = 1.0f;
            command = 10;
            skillIds = new List<int>();
            permanentBuffs = new PermanentBuffs();
            temporaryBuff = null;
            restTurns = 0;
        }

        /// <summary>
        /// 添加一个统一 buff（自动处理叠加/刷新）
        /// </summary>
        public bool AddUnifiedBuff(UnifiedBuff buff)
        {
            if (buff == null) return false;
            // 尝试叠加现有同类 buff
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].buffId == buff.buffId)
                {
                    ActiveBuffs[i].TryStackOrRefresh(buff);
                    return true;
                }
            }
            ActiveBuffs.Add(buff.Clone());
            return true;
        }

        /// <summary>
        /// 移除指定 buffId 的 buff
        /// </summary>
        public bool RemoveBuff(string buffId)
        {
            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                if (ActiveBuffs[i].buffId == buffId)
                {
                    ActiveBuffs.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 移除指定来源的所有 buff
        /// </summary>
        public int RemoveBuffBySource(string sourceId)
        {
            int removed = 0;
            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                if (ActiveBuffs[i].sourceId == sourceId)
                {
                    ActiveBuffs.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// 获取指定持久性的所有 buff
        /// </summary>
        public List<UnifiedBuff> GetBuffsByPersistence(BuffPersistence persistence)
        {
            var result = new List<UnifiedBuff>();
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].persistence == persistence)
                    result.Add(ActiveBuffs[i]);
            }
            return result;
        }

        /// <summary>
        /// 清除所有战斗内 buff（战斗结束时调用）
        /// </summary>
        public int ClearBattleBuffs()
        {
            int removed = 0;
            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                if (ActiveBuffs[i].persistence == BuffPersistence.BattleOnly)
                {
                    ActiveBuffs.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }
    }

    /// <summary>
    /// 小猫数据
    /// </summary>
    [Serializable]
    public class CatData
    {
        public long catId;
        public CatQuality quality;
        public TribeType tribeType;
        public UnitTier tier;
        public float attackMultiplier;
        public float defenseMultiplier;
        public float hpMultiplier;
        public float speedMultiplier;
        public int staticAttack;
        public int staticDefense;
        public int staticHp;
        public float staticMoveSpeed;
        public float staticAttackSpeed;

        // ── 统一 buff 运行时列表 ──
        [NonSerialized] private List<UnifiedBuff> _activeBuffs;
        public List<UnifiedBuff> ActiveBuffs
        {
            get
            {
                if (_activeBuffs == null) _activeBuffs = new List<UnifiedBuff>();
                return _activeBuffs;
            }
        }

        public CatData()
        {
            catId = -1;
            quality = CatQuality.White;
            tier = UnitTier.Tier1;
            attackMultiplier = 1.0f;
            defenseMultiplier = 1.0f;
            hpMultiplier = 1.0f;
            speedMultiplier = 1.0f;
            staticAttack = 0;
            staticDefense = 0;
            staticHp = 0;
            staticMoveSpeed = 1.0f;
            staticAttackSpeed = 0.5f;
        }

        /// <summary>
        /// 添加一个统一 buff（自动处理叠加/刷新）
        /// </summary>
        public bool AddUnifiedBuff(UnifiedBuff buff)
        {
            if (buff == null) return false;
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].buffId == buff.buffId)
                {
                    ActiveBuffs[i].TryStackOrRefresh(buff);
                    return true;
                }
            }
            ActiveBuffs.Add(buff.Clone());
            return true;
        }

        /// <summary>
        /// 移除指定 buffId 的 buff
        /// </summary>
        public bool RemoveBuff(string buffId)
        {
            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                if (ActiveBuffs[i].buffId == buffId)
                {
                    ActiveBuffs.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 移除指定来源的所有 buff
        /// </summary>
        public int RemoveBuffBySource(string sourceId)
        {
            int removed = 0;
            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                if (ActiveBuffs[i].sourceId == sourceId)
                {
                    ActiveBuffs.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// 获取指定持久性的所有 buff
        /// </summary>
        public List<UnifiedBuff> GetBuffsByPersistence(BuffPersistence persistence)
        {
            var result = new List<UnifiedBuff>();
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].persistence == persistence)
                    result.Add(ActiveBuffs[i]);
            }
            return result;
        }

        /// <summary>
        /// 清除所有战斗内 buff（战斗结束时调用）
        /// </summary>
        public int ClearBattleBuffs()
        {
            int removed = 0;
            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                if (ActiveBuffs[i].persistence == BuffPersistence.BattleOnly)
                {
                    ActiveBuffs.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// 创建指定品质的小猫（从 fighter_config.json 读取静态属性）
        /// </summary>
        public static CatData CreateWithQuality(CatQuality quality, TribeType tribeType, UnitTier? tier = null)
        {
            var cat = new CatData
            {
                catId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                quality = quality,
                tribeType = tribeType,
                tier = tier ?? UnitTier.Tier1
            };

            // 从 fighter_config.json 读取小猫基础属性
            var tribeConfig = TribeConfigLoader.Instance?.GetTribeConfig(tribeType);
            var unitType = tribeConfig?.GetUnitType(cat.tier);
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(unitType?.fighterId ?? 0);
            if (fighterConfig != null)
            {
                cat.staticAttack = fighterConfig.attack;
                cat.staticDefense = fighterConfig.defense;
                cat.staticHp = fighterConfig.hp;
                cat.staticMoveSpeed = fighterConfig.moveSpeed;
                cat.staticAttackSpeed = fighterConfig.attackSpeed;
            }

            return cat;
        }

        /// <summary>
        /// 创建随机品质的小猫（白40% 蓝30% 紫20% 金10%）
        /// </summary>
        public static CatData CreateWithRandomQuality(TribeType tribeType)
        {
            float roll = UnityEngine.Random.value;
            CatQuality quality;
            if (roll < 0.4f)       quality = CatQuality.White;
            else if (roll < 0.7f)  quality = CatQuality.Blue;
            else if (roll < 0.9f)  quality = CatQuality.Purple;
            else                   quality = CatQuality.Gold;
            return CreateWithQuality(quality, tribeType);
        }

        /// <summary>
        /// 尝试进化到下一品质（50%概率）
        /// </summary>
        public bool TryEvolve()
        {
            if (UnityEngine.Random.value < 0.5f)
            {
                // 进化成功
                if (quality < CatQuality.Gold)
                {
                    quality++;
                    // 从配置表读取新品质的静态属性
                    var stats = TribeConfigLoader.Instance?.GetCatStaticStats(tribeType, quality);
                    if (stats != null)
                    {
                        staticAttack = stats.attack;
                        staticDefense = stats.defense;
                        staticHp = stats.hp;
                        staticMoveSpeed = stats.moveSpeed;
                    }
                    return true;
                }
            }
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  卡牌运行时 — CardEntry、BuffList、Buff
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 卡牌条目 — 手牌中一张可被 buff/nerf 的卡的运行时实例
    /// 拥有自己的 BuffList，所有属性修改通过 buff 叠加实现
    /// </summary>
    [Serializable]
    public class CardEntry
    {
        /// <summary>运行时全局唯一 ID（时间戳生成）</summary>
        public long instanceId;

        /// <summary>引用 Card.dataId（指向静态模板）</summary>
        public string dataId;

        /// <summary>拥有的 buff 列表</summary>
        public BuffList buffList;

        public CardEntry()
        {
            instanceId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            dataId = "";
            buffList = new BuffList();
        }

        /// <summary>
        /// 计算最终属性：基础值 + 所有 buff 的 effects 叠加
        /// 基础值由外部传入（来自 Card 或 LeaderData/CatData）
        /// </summary>
        public int GetFinalStat(StatType statType, int baseValue)
        {
            float percentSum = 0f;
            int flatSum = 0;

            foreach (var buff in buffList.buffs)
            {
                foreach (var eff in buff.effects)
                {
                    if (eff.type == GameEffect.AllPercent)
                    {
                        percentSum += eff.value;
                    }
                    else
                    {
                        bool matches = (statType == StatType.Attack && (eff.type == GameEffect.AttackPercent || eff.type == GameEffect.AttackFlat))
                                     || (statType == StatType.Defense && (eff.type == GameEffect.DefensePercent || eff.type == GameEffect.DefenseFlat))
                                     || (statType == StatType.Hp && (eff.type == GameEffect.HpPercent || eff.type == GameEffect.HpFlat))
                                     || (statType == StatType.MoveSpeed && (eff.type == GameEffect.SpeedPercent || eff.type == GameEffect.SpeedFlat));

                        if (!matches) continue;

                        if (eff.type == GameEffect.AttackPercent || eff.type == GameEffect.DefensePercent
                            || eff.type == GameEffect.HpPercent || eff.type == GameEffect.SpeedPercent)
                            percentSum += eff.value;
                        else
                            flatSum += Mathf.RoundToInt(eff.value);
                    }
                }
            }

            return Mathf.Max(1, Mathf.RoundToInt(baseValue * (1f + percentSum) + flatSum));
        }

        /// <summary>添加 buff</summary>
        public void AddBuff(Buff buff)
        {
            buff.target = this;
            buffList.Add(buff);
        }

        /// <summary>移除指定 buff</summary>
        public void RemoveBuff(long buffId)
        {
            buffList.Remove(buffId);
        }

        /// <summary>扣所有临时 buff 回合</summary>
        public void TickBuffs()
        {
            buffList.TickAll();
        }
    }

    /// <summary>
    /// Buff 列表容器 — CardEntry 拥有的 buff 集合
    /// </summary>
    [Serializable]
    public class BuffList
    {
        public List<Buff> buffs;

        public BuffList()
        {
            buffs = new List<Buff>();
        }

        /// <summary>添加 buff</summary>
        public void Add(Buff buff)
        {
            buffs.Add(buff);
        }

        /// <summary>按 ID 移除</summary>
        public bool Remove(long buffId)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].buffId == buffId)
                {
                    buffs.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>按类型移除</summary>
        public void RemoveByType(BuffType type)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].buffType == type)
                    buffs.RemoveAt(i);
            }
        }

        /// <summary>按类型筛选</summary>
        public List<Buff> GetByType(BuffType type)
        {
            var result = new List<Buff>();
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].buffType == type)
                    result.Add(buffs[i]);
            }
            return result;
        }

        /// <summary>扣所有临时 buff 回合，移除过期的</summary>
        public void TickAll()
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (!buffs[i].isPermanent)
                {
                    buffs[i].remainingTurns--;
                    if (buffs[i].remainingTurns <= 0)
                        buffs.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Buff — 一个 buff 实例，可包含多个效果
    /// </summary>
    [Serializable]
    public class Buff
    {
        /// <summary>全局唯一 buff ID</summary>
        public long buffId;

        /// <summary>buff 类型（用于按类型筛选）</summary>
        public BuffType buffType;

        /// <summary>引用目标卡牌</summary>
        [NonSerialized] public CardEntry target;

        /// <summary>来源描述（如 "祈祷：战舞"）</summary>
        public string source;

        /// <summary>是否显示在 buff 栏</summary>
        public bool isVisible;

        /// <summary>true=永久, false=临时</summary>
        public bool isPermanent;

        /// <summary>剩余回合（-1=永久）</summary>
        public int remainingTurns;

        /// <summary>效果列表（一个 buff 可同时加攻击+生命）</summary>
        public List<GameEffectEntry> effects;

        public Buff()
        {
            buffId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            buffType = BuffType.Recruitment;
            target = null;
            source = "";
            isVisible = false;
            isPermanent = true;
            remainingTurns = -1;
            effects = new List<GameEffectEntry>();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  计算结果快照（只读）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 计算后的族长属性
    /// </summary>
    public struct LeaderStats
    {
        public int attack;
        public int defense;
        public int hp;
        public float moveSpeed;
        public float attackSpeed;
        public int command;

        public LeaderStats(int atk, int def, int hp, float moveSpd, float atkSpd, int cmd)
        {
            attack = atk;
            defense = def;
            this.hp = hp;
            moveSpeed = moveSpd;
            attackSpeed = atkSpd;
            command = cmd;
        }
    }

    /// <summary>
    /// 计算后的小猫属性
    /// </summary>
    public struct CatStats
    {
        public int attack;
        public int defense;
        public int hp;
        public float moveSpeed;
        public float attackSpeed;

        public CatStats(int atk, int def, int hp, float moveSpd, float atkSpd)
        {
            attack = atk;
            defense = def;
            this.hp = hp;
            moveSpeed = moveSpd;
            attackSpeed = atkSpd;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  招募相关
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 招募选项
    /// </summary>
    [Serializable]
    public class RecruitmentOption
    {
        public ChoiceCategory optionType;
        public int cost;
        public TribeType? targetTribeType; // 目标族群类型（新增族群时）
        public int targetTribeId;          // 目标族群ID（已有族群操作时）
        public StatType targetStatType;    // 目标属性类型（族长强化时）
        public float boostValue;           // 属性提升的百分比值（例如0.2代表20%）
        public int bonusAttack;            // 固定攻击加成
        public int bonusHp;                // 固定血量加成
        public string description;
        public UnitTier? targetTier;        // 目标单位等级（招募特定等级兵时）

        [System.NonSerialized]
        public GameChoice gameChoice;      // 关联的 GameChoice（从 choice_config 生成时附带）

        public RecruitmentOption()
        {
            optionType = ChoiceCategory.Reinforcement;
            cost = 0;
            targetTribeType = null;
            targetTribeId = -1;
            boostValue = 0.2f;
            bonusAttack = 0;
            bonusHp = 0;
            description = "";
            targetTier = null;
            gameChoice = null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  祈祀相关
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 祭祀奖励类型
    /// </summary>
    public enum RitualRewardType
    {
        LeaderStatBoostTemporary,   // 族长属性临时提升
        LeaderStatBoostPermanent,   // 族长属性永久提升
        LeaderStatBoostPercent,     // 族长属性百分比提升
        Cats,                       // 小猫
        Consumable,                 // 一次性道具
        CatFood,                    // 猫粮
        Accessory,                  // 饰品
        LeaderSkill                 // 族长技能
    }

    /// <summary>
    /// 祈愿效果类型（需求优化版）
    /// </summary>
    public enum PrayerEffectType
    {
        Luck,               // 气运：影响地形/天气出现概率
        WarDance,           // 战舞：提升小猫品质
        SpiritCommunion     // 通灵：改变心情
    }

    /// <summary>
    /// 祈愿品质等级（蓝/紫/金/橙）
    /// </summary>
    public enum PrayerGrade
    {
        Blue,   // 蓝色 - 最差
        Purple, // 紫色
        Gold,   // 金色
        Orange  // 橙色 - 最好
    }

    /// <summary>
    /// 祭祀奖励
    /// </summary>
    [Serializable]
    public class RitualReward
    {
        public List<RitualRewardItem> rewards;

        public RitualReward()
        {
            rewards = new List<RitualRewardItem>();
        }
    }

    /// <summary>
    /// 祭祀奖励项
    /// </summary>
    [Serializable]
    public class RitualRewardItem
    {
        public RitualRewardType rewardType;
        public StatType? statType;         // 属性类型
        public int amount;                 // 数值
        public int catCount;               // 小猫数量
        public TribeType catTribeType;     // 小猫族群
        public CatQuality? catQuality;     // 小猫品质
        public int consumableId;           // 道具ID
        public int accessoryId;            // 饰品ID
        public int leaderSkillId;          // 族长技能ID
        public string displayName;         // UI 显示文本（在 DrawBlessings 时生成）

        public RitualRewardItem()
        {
            rewardType = RitualRewardType.CatFood;
            statType = null;
            amount = 0;
            catCount = 0;
            catTribeType = TribeType.Tabby;
            catQuality = null;
            consumableId = -1;
            accessoryId = -1;
            leaderSkillId = -1;
            displayName = "";
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  商店相关
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 商店物品类型
    /// </summary>
    public enum ShopItemType
    {
        Artifact,       // 奇物
        Consumable,     // 一次性道具
        Cat             // 小猫
    }

    /// <summary>
    /// 奇物效果类型
    /// </summary>
    public enum ArtifactEffectType
    {
        LeaderHpFlat,    // 族长生命值+500
        CatAttackFlat    // 小猫攻击力+20
    }

    /// <summary>
    /// 消耗品效果类型
    /// </summary>
    public enum ConsumableEffectType
    {
        Bomb,         // 炸弹：对所有敌人造成200点真实伤害
        FreezeTrap,   // 冰冻陷阱：所有敌人停止攻击3秒
        HealPotion,   // 回复药水：回复所有己方单位50%最大生命值
        AttackBuff,   // 攻击强化：所有己方单位攻击力+30%，持续15秒
        DefenseBuff   // 防御强化：所有己方单位防御力+30%，持续15秒
    }

    /// <summary>
    /// 消耗品数据
    /// </summary>
    [Serializable]
    public class ConsumableItem
    {
        public int id;
        public string name;
        public ConsumableEffectType effectType;
        public int basePrice;

        public ConsumableItem()
        {
            id = 0;
            name = "";
            effectType = ConsumableEffectType.Bomb;
            basePrice = 0;
        }
    }

    /// <summary>
    /// 商店物品
    /// </summary>
    [Serializable]
    public class ShopItem
    {
        public int itemId;
        public ShopItemType itemType;
        public TribeType? catTribeType; // 猫的族群类型
        public CatQuality? catQuality;  // 猫的品质
        public UnitTier? catTier;       // 猫的等级（T1/T2/T3）
        public ConsumableEffectType? consumableEffectType; // 消耗品效果类型
        public ArtifactEffectType? artifactEffectType; // 奇物效果类型
        public int basePrice;
        public string name;
        public string description;
        public int stock;               // 库存，买完后从商店移除
        public string iconAddress;      // 图标资源地址（Addressable）

        public ShopItem()
        {
            itemId = -1;
            itemType = ShopItemType.Consumable;
            catTribeType = null;
            catQuality = null;
            catTier = null;
            basePrice = 0;
            name = "";
            description = "";
            stock = 1;
            iconAddress = "";
        }

        /// <summary>
        /// 获取实际价格（有随机浮动）
        /// </summary>
        public int GetActualPrice()
        {
            if (itemType == ShopItemType.Cat)
            {
                // 猫咪价格在基础价格上随机*50%~150%
                float multiplier = UnityEngine.Random.Range(0.5f, 1.5f);
                return Mathf.RoundToInt(basePrice * multiplier);
            }
            return basePrice;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  事件相关
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 新部族事件选项类型
    /// </summary>
    public enum NewTribeEventOptionType
    {
        NewTribe,         // 选择一个新部族
    }

    /// <summary>
    /// 新部族事件选项
    /// </summary>
    [Serializable]
    public class NewTribeEventOption
    {
        public NewTribeEventOptionType optionType;
        public TribeType tribeType;
        public string description;

        public NewTribeEventOption()
        {
            optionType = NewTribeEventOptionType.NewTribe;
            tribeType = TribeType.None;
            description = "";
        }
    }

    /// <summary>
    /// 敌人情况选项卡（地形+天气+敌人类别组合）
    /// </summary>
    [Serializable]
    public class BattleScenarioOption
    {
        public TerrainType terrain;
        public WeatherType weather;
        public EnemyFormationType formationType;

        public BattleScenarioOption()
        {
            terrain = TerrainType.Plain;
            weather = WeatherType.Sunny;
            formationType = EnemyFormationType.Single;
        }

        public string GetDisplayName()
        {
            return $"{GetTerrainName(terrain)} / {GetWeatherName(weather)}";
        }

        public static string GetTerrainName(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Plain: return "平地";
                case TerrainType.Brush: return "灌木";
                default: return t.ToString();
            }
        }

        public static string GetWeatherName(WeatherType w)
        {
            switch (w)
            {
                case WeatherType.Sunny: return "晴天";
                case WeatherType.Rainy: return "雨天";
                case WeatherType.Night: return "夜晚";
                case WeatherType.Windy: return "大风";
                default: return w.ToString();
            }
        }

        public static string GetFormationName(EnemyFormationType f)
        {
            switch (f)
            {
                case EnemyFormationType.Single: return "强敌";
                case EnemyFormationType.Swarm: return "群敌";
                default: return f.ToString();
            }
        }
    }
}
