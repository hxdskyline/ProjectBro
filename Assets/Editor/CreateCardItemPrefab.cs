using UnityEngine;
using UnityEditor;

public static class CreateCardItemPrefab
{
    [MenuItem("Tools/Prefabs/Create CardItem Prefab")]
    public static void Create()
    {
        string path = "Assets/Bundle/UI/CardItem.prefab";

        GameObject go = new GameObject("CardItem", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 160f);

        // Background image
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.color = Color.white;

        // LayoutElement
        var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
        le.minWidth = 160f;
        le.minHeight = 160f;

        // CanvasGroup
        go.AddComponent<CanvasGroup>();

        // CardDragItem (script will be resolved by Unity when importing)
        go.AddComponent(typeof(CardDragItem));

        // Portrait child
        GameObject portrait = new GameObject("Portrait", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        portrait.transform.SetParent(go.transform, false);
        RectTransform pr = portrait.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.one;
        pr.offsetMin = new Vector2(4f, 4f);
        pr.offsetMax = new Vector2(-4f, -4f);

        // Name text
        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        nameGo.transform.SetParent(go.transform, false);

        // Stats text
        GameObject statsGo = new GameObject("Stats", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        statsGo.transform.SetParent(go.transform, false);

        // Ensure target folder exists
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        // Save prefab
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
        GameObject.DestroyImmediate(go);

        Debug.Log($"Created prefab at {path}");
    }
}
