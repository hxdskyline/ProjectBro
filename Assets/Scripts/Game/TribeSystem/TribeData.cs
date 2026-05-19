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

    /// <summary>
    /// 拥有 buff 列表的单位接口
    /// </summary>
    public interface IHasBuffs
    {
        bool AddUnifiedBuff(UnifiedBuff buff);
    }

    // ═══════════════════════════════════════════════════════════
    //  玩家状态 — 族群实例、兵种
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 族群记录
    /// </summary>
    [Serializable]
    public class TribeRecord
    {
        public int tribeId;
        public int fighterId;          // 关联的兵种ID（该族群的代表兵种）
        public TribeType tribeType;
        public List<FighterData> units;
        public string moodId;
        public bool isActive;

        public TribeRecord()
        {
            tribeId = -1;
            fighterId = 0;
            tribeType = TribeType.Tabby;
            units = new List<FighterData>();
            moodId = null;
            isActive = true;
        }

        /// <summary>
        /// 获取单位总数
        /// </summary>
        public int GetUnitCount()
        {
            return units != null ? units.Count : 0;
        }

        /// <summary>
        /// 获取单位总数（兼容旧代码）
        /// </summary>
        public int GetCatCount()
        {
            return GetUnitCount();
        }
    }

    /// <summary>
    /// 兵种数据 — 所有战斗单位的统一数据结构
    /// </summary>
    [Serializable]
    public class FighterData : IHasBuffs
    {
        public long id;                    // 实例唯一 ID
        public int fighterId;              // 关联 fighter_config 的 ID
        public CatQuality quality;         // 品质
        public UnitTier tier;              // 等级

        // 静态属性（从 fighter_config 加载）
        public float staticAttack;
        public float staticDefense;
        public float staticHp;
        public float staticMoveSpeed;
        public float staticAttackSpeed;

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

        // ── 首领技能 ID 列表（不序列化，运行时维护） ──
        [NonSerialized] private List<int> _skillIds;
        public List<int> skillIds
        {
            get
            {
                if (_skillIds == null) _skillIds = new List<int>();
                return _skillIds;
            }
        }

        public FighterData()
        {
            id = -1;
            fighterId = 0;
            quality = CatQuality.White;
            tier = UnitTier.Tier1;
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
        /// 从 fighter_config 创建兵种实例
        /// </summary>
        public static FighterData CreateWithFighterId(int fighterId, CatQuality quality = CatQuality.White, UnitTier? tier = null)
        {
            var unit = new FighterData
            {
                id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + UnityEngine.Random.Range(0, 1000),
                fighterId = fighterId,
                quality = quality,
                tier = tier ?? UnitTier.Tier1
            };

            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
            if (fighterConfig != null)
            {
                unit.staticAttack = fighterConfig.attack;
                unit.staticDefense = fighterConfig.defense;
                unit.staticHp = fighterConfig.hp;
                unit.staticMoveSpeed = fighterConfig.moveSpeed;
                unit.staticAttackSpeed = fighterConfig.attackSpeed;
            }

            return unit;
        }

        /// <summary>
        /// 创建指定品质的兵种（从 fighter_config.json 读取静态属性）
        /// </summary>
        public static FighterData CreateWithQuality(CatQuality quality, TribeType tribeType, UnitTier? tier = null)
        {
            var unit = new FighterData
            {
                id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                quality = quality,
                tier = tier ?? UnitTier.Tier1
            };

            var tribeConfig = TribeConfigLoader.Instance?.GetTribeConfig(tribeType);
            var unitType = tribeConfig?.GetUnitType(unit.tier);
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(unitType?.fighterId ?? 0);
            if (fighterConfig != null)
            {
                unit.fighterId = fighterConfig.fighterId;
                unit.staticAttack = fighterConfig.attack;
                unit.staticDefense = fighterConfig.defense;
                unit.staticHp = fighterConfig.hp;
                unit.staticMoveSpeed = fighterConfig.moveSpeed;
                unit.staticAttackSpeed = fighterConfig.attackSpeed;
            }

            return unit;
        }

        /// <summary>
        /// 创建随机品质的兵种（白40% 蓝30% 紫20% 金10%）
        /// </summary>
        public static FighterData CreateWithRandomQuality(TribeType tribeType)
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
        public bool TryEvolve(TribeType tribeType)
        {
            if (UnityEngine.Random.value < 0.5f)
            {
                if (quality < CatQuality.Gold)
                {
                    quality++;
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
    //  计算结果快照（只读）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 计算后的兵种属性
    /// </summary>
    public struct FighterStats
    {
        public int attack;
        public int defense;
        public int hp;
        public float moveSpeed;
        public float attackSpeed;

        public FighterStats(int atk, int def, int hp, float moveSpd, float atkSpd)
        {
            attack = atk;
            defense = def;
            this.hp = hp;
            moveSpeed = moveSpd;
            attackSpeed = atkSpd;
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
        /// 基础值由外部传入（来自 Card 或 FighterData）
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
        public StatType targetStatType;    // 目标属性类型（兵种强化时）
        public float boostValue;           // 属性提升的百分比值（例如0.2代表20%）
        public int bonusAttack;            // 固定攻击加成
        public int bonusHp;                // 固定血量加成
        public string description;
        public UnitTier? targetTier;        // 目标单位等级（招募特定等级兵时）

        [System.NonSerialized]
        public GameChoice gameChoice;      // 关联的 GameChoice（从 choice_config 生成时附带）

        [System.NonSerialized]
        public AffixData affixData;        // 词缀数据（撸铁系统使用）

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
            affixData = null;
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
        LeaderStatBoostTemporary,   // 兵种属性临时提升
        LeaderStatBoostPermanent,   // 兵种属性永久提升
        LeaderStatBoostPercent,     // 兵种属性百分比提升
        Consumable,                 // 一次性道具
        CatFood,                    // 猫粮
        LeaderSkill                 // 固有技能
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
        public int leaderSkillId;          // 固有技能ID
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
        public string artifactConfigId; // 奇物配置ID
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
            artifactConfigId = "";
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
        AddCats,          // 增加已有族群猫咪数量
    }

    /// <summary>
    /// 新部族事件选项
    /// </summary>
    [Serializable]
    public class NewTribeEventOption
    {
        public NewTribeEventOptionType optionType;
        public TribeType tribeType;
        public int tribeId;
        public int fighterId;
        public int catCount;
        public string description;

        public NewTribeEventOption()
        {
            optionType = NewTribeEventOptionType.NewTribe;
            tribeType = TribeType.None;
            tribeId = 0;
            fighterId = 0;
            catCount = 0;
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
