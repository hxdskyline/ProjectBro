using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using LitJson;

namespace TribeSystem.UI
{
    /// <summary>
    /// 饰品图鉴面板 - 展示所有已收集和未收集的饰品
    /// </summary>
    public class AccessoryCodexPanel : MonoBehaviour
    {
        private RectTransform _externalRoot;
        private bool _isRuntimeCreated;
        private RectTransform _contentRoot;
        private List<RectTransform> _entries = new List<RectTransform>();
        private Font _font;

        public void SetExternalRoot(RectTransform externalRoot)
        {
            _externalRoot = externalRoot;
        }

        public void Initialize()
        {
            EnsureRuntimeUI();
        }

        private void EnsureRuntimeUI()
        {
            if (_isRuntimeCreated) return;
            _isRuntimeCreated = true;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform targetParent = _externalRoot != null ? _externalRoot : transform as RectTransform;

            // 背景面板
            GameObject panelGo = new GameObject("CodexContent", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(targetParent, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0.1f);
            panelRect.anchorMax = new Vector2(0.85f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.1f, 0.08f, 0.12f, 0.98f);

            // 标题
            CreateTitle(panelRect, "饰品图鉴", new Vector2(0, -10), 40, 28);

            // 滚动区域
            CreateScrollArea(panelRect);

            // 关闭按钮
            CreateCloseButton(panelRect);
        }

        public void Show()
        {
            EnsureRuntimeUI();

            // 清空旧条目
            ClearEntries();

            // 加载数据并重建列表
            BuildAccessoryList();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ClearEntries()
        {
            foreach (var entry in _entries)
            {
                if (entry != null) Destroy(entry.gameObject);
            }
            _entries.Clear();
        }

        private void BuildAccessoryList()
        {
            if (_contentRoot == null) return;

            // 加载饰品配置
            string configPath = Application.streamingAssetsPath + "/accessory_config.json";
            if (!System.IO.File.Exists(configPath)) return;

            var configText = System.IO.File.ReadAllText(configPath);
            var root = JsonMapper.ToObject(configText);
            var accessories = root["accessories"];

            // 获取已解锁列表
            var unlocked = GameManager.Instance?.DataManager?.GetUnlockedAccessories() ?? new List<string>();

            for (int i = 0; i < accessories.Count; i++)
            {
                var acc = accessories[i];
                string id = acc["id"].ToString();
                string name = acc["name"].ToString();
                string desc = acc["description"].ToString();
                string effectType = acc["effectType"].ToString();
                float effectValue = float.Parse(acc["effectValue"].ToString());
                bool isUnlocked = unlocked.Contains(id);

                CreateAccessoryEntry(id, name, desc, effectType, effectValue, isUnlocked);
            }
        }

        private void CreateAccessoryEntry(string id, string name, string description, string effectType, float effectValue, bool isUnlocked)
        {
            if (_contentRoot == null) return;

            // 条目容器
            GameObject entryGo = new GameObject($"Entry_{id}", typeof(RectTransform));
            entryGo.transform.SetParent(_contentRoot, false);
            RectTransform entryRect = entryGo.GetComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(480f, 50f);

            // Icon 色块
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(entryRect, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0, 0.15f);
            iconRt.anchorMax = new Vector2(0, 0.85f);
            iconRt.sizeDelta = new Vector2(36f, 0f);
            iconRt.anchoredPosition = new Vector2(28f, 0f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.color = isUnlocked ? GetEffectColor(effectType) : new Color(0.3f, 0.3f, 0.3f, 1f);

            // 名称
            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(entryRect, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.12f, 0.1f);
            nameRt.anchorMax = new Vector2(0.35f, 0.9f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = _font;
            nameText.fontSize = 16;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.text = isUnlocked ? name : "???";
            nameText.color = isUnlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);

            // 描述/效果
            GameObject descGo = new GameObject("Desc", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(entryRect, false);
            RectTransform descRt = descGo.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.37f, 0.1f);
            descRt.anchorMax = new Vector2(0.7f, 0.9f);
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;
            Text descText = descGo.GetComponent<Text>();
            descText.font = _font;
            descText.fontSize = 14;
            descText.alignment = TextAnchor.MiddleLeft;
            descText.text = isUnlocked ? description : "未收集";
            descText.color = isUnlocked ? new Color(0.8f, 0.8f, 0.8f, 1f) : new Color(0.4f, 0.4f, 0.4f, 1f);

            // 状态标记
            GameObject statusGo = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusGo.transform.SetParent(entryRect, false);
            RectTransform statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.8f, 0.1f);
            statusRt.anchorMax = new Vector2(1f, 0.9f);
            statusRt.offsetMin = Vector2.zero;
            statusRt.offsetMax = Vector2.zero;
            Text statusText = statusGo.GetComponent<Text>();
            statusText.font = _font;
            statusText.fontSize = 14;
            statusText.alignment = TextAnchor.MiddleRight;
            statusText.text = isUnlocked ? "已收集" : "";
            statusText.color = new Color(0.4f, 0.8f, 0.4f, 1f);

            // 分割线
            GameObject lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(entryRect, false);
            RectTransform lineRt = lineGo.GetComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(0.02f, 0f);
            lineRt.anchorMax = new Vector2(0.98f, 0f);
            lineRt.sizeDelta = new Vector2(0f, 1f);
            lineRt.anchoredPosition = new Vector2(0f, -0.5f);
            Image lineImg = lineGo.GetComponent<Image>();
            lineImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            _entries.Add(entryRect);
        }

        private void CreateScrollArea(RectTransform parent)
        {
            // Scroll View
            GameObject scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.05f, 0.12f);
            scrollRt.anchorMax = new Vector2(0.95f, 0.85f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            // Viewport
            GameObject viewGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewGo.transform.SetParent(scrollRt, false);
            RectTransform viewRt = viewGo.GetComponent<RectTransform>();
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = Vector2.zero;
            Image viewImg = viewGo.GetComponent<Image>();
            viewImg.color = Color.clear;
            Mask mask = viewGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content (VerticalLayoutGroup + ContentSizeFitter)
            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewRt, false);
            _contentRoot = contentGo.GetComponent<RectTransform>();
            _contentRoot.anchorMin = new Vector2(0, 1);
            _contentRoot.anchorMax = new Vector2(1, 1);
            _contentRoot.pivot = new Vector2(0.5f, 1);
            _contentRoot.sizeDelta = new Vector2(0, 400f);

            VerticalLayoutGroup layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.UpperCenter;

            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 绑定 ScrollRect
            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = _contentRoot;
            scroll.viewport = viewRt;
            scroll.movementType = ScrollRect.MovementType.Elastic;
        }

        private void CreateTitle(RectTransform parent, string text, Vector2 anchoredPos, float height, int fontSize)
        {
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(parent, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, height);
            titleRect.anchoredPosition = anchoredPos;
            Text titleText = titleGo.GetComponent<Text>();
            titleText.font = _font;
            titleText.fontSize = fontSize;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = text;
        }

        private void CreateCloseButton(RectTransform parent)
        {
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(parent, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.35f, 0.015f);
            closeRect.anchorMax = new Vector2(0.65f, 0.09f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            Button closeBtn = closeGo.GetComponent<Button>();
            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.5f, 0.25f, 0.2f, 1f);
            closeBtn.targetGraphic = closeImg;

            GameObject closeLabel = new GameObject("Label", typeof(RectTransform), typeof(Text));
            closeLabel.transform.SetParent(closeRect, false);
            RectTransform clRect = closeLabel.GetComponent<RectTransform>();
            clRect.anchorMin = Vector2.zero;
            clRect.anchorMax = Vector2.one;
            clRect.offsetMin = Vector2.zero;
            clRect.offsetMax = Vector2.zero;
            Text clText = closeLabel.GetComponent<Text>();
            clText.font = _font;
            clText.fontSize = 18;
            clText.alignment = TextAnchor.MiddleCenter;
            clText.color = Color.white;
            clText.text = "关闭";

            closeBtn.onClick.AddListener(Hide);
        }

        private Color GetEffectColor(string effectType)
        {
            switch (effectType)
            {
                case "AttackPercent": return new Color(0.9f, 0.3f, 0.2f, 1f);
                case "DefensePercent": return new Color(0.3f, 0.5f, 0.9f, 1f);
                case "HpPercent": return new Color(0.2f, 0.8f, 0.3f, 1f);
                case "SpeedPercent": return new Color(0.8f, 0.6f, 0.1f, 1f);
                case "AllPercent": return new Color(0.7f, 0.4f, 0.9f, 1f);
                default: return Color.gray;
            }
        }
    }
}
