using UnityEngine;

[System.Serializable]
public struct BattleFighterSpawnDefinition
{
    public string Name;
    public UnitStaticAttributes StaticAttributes;
    public AvatarAnimationDefinition AvatarDefinition;
    public float ScaleMultiplier;

    public BattleFighterSpawnDefinition(string name, UnitStaticAttributes staticAttributes, AvatarAnimationDefinition avatarDefinition = null, float scaleMultiplier = 1.0f)
    {
        Name = name;
        StaticAttributes = staticAttributes;
        AvatarDefinition = avatarDefinition;
        ScaleMultiplier = scaleMultiplier;
    }
}
