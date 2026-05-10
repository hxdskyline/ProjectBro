using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TribeSystem;
using BattleSystem.Fighter;

/// <summary>
/// 战斗血条面板 - 左上角显示双方阵营的竖向血条
/// 左侧玩家（绿色），右侧敌人（红色）
/// 每个种族一列，族长血条略粗在最左侧，小猫依次排列
/// 血条从下往上涨满
/// </summary>
public class BattleHUDPanel : MonoBehaviour
{
    private const float BarWidthLeader = 18f;
    private const float BarWidthCat = 12f;
    private const float BarHeight = 80f;
    private const float BarSpacing = 3f;
    private const float GroupSpacing = 12f;
    private const float PaddingLeft = 12f;
    private const float PaddingTop = 12f;

    private RectTransform _playerRoot;
    private RectTransform _enemyRoot;
    private Font _uiFont;

    private readonly List<BarGroup> _playerGroups = new List<BarGroup>();
    private readonly List<BarGroup> _enemyGroups = new List<BarGroup>();

    private struct BarEntry
    {
        public BattleFighter fighter;
        public Image fillImage;
        public Image bgImage;
        public Text nameText;
        public RectTransform rect;
        public int maxHp;
        public bool isLeader;
    }

    private class BarGroup
    {
        public RectTransform root;
        public Text titleText;
        public List<BarEntry> bars = new List<BarEntry>();
    }

    public void Initialize(BattleFighter[] playerFighters, BattleFighter[] enemyFighters,
        List<TribeRecord> deployedTribes)
    {
        _uiFont = LoadBuiltinFont();

        CreateRoots();
        BuildPlayerBars(playerFighters, deployedTribes);
        BuildEnemyBars(enemyFighters);
    }

    private void CreateRoots()
    {
        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect == null)
        {
            panelRect = gameObject.AddComponent<RectTransform>();
        }

        // Player root: top-left
        GameObject playerGo = new GameObject("PlayerBars", typeof(RectTransform));
        playerGo.transform.SetParent(transform, false);
        _playerRoot = playerGo.GetComponent<RectTransform>();
        _playerRoot.anchorMin = new Vector2(0f, 1f);
        _playerRoot.anchorMax = new Vector2(0f, 1f);
        _playerRoot.pivot = new Vector2(0f, 1f);
        _playerRoot.anchoredPosition = new Vector2(PaddingLeft, -PaddingTop);

        HorizontalLayoutGroup playerLayout = playerGo.AddComponent<HorizontalLayoutGroup>();
        playerLayout.spacing = GroupSpacing;
        playerLayout.childAlignment = TextAnchor.UpperLeft;
        playerLayout.childControlWidth = false;
        playerLayout.childControlHeight = false;
        playerLayout.childForceExpandWidth = false;
        playerLayout.childForceExpandHeight = false;

        // Enemy root: top-right
        GameObject enemyGo = new GameObject("EnemyBars", typeof(RectTransform));
        enemyGo.transform.SetParent(transform, false);
        _enemyRoot = enemyGo.GetComponent<RectTransform>();
        _enemyRoot.anchorMin = new Vector2(1f, 1f);
        _enemyRoot.anchorMax = new Vector2(1f, 1f);
        _enemyRoot.pivot = new Vector2(1f, 1f);
        _enemyRoot.anchoredPosition = new Vector2(-PaddingLeft, -PaddingTop);

        HorizontalLayoutGroup enemyLayout = enemyGo.AddComponent<HorizontalLayoutGroup>();
        enemyLayout.spacing = GroupSpacing;
        enemyLayout.childAlignment = TextAnchor.UpperRight;
        enemyLayout.childControlWidth = false;
        enemyLayout.childControlHeight = false;
        enemyLayout.childForceExpandWidth = false;
        enemyLayout.childForceExpandHeight = false;
    }

    private void BuildPlayerBars(BattleFighter[] fighters, List<TribeRecord> deployedTribes)
    {
        if (fighters == null || fighters.Length == 0) return;

        // Group fighters by tribe: each tribe has 1 leader + N cats, in order
        // The spawn order is: tribe1_leader, tribe1_cat1, tribe1_cat2, ..., tribe2_leader, ...
        // We detect groups by checking spawn definitions order
        int fighterIndex = 0;

        if (deployedTribes != null)
        {
            foreach (TribeRecord tribe in deployedTribes)
            {
                if (fighterIndex >= fighters.Length) break;

                // 使用 fighter 表中的名称
                var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
                string groupName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";
                BarGroup group = CreateBarGroup(_playerRoot, groupName,
                    GetTribeColor(tribe.tribeType));

                // Leader
                if (fighterIndex < fighters.Length && fighters[fighterIndex] != null)
                {
                    group.bars.Add(CreateBar(group.root, fighters[fighterIndex], true, false));
                    fighterIndex++;
                }

                // Cats
                int catCount = tribe.cats?.Count ?? 0;
                for (int c = 0; c < catCount && fighterIndex < fighters.Length; c++)
                {
                    if (fighters[fighterIndex] != null)
                    {
                        group.bars.Add(CreateBar(group.root, fighters[fighterIndex], false, false));
                    }
                    fighterIndex++;
                }

                _playerGroups.Add(group);
            }
        }

        // Remaining fighters (if any) go into a single group
        if (fighterIndex < fighters.Length)
        {
            BarGroup fallbackGroup = CreateBarGroup(_playerRoot, "其他", new Color(0.3f, 0.7f, 0.3f));
            for (int i = fighterIndex; i < fighters.Length; i++)
            {
                if (fighters[i] != null)
                {
                    fallbackGroup.bars.Add(CreateBar(fallbackGroup.root, fighters[i], false, false));
                }
            }
            if (fallbackGroup.bars.Count > 0)
                _playerGroups.Add(fallbackGroup);
        }
    }

    private void BuildEnemyBars(BattleFighter[] fighters)
    {
        if (fighters == null || fighters.Length == 0) return;

        // Enemies are all one group
        BarGroup group = CreateBarGroup(_enemyRoot, "敌方", new Color(0.8f, 0.2f, 0.2f));

        for (int i = 0; i < fighters.Length; i++)
        {
            if (fighters[i] != null)
            {
                bool isFirst = (i == 0);
                group.bars.Add(CreateBar(group.root, fighters[i], isFirst, true));
            }
        }

        _enemyGroups.Add(group);
    }

    private BarGroup CreateBarGroup(RectTransform parent, string title, Color titleColor)
    {
        BarGroup group = new BarGroup();

        GameObject groupGo = new GameObject($"Group_{title}", typeof(RectTransform));
        groupGo.transform.SetParent(parent, false);
        group.root = groupGo.GetComponent<RectTransform>();

        VerticalLayoutGroup vLayout = groupGo.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = BarSpacing;
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;
        vLayout.padding = new RectOffset(2, 2, 0, 0);

        LayoutElement groupLe = groupGo.AddComponent<LayoutElement>();
        groupLe.preferredWidth = 60f;
        groupLe.minWidth = 40f;

        // Title text at top
        GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        titleGo.transform.SetParent(groupGo.transform, false);
        group.titleText = titleGo.GetComponent<Text>();
        group.titleText.font = _uiFont;
        group.titleText.fontSize = 12;
        group.titleText.color = titleColor;
        group.titleText.alignment = TextAnchor.MiddleCenter;
        group.titleText.text = title;
        group.titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        group.titleText.verticalOverflow = VerticalWrapMode.Overflow;
        LayoutElement titleLe = titleGo.GetComponent<LayoutElement>();
        titleLe.preferredHeight = 18f;

        return group;
    }

    private BarEntry CreateBar(RectTransform parent, BattleFighter fighter, bool isLeader, bool isEnemy)
    {
        BarEntry entry = new BarEntry();
        entry.fighter = fighter;
        entry.maxHp = Mathf.Max(1, fighter.StaticAttributes.MaxHp);
        entry.isLeader = isLeader;

        float barWidth = isLeader ? BarWidthLeader : BarWidthCat;

        // Background
        GameObject bgGo = new GameObject($"Bar_{fighter.Name}",
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bgGo.transform.SetParent(parent, false);

        entry.rect = bgGo.GetComponent<RectTransform>();
        entry.rect.sizeDelta = new Vector2(barWidth, BarHeight);

        entry.bgImage = bgGo.GetComponent<Image>();
        entry.bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        LayoutElement le = bgGo.GetComponent<LayoutElement>();
        le.preferredWidth = barWidth;
        le.preferredHeight = BarHeight;

        // Fill (grows bottom-to-top)
        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bgGo.transform, false);

        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);
        fillRect.pivot = new Vector2(0.5f, 0f);

        entry.fillImage = fillGo.GetComponent<Image>();
        if (isEnemy)
        {
            entry.fillImage.color = isLeader
                ? new Color(0.9f, 0.15f, 0.1f, 0.9f)
                : new Color(0.7f, 0.25f, 0.2f, 0.85f);
        }
        else
        {
            entry.fillImage.color = isLeader
                ? new Color(0.15f, 0.75f, 0.2f, 0.9f)
                : new Color(0.2f, 0.55f, 0.25f, 0.85f);
        }

        // Name label (only for leader)
        if (isLeader)
        {
            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(bgGo.transform, false);

            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 2f);
            nameRect.sizeDelta = new Vector2(0f, 14f);

            entry.nameText = nameGo.GetComponent<Text>();
            entry.nameText.font = _uiFont;
            entry.nameText.fontSize = 10;
            entry.nameText.color = Color.white;
            entry.nameText.alignment = TextAnchor.MiddleCenter;
            entry.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            entry.nameText.verticalOverflow = VerticalWrapMode.Overflow;
            entry.nameText.raycastTarget = false;
        }

        // Apply initial fill
        UpdateBarFill(entry);

        return entry;
    }

    private void Update()
    {
        UpdateGroupBars(_playerGroups);
        UpdateGroupBars(_enemyGroups);
    }

    private void UpdateGroupBars(List<BarGroup> groups)
    {
        for (int g = 0; g < groups.Count; g++)
        {
            BarGroup group = groups[g];
            for (int b = 0; b < group.bars.Count; b++)
            {
                BarEntry entry = group.bars[b];
                if (entry.fighter == null || entry.fighter.IsRemoved)
                {
                    // Dead / removed: set fill to 0
                    if (entry.fillImage != null)
                    {
                        entry.fillImage.transform.localScale = new Vector3(1f, 0f, 1f);
                        entry.fillImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                    }
                    continue;
                }

                if (entry.fighter.IsDying)
                {
                    if (entry.fillImage != null)
                    {
                        entry.fillImage.transform.localScale = new Vector3(1f, 0f, 1f);
                    }
                    continue;
                }

                UpdateBarFill(entry);
            }
        }
    }

    private void UpdateBarFill(BarEntry entry)
    {
        if (entry.fillImage == null || entry.fighter == null) return;

        int currentHp = entry.fighter.CurrentHp;
        float ratio = Mathf.Clamp01((float)currentHp / entry.maxHp);

        // Scale the fill from bottom (pivot at 0,0)
        entry.fillImage.transform.localScale = new Vector3(1f, ratio, 1f);

        // Update name text with HP
        if (entry.nameText != null)
        {
            entry.nameText.text = $"{currentHp}";
        }
    }

    /// <summary>
    /// 战斗结束时销毁面板
    /// </summary>
    public void Cleanup()
    {
        _playerGroups.Clear();
        _enemyGroups.Clear();
        Destroy(gameObject);
    }

    // --- Helpers ---

    private static Color GetTribeColor(TribeType type)
    {
        switch (type)
        {
            case TribeType.Tabby: return new Color(0.8f, 0.6f, 0.3f);
            case TribeType.Orange: return new Color(0.9f, 0.7f, 0.2f);
            case TribeType.Cow: return new Color(0.6f, 0.6f, 0.7f);
            case TribeType.Siamese: return new Color(0.7f, 0.5f, 0.8f);
            default: return Color.white;
        }
    }

    private static Font LoadBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
