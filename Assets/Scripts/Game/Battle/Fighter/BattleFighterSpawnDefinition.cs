using UnityEngine;
using TribeSystem;
using System.Collections.Generic;
using BattleSystem.Avatar;

namespace BattleSystem.Fighter
{
    [System.Serializable]
    public struct BattleFighterSpawnDefinition
    {
        public string Name;
        public UnitStaticAttributes StaticAttributes;
        public AvatarAnimationDefinition AvatarDefinition;
        public float ScaleMultiplier;
        public TribeType TribeType;
        public int FighterId; // fighter_config.json 中的 fighterId
        public List<UnifiedBuff> AuraBuffs; // 从 CatData/LeaderData 传入的光环 buff
        public bool IsLeader; // 是否是族长

        public BattleFighterSpawnDefinition(string name, UnitStaticAttributes staticAttributes, AvatarAnimationDefinition avatarDefinition = null, float scaleMultiplier = 1.0f, TribeType tribeType = TribeType.Tabby, int fighterId = 0)
        {
            Name = name;
            StaticAttributes = staticAttributes;
            AvatarDefinition = avatarDefinition;
            ScaleMultiplier = scaleMultiplier;
            TribeType = tribeType;
            FighterId = fighterId;
            AuraBuffs = null;
            IsLeader = false;
        }
    }
}
