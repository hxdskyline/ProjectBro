using UnityEngine;
using System.Collections.Generic;

public class OutingManager : MonoBehaviour
{
    public static OutingManager Instance { get; private set; }
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

    public OutingRequestRecord RequestOuting(List<long> pairIds)
    {
        if (_dataManager == null) return null;
        var req = new OutingRequestRecord();
        req.requestId = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        req.pairIds = new List<long>(pairIds);
        req.initiatedCycle = 0;
        req.returnCycle = 0;
        req.status = "Pending";
        _dataManager.AddOutingRequest(req);
        return req;
    }

    // Called when battle starts to validate and activate pending requests
    public void ActivatePendingOutings(int currentCycle, int minReturnDelay, int maxReturnDelay)
    {
        var list = _dataManager.GetOutingRequests();
        foreach (var r in list)
        {
            if (r.status != "Pending") continue;
            r.initiatedCycle = currentCycle;
            r.returnCycle = currentCycle + Random.Range(minReturnDelay, maxReturnDelay + 1);
            r.status = "Active";
        }
        _dataManager.SavePlayerData();
    }

    public List<OutingRequestRecord> GetAllRequests()
    {
        return _dataManager?.GetOutingRequests();
    }
}
