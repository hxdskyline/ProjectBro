using System.Collections.Generic;
using UnityEngine;
using System.IO;

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

        // 配置文件路径 (StreamingAssets)
        private const string TRIBE_CONFIG_PATH = "Tables/tribe_config.json";
        private const string QUALITY_CONFIG_PATH = "Tables/quality_config.json";
        private const string RECRUITMENT_CONFIG_PATH = "Tables/recruitment_config.json";
        private const string RITUAL_CONFIG_PATH = "Tables/ritual_config.json";
        private const string SHOP_CONFIG_PATH = "Tables/shop_config.json";
        private const string CAT_STATS_TABLE_PATH = "Tables/cat_stats_table.json";

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
                var json = JsonUtility.FromJson<TribeConfigWrapper>(jsonText);
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
                var json = JsonUtility.FromJson<QualityConfigWrapper>(jsonText);
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
                var json = JsonUtility.FromJson<RecruitmentConfigWrapper>(jsonText);
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
                var json = JsonUtility.FromJson<RitualConfigWrapper>(jsonText);
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
                var json = JsonUtility.FromJson<ShopConfigWrapper>(jsonText);
                Debug.Log("[TribeConfigLoader] Loaded shop config");
                return new ShopConfig
                {
                    baseRefreshCost = json.baseRefreshCost,
                    refreshIncrement = json.refreshIncrement,
                    slotCount = json.slotCount,
                    shopInterval = json.shopInterval,
                    startRound = json.startRound,
                    items = json.items
                };
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
                var json = JsonUtility.FromJson<CatStaticStatsWrapper>(jsonText);
                Debug.Log($"[TribeConfigLoader] Loaded {json.catStats.Count} cat static stats entries");
                return json.catStats;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TribeConfigLoader] Error parsing cat stats table: {e.Message}");
                return new List<CatStaticStats>();
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
                    initialCatCount = 3,
                    leaderBaseStats = new LeaderBaseStats(),
                    catBaseStats = new LeaderBaseStats
                    {
                        attack = 70,
                        defense = 55,
                        hp = 600,
                        speed = 700,
                        command = 0
                    }
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
    }

    [System.Serializable]
    public class ArtifactPriceConfig
    {
        public int basePrice;
    }

    [System.Serializable]
    public class ConsumablePriceConfig
    {
        public int basePriceMin;
        public int basePriceMax;
    }

    [System.Serializable]
    public class CatPriceConfig
    {
        public Dictionary<string, int> basePrices;
        public Dictionary<string, float> qualityBonusMultipliers;
        public float priceVariation;
        public float sellRatio = 0.5f;
    }

    #endregion
}
