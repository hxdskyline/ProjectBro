using UnityEngine;
using TribeSystem;

[System.Serializable]
public struct BattleFighterSpawnDefinition
{
    public string Name;
    public UnitStaticAttributes StaticAttributes;
    public AvatarAnimationDefinition AvatarDefinition;
    public float ScaleMultiplier;
    public TribeType TribeType;

    public BattleFighterSpawnDefinition(string name, UnitStaticAttributes staticAttributes, AvatarAnimationDefinition avatarDefinition = null, float scaleMultiplier = 1.0f, TribeType tribeType = TribeType.Tabby)
    {
        Name = name;
        StaticAttributes = staticAttributes;
        AvatarDefinition = avatarDefinition;
        ScaleMultiplier = scaleMultiplier;
        TribeType = tribeType;
    }
}
