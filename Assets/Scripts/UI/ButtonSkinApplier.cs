using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class ButtonSkinApplier
{
    private static readonly string[] CandidateAddresses = new[]
    {
        "ui/sprite/commonbutton",
        "bundle/ui/sprite/commonbutton",
        "assets/bundle/ui/sprite/commonbutton",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        ApplyToAllButtons();
    }

    public static void ApplyToAllButtons()
    {
        Sprite sprite = TryLoadCommonSprite();
        if (sprite == null)
        {
            Debug.LogWarning("[ButtonSkinApplier] commonbutton sprite not found. Skipping skin apply.");
            return;
        }

        Button[] buttons = Object.FindObjectsOfType<Button>(includeInactive: true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            if (btn == null) continue;

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }

            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = new SpriteState
            {
                highlightedSprite = sprite,
                pressedSprite = sprite,
                disabledSprite = sprite,
                selectedSprite = sprite
            };
            btn.spriteState = ss;
        }

        Debug.Log($"[ButtonSkinApplier] Applied commonbutton to {buttons.Length} buttons.");
    }

    private static Sprite TryLoadCommonSprite()
    {
        // Try GameManager.ResourceManager first if available
        try
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.ResourceManager != null)
            {
                foreach (var addr in CandidateAddresses)
                {
                    var sp = gm.ResourceManager.LoadSprite(addr);
                    if (sp != null) return sp;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ButtonSkinApplier] ResourceManager load failed: {e.Message}");
        }

        // Fallback: try Resources (if project placed a copy there)
        foreach (var addr in CandidateAddresses)
        {
            string path = addr.Replace("assets/bundle/", "").Replace("bundle/", "");
            var res = Resources.Load<Sprite>(path);
            if (res != null) return res;
        }

        return null;
    }
}
