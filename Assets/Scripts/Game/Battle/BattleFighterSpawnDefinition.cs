using UnityEngine;
using TribeSystem;
using System.Collections.Generic;

[System.Serializable]
public struct BattleFighterSpawnDefinition
{
    public string Name;
    public UnitStaticAttributes StaticAttributes;
    public AvatarAnimationDefinition AvatarDefinition;
    public float ScaleMultiplier;
    public TribeType TribeType;
    public List<TribeBuff> InnateBuffs;

    public BattleFighterSpawnDefinition(string name, UnitStaticAttributes staticAttributes, AvatarAnimationDefinition avatarDefinition = null, float scaleMultiplier = 1.0f, TribeType tribeType = TribeType.Tabby, List<TribeBuff> innateBuffs = null)
    {
        Name = name;
        StaticAttributes = staticAttributes;
        AvatarDefinition = avatarDefinition;
        ScaleMultiplier = scaleMultiplier;
        TribeType = tribeType;
        InnateBuffs = innateBuffs;
    }
}
