using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 地形类型
    /// </summary>
    public enum TerrainType
    {
        Plain = 0,   // 平地
        Brush = 1    // 灌木
    }

    /// <summary>
    /// 天气类型
    /// </summary>
    public enum WeatherType
    {
        Sunny = 0,   // 晴天
        Rainy = 1,   // 雨天
        Night = 2,   // 夜晚
        Windy = 3    // 大风
    }

    /// <summary>
    /// 难度等级
    /// </summary>
    public enum DifficultyLevel
    {
        Normal = 0,      // 普通
        Hard = 1,        // 困难
        Bloodbath = 2    // 血战
    }

    /// <summary>
    /// 敌人类别
    /// </summary>
    public enum EnemyFormationType
    {
        Single = 0,  // 强力单体怪
        Swarm = 1    // 大量小怪
    }

    /// <summary>
    /// 六大族群类型
    /// </summary>
    public enum TribeType
    {
        Maine = 0,      // 缅因猫族 - 均衡型
        Tabby = 1,      // 狸花猫族 - 攻击型
        Orange = 2,     // 大橘猫族 - 坦克型
        Cow = 3,        // 奶牛猫族 - 防御型
        Siamese = 4,    // 暹罗猫族 - 敏捷型
        Ragdoll = 5     // 布偶猫族 - 特殊型
    }

    /// <summary>
    /// 小猫品质等级
    /// </summary>
    public enum CatQuality
    {
        White = 0,      // 菜鸟 - 10%~20%
        Blue = 1,       // 老手 - 20%~30%
        Purple = 2,     // 精英 - 30%~40%
        Gold = 3        // 大师 - 40%~50%
    }

    /// <summary>
    /// 属性类型
    /// </summary>
    public enum StatType
    {
        Attack,
        Defense,
        Hp,
        Speed,
        Command
    }

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
            tribeType = TribeType.Maine;
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
        public int baseSpeed;
        public int command;
        public List<int> skillIds;
        public PermanentBuffs permanentBuffs;
        public TemporaryBuff temporaryBuff;
        public int restTurns;

        public LeaderData()
        {
            leaderId = -1;
            name = "族长";
            baseAttack = 100;
            baseDefense = 80;
            baseHp = 1000;
            baseSpeed = 1000;
            command = 10;
            skillIds = new List<int>();
            permanentBuffs = new PermanentBuffs();
            temporaryBuff = null;
            restTurns = 0;
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
        public float attackMultiplier;
        public float defenseMultiplier;
        public float hpMultiplier;
        public float speedMultiplier;

        public CatData()
        {
            catId = -1;
            quality = CatQuality.White;
            attackMultiplier = 0.35f;
            defenseMultiplier = 0.35f;
            hpMultiplier = 0.35f;
            speedMultiplier = 1.0f;
        }

        /// <summary>
        /// 创建指定品质的小猫
        /// </summary>
        public static CatData CreateWithQuality(CatQuality quality)
        {
            var cat = new CatData
            {
                catId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                quality = quality
            };

            // 根据品质设置属性比例范围
            float minRatio, maxRatio;
            switch (quality)
            {
                case CatQuality.White:
                    minRatio = 0.3f;
                    maxRatio = 0.4f;
                    break;
                case CatQuality.Blue:
                    minRatio = 0.4f;
                    maxRatio = 0.5f;
                    break;
                case CatQuality.Purple:
                    minRatio = 0.5f;
                    maxRatio = 0.6f;
                    break;
                case CatQuality.Gold:
                    minRatio = 0.6f;
                    maxRatio = 0.7f;
                    break;
                default:
                    minRatio = 0.3f;
                    maxRatio = 0.4f;
                    break;
            }

            // 在范围内随机生成
            float ratio = UnityEngine.Random.Range(minRatio, maxRatio);
            cat.attackMultiplier = ratio;
            cat.defenseMultiplier = ratio;
            cat.hpMultiplier = ratio;
            cat.speedMultiplier = 1.0f; // 移动速度全继承族长

            return cat;
        }

        /// <summary>
        /// 创建随机品质的小猫（白40% 蓝30% 紫20% 金10%）
        /// </summary>
        public static CatData CreateWithRandomQuality()
        {
            float roll = UnityEngine.Random.value;
            CatQuality quality;
            if (roll < 0.4f)       quality = CatQuality.White;
            else if (roll < 0.7f)  quality = CatQuality.Blue;
            else if (roll < 0.9f)  quality = CatQuality.Purple;
            else                   quality = CatQuality.Gold;
            return CreateWithQuality(quality);
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
                    // 重新生成属性比例
                    var newCat = CreateWithQuality(quality);
                    attackMultiplier = newCat.attackMultiplier;
                    defenseMultiplier = newCat.defenseMultiplier;
                    hpMultiplier = newCat.hpMultiplier;
                    speedMultiplier = 1.0f; // 移动速度始终全继承
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 永久加成
    /// </summary>
    [Serializable]
    public class PermanentBuffs
    {
        public int attackBonus;
        public float attackPercent;
        public int defenseBonus;
        public float defensePercent;
        public int hpBonus;
        public float hpPercent;
        public int speedBonus;
        public float speedPercent;
        public int commandBonus;
        public float commandPercent;

        public PermanentBuffs()
        {
            attackBonus = 0;
            attackPercent = 0f;
            defenseBonus = 0;
            defensePercent = 0f;
            hpBonus = 0;
            hpPercent = 0f;
            speedBonus = 0;
            speedPercent = 0f;
            commandBonus = 0;
            commandPercent = 0f;
        }
    }

    /// <summary>
    /// 限时加成
    /// </summary>
    [Serializable]
    public class TemporaryBuff
    {
        public float attackPercent;
        public float defensePercent;
        public float hpPercent;
        public float speedPercent;
        public int duration; // 剩余回合数

        public TemporaryBuff()
        {
            attackPercent = 0f;
            defensePercent = 0f;
            hpPercent = 0f;
            speedPercent = 0f;
            duration = 0;
        }

        public bool IsActive()
        {
            return duration > 0;
        }

        public void DecreaseDuration()
        {
            if (duration > 0)
            {
                duration--;
            }
        }
    }

    /// <summary>
    /// 招募选项类型
    /// </summary>
    public enum RecruitmentOptionType
    {
        NewTribe,           // 新增族群
        AddCats,            // 增加小猫
        QualityEvolution,   // 品质进化
        LeaderBoost         // 族长强化
    }

    /// <summary>
    /// 招募选项
    /// </summary>
    [Serializable]
    public class RecruitmentOption
    {
        public RecruitmentOptionType optionType;
        public int cost;
        public TribeType? targetTribeType; // 目标族群类型（新增族群时）
        public int targetTribeId;          // 目标族群ID（已有族群操作时）
        public StatType targetStatType;    // 目标属性类型（族长强化时）
        public float boostValue;           // 属性提升的百分比值（例如0.2代表20%）
        public string description;

        public RecruitmentOption()
        {
            optionType = RecruitmentOptionType.NewTribe;
            cost = 0;
            targetTribeType = null;
            targetTribeId = -1;
            boostValue = 0.2f;
            description = "";
        }
    }

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
        Accessory                   // 饰品
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
        public string displayName;         // UI 显示文本（在 DrawBlessings 时生成）

        public RitualRewardItem()
        {
            rewardType = RitualRewardType.CatFood;
            statType = null;
            amount = 0;
            catCount = 0;
            catTribeType = TribeType.Maine;
            catQuality = null;
            consumableId = -1;
            accessoryId = -1;
            displayName = "";
        }
    }

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
        public ConsumableEffectType? consumableEffectType; // 消耗品效果类型
        public int basePrice;
        public string name;
        public string description;
        public int stock;               // 库存，买完后从商店移除

        public ShopItem()
        {
            itemId = -1;
            itemType = ShopItemType.Consumable;
            catTribeType = null;
            catQuality = null;
            basePrice = 0;
            name = "";
            description = "";
            stock = 1;
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

    /// <summary>
    /// 族群配置
    /// </summary>
    [Serializable]
    public class TribeConfig
    {
        public TribeType tribeType;
        public string tribeName;
        public int initialCatCount;
        public int deployCostPerCat;          // 每只小猫的出战消耗（猫粮）
        public LeaderBaseStats leaderBaseStats;
        public LeaderBaseStats catBaseStats;  // 小猫的基础属性（command属性不使用）

        public TribeConfig()
        {
            tribeType = TribeType.Maine;
            tribeName = "";
            initialCatCount = 3;
            deployCostPerCat = 10;
            leaderBaseStats = new LeaderBaseStats();
            catBaseStats = new LeaderBaseStats();
        }
    }

    /// <summary>
    /// 族长基础属性配置
    /// </summary>
    [Serializable]
    public class LeaderBaseStats
    {
        public int attack;
        public int defense;
        public int hp;
        public int speed;
        public int command;

        public LeaderBaseStats()
        {
            attack = 100;
            defense = 80;
            hp = 1000;
            speed = 1000;
            command = 10;
        }
    }

    /// <summary>
    /// 品质配置
    /// </summary>
    [Serializable]
    public class QualityConfig
    {
        public CatQuality quality;
        public string qualityName;
        public float minRatio;
        public float maxRatio;
        public float baseProbability;

        public QualityConfig()
        {
            quality = CatQuality.White;
            qualityName = "菜鸟";
            minRatio = 0.1f;
            maxRatio = 0.2f;
            baseProbability = 0.4f;
        }
    }

    /// <summary>
    /// 计算后的族长属性
    /// </summary>
    public struct LeaderStats
    {
        public int attack;
        public int defense;
        public int hp;
        public int speed;
        public int command;

        public LeaderStats(int atk, int def, int hp, int spd, int cmd)
        {
            attack = atk;
            defense = def;
            this.hp = hp;
            speed = spd;
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
        public int speed;

        public CatStats(int atk, int def, int hp, int spd)
        {
            attack = atk;
            defense = def;
            this.hp = hp;
            speed = spd;
        }
    }

    /// <summary>
    /// 新部族事件选项类型
    /// </summary>
    public enum NewTribeEventOptionType
    {
        NewRandomTribe,   // 获得随机新部族
        CatFoodReward     // 获得1000猫粮
    }

    /// <summary>
    /// 新部族事件选项
    /// </summary>
    [Serializable]
    public class NewTribeEventOption
    {
        public NewTribeEventOptionType optionType;
        public string description;

        public NewTribeEventOption()
        {
            optionType = NewTribeEventOptionType.NewRandomTribe;
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

    /// <summary>
    /// 种族在特定地形/天气下的属性修正
    /// </summary>
    public struct TerrainWeatherBuff
    {
        public float attackPercent;
        public float defensePercent;
        public float hpPercent;
        public float speedPercent;

        public bool IsNeutral =>
            Mathf.Approximately(attackPercent, 0f) &&
            Mathf.Approximately(defensePercent, 0f) &&
            Mathf.Approximately(hpPercent, 0f) &&
            Mathf.Approximately(speedPercent, 0f);

        public string GetDescription()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!Mathf.Approximately(attackPercent, 0f))
                parts.Add($"攻{(attackPercent > 0 ? "+" : "")}{Mathf.RoundToInt(attackPercent * 100)}%");
            if (!Mathf.Approximately(defensePercent, 0f))
                parts.Add($"防{(defensePercent > 0 ? "+" : "")}{Mathf.RoundToInt(defensePercent * 100)}%");
            if (!Mathf.Approximately(hpPercent, 0f))
                parts.Add($"血{(hpPercent > 0 ? "+" : "")}{Mathf.RoundToInt(hpPercent * 100)}%");
            if (!Mathf.Approximately(speedPercent, 0f))
                parts.Add($"速{(speedPercent > 0 ? "+" : "")}{Mathf.RoundToInt(speedPercent * 100)}%");
            return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : "无修正";
        }
    }
}
