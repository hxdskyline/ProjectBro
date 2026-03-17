using UnityEngine;

[System.Serializable]
public struct BattleFighterSpawnDefinition
{
    public string Name;
    public UnitStaticAttributes StaticAttributes;
    public AvatarAnimationDefinition AvatarDefinition;

    public BattleFighterSpawnDefinition(string name, UnitStaticAttributes staticAttributes, AvatarAnimationDefinition avatarDefinition = null)
    {
        Name = name;
        StaticAttributes = staticAttributes;
        AvatarDefinition = avatarDefinition;
    }
}
