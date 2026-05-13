using System.Collections.Generic;
using UnityEngine;
using System.IO;
using LitJson;

namespace TribeSystem
{
    /// <summary>
    /// 族群配置加载器 - 负责加载和管理族群相关的配置数据
    /// </summary>
    public class TribeConfigLoader : MonoBehaviour
    {
        private static TribeConfigLoader _instance;
        public static TribeConfigLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("TribeConfigLoader");
                    _instance = go.AddComponent<TribeConfigLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // 缓存配置数据
        private List<TribeConfig> _tribeConfigs;
        private List<QualityConfig> _qualityConfigs;
        private RecruitmentConfig _recruitmentConfig;
        private RitualConfig _ritualConfig;
        private ShopConfig _shopConfig;
        private List<CatStaticStats> _catStaticStats;
        private ChoiceConfigWrapper _choiceConfig;
        private List<FighterConfig> _fighterConfigs;
        private List<BuffConfig> _buffConfigs;

        // 配置文件路径 (StreamingAssets)
        private const string TRIBE_CONFIG_PATH = "Tables/tribe_config.json";
        private const string QUALITY_CONFIG_PATH = "Tables/quality_config.json";
        private const string RECRUITMENT_CONFIG_PATH = "Tables/recruitment_config.json";
        private const string RITUAL_CONFIG_PATH = "Tables/ritual_config.json";
        private const string SHOP_CONFIG_PATH = "Tables/shop_config.json";
        private const string CAT_STATS_TABLE_PATH = "Tables/cat_stats_table.json";
        private const string CHOICE_CONFIG_PATH = "Tables/choice_config.json";
        private const string FIGHTER_CONFIG_PATH = "Tables/fighter_config.json";
        private const string BUFF_CONFIG_PATH = "Tables/buff_config.json";

        private string GetStreamingAssetsPath(string relativePath)
        {
            // 使用 Unity 标准的 StreamingAssets 路径 (Assets/StreamingAssets)
            return Path.Combine(Application.streamingAssetsPath, relativePath);
        }

        private bool _isLoaded = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 加载所有配置文件
        /// </summary>
        public void LoadAllConfigs()
        {
            if (_isLoaded) return;

            _tribeConfigs = LoadTribeConfigs();
            _qualityConfigs = LoadQualityConfigs();
            _recruitmentConfig = LoadRecruitmentConfig();
            _ritualConfig = LoadRitualConfig();
            _shopConfig = LoadShopConfig();
            _catStaticStats = LoadCatStaticStats();
            _choiceConfig = LoadChoiceConfig();
            _fighterConfigs = LoadFighterConfigs();
            _buffConfigs = LoadBuffConfigs();

            _isLoaded = true;

            Debug.Log("[TribeConfigLoader] All configs loaded successfully");
        }

        /// <summary>
        /// 获取族群配置
        /// </summary>
        public TribeConfig GetTribeConfig(TribeType tribeType)
        {
            EnsureLoaded();
            return _tribeConfigs?.Find(t => t.tribeType == tribeType);
        }

        /// <summary>
        /// 获取族长的 avatarId（从 tribe_config.json → fighter_config.json 链式查找）
        /// </summary>
        public string GetLeaderAvatarId(TribeType tribeType)
        {
            var tribeConfig = GetTribeConfig(tribeType);
            if (tribeConfig == null || tribeConfig.leaderFighterId <= 0) return null;
            var fighterConfig = GetFighterConfig(tribeConfig.leaderFighterId);
            return fighterConfig?.avatarId;
        }

        /// <summary>
        /// 获取族长的 Addressable 精灵路径，如 "avatartemp/youxia1"
        /// </summary>
        public string GetLeaderAvatarAddress(TribeType tribeType, int variant = 1)
        {
            string avatarId = GetLeaderAvatarId(tribeType);
            if (string.IsNullOrEmpty(avatarId))
            {
                Debug.LogWarning($"[TribeConfigLoader] GetLeaderAvatarAddress: avatarId is null for {tribeType}");
                return null;
            }
            string addr = $"avatartemp/{avatarId}{variant}";
            Debug.Log($"[TribeConfigLoader] GetLeaderAvatarAddress: {tribeType} → {addr}");
            return addr;
        }

        /// <summary>
        /// 获取指定兵种的 Addressable 精灵路径
        /// </summary>
        public string GetFighterAvatarAddress(int fighterId, int variant = 1)
        {
            var fighterConfig = GetFighterConfig(fighterId);
            if (fighterConfig == null || string.IsNullOrEmpty(fighterConfig.avatarId))
            {
                Debug.LogWarning($"[TribeConfigLoader] GetFighterAvatarAddress: avatarId is null for fighterId={fighterId}");
                return null;
            }
            string addr = $"avatartemp/{fighterConfig.avatarId}{variant}";
            Debug.Log($"[TribeConfigLoader] GetFighterAvatarAddress: fighterId={fighterId} → {addr}");
            return addr;
        }

        /// <summary>
        /// 获取所有族群配置
        /// </summary>
        public List<TribeConfig> GetAllTribeConfigs()
        {
            EnsureLoaded();
            return _tribeConfigs;
        }

        /// <summary>
        /// 获取指定种族和品质的小猫静态属性
        /// </summary>
        public CatStaticStats GetCatStaticStats(TribeType tribeType, CatQuality quality)
        {
            EnsureLoaded();
            return _catStaticStats?.Find(s => s.tribeType == (int)tribeType && s.quality == (int)quality);
        }

        /// <summary>
        /// 获取品质配置
        /// </summary>
        public QualityConfig GetQualityConfig(CatQuality quality)
        {
            EnsureLoaded();
            return _qualityConfigs?.Find(q => q.quality == quality);
        }

        /// <summary>
        /// 获取所有品质配置
        /// </summary>
        public List<QualityConfig> GetAllQualityConfigs()
        {
            EnsureLoaded();
            return _qualityConfigs;
        }

        /// <summary>
        /// 获取招募配置
        /// </summary>
        public RecruitmentConfig GetRecruitmentConfig()
        {
            EnsureLoaded();
            return _recruitmentConfig;
        }

        /// <summary>
        /// 获取祭祀配置
        /// </summary>
        public RitualConfig GetRitualConfig()
        {
            EnsureLoaded();
            return _ritualConfig;
        }

        /// <summary>
        /// 获取商店配置
        /// </summary>
        public ShopConfig GetShopConfig()
        {
            EnsureLoaded();
            return _shopConfig;
        }

        /// <summary>
        /// 获取所有 choice 原型
        /// </summary>
        public List<ChoiceArchetype> GetAllChoiceArchetypes()
        {
            EnsureLoaded();
            return _choiceConfig?.archetypes;
        }

        /// <summary>
        /// 按来源获取 choice 原型
        /// </summary>
        public List<ChoiceArchetype> GetArchetypesBySource(string source)
        {
            EnsureLoaded();
            if (_choiceConfig?.archetypes == null) return new List<ChoiceArchetype>();
            return _choiceConfig.archetypes.FindAll(a => a.source == source);
        }

        /// <summary>
        /// 按 ID 获取 choice 原型
        /// </summary>
        public ChoiceArchetype GetArchetypeById(string id)
        {
            EnsureLoaded();
            if (_choiceConfig?.archetypes == null) return null;
            return _choiceConfig.archetypes.Find(a => a.id == id);
        }

        /// <summary>
        /// 按 fighterId 获取 fighter 配置
        /// </summary>
        public FighterConfig GetFighterConfig(int fighterId)
        {
            EnsureLoaded();
            return _fighterConfigs?.Find(f => f.fighterId == fighterId);
        }

        /// <summary>
        /// 获取所有 fighter 配置（排除敌人 tribeType==0）
        /// </summary>
        public List<FighterConfig> GetAllFighterConfigs()
        {
            EnsureLoaded();
            return _fighterConfigs?.FindAll(f => f.tribeType != 0) ?? new List<FighterConfig>();
        }

        /// <summary>
        /// 按 tribeType 获取所有 fighter 配置
        /// </summary>
        public List<FighterConfig> GetFighterConfigsByTribe(int tribeType)
        {
            EnsureLoaded();
            return _fighterConfigs?.FindAll(f => f.tribeType == tribeType) ?? new List<FighterConfig>();
        }

        /// <summary>
        /// 按 buffId 获取 buff 配置
        /// </summary>
        public BuffConfig GetBuffConfig(int buffId)
        {
            EnsureLoaded();
            return _buffConfigs?.Find(b => b.buffId == buffId);
        }

        /// <summary>
        /// 获取所有 buff 配置
        /// </summary>
        public List<BuffConfig> GetAllBuffConfigs()
        {
            EnsureLoaded();
            return _buffConfigs;
        }

        /// <summary>
        /// 按 ID 列表批量获取 buff 配置
        /// </summary>
        public List<BuffConfig> GetBuffByIds(List<int> buffIds)
        {
            EnsureLoaded();
            var result = new List<BuffConfig>();
            if (buffIds == null) return result;
            foreach (var id in buffIds)
            {
                var cfg = _buffConfigs?.Find(b => b.buffId == id);
                if (cfg != null) result.Add(cfg);
            }
            return result;
        }

        private void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                LoadAllConfigs();
            }
        }

        #region Config Loading Methods

        private List<TribeConfig> LoadTribeConfigs()
        {
            string filePath = GetStreamingAssetsPath(TRIBE_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[TribeConfigLoader] Failed to load tribe config from {filePath}");
                return CreateDefaultTribeConfigs();
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<TribeConfigWrapper>(jsonText);
                Debug.Log($"[TribeConfigLoader] Loaded {json.tribes.Count} tribe configs");
                return json.tribes;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing tribe config: {e.Message}");
                return CreateDefaultTribeConfigs();
            }
        }

        private List<QualityConfig> LoadQualityConfigs()
        {
            string filePath = GetStreamingAssetsPath(QUALITY_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[TribeConfigLoader] Failed to load quality config from {filePath}");
                return CreateDefaultQualityConfigs();
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<QualityConfigWrapper>(jsonText);
                Debug.Log($"[TribeConfigLoader] Loaded {json.qualities.Count} quality configs");
                return json.qualities;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing quality config: {e.Message}");
                return CreateDefaultQualityConfigs();
            }
        }

        private RecruitmentConfig LoadRecruitmentConfig()
        {
            string filePath = GetStreamingAssetsPath(RECRUITMENT_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[TribeConfigLoader] Failed to load recruitment config from {filePath}");
                return CreateDefaultRecruitmentConfig();
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<RecruitmentConfigWrapper>(jsonText);
                Debug.Log("[TribeConfigLoader] Loaded recruitment config");
                return json.options;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing recruitment config: {e.Message}");
                return CreateDefaultRecruitmentConfig();
            }
        }

        private RitualConfig LoadRitualConfig()
        {
            string filePath = GetStreamingAssetsPath(RITUAL_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[TribeConfigLoader] Failed to load ritual config from {filePath}");
                return CreateDefaultRitualConfig();
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<RitualConfigWrapper>(jsonText);
                Debug.Log($"[TribeConfigLoader] Loaded ritual config with {json.tiers.Count} tiers");
                return new RitualConfig { tiers = json.tiers };
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing ritual config: {e.Message}");
                return CreateDefaultRitualConfig();
            }
        }

        private ShopConfig LoadShopConfig()
        {
            string filePath = GetStreamingAssetsPath(SHOP_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[TribeConfigLoader] Failed to load shop config from {filePath}");
                return CreateDefaultShopConfig();
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<ShopConfig>(jsonText);
                Debug.Log("[TribeConfigLoader] Loaded shop config");
                return json;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing shop config: {e.Message}");
                return CreateDefaultShopConfig();
            }
        }

        private List<CatStaticStats> LoadCatStaticStats()
        {
            string filePath = GetStreamingAssetsPath(CAT_STATS_TABLE_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[TribeConfigLoader] cat_stats_table.json not found at {filePath}, cat static stats unavailable");
                return new List<CatStaticStats>();
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<CatStaticStatsWrapper>(jsonText);
                // JSON 中速度值为整数（*1000），转换回 float
                foreach (var cat in json.catStats)
                {
                    cat.moveSpeed /= 1000f;
                    cat.attackSpeed /= 1000f;
                }
                Debug.Log($"[TribeConfigLoader] Loaded {json.catStats.Count} cat static stats entries");
                return json.catStats;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing cat stats table: {e.Message}");
                return new List<CatStaticStats>();
            }
        }

        private ChoiceConfigWrapper LoadChoiceConfig()
        {
            string filePath = GetStreamingAssetsPath(CHOICE_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[TribeConfigLoader] choice_config.json not found at {filePath}");
                return new ChoiceConfigWrapper { archetypes = new List<ChoiceArchetype>() };
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<ChoiceConfigWrapper>(jsonText);
                Debug.Log($"[TribeConfigLoader] Loaded {json.archetypes?.Count ?? 0} choice archetypes");
                return json;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing choice config: {e.Message}");
                return new ChoiceConfigWrapper { archetypes = new List<ChoiceArchetype>() };
            }
        }

        private List<FighterConfig> LoadFighterConfigs()
        {
            string filePath = GetStreamingAssetsPath(FIGHTER_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[TribeConfigLoader] fighter_config.json not found at {filePath}");
                return new List<FighterConfig>();
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<FighterConfigWrapper>(jsonText);
                Debug.Log($"[TribeConfigLoader] Loaded {json.fighters.Count} fighter configs");
                return json.fighters;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing fighter config: {e.Message}");
                return new List<FighterConfig>();
            }
        }

        private List<BuffConfig> LoadBuffConfigs()
        {
            string filePath = GetStreamingAssetsPath(BUFF_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[TribeConfigLoader] buff_config.json not found at {filePath}");
                return new List<BuffConfig>();
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var json = JsonMapper.ToObject<BuffConfigWrapper>(jsonText);
                Debug.Log($"[TribeConfigLoader] Loaded {json.buffs.Count} buff configs");
                return json.buffs;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing buff config: {e.Message}");
                return new List<BuffConfig>();
            }
        }

        #endregion

        #region Default Config Creators

        private List<TribeConfig> CreateDefaultTribeConfigs()
        {
            var configs = new List<TribeConfig>();
            foreach (TribeType type in System.Enum.GetValues(typeof(TribeType)))
            {
                if (type == TribeType.None) continue;
                configs.Add(new TribeConfig
                {
                    tribeType = type,
                    tribeName = type.ToString(),
                    leaderFighterId = 0
                });
            }
            return configs;
        }

        private List<QualityConfig> CreateDefaultQualityConfigs()
        {
            var configs = new List<QualityConfig>();
            foreach (CatQuality quality in System.Enum.GetValues(typeof(CatQuality)))
            {
                configs.Add(new QualityConfig
                {
                    quality = quality,
                    qualityName = quality.ToString()
                });
            }
            return configs;
        }

        private RecruitmentConfig CreateDefaultRecruitmentConfig()
        {
            return new RecruitmentConfig
            {
                newTribe = new NewTribeOption { cost = 300, description = "获得一个新的族群" },
                addCats = new AddCatsOption { cost = 200, description = "为已有族群增加小猫" },
                qualityEvolution = new QualityEvolutionOption { cost = 150, description = "已有族群小猫品质进化", evolutionChance = 0.5f },
                leaderBoost = new LeaderBoostOption { cost = 150, description = "已有族群族长属性提升", boostPercent = 0.2f }
            };
        }

        private RitualConfig CreateDefaultRitualConfig()
        {
            return new RitualConfig
            {
                tiers = new List<RitualTier>(),
                ritualInterval = 3,
                startRound = 3
            };
        }

        private ShopConfig CreateDefaultShopConfig()
        {
            return new ShopConfig
            {
                baseRefreshCost = 50,
                refreshIncrement = 50,
                slotCount = 5,
                shopInterval = 5,
                startRound = 5
            };
        }

        #endregion
    }

    #region JSON Wrapper Classes

    [System.Serializable]
    public class TribeConfigWrapper
    {
        public List<TribeConfig> tribes;
    }

    [System.Serializable]
    public class QualityConfigWrapper
    {
        public List<QualityConfig> qualities;
    }

    [System.Serializable]
    public class RecruitmentConfigWrapper
    {
        public RecruitmentConfig options;
    }

    [System.Serializable]
    public class RitualConfigWrapper
    {
        public List<RitualTier> tiers;
        public int ritualInterval;
        public int startRound;
    }

    [System.Serializable]
    public class ShopConfigWrapper
    {
        public int baseRefreshCost;
        public int refreshIncrement;
        public int slotCount;
        public int shopInterval;
        public int startRound;
        public ShopItemConfig items;
    }

    [System.Serializable]
    public class CatStaticStatsWrapper
    {
        public List<CatStaticStats> catStats;
    }

    #endregion

    #region Config Data Classes

    [System.Serializable]
    public class RecruitmentConfig
    {
        public NewTribeOption newTribe;
        public AddCatsOption addCats;
        public QualityEvolutionOption qualityEvolution;
        public LeaderBoostOption leaderBoost;
    }

    [System.Serializable]
    public class NewTribeOption
    {
        public int cost;
        public string description;
    }

    [System.Serializable]
    public class AddCatsOption
    {
        public int cost;
        public string description;
        public Dictionary<string, int> catCounts;
    }

    [System.Serializable]
    public class QualityEvolutionOption
    {
        public int cost;
        public string description;
        public float evolutionChance;
    }

    [System.Serializable]
    public class LeaderBoostOption
    {
        public int cost;
        public string description;
        public float boostPercent;
    }

    [System.Serializable]
    public class RitualConfig
    {
        public List<RitualTier> tiers;
        public int ritualInterval;
        public int startRound;
    }

    [System.Serializable]
    public class RitualTier
    {
        public string tierName;
        public string displayName;
        public int cost;
        public int drawCount;
        public List<RitualRewardConfig> blessings;
    }

    [System.Serializable]
    public class RitualRewardConfig
    {
        public string type;
        public int weight;
        public string[] statTypes;
        public int minAmount;
        public int maxAmount;
        public string[] qualities;
        public float qualityChance;
        public int minCount;
        public int maxCount;
        public float multiplierMin;
        public float multiplierMax;
        public float minPercent;
        public float maxPercent;
    }

    [System.Serializable]
    public class ShopConfig
    {
        public int baseRefreshCost;
        public int refreshIncrement;
        public int slotCount;
        public int shopInterval;
        public int startRound;
        public ShopItemConfig items;
    }

    [System.Serializable]
    public class ShopItemConfig
    {
        public ArtifactPriceConfig artifact;
        public ConsumablePriceConfig consumable;
        public CatPriceConfig cat;
        public Dictionary<string, int> artifactEffects; // 奇物效果值，如 {"LeaderHpFlat": 500, "CatAttackFlat": 20}
    }

    [System.Serializable]
    public class ArtifactPriceConfig
    {
        public int basePrice;
        public string icon; // 兼容旧配置
        public Dictionary<string, string> icons; // 按效果类型分图标
    }

    [System.Serializable]
    public class ConsumablePriceConfig
    {
        public int basePriceMin;
        public int basePriceMax;
        public Dictionary<string, string> icons;
    }

    [System.Serializable]
    public class CatPriceConfig
    {
        public Dictionary<string, int> basePrices;
        public Dictionary<string, float> qualityBonusMultipliers;
        public float priceVariation;
        public float sellRatio = 0.5f;
        public Dictionary<string, List<string>> tribeIcons;
    }

    #endregion

    #region Fighter & Buff Config Classes

    [System.Serializable]
    public class FighterConfigWrapper
    {
        public List<FighterConfig> fighters;
    }

    [System.Serializable]
    public class FighterConfig
    {
        public int fighterId;
        public string fighterName;
        public int tribeType;
        public int tier;
        public int attack;
        public int defense;
        public int hp;
        public float moveSpeed;
        public float attackSpeed;
        public float attackRange;
        public List<int> innateBuffIds;
        public string avatarId;       // 外观 ID（空=使用族群默认外观）
        public List<string> tags;     // 设计标签（如 glass_cannon、tank、summoner）
    }

    [System.Serializable]
    public class BuffConfigWrapper
    {
        public List<BuffConfig> buffs;
    }

    [System.Serializable]
    public class BuffConfig
    {
        public int buffId;
        public string buffName;
        public string description;
        public int gameEffectType;
        public float effectParam1;
        public float effectParam2;
        public int duration;
        public bool visible;
        public int iconColorIndex;
    }

    #endregion
}
