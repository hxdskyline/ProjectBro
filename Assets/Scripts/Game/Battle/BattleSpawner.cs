using System.Collections.Generic;
using UnityEngine;

public static class BattleSpawner
{
    public static BattleSpawnResult Spawn(Transform parent, BattleSpawnConfig config)
    {
        int fightersPerCamp = Mathf.Max(1, config.FightersPerCamp);
        List<Vector3> occupiedPositions = new List<Vector3>(fightersPerCamp * 2);

        BattleFighter[] playerFighters = CreateFighterGroup(
            parent,
            "PlayerAvatar",
            BattleCamp.Player,
            config.PlayerHp,
            config.PlayerAttack,
            config.PlayerDefense,
            config.PlayerAvatarDefinition,
            fightersPerCamp,
            occupiedPositions,
            true,
            config.PlayerTint,
            config);

        BattleFighter[] enemyFighters = CreateFighterGroup(
            parent,
            "EnemyAvatar",
            BattleCamp.Enemy,
            config.EnemyHp,
            config.EnemyAttack,
            config.EnemyDefense,
            config.EnemyAvatarDefinition,
            fightersPerCamp,
            occupiedPositions,
            false,
            config.EnemyTint,
            config);

        LoadGroupIdle(playerFighters);
        LoadGroupIdle(enemyFighters);

        return new BattleSpawnResult
        {
            PlayerFighters = playerFighters,
            EnemyFighters = enemyFighters
        };
    }

    private static BattleFighter[] CreateFighterGroup(
        Transform parent,
        string baseName,
        BattleCamp camp,
        int hp,
        int attack,
        int defense,
        AvatarAnimationDefinition definition,
        int count,
        List<Vector3> occupiedPositions,
        bool faceRight,
        Color tint,
        BattleSpawnConfig config)
    {
        BattleFighter[] fighters = new BattleFighter[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = GetRandomSpawnPosition(config, occupiedPositions);
            string name = count > 1 ? $"{baseName}_{i + 1}" : baseName;
            fighters[i] = CreateFighter(
                parent,
                name,
                camp,
                hp,
                attack,
                defense,
                definition,
                spawnPosition,
                faceRight,
                tint,
                config);

            occupiedPositions.Add(spawnPosition);
        }

        return fighters;
    }

    private static Vector3 GetRandomSpawnPosition(BattleSpawnConfig config, List<Vector3> occupiedPositions)
    {
        float minDistance = Mathf.Max(0.1f, config.SpawnMinDistance);
        int tryCount = Mathf.Max(1, config.SpawnTryCount);

        for (int i = 0; i < tryCount; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(config.SpawnAreaMin.x, config.SpawnAreaMax.x),
                Random.Range(config.SpawnAreaMin.y, config.SpawnAreaMax.y),
                0f);

            bool tooClose = false;
            for (int j = 0; j < occupiedPositions.Count; j++)
            {
                if (Vector3.Distance(candidate, occupiedPositions[j]) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                return candidate;
            }
        }

        return new Vector3(
            Random.Range(config.SpawnAreaMin.x, config.SpawnAreaMax.x),
            Random.Range(config.SpawnAreaMin.y, config.SpawnAreaMax.y),
            0f);
    }

    private static BattleFighter CreateFighter(
        Transform parent,
        string objectName,
        BattleCamp camp,
        int hp,
        int attack,
        int defense,
        AvatarAnimationDefinition definition,
        Vector3 position,
        bool faceRight,
        Color tint,
        BattleSpawnConfig config)
    {
        GameObject go;
        if (config.FighterPrefab != null)
        {
            go = Object.Instantiate(config.FighterPrefab, parent);
            go.name = objectName;
        }
        else
        {
            go = new GameObject(objectName);
            go.transform.SetParent(parent);
        }

        go.transform.position = position;
        float scale = Mathf.Max(0.1f, config.FighterScale);
        Vector3 baseScale = new Vector3(scale, scale, 1f);
        bool initialFaceRight = position.x < 0f ? false : (position.x > 0f ? true : faceRight);
        go.transform.localScale = initialFaceRight ? baseScale : new Vector3(-baseScale.x, baseScale.y, baseScale.z);

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = go.AddComponent<SpriteRenderer>();
        }

        renderer.color = tint;
        renderer.sortingOrder = 10;

        AvatarSequencePlayer sequencePlayer = go.GetComponent<AvatarSequencePlayer>();
        if (sequencePlayer == null)
        {
            sequencePlayer = go.AddComponent<AvatarSequencePlayer>();
        }

        BattleAvatar battleAvatar = go.GetComponent<BattleAvatar>();
        if (battleAvatar == null)
        {
            battleAvatar = go.AddComponent<BattleAvatar>();
        }

        battleAvatar.SetAnimationDefinition(definition);

        return new BattleFighter
        {
            Name = objectName,
            Camp = camp,
            HP = hp,
            Attack = attack,
            Defense = defense,
            Avatar = battleAvatar,
            Transform = go.transform,
            BaseScale = scale
        };
    }

    private static void LoadGroupIdle(BattleFighter[] fighters)
    {
        if (fighters == null)
        {
            return;
        }

        for (int i = 0; i < fighters.Length; i++)
        {
            fighters[i]?.Avatar?.LoadAndPlayIdle();
        }
    }
}
