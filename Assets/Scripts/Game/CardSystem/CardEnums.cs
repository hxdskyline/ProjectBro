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
    /// 四大族群类型
    /// </summary>
    public enum TribeType
    {
        None = 0,       // 无（敌人等非族长单位）
        Tabby = 1,      // 狸花猫族 - 攻击型
        Orange = 2,     // 大橘猫族 - 坦克型
        Cow = 3,        // 奶牛猫族 - 防御型
        Siamese = 4,    // 暹罗猫族 - 敏捷型
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
        MoveSpeed,
        AttackSpeed
    }

    /// <summary>
    /// Buff 类别
    /// </summary>
    public enum BuffCategory
    {
        StatModifier,   // 纯属性修改，visible=false
        Special         // 特殊逻辑buff，visible=true
    }

    /// <summary>
    /// Buff 类型 — 用于按类型遍历/筛选
    /// </summary>
    public enum BuffType
    {
        Innate,         // 天生被动（各族固有能力，不可移除）
        Equipment,      // 装备/饰品效果（本局有效）
        Prayer,         // 祈愿/祭祀效果
        Recruitment,    // 招募获得的加成
        Consumable,     // 消耗品效果（临时）
        Mood,           // 心情修正
        Terrain         // 地形天气修正
    }
}
