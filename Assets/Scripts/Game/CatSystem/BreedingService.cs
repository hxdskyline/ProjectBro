using UnityEngine;
using System.Collections.Generic;

public static class BreedingService
{
    // Generate offspring list given two parent records and config parameters
    public static List<CatRecord> GenerateOffspring(CatRecord father, CatRecord mother, int maxChildren = 2, float minMultiplier = 1.2f, float maxMultiplier = 1.5f)
    {
        var result = new List<CatRecord>();
        if (father == null || mother == null) return result;

        // Simple child count sampling: 50% 1 child, 30% 0, 20% 2 (example)
        float roll = Random.value;
        int childCount = 0;
        if (roll < 0.3f) childCount = 0;
        else if (roll < 0.8f) childCount = 1;
        else childCount = 2;

        for (int i = 0; i < childCount && i < maxChildren; i++)
        {
            var c = new CatRecord();
            c.id = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i;
            c.templateId = (Random.value > 0.5f) ? father.templateId : mother.templateId;
            c.name = "Kitten";
            c.gender = (Random.value > 0.5f) ? "Male" : "Female";
            c.level = 1;

            float mul = Random.Range(minMultiplier, maxMultiplier);
            c.attack = Mathf.Max(1, Mathf.RoundToInt((father.attack + mother.attack) / 2f * mul));
            c.defense = Mathf.Max(1, Mathf.RoundToInt((father.defense + mother.defense) / 2f * mul));
            c.hp = Mathf.Max(1, Mathf.RoundToInt((father.hp + mother.hp) / 2f * mul));
            c.moveSpeed = Mathf.Max(0.1f, ((father.moveSpeed + mother.moveSpeed) / 2f) * mul);

            c.energyMax = Mathf.Max(50, Mathf.RoundToInt((father.energyMax + mother.energyMax) / 2f));
            c.energy = c.energyMax;
            c.skills = new List<int>();
            c.traits = new List<int>();
            c.parents = new List<long> { father.id, mother.id };
            c.children = new List<long>();
            c.flags = new CatFlags();
            c.createdAt = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            result.Add(c);
        }

        return result;
    }
}
