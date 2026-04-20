using UnityEngine;
using UnityEditor;

public static class CopyFullPathMenu
{
    [MenuItem("GameObject/Copy Full Path #F12", false, 0)]
    private static void CopyFullPath()
    {
        if (Selection.activeGameObject == null) return;

        Transform t = Selection.activeGameObject.transform;
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        EditorGUIUtility.systemCopyBuffer = path;
        Debug.Log($"[CopyFullPath] Copied: {path}");
    }

    [MenuItem("GameObject/Copy Full Path", true)]
    private static bool CopyFullPathValidate()
    {
        return Selection.activeGameObject != null;
    }
}
