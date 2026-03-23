using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 为卡牌界面解析猫咪头像，统一使用 Idle 动作的第一帧。
/// </summary>
public static class CardPortraitResolver
{
    private static readonly Dictionary<string, string> IdleFrameAddressCache = new Dictionary<string, string>();
    private static readonly Dictionary<string, Sprite> PortraitSpriteCache = new Dictionary<string, Sprite>();

    public static Sprite ResolvePortrait(string avatarDefinitionAddress)
    {
        string cacheKey = NormalizeKey(avatarDefinitionAddress);
        if (string.IsNullOrEmpty(cacheKey))
        {
            return null;
        }

        if (PortraitSpriteCache.TryGetValue(cacheKey, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        ResourceManager resourceManager = GameManager.Instance != null ? GameManager.Instance.ResourceManager : null;
        if (resourceManager == null)
        {
            return null;
        }

        string idleFrameAddress = ResolveIdleFrameAddress(cacheKey, avatarDefinitionAddress, resourceManager);
        if (string.IsNullOrEmpty(idleFrameAddress))
        {
            return null;
        }

        Sprite portraitSprite = resourceManager.LoadSprite(idleFrameAddress);
        if (portraitSprite != null)
        {
            PortraitSpriteCache[cacheKey] = portraitSprite;
        }

        return portraitSprite;
    }

    private static string ResolveIdleFrameAddress(string cacheKey, string avatarDefinitionAddress, ResourceManager resourceManager)
    {
        if (IdleFrameAddressCache.TryGetValue(cacheKey, out string cachedAddress))
        {
            return cachedAddress;
        }

        AvatarAnimationDefinition definition = resourceManager.LoadResource<AvatarAnimationDefinition>(avatarDefinitionAddress);
        if (definition == null)
        {
            return null;
        }

        AvatarAnimationDefinition.ActionDefinition idleAction = definition.GetAction(AvatarActionType.Idle);
        if (idleAction == null || idleAction.FrameAddresses == null || idleAction.FrameAddresses.Count == 0)
        {
            return null;
        }

        string frameAddress = idleAction.FrameAddresses[0];
        IdleFrameAddressCache[cacheKey] = frameAddress;
        return frameAddress;
    }

    private static string NormalizeKey(string address)
    {
        return string.IsNullOrEmpty(address)
            ? string.Empty
            : address.Replace('\\', '/').ToLowerInvariant();
    }
}