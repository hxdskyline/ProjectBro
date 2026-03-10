using UnityEngine;

[System.Serializable]
public struct BattleFighterSpawnDefinition
{
    public string Name;
    public UnitStaticAttributes StaticAttributes;

    public BattleFighterSpawnDefinition(string name, UnitStaticAttributes staticAttributes)
    {
        Name = name;
        StaticAttributes = staticAttributes;
    }
}
