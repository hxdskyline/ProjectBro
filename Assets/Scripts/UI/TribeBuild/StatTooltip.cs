using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 属性详情 Tooltip - 悬浮在属性行或 buff 条目上时显示
    /// 公式格式：final = (base + SUM(flatBuffs)) x (1 + SUM(percentBuffs))
    /// </summary>
    public class StatTooltip : MonoBehaviour
    {
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _formulaText;
        [SerializeField] private RectTransform _buffListContainer;
        [SerializeField] private GameObject _buffLinePrefab;

        private static StatTooltip _instance;
        private static readonly Color SourceRecruitColor = new Color(0.4f, 0.8f, 0.4f, 1f);
        private static readonly Color SourceArtifactColor = new Color(0.9f, 0.7f, 0.2f, 1f);
        private static readonly Color SourceRitualColor = new Color(0.6f, 0.4f, 0.9f, 1f);
        private static readonly Color SourceLegacyColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 显示属性 tooltip，锚定在指定 RectTransform 旁边
        /// </summary>
        public static void Show(StatType stat, int finalValue, int baseValue,
            List<UnifiedBuff> flatEntries, List<UnifiedBuff> percentEntries, RectTransform anchor)
        {
            if (_instance == null) return;

            var inst = _instance;
            inst.gameObject.SetActive(true);

            // 标题
            if (inst._titleText != null)
                inst._titleText.text = GetStatName(stat);

            // 公式：final = (base+flat1+flat2) x (1+pct1+pct2)
            if (inst._formulaText != null)
                inst._formulaText.text = BuildFormula(stat, finalValue, baseValue, flatEntries, percentEntries);

            // buff 来源列表
            inst.RebuildBuffList(flatEntries, percentEntries);

            // 定位：锚定在 attribute item 右侧
            if (anchor != null)
            {
                var tooltipRect = inst.GetComponent<RectTransform>();
                var canvas = inst.GetComponentInParent<Canvas>();
                if (tooltipRect != null && canvas != null)
                {
                    // 将 anchor 的世界位置转换为 Canvas 坐标
                    Vector3[] worldCorners = new Vector3[4];
                    anchor.GetWorldCorners(worldCorners);
                    Vector3 anchorRight = worldCorners[2]; // 右上角

                    Vector2 localPoint;
                    RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, anchorRight, canvas.worldCamera, out localPoint);

                    tooltipRect.anchoredPosition = localPoint + new Vector2(10f, 0f);
                }
            }
        }

        public static void Hide()
        {
            if (_instance != null)
                _instance.gameObject.SetActive(false);
        }

        private void RebuildBuffList(List<UnifiedBuff> flatEntries, List<UnifiedBuff> percentEntries)
        {
            if (_buffListContainer == null || _buffLinePrefab == null) return;

            // 清空
            for (int i = _buffListContainer.childCount - 1; i >= 0; i--)
                Destroy(_buffListContainer.GetChild(i).gameObject);

            // 合并所有条目
            var allEntries = new List<UnifiedBuff>();
            if (flatEntries != null) allEntries.AddRange(flatEntries);
            if (percentEntries != null) allEntries.AddRange(percentEntries);

            foreach (var entry in allEntries)
            {
                GameObject lineGo = Instantiate(_buffLinePrefab, _buffListContainer, false);
                Text lineText = lineGo.GetComponentInChildren<Text>();
                if (lineText != null)
                {
                    string valueStr = FormatBuffValue(entry);
                    lineText.text = $"{entry.displayName} {valueStr}";
                    lineText.color = GetSourceColor(entry.source);
                }
            }
        }

        private static string FormatBuffValue(UnifiedBuff buff)
        {
            float totalValue = buff.value * buff.currentStacks;
            if (buff.isPercent) return $"+{Mathf.RoundToInt(totalValue * 100)}%";
            return $"+{Mathf.RoundToInt(totalValue)}";
        }

        private static string BuildFormula(StatType stat, int finalValue, int baseValue,
            List<UnifiedBuff> flatEntries, List<UnifiedBuff> percentEntries)
        {
            float sumFlat = 0f;
            if (flatEntries != null)
            {
                foreach (var e in flatEntries) sumFlat += e.value * e.currentStacks;
            }

            float sumPercent = 0f;
            if (percentEntries != null)
            {
                foreach (var e in percentEntries) sumPercent += e.value * e.currentStacks;
            }

            // 公式：final = (base+flat1+flat2) x (1+pct1)
            bool hasFlat = flatEntries != null && flatEntries.Count > 0;
            bool hasPercent = percentEntries != null && percentEntries.Count > 0;

            if (!hasFlat && !hasPercent)
            {
                return $"{finalValue} = {baseValue}";
            }

            string formula = $"{finalValue} = ";

            if (hasFlat)
            {
                // 构建 (base+6+20)
                var parts = new List<string> { baseValue.ToString() };
                foreach (var e in flatEntries) parts.Add($"+{Mathf.RoundToInt(e.value * e.currentStacks)}");
                formula += $"({string.Join("", parts.ToArray())})";
            }
            else
            {
                formula += $"{baseValue}";
            }

            if (hasPercent)
            {
                float multiplier = 1f + sumPercent;
                formula += $" x {multiplier:0.##}";
            }

            return formula;
        }

        private static string GetStatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return "攻击力";
                case StatType.Defense: return "防御力";
                case StatType.Hp: return "生命值";
                case StatType.MoveSpeed: return "移速";
                case StatType.AttackSpeed: return "攻速";
                default: return stat.ToString();
            }
        }

        private static Color GetSourceColor(BuffSource source)
        {
            switch (source)
            {
                case BuffSource.Recruitment: return SourceRecruitColor;
                case BuffSource.Artifact: return SourceArtifactColor;
                case BuffSource.Ritual: return SourceRitualColor;
                case BuffSource.Equipment: return SourceArtifactColor;
                default: return SourceLegacyColor;
            }
        }
    }
}
