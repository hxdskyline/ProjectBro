using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BattleSystem;

namespace TribeSystem
{
    /// <summary>
    /// 词缀抽取服务 - 处理词缀抽取逻辑
    /// </summary>
    public class AffixDrawService
    {
        private AffixDrawTableConfig _drawTableConfig;
        private Dictionary<string, AffixData> _allAffixes;
        internal static Dictionary<int, List<BuffEffectItem>> _buffEffectsMap;

        public AffixDrawService()
        {
            LoadDrawTable();
            LoadBuffEffects();
            LoadAllAffixes();
        }

        /// <summary>
        /// 加载词缀抽取概率表
        /// </summary>
        private void LoadDrawTable()
        {
            _drawTableConfig = AffixDrawTableConfig.Load();
            if (_drawTableConfig == null)
            {
                Debug.LogError("[AffixDrawService] 无法加载词缀抽取概率表");
            }
        }

        /// <summary>
        /// 从 buff_config.json 加载所有 buff 的效果数据
        /// </summary>
        private void LoadBuffEffects()
        {
            _buffEffectsMap = new Dictionary<int, List<BuffEffectItem>>();
            try
            {
                string configPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Tables/buff_config.json");
                if (!System.IO.File.Exists(configPath)) return;

                string json = System.IO.File.ReadAllText(configPath);
                var root = LitJson.JsonMapper.ToObject(json);

                if (root != null && root.Keys.Contains("buffs"))
                {
                    var buffsJson = root["buffs"];
                    for (int i = 0; i < buffsJson.Count; i++)
                    {
                        var item = buffsJson[i];
                        int buffId = ReadInt(item, "buffId", -1);
                        if (buffId < 0) continue;

                        var effects = new List<BuffEffectItem>();
                        if (item.Keys.Contains("buffEffects") && item["buffEffects"].IsArray)
                        {
                            var effArray = item["buffEffects"];
                            for (int e = 0; e < effArray.Count; e++)
                            {
                                var effJson = effArray[e];
                                effects.Add(new BuffEffectItem(
                                    ParseStatType(ReadString(effJson, "statType", "Attack")),
                                    ReadBool(effJson, "isPercent"),
                                    ReadFloat(effJson, "value", 0f)
                                ));
                            }
                        }
                        _buffEffectsMap[buffId] = effects;
                    }
                }

                Debug.Log($"[AffixDrawService] 加载了 {_buffEffectsMap.Count} 个 buff 效果");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AffixDrawService] 加载 buff 效果失败: {e.Message}");
            }
        }

        /// <summary>
        /// 加载所有词缀数据
        /// </summary>
        private void LoadAllAffixes()
        {
            _allAffixes = new Dictionary<string, AffixData>();
            try
            {
                string configPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Tables/affix_config.json");
                if (!System.IO.File.Exists(configPath))
                {
                    Debug.LogError($"[AffixDrawService] 词缀配置文件不存在: {configPath}");
                    return;
                }

                string json = System.IO.File.ReadAllText(configPath);
                var root = LitJson.JsonMapper.ToObject(json);

                if (root != null && root.Keys.Contains("affixes"))
                {
                    var affixesJson = root["affixes"];
                    for (int i = 0; i < affixesJson.Count; i++)
                    {
                        var item = affixesJson[i];
                        var affix = new AffixData
                        {
                            affixId = ReadString(item, "affixId", ""),
                            displayName = ReadString(item, "displayName", ""),
                            fighterId = ReadInt(item, "fighterId", 0),
                            tier = ParseAffixTier(ReadString(item, "tier", "Low")),
                            weight = ReadInt(item, "weight", 10),
                            upgradeFrom = ReadString(item, "upgradeFrom", ""),
                            upgradeTo = ReadString(item, "upgradeTo", ""),
                            buffIds = new List<int>()
                        };

                        // 解析 buffIds
                        if (item.Keys.Contains("buffIds") && item["buffIds"].IsArray)
                        {
                            var buffIdsJson = item["buffIds"];
                            for (int b = 0; b < buffIdsJson.Count; b++)
                            {
                                if (int.TryParse(buffIdsJson[b].ToString(), out int buffId))
                                    affix.buffIds.Add(buffId);
                            }
                        }

                        // 从 buff_config 解析描述
                        affix.description = affix.ResolveDescription();

                        if (!string.IsNullOrEmpty(affix.affixId))
                        {
                            _allAffixes[affix.affixId] = affix;
                        }
                    }
                }

                Debug.Log($"[AffixDrawService] 加载了 {_allAffixes.Count} 个词缀");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AffixDrawService] 加载词缀数据失败: {e.Message}");
            }
        }

        /// <summary>
        /// 抽取词缀
        /// </summary>
        /// <param name="level">当前关卡</param>
        /// <param name="difficulty">关卡难度</param>
        /// <param name="fighterId">目标兵种ID</param>
        /// <param name="ownedAffixes">已拥有的词缀ID列表</param>
        /// <param name="drawCount">抽取数量（默认3）</param>
        /// <returns>抽取的词缀列表</returns>
        public List<AffixData> DrawAffixes(int level, DifficultyLevel difficulty, int fighterId, List<string> ownedAffixes, int drawCount = 3)
        {
            if (_drawTableConfig == null)
            {
                Debug.LogError("[AffixDrawService] 抽取概率表未加载");
                return new List<AffixData>();
            }

            // 获取当前关卡和难度对应的概率配置
            var drawConfig = _drawTableConfig.GetDrawConfig(level, difficulty);
            if (drawConfig == null)
            {
                Debug.LogError($"[AffixDrawService] 未找到关卡{level}难度{difficulty}的抽取配置");
                return new List<AffixData>();
            }

            // 获取该兵种的所有词缀（包含通用词缀）
            var fighterAffixes = GetFighterAffixes(fighterId);
            if (fighterAffixes == null || fighterAffixes.Count == 0)
            {
                Debug.LogWarning($"[AffixDrawService] 兵种{fighterId}没有词缀数据");
                return new List<AffixData>();
            }

            // 过滤已拥有的词缀和不满足前置条件的词缀
            var availableAffixes = FilterAvailableAffixes(fighterAffixes, ownedAffixes);
            if (availableAffixes.Count == 0)
            {
                Debug.LogWarning($"[AffixDrawService] 兵种{fighterId}没有可抽取的词缀");
                return new List<AffixData>();
            }

            // 按词缀类型分组
            var lowAffixes = availableAffixes.Where(a => a.tier == AffixTier.Low).ToList();
            var highAffixes = availableAffixes.Where(a => a.tier == AffixTier.High).ToList();
            var fixedAffixes = availableAffixes.Where(a => a.tier == AffixTier.Fixed).ToList();

            // 根据概率抽取
            List<AffixData> result = new List<AffixData>();
            int remaining = drawCount;

            while (remaining > 0 && availableAffixes.Count > 0)
            {
                // 随机决定抽取哪种类型的词缀
                float rand = Random.Range(0f, 100f);
                AffixTier targetTier;

                if (rand < drawConfig.low)
                {
                    targetTier = AffixTier.Low;
                }
                else if (rand < drawConfig.low + drawConfig.high)
                {
                    targetTier = AffixTier.High;
                }
                else
                {
                    targetTier = AffixTier.Fixed;
                }

                // 从对应类型的词缀中随机选择
                var candidates = GetAffixesByTier(availableAffixes, targetTier);
                if (candidates.Count == 0)
                {
                    // 如果该类型没有可抽取的词缀，从其他类型中选择
                    candidates = availableAffixes;
                }

                if (candidates.Count > 0)
                {
                    var selected = candidates[Random.Range(0, candidates.Count)];
                    result.Add(selected);
                    availableAffixes.Remove(selected);
                    remaining--;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取指定兵种的所有词缀（包含通用词缀 fighterId=0）
        /// </summary>
        private List<AffixData> GetFighterAffixes(int fighterId)
        {
            var result = new List<AffixData>();
            foreach (var affix in _allAffixes.Values)
            {
                // fighterId=0 表示通用词缀，对所有友方生效
                if (affix.fighterId == 0 || affix.fighterId == fighterId)
                {
                    result.Add(affix);
                }
            }
            return result;
        }

        /// <summary>
        /// 过滤可用的词缀
        /// </summary>
        private List<AffixData> FilterAvailableAffixes(List<AffixData> allAffixes, List<string> ownedAffixes)
        {
            var result = new List<AffixData>();

            foreach (var affix in allAffixes)
            {
                // 已拥有的固定词缀不再出现
                if (affix.tier == AffixTier.Fixed && ownedAffixes.Contains(affix.affixId))
                {
                    continue;
                }

                // 已拥有的高级词缀不再出现
                if (affix.tier == AffixTier.High && ownedAffixes.Contains(affix.affixId))
                {
                    continue;
                }

                // 高级词缀需要先拥有对应的低级词缀
                if (affix.tier == AffixTier.High && !string.IsNullOrEmpty(affix.upgradeFrom))
                {
                    if (!ownedAffixes.Contains(affix.upgradeFrom))
                    {
                        continue;
                    }
                }

                // 已拥有高级词缀后，对应的低级词缀不再出现
                if (affix.tier == AffixTier.Low && !string.IsNullOrEmpty(affix.upgradeTo))
                {
                    if (ownedAffixes.Contains(affix.upgradeTo))
                    {
                        continue;
                    }
                }

                result.Add(affix);
            }

            return result;
        }

        /// <summary>
        /// 按类型获取词缀
        /// </summary>
        private List<AffixData> GetAffixesByTier(List<AffixData> affixes, AffixTier tier)
        {
            return affixes.Where(a => a.tier == tier).ToList();
        }

        /// <summary>
        /// 应用词缀到猫咪
        /// </summary>
        public void ApplyAffix(TribeRecord tribe, int catIndex, AffixData affix)
        {
            if (tribe == null || affix == null)
            {
                Debug.LogError("[AffixDrawService] 参数无效");
                return;
            }

            // 通过 buffIds 从 buff_config 获取效果
            var effects = affix.ResolveEffects();
            if (effects == null || effects.Count == 0)
            {
                Debug.LogWarning($"[AffixDrawService] 词缀{affix.displayName}没有效果（buffIds为空或buff_config中未找到）");
                return;
            }

            // TODO: 实际应用词缀效果到猫咪属性
            Debug.Log($"[AffixDrawService] 应用词缀{affix.displayName}到{tribe.tribeType}的第{catIndex}只猫，{effects.Count}个效果");
        }

        #region JSON 解析辅助方法

        private static string ReadString(LitJson.JsonData json, string key, string defaultValue)
        {
            return json.Keys.Contains(key) ? json[key].ToString() : defaultValue;
        }

        private static int ReadInt(LitJson.JsonData json, string key, int defaultValue)
        {
            return json.Keys.Contains(key) && int.TryParse(json[key].ToString(), out int v) ? v : defaultValue;
        }

        private static float ReadFloat(LitJson.JsonData json, string key, float defaultValue)
        {
            return json.Keys.Contains(key) && float.TryParse(json[key].ToString(), out float v) ? v : defaultValue;
        }

        private static bool ReadBool(LitJson.JsonData json, string key)
        {
            return json.Keys.Contains(key)
                && bool.TryParse(json[key].ToString(), out bool v)
                && v;
        }

        private static TribeType ParseTribeType(string s)
        {
            switch (s)
            {
                case "Tabby": return TribeType.Tabby;
                case "Orange": return TribeType.Orange;
                case "Cow": return TribeType.Cow;
                case "Siamese": return TribeType.Siamese;
                default: return TribeType.Tabby;
            }
        }

        private static AffixTier ParseAffixTier(string s)
        {
            switch (s)
            {
                case "High": return AffixTier.High;
                case "Fixed": return AffixTier.Fixed;
                default: return AffixTier.Low;
            }
        }

        private static StatType ParseStatType(string s)
        {
            switch (s)
            {
                case "Attack": return StatType.Attack;
                case "Defense": return StatType.Defense;
                case "Hp": return StatType.Hp;
                case "MoveSpeed": return StatType.MoveSpeed;
                case "AttackSpeed": return StatType.AttackSpeed;
                default: return StatType.Attack;
            }
        }

        #endregion
    }

    /// <summary>
    /// 词缀抽取概率配置
    /// </summary>
    [System.Serializable]
    public class AffixDrawTableConfig
    {
        public List<AffixDrawConfig> drawTable;
        public int drawCount;

        public static AffixDrawTableConfig Load()
        {
            try
            {
                string json = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(Application.streamingAssetsPath, "Tables/affix_draw_table.json"));
                return JsonUtility.FromJson<AffixDrawTableConfig>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AffixDrawTableConfig] 加载失败: {e.Message}");
                return null;
            }
        }

        public AffixDrawConfig GetDrawConfig(int level, DifficultyLevel difficulty)
        {
            string difficultyStr = difficulty switch
            {
                DifficultyLevel.Normal => "normal",
                DifficultyLevel.Hard => "hard",
                DifficultyLevel.Bloodbath => "elite", // 极难对应精英关配置
                _ => "normal"
            };

            foreach (var config in drawTable)
            {
                if (level >= config.levelRange[0] && level <= config.levelRange[1])
                {
                    if (config.difficulty == difficultyStr)
                    {
                        return config;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 词缀抽取配置项
    /// </summary>
    [System.Serializable]
    public class AffixDrawConfig
    {
        public int[] levelRange;
        public string difficulty;
        public float low;
        public float high;
        public float fixedTier;
    }

    /// <summary>
    /// 词缀数据
    /// </summary>
    [System.Serializable]
    public class AffixData
    {
        public string affixId;
        public string displayName;
        public string description;
        public int fighterId;           // 关联的兵种ID（0=影响所有友方/族群）
        public AffixTier tier;
        public string upgradeFrom;
        public string upgradeTo;
        public List<int> buffIds;       // 关联的 buff_config ID 列表
        public int weight;

        /// <summary>
        /// 通过 buffIds 从 buffEffectsMap 解析所有效果
        /// </summary>
        public List<BuffEffectItem> ResolveEffects()
        {
            var effects = new List<BuffEffectItem>();
            if (buffIds == null) return effects;
            foreach (var buffId in buffIds)
            {
                if (AffixDrawService._buffEffectsMap != null
                    && AffixDrawService._buffEffectsMap.TryGetValue(buffId, out var effs))
                {
                    effects.AddRange(effs);
                }
            }
            return effects;
        }

        /// <summary>
        /// 通过 buffIds 从 buff_config 解析描述（取第一个 buff 的 description）
        /// </summary>
        public string ResolveDescription()
        {
            if (buffIds == null || buffIds.Count == 0) return "";
            var cfg = TribeConfigLoader.Instance?.GetBuffConfig(buffIds[0]);
            return cfg?.description ?? "";
        }
    }

    /// <summary>
    /// 词缀类型
    /// </summary>
    public enum AffixTier
    {
        Low,
        High,
        Fixed
    }
}
