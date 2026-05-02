using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 商店服务 - 处理商店生成、刷新和买卖
    /// </summary>
    public class ShopService
    {
        private DataManager _dataManager;
        private AuraService _auraService;
        private int _currentRound;

        public ShopService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        public void SetAuraService(AuraService auraService)
        {
            _auraService = auraService;
        }

        /// <summary>
        /// 检查当前回合是否可以开放商店
        /// </summary>
        public bool CanOpenShop(int currentRound)
        {
            var config = TribeConfigLoader.Instance.GetShopConfig();
            return currentRound >= config.startRound && (currentRound - config.startRound) % config.shopInterval == 0;
        }

        /// <summary>
        /// 生成商店商品列表
        /// </summary>
        public List<ShopItem> GenerateShopItems()
        {
            var items = new List<ShopItem>();
            var config = TribeConfigLoader.Instance.GetShopConfig();
            var usedKeys = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < config.slotCount; i++)
            {
                ShopItem item = GenerateRandomShopItemWithRetry(config, usedKeys);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private ShopItem GenerateRandomShopItemWithRetry(ShopConfig config, System.Collections.Generic.HashSet<string> usedKeys)
        {
            // 最多重试 slotCount * 2 次，避免死循环
            int maxRetry = config.slotCount * 2;
            for (int i = 0; i < maxRetry; i++)
            {
                ShopItem item = GenerateRandomShopItem(config);
                if (item == null) return null;

                string key = GetItemKey(item);
                if (usedKeys.Add(key))
                    return item;
            }
            // 重试用尽，放一个不重复的
            return GenerateNonDuplicateItem(config, usedKeys);
        }

        private string GetItemKey(ShopItem item)
        {
            switch (item.itemType)
            {
                case ShopItemType.Artifact:
                    return $"Artifact_{item.artifactEffectType}";
                case ShopItemType.Consumable:
                    return $"Consumable_{item.consumableEffectType}";
                case ShopItemType.Cat:
                    return $"Cat_{item.catTribeType}_{item.catQuality}";
                default:
                    return $"{item.itemType}_{item.itemId}";
            }
        }

        private ShopItem GenerateNonDuplicateItem(ShopConfig config, System.Collections.Generic.HashSet<string> usedKeys)
        {
            // 遍历所有消耗品类型，找一个没用过的
            string[] consumableNames = { "炸弹", "冰冻陷阱", "回复药水", "攻击强化", "防御强化" };
            foreach (string name in consumableNames)
            {
                ConsumableEffectType effectType = GetConsumableEffectType(name);
                string key = $"Consumable_{effectType}";
                if (!usedKeys.Contains(key))
                {
                    int price = GetConsumablePrice(effectType);
                    string iconAddress = "";
                    if (config.items.consumable.icons != null)
                        config.items.consumable.icons.TryGetValue(effectType.ToString(), out iconAddress);

                    return new ShopItem
                    {
                        itemId = Random.Range(200, 300),
                        itemType = ShopItemType.Consumable,
                        consumableEffectType = effectType,
                        basePrice = price,
                        name = name,
                        description = GetConsumableDescription(effectType),
                        iconAddress = iconAddress ?? ""
                    };
                }
            }
            return null;
        }

        /// <summary>
        /// 计算刷新消耗
        /// </summary>
        public int CalculateRefreshCost()
        {
            var config = TribeConfigLoader.Instance.GetShopConfig();
            int refreshCount = _dataManager.GetShopRefreshCount();
            return config.baseRefreshCost + refreshCount * config.refreshIncrement;
        }

        /// <summary>
        /// 执行刷新
        /// </summary>
        public List<ShopItem> RefreshShop()
        {
            int cost = CalculateRefreshCost();

            if (!_dataManager.TrySpendCatFood(cost))
            {
                Debug.LogWarning("[ShopService] Not enough cat food to refresh shop");
                return null;
            }

            _dataManager.IncrementShopRefreshCount();
            return GenerateShopItems();
        }

        /// <summary>
        /// 购买物品
        /// </summary>
        /// <summary>
        /// 购买物品，返回 1=成功, 0=猫粮不足, -1=售罄
        /// </summary>
        public int BuyItem(ShopItem item)
        {
            if (item.stock <= 0)
            {
                Debug.LogWarning($"[ShopService] Item sold out: {item.name}");
                return -1;
            }

            int actualPrice = item.GetActualPrice();

            if (!_dataManager.TrySpendCatFood(actualPrice))
            {
                Debug.LogWarning($"[ShopService] Not enough cat food to buy {item.name}");
                return 0;
            }

            item.stock--;

            // 根据物品类型处理
            switch (item.itemType)
            {
                case ShopItemType.Artifact:
                    if (item.artifactEffectType.HasValue)
                    {
                        ApplyArtifactEffect(item.artifactEffectType.Value);
                    }
                    break;

                case ShopItemType.Consumable:
                    if (item.consumableEffectType.HasValue)
                    {
                        var consumable = new ConsumableItem
                        {
                            id = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue),
                            name = item.name,
                            effectType = item.consumableEffectType.Value,
                            basePrice = item.basePrice
                        };
                        _dataManager.AddConsumable(consumable);
                        Debug.Log($"[ShopService] Bought consumable: {item.name} ({consumable.effectType})");
                    }
                    break;

                case ShopItemType.Cat:
                    if (item.catTribeType.HasValue && item.catQuality.HasValue)
                    {
                        AddCatToPlayer(item.catTribeType.Value, item.catQuality.Value);
                    }
                    break;
            }

            _dataManager.SavePlayerData();
            return 1;
        }

        private void ApplyArtifactEffect(ArtifactEffectType effectType)
        {
            // 读取奇物配置获取效果值
            var config = TribeConfigLoader.Instance.GetShopConfig();
            int value = GetArtifactEffectValue(effectType, config);

            string artifactName = effectType == ArtifactEffectType.LeaderHpFlat ? "猫爬架" : "苍蝇拍";
            StatType stat = effectType == ArtifactEffectType.LeaderHpFlat ? StatType.Hp : StatType.Attack;

            // 确定影响范围：族长加血→全体族长，小猫加攻→全体小猫
            var scopeFilter = effectType == ArtifactEffectType.LeaderHpFlat
                ? new BuffScopeFilter { role = ScopeRoleFilter.Leader }
                : new BuffScopeFilter { role = ScopeRoleFilter.Soldier };

            // 小猫攻击力奇物：累计全局值，新小猫自动继承
            if (effectType == ArtifactEffectType.CatAttackFlat)
            {
                _dataManager.PlayerData.globalCatAttackFlatBonus += value;
            }

            // 构造 EquipmentRecord 并注册
            var equip = new EquipmentRecord
            {
                equipmentId = $"Artifact_{effectType}_{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                configId = $"Artifact_{effectType}",
                displayName = artifactName,
                description = effectType == ArtifactEffectType.LeaderHpFlat ? $"族长生命值+{value}" : $"小猫攻击力+{value}",
                buffScopeFilter = scopeFilter,
                buffScopeText = scopeFilter.GetDisplayString(),
                buffApplyType = BuffApplyType.Aura,
                acquiredRound = _dataManager.GetCurrentRound(),
                effects = new List<BuffEffectItem> { new BuffEffectItem(stat, false, value) }
            };

            _auraService?.RegisterEquipment(equip);

            // 同时记录到旧的 unlockedAccessories 保持兼容
            _dataManager.UnlockAccessory($"Artifact_{effectType}");
        }

        private int GetArtifactEffectValue(ArtifactEffectType effectType, ShopConfig config)
        {
            // 从配置读取，JSON 中新增 artifactEffects 字段
            if (config.items.artifactEffects != null &&
                config.items.artifactEffects.ContainsKey(effectType.ToString()))
            {
                return config.items.artifactEffects[effectType.ToString()];
            }
            // 默认值
            return effectType == ArtifactEffectType.LeaderHpFlat ? 500 : 20;
        }

        /// <summary>
        /// 卖出消耗品
        /// </summary>
        public int SellConsumable(int consumableId, int basePrice)
        {
            var config = TribeConfigLoader.Instance.GetShopConfig();
            int sellPrice = Mathf.RoundToInt(basePrice * 0.5f); // 5折出售

            _dataManager.AddCatFood(sellPrice);
            Debug.Log($"[ShopService] Sold consumable for {sellPrice} cat food");

            return sellPrice;
        }

        /// <summary>
        /// 卖出小猫
        /// </summary>
        public int SellCat(TribeRecord tribe, CatData cat)
        {
            int sellPrice = GetCatSellPrice(tribe.tribeType, cat.quality);

            _dataManager.AddCatFood(sellPrice);
            tribe.cats.Remove(cat);

            Debug.Log($"[ShopService] Sold {cat.quality} cat for {sellPrice} cat food");

            return sellPrice;
        }

        private ShopItem GenerateRandomShopItem(ShopConfig config)
        {
            // 简单随机生成，可以根据权重调整
            int roll = Random.Range(0, 100);

            if (roll < 30)
            {
                return GenerateArtifactItem(config);
            }
            else if (roll < 70)
            {
                return GenerateConsumableItem(config);
            }
            else
            {
                return GenerateCatItem(config);
            }
        }

        private ShopItem GenerateArtifactItem(ShopConfig config)
        {
            // 两种奇物：族长加血 / 小猫加攻
            bool isLeaderHp = Random.value < 0.5f;
            ArtifactEffectType effectType = isLeaderHp ? ArtifactEffectType.LeaderHpFlat : ArtifactEffectType.CatAttackFlat;
            string name = isLeaderHp ? "猫爬架" : "苍蝇拍";

            // 从配置读取效果值
            int value = GetArtifactEffectValue(effectType, config);
            string desc = isLeaderHp ? $"族长生命值+{value}" : $"小猫攻击力+{value}";

            // 从配置读取图标
            string icon = config.items.artifact.icon ?? "";
            if (config.items.artifact.icons != null &&
                config.items.artifact.icons.ContainsKey(effectType.ToString()))
            {
                icon = config.items.artifact.icons[effectType.ToString()];
            }

            return new ShopItem
            {
                itemId = Random.Range(100, 200),
                itemType = ShopItemType.Artifact,
                artifactEffectType = effectType,
                basePrice = config.items.artifact.basePrice,
                name = name,
                description = desc,
                iconAddress = icon
            };
        }

        private ShopItem GenerateConsumableItem(ShopConfig config)
        {
            string name = GetRandomConsumableName();
            ConsumableEffectType effectType = GetConsumableEffectType(name);
            int price = GetConsumablePrice(effectType);

            string iconAddress = "";
            if (config.items.consumable.icons != null)
                config.items.consumable.icons.TryGetValue(effectType.ToString(), out iconAddress);

            return new ShopItem
            {
                itemId = Random.Range(200, 300),
                itemType = ShopItemType.Consumable,
                consumableEffectType = effectType,
                basePrice = price,
                name = name,
                description = GetConsumableDescription(effectType),
                iconAddress = iconAddress ?? ""
            };
        }

        private ShopItem GenerateCatItem(ShopConfig config)
        {
            // 随机选择族群
            var playerData = _dataManager.PlayerData;
            if (playerData?.tribes == null || playerData.tribes.Count == 0)
            {
                return null;
            }

            var randomTribe = playerData.tribes[Random.Range(0, playerData.tribes.Count)];
            var tribeType = randomTribe.tribeType;

            // 随机选择品质
            CatQuality quality = TribeStatsCalculator.RandomCatQuality();

            // 计算价格
            int basePrice = CalculateCatPrice(config, tribeType, quality);

            return new ShopItem
            {
                itemId = Random.Range(300, 400),
                itemType = ShopItemType.Cat,
                catTribeType = tribeType,
                catQuality = quality,
                basePrice = basePrice,
                name = $"{GetTribeTypeName(tribeType)}({GetQualityName(quality)})",
                description = $"{TribeConfigLoader.Instance.GetTribeConfig(tribeType)?.initialCatCount ?? 1}只{GetQualityName(quality)}品质的小猫",
                iconAddress = GetTribeIcon(tribeType, config)
            };
        }

        /// <summary>
        /// 获取小猫出售价格（买入价 × sellRatio）
        /// </summary>
        public int GetCatSellPrice(TribeType tribeType, CatQuality quality)
        {
            var config = TribeConfigLoader.Instance.GetShopConfig();
            int buyPrice = CalculateCatPrice(config, tribeType, quality);
            float sellRatio = config.items.cat?.sellRatio ?? 0.5f;
            return Mathf.RoundToInt(buyPrice * sellRatio);
        }

        private int CalculateCatPrice(ShopConfig config, TribeType tribeType, CatQuality quality)
        {
            // 获取基础价格
            string tribeKey = ((int)tribeType).ToString();
            int basePrice = 100; // 默认

            if (config.items.cat?.basePrices != null && config.items.cat.basePrices.ContainsKey(tribeKey))
            {
                basePrice = config.items.cat.basePrices[tribeKey];
            }

            // 应用品质加成
            float qualityMultiplier = 1.0f;
            string qualityKey = ((int)quality).ToString();

            if (config.items.cat?.qualityBonusMultipliers != null && config.items.cat.qualityBonusMultipliers.ContainsKey(qualityKey))
            {
                qualityMultiplier = config.items.cat.qualityBonusMultipliers[qualityKey];
            }

            return Mathf.RoundToInt(basePrice * qualityMultiplier);
        }

        private void AddCatToPlayer(TribeType tribeType, CatQuality quality)
        {
            var playerData = _dataManager.PlayerData;

            // 找到对应族群
            TribeRecord targetTribe = null;
            foreach (var tribe in playerData.tribes)
            {
                if (tribe.tribeType == tribeType && tribe.isActive)
                {
                    targetTribe = tribe;
                    break;
                }
            }

            if (targetTribe != null)
            {
                var config = TribeConfigLoader.Instance.GetTribeConfig(tribeType);
                int catsToAdd = config != null ? config.initialCatCount : 1;
                for (int i = 0; i < catsToAdd; i++)
                {
                    var cat = CatData.CreateWithQuality(quality, targetTribe.tribeType);
                    _auraService?.ApplyAurasToNewCat(cat, targetTribe.tribeType);
                    targetTribe.cats.Add(cat);
                }
                Debug.Log($"[ShopService] Added {catsToAdd} {quality} cats to tribe {tribeType}");
            }
            else
            {
                Debug.LogWarning($"[ShopService] No active tribe found for type {tribeType}");
            }
        }

        private string GetRandomConsumableName()
        {
            string[] names = { "炸弹", "冰冻陷阱", "回复药水", "攻击强化", "防御强化" };
            return names[Random.Range(0, names.Length)];
        }

        private int GetConsumablePrice(ConsumableEffectType effectType)
        {
            switch (effectType)
            {
                case ConsumableEffectType.Bomb: return 999;
                case ConsumableEffectType.FreezeTrap: return 63;
                case ConsumableEffectType.HealPotion: return 85;
                case ConsumableEffectType.AttackBuff: return 55;
                case ConsumableEffectType.DefenseBuff: return 30;
                default: return 50;
            }
        }

        private ConsumableEffectType GetConsumableEffectType(string name)
        {
            switch (name)
            {
                case "炸弹": return ConsumableEffectType.Bomb;
                case "冰冻陷阱": return ConsumableEffectType.FreezeTrap;
                case "回复药水": return ConsumableEffectType.HealPotion;
                case "攻击强化": return ConsumableEffectType.AttackBuff;
                case "防御强化": return ConsumableEffectType.DefenseBuff;
                default: return ConsumableEffectType.Bomb;
            }
        }

        private string GetConsumableDescription(ConsumableEffectType type)
        {
            switch (type)
            {
                case ConsumableEffectType.Bomb: return "对所有敌人造成200点伤害";
                case ConsumableEffectType.FreezeTrap: return "所有敌人停止攻击3秒";
                case ConsumableEffectType.HealPotion: return "回复所有己方单位50%生命值";
                case ConsumableEffectType.AttackBuff: return "己方攻击力+30%，持续15秒";
                case ConsumableEffectType.DefenseBuff: return "己方防御力+30%，持续15秒";
                default: return "消耗品";
            }
        }

        private string GetTribeTypeName(TribeType type)
        {
            switch (type)
            {
                case TribeType.Tabby: return "狸花";
                case TribeType.Orange: return "大橘";
                case TribeType.Cow: return "奶牛";
                case TribeType.Siamese: return "暹罗";
                default: return type.ToString();
            }
        }

        private string GetQualityName(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White: return "菜鸟";
                case CatQuality.Blue: return "老手";
                case CatQuality.Purple: return "精英";
                case CatQuality.Gold: return "大师";
                default: return quality.ToString();
            }
        }

        private string GetTribeIcon(TribeType tribeType, ShopConfig config)
        {
            var tribeIcons = config.items.cat?.tribeIcons;
            if (tribeIcons != null)
            {
                string key = ((int)tribeType).ToString();
                if (tribeIcons.ContainsKey(key))
                {
                    var icons = tribeIcons[key];
                    if (icons != null && icons.Count > 0)
                        return icons[0];
                }
            }
            return "";
        }
    }
}
