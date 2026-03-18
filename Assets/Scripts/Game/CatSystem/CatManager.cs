using UnityEngine;
using System.Collections.Generic;

public class CatManager : MonoBehaviour
{
    public static CatManager Instance { get; private set; }
    private DataManager _dataManager;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        Instance = this;
    }

    public void Initialize(DataManager dataManager)
    {
        _dataManager = dataManager;
    }

    public CatRecord CreateCatFromTemplate(int templateId, List<long> parentIds = null)
    {
        var rec = new CatRecord();
        rec.templateId = templateId;
        rec.id = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        rec.name = "New Cat";
        rec.nameChanged = false;
        rec.gender = (Random.value > 0.5f) ? "Male" : "Female";
        rec.level = 1;
        rec.attack = 1;
        rec.defense = 1;
        rec.hp = 10;
        rec.moveSpeed = 1.0f;
        rec.energyMax = 100;
        rec.energy = rec.energyMax;
        rec.skills = new List<int>();
        rec.traits = new List<int>();
        rec.parents = parentIds ?? new List<long>();
        rec.children = new List<long>();
        rec.flags = new CatFlags();
        rec.createdAt = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        _dataManager?.AddCat(rec);
        return rec;
    }

    public bool RenameCat(long catId, string newName)
    {
        var cat = _dataManager?.GetCat(catId);
        if (cat == null) return false;
        if (cat.nameChanged) return false; // only once
        cat.name = newName;
        cat.nameChanged = true;
        _dataManager?.SavePlayerData();
        return true;
    }

    public bool EquipArtifact(long catId, long artifactInstanceId)
    {
        var cat = _dataManager?.GetCat(catId);
        if (cat == null) return false;
        cat.accessoryInstanceId = artifactInstanceId;
        _dataManager?.SavePlayerData();
        return true;
    }
}
