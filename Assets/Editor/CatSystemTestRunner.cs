using UnityEditor;
using UnityEngine;

public class CatSystemTestRunner
{
    [MenuItem("CatSystem/Run Simple Tests")]
    public static void RunTests()
    {
        var dm = Object.FindObjectOfType<DataManager>();
        if (dm == null)
        {
            Debug.LogError("DataManager not found in scene. Please add one to run tests.");
            return;
        }

        dm.LoadPlayerData();
        Debug.Log("PlayerId: " + dm.PlayerData.playerId);

        // Create a cat
        var cmObj = Object.FindObjectOfType<CatManager>();
        if (cmObj == null)
        {
            Debug.LogError("CatManager not found in scene.");
            return;
        }

        var cat = cmObj.CreateCatFromTemplate(1, null);
        Debug.Log($"Created cat id={cat.id} name={cat.name} template={cat.templateId}");

        // Request an outing
        var om = Object.FindObjectOfType<OutingManager>();
        if (om != null)
        {
            var req = om.RequestOuting(new System.Collections.Generic.List<long> { cat.id, cat.id });
            Debug.Log($"Outing requested id={req.requestId} status={req.status}");
        }

        Debug.Log("Simple tests finished.");
    }
}
