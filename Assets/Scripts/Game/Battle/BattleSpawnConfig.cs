using UnityEngine;

public struct BattleSpawnConfig
{
    public GameObject FighterPrefab;
    public AvatarAnimationDefinition PlayerAvatarDefinition;
    public AvatarAnimationDefinition EnemyAvatarDefinition;

    public int FightersPerCamp;
    public Vector2 SpawnAreaMin;
    public Vector2 SpawnAreaMax;
    public float SpawnMinDistance;
    public int SpawnTryCount;
    public float FighterScale;

    public Color PlayerTint;
    public Color EnemyTint;

    public int PlayerHp;
    public int EnemyHp;
    public int PlayerAttack;
    public int EnemyAttack;
    public int PlayerDefense;
    public int EnemyDefense;
}

public struct BattleSpawnResult
{
    public BattleFighter[] PlayerFighters;
    public BattleFighter[] EnemyFighters;
}
